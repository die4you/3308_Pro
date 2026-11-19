using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace MebKoordinatorluk.Core.Services;

public class EOkulParsedStudent
{
    public int SequenceNumber { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Gender { get; set; } = "Kız";
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
    public string Classroom { get; set; } = string.Empty; // Örn: 9-A
    public string Grade { get; set; } = "9";
    public string Branch { get; set; } = "A";
    public string FieldName { get; set; } = string.Empty;
    public string EducationType { get; set; } = "Örgün Eğitim"; // Örgün Eğitim veya MESEM
    public string? TeacherName { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsAlreadyInDb { get; set; } = false;
    public int? ExistingStudentId { get; set; }

    public string InternshipDays
    {
        get
        {
            bool isMesem = string.Equals(EducationType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                           (!string.IsNullOrWhiteSpace(Classroom) && Classroom.Contains("MESEM", StringComparison.OrdinalIgnoreCase));
            if (isMesem)
            {
                return "Pazartesi,Salı,Çarşamba,Perşembe";
            }
            if (Grade == "12")
            {
                return "Pazartesi,Salı,Çarşamba";
            }
            return string.Empty;
        }
    }
}

public class EOkulClassGroup
{
    public string Classroom { get; set; } = string.Empty;
    public string Grade { get; set; } = "9";
    public string Branch { get; set; } = "A";
    public string FieldName { get; set; } = string.Empty;
    public string EducationType { get; set; } = "Örgün Eğitim";
    public string? TeacherName { get; set; }
    public List<EOkulParsedStudent> Students { get; set; } = [];

    public string InternshipDays
    {
        get
        {
            bool isMesem = string.Equals(EducationType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                           (!string.IsNullOrWhiteSpace(Classroom) && Classroom.Contains("MESEM", StringComparison.OrdinalIgnoreCase));
            if (isMesem)
            {
                return "Pazartesi,Salı,Çarşamba,Perşembe";
            }
            if (Grade == "12")
            {
                return "Pazartesi,Salı,Çarşamba";
            }
            return string.Empty;
        }
    }
}

public class EOkulParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<EOkulClassGroup> Groups { get; set; } = [];
    public List<EOkulParsedStudent> AllStudents { get; set; } = [];
    public int TotalStudentsCount => AllStudents.Count;
    public int TotalClassesCount => Groups.Select(g => g.Classroom).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int TotalFieldsCount => Groups.Select(g => g.FieldName).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

public static class EOkulClassListParser
{
    static EOkulClassListParser()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch
        {
            // Ignore if already registered
        }
    }

    public static EOkulParseResult ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new EOkulParseResult { Success = false, ErrorMessage = "Dosya bulunamadı." };
        }

        using var stream = File.OpenRead(filePath);
        return ParseStream(stream, Path.GetFileName(filePath));
    }

    public static EOkulParseResult ParseStream(Stream stream, string fileName)
    {
        try
        {
            var grid = ExtractGrid(stream, fileName);
            if (grid.Count == 0)
            {
                return new EOkulParseResult { Success = false, ErrorMessage = "Dosya içerisinde okunabilir veri satırı bulunamadı." };
            }

            return ParseGrid(grid);
        }
        catch (Exception ex)
        {
            return new EOkulParseResult { Success = false, ErrorMessage = $"Ayrıştırma hatası: {ex.Message}" };
        }
    }

    public static List<List<string>> ExtractGridFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return [];
        using var stream = File.OpenRead(filePath);
        return ExtractGrid(stream, Path.GetFileName(filePath));
    }

    public static List<List<string>> ExtractGrid(Stream stream, string fileName)
    {
        var grid = new List<List<string>>();

        // İlk birkaç baytı incele (HTML tablo mu, ikili Excel mi?)
        var buffer = new byte[1024];
        long initialPos = stream.CanSeek ? stream.Position : 0;
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        if (stream.CanSeek)
        {
            stream.Position = initialPos;
        }

        string headerPreview = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimStart();

        bool isHtml = headerPreview.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                      headerPreview.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                      headerPreview.StartsWith("<table", StringComparison.OrdinalIgnoreCase) ||
                      headerPreview.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

        if (isHtml)
        {
            // HTML / XML Tablo Ayrıştırma
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            string fullText = reader.ReadToEnd();
            grid = ParseHtmlTable(fullText);
            if (grid.Count > 0) return grid;
        }

        // İkili Excel (.xls / .xlsx) okuyucu
        try
        {
            using var excelReader = ExcelReaderFactory.CreateReader(stream);
            var ds = excelReader.AsDataSet();
            foreach (DataTable table in ds.Tables)
            {
                foreach (DataRow row in table.Rows)
                {
                    var rowList = new List<string>();
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        var cellVal = row[i]?.ToString()?.Trim() ?? "";
                        rowList.Add(cellVal);
                    }
                    if (rowList.Any(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        grid.Add(rowList);
                    }
                }
            }
        }
        catch
        {
            // ExcelDataReader başarısız olursa ve dosya metin ise satır satır dene
            if (stream.CanSeek)
            {
                stream.Position = 0;
                using var textReader = new StreamReader(stream, Encoding.UTF8);
                string line;
                while ((line = textReader.ReadLine()!) != null)
                {
                    var parts = line.Split(new[] { '\t', ';', ',' }).Select(p => p.Trim()).ToList();
                    if (parts.Any(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        grid.Add(parts);
                    }
                }
            }
        }

        return grid;
    }

    private static List<List<string>> ParseHtmlTable(string html)
    {
        var grid = new List<List<string>>();

        // TR eşleştirme
        var trRegex = new Regex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var tdRegex = new Regex(@"<(td|th)[^>]*>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var trMatches = trRegex.Matches(html);
        foreach (Match trMatch in trMatches)
        {
            var trContent = trMatch.Groups[1].Value;
            var tdMatches = tdRegex.Matches(trContent);
            var row = new List<string>();
            foreach (Match tdMatch in tdMatches)
            {
                var cellHtml = tdMatch.Groups[2].Value;
                // Tagları temizle
                var text = Regex.Replace(cellHtml, @"<[^>]+>", " ");
                text = System.Net.WebUtility.HtmlDecode(text).Trim();
                row.Add(text);
            }
            if (row.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                grid.Add(row);
            }
        }

        return grid;
    }

    public static EOkulParseResult ParseGrid(List<List<string>> grid)
    {
        var result = new EOkulParseResult { Success = true };

        EOkulClassGroup? currentGroup = null;
        string currentClassroom = "";
        string currentGrade = "9";
        string currentBranch = "A";
        string currentFieldName = "";
        string currentEduType = "Örgün Eğitim";
        string? currentTeacher = null;

        bool inStudentTable = false;
        int colSeq = -1;
        int colNumber = -1;
        int colFirst = -1;
        int colLast = -1;
        int colGender = -1;
        int colBirth = -1;
        int colAge = -1;

        foreach (var row in grid)
        {
            var combinedRowText = string.Join(" ", row.Where(c => !string.IsNullOrWhiteSpace(c))).Trim();
            if (string.IsNullOrWhiteSpace(combinedRowText)) continue;

            // 1. BLOK BAŞLIĞI TESPİTİ (Örn: "... AMP - 9. Sınıf / A Şubesi (BİLİŞİM TEKNOLOJİLERİ ALANI) Sınıf Listesi")
            if (combinedRowText.Contains("Sınıf Listesi", StringComparison.OrdinalIgnoreCase) ||
                (combinedRowText.Contains("Sınıf", StringComparison.OrdinalIgnoreCase) && combinedRowText.Contains("Şube", StringComparison.OrdinalIgnoreCase)))
            {
                inStudentTable = false;

                // Öğretim Türü
                if (combinedRowText.Contains("MESEM", StringComparison.OrdinalIgnoreCase))
                {
                    currentEduType = "MESEM";
                }
                else
                {
                    currentEduType = "Örgün Eğitim";
                }

                // Sınıf Seviyesi ve Şube Ayrıştırma
                // Regex 1: "9. Sınıf / A Şubesi" veya "12. Sınıf / B Şubesi"
                var classMatch = Regex.Match(combinedRowText, @"(\d+)\s*\.?\s*Sınıf\s*/\s*([A-Za-zÇĞİÖŞÜçğıöşü0-9]+)\s*Şube", RegexOptions.IgnoreCase);
                if (classMatch.Success)
                {
                    currentGrade = classMatch.Groups[1].Value;
                    currentBranch = classMatch.Groups[2].Value.ToUpperInvariant();
                    currentClassroom = $"{currentGrade}-{currentBranch}";
                }
                else
                {
                    // Fallback
                    var gradeMatch = Regex.Match(combinedRowText, @"\b(9|10|11|12)\b");
                    if (gradeMatch.Success) currentGrade = gradeMatch.Value;
                    var branchMatch = Regex.Match(combinedRowText, @"([A-ZÇĞİÖŞÜ])\s*Şubesi", RegexOptions.IgnoreCase);
                    currentBranch = branchMatch.Success ? branchMatch.Groups[1].Value.ToUpperInvariant() : "A";
                    currentClassroom = $"{currentGrade}-{currentBranch}";
                }

                // Alan Adı Ayrıştırma (Örn: "(BİLİŞİM TEKNOLOJİLERİ ALANI)")
                var fieldMatch = Regex.Match(combinedRowText, @"\(([^)]*ALANI?[^)]*)\)", RegexOptions.IgnoreCase);
                if (fieldMatch.Success)
                {
                    string rawField = fieldMatch.Groups[1].Value;
                    rawField = Regex.Replace(rawField, @"\bALANI?\b", "", RegexOptions.IgnoreCase).Trim();
                    currentFieldName = CleanTurkishTitle(rawField);
                }
                else
                {
                    var parenMatch = Regex.Match(combinedRowText, @"\(([^)]+)\)");
                    if (parenMatch.Success)
                    {
                        currentFieldName = CleanTurkishTitle(parenMatch.Groups[1].Value.Trim());
                    }
                }

                // Yeni grup oluştur
                currentGroup = new EOkulClassGroup
                {
                    Classroom = currentClassroom,
                    Grade = currentGrade,
                    Branch = currentBranch,
                    FieldName = currentFieldName,
                    EducationType = currentEduType,
                    TeacherName = currentTeacher
                };
                result.Groups.Add(currentGroup);
                continue;
            }

            // 2. SINIF ÖĞRETMENİ VE MÜDÜR YRD SATIRI
            if (combinedRowText.Contains("Sınıf Öğretmeni", StringComparison.OrdinalIgnoreCase))
            {
                var teachMatch = Regex.Match(combinedRowText, @"Sınıf\s*Öğretmeni\s*:\s*([^:\r\n\t]+?)(?:Sınıf\s*Başkanı|$)", RegexOptions.IgnoreCase);
                if (teachMatch.Success)
                {
                    currentTeacher = CleanTurkishTitle(teachMatch.Groups[1].Value.Trim());
                    if (currentGroup != null) currentGroup.TeacherName = currentTeacher;
                }
                continue;
            }

            // 3. TABLO SÜTUN BAŞLIKLARI (Örn: "S.No", "Öğrenci No", "Adı", "Soyadı", "Cinsiyet", "Doğum Tarihi", "Yaşı")
            if (combinedRowText.Contains("Öğrenci No", StringComparison.OrdinalIgnoreCase) ||
                (combinedRowText.Contains("S.No", StringComparison.OrdinalIgnoreCase) && combinedRowText.Contains("Adı", StringComparison.OrdinalIgnoreCase)))
            {
                inStudentTable = true;
                colSeq = -1; colNumber = -1; colFirst = -1; colLast = -1; colGender = -1; colBirth = -1; colAge = -1;

                for (int i = 0; i < row.Count; i++)
                {
                    var colHeader = row[i].Trim().ToLowerInvariant();
                    if (colHeader.Contains("s.no") || colHeader == "sno" || colHeader == "sıra") colSeq = i;
                    else if (colHeader.Contains("öğrenci no") || colHeader.Contains("ogrenci no") || colHeader.Contains("okul no")) colNumber = i;
                    else if (colHeader == "adı" || colHeader == "adi" || colHeader == "ad") colFirst = i;
                    else if (colHeader == "soyadı" || colHeader == "soyadi" || colHeader == "soyad") colLast = i;
                    else if (colHeader.Contains("cinsiyet")) colGender = i;
                    else if (colHeader.Contains("doğum") || colHeader.Contains("dogum")) colBirth = i;
                    else if (colHeader.Contains("yaş") || colHeader.Contains("yas")) colAge = i;
                }

                // Varsayılan kolon indeksleri eğer tam uyuşmadıysa
                if (colNumber == -1 && row.Count >= 2) colNumber = 1;
                if (colFirst == -1 && row.Count >= 3) colFirst = 2;
                if (colLast == -1 && row.Count >= 4) colLast = 3;
                if (colGender == -1 && row.Count >= 5) colGender = 4;
                if (colBirth == -1 && row.Count >= 6) colBirth = 5;
                if (colAge == -1 && row.Count >= 7) colAge = 6;
                continue;
            }

            // 4. BLOK SONU ÖZET SATIRI (Örn: "Kız Öğrenci Sayısı : 18 Toplam Öğrenci Sayısı : 18")
            if (combinedRowText.Contains("Toplam Öğrenci", StringComparison.OrdinalIgnoreCase) ||
                combinedRowText.Contains("Öğrenci Sayısı", StringComparison.OrdinalIgnoreCase))
            {
                inStudentTable = false;
                continue;
            }

            // 5. ÖĞRENCİ VERİ SATIRI
            if (inStudentTable)
            {
                // Hücrelerden değerleri oku
                string getVal(int idx) => (idx >= 0 && idx < row.Count) ? row[idx].Trim() : "";

                string seqStr = getVal(colSeq);
                string numStr = getVal(colNumber);
                string firstStr = getVal(colFirst);
                string lastStr = getVal(colLast);
                string genderStr = getVal(colGender);
                string birthStr = getVal(colBirth);
                string ageStr = getVal(colAge);

                // Eğer numStr sayısal değilse satırı kontrol et
                if (string.IsNullOrWhiteSpace(numStr) || !numStr.All(char.IsDigit))
                {
                    // Belki colSeq ile colNumber kaymıştır
                    if (seqStr.All(char.IsDigit) && firstStr.All(char.IsDigit))
                    {
                        numStr = firstStr;
                        firstStr = getVal(colFirst + 1);
                        lastStr = getVal(colLast + 1);
                    }
                    else
                    {
                        continue;
                    }
                }

                // En az numara ve ad/soyad olmalı
                if (string.IsNullOrWhiteSpace(numStr) || (string.IsNullOrWhiteSpace(firstStr) && string.IsNullOrWhiteSpace(lastStr)))
                {
                    continue;
                }

                int.TryParse(seqStr, out int seqNum);
                int.TryParse(ageStr, out int ageVal);
                var birthDate = ParseTurkishDate(birthStr);

                // Cinsiyet normalize et
                string normGender = "Kız";
                if (genderStr.Contains("erkek", StringComparison.OrdinalIgnoreCase) || genderStr.Equals("E", StringComparison.OrdinalIgnoreCase))
                {
                    normGender = "Erkek";
                }

                var student = new EOkulParsedStudent
                {
                    SequenceNumber = seqNum,
                    StudentNumber = numStr,
                    FirstName = CleanTurkishTitle(firstStr),
                    LastName = lastStr.ToUpper(new CultureInfo("tr-TR")),
                    Gender = normGender,
                    BirthDate = birthDate,
                    Age = ageVal > 0 ? ageVal : null,
                    Classroom = currentClassroom,
                    Grade = currentGrade,
                    Branch = currentBranch,
                    FieldName = currentFieldName,
                    EducationType = currentEduType,
                    TeacherName = currentTeacher,
                    IsSelected = true
                };

                if (currentGroup == null)
                {
                    currentGroup = new EOkulClassGroup
                    {
                        Classroom = currentClassroom,
                        Grade = currentGrade,
                        Branch = currentBranch,
                        FieldName = currentFieldName,
                        EducationType = currentEduType
                    };
                    result.Groups.Add(currentGroup);
                }

                currentGroup.Students.Add(student);
                result.AllStudents.Add(student);
            }
        }

        if (result.AllStudents.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Dosyada e-Okul formatına uygun öğrenci kaydı bulunamadı. Lütfen e-Okul Sınıf Listesi dosyasını kontrol ediniz.";
        }

        return result;
    }

    public static DateTime? ParseTurkishDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();

        var formats = new[] { "dd/MM/yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "d/M/yyyy", "d.M.yyyy", "dd-MM-yyyy", "d-M-yyyy" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt;
        }

        if (DateTime.TryParse(text, new CultureInfo("tr-TR"), DateTimeStyles.None, out var dt2))
        {
            return dt2;
        }

        // Metin içerisinde tarih deseni ara (örn. "22/08/1997 ANKARA" veya "16.07.2012")
        var match = Regex.Match(text, @"\b(\d{1,2}[\./\-]\d{1,2}[\./\-]\d{4})\b");
        if (match.Success)
        {
            var datePart = match.Value;
            if (DateTime.TryParseExact(datePart, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt3))
            {
                return dt3;
            }
            if (DateTime.TryParse(datePart, new CultureInfo("tr-TR"), DateTimeStyles.None, out var dt4))
            {
                return dt4;
            }
        }

        return null;
    }

    public static string CleanTurkishTitle(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var words = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var trCulture = new CultureInfo("tr-TR");
        var sb = new StringBuilder();
        foreach (var w in words)
        {
            if (w.Length == 1)
            {
                sb.Append(w.ToUpper(trCulture)).Append(' ');
            }
            else
            {
                sb.Append(char.ToUpper(w[0], trCulture))
                  .Append(w.Substring(1).ToLower(trCulture))
                  .Append(' ');
            }
        }
        return sb.ToString().Trim();
    }
}
