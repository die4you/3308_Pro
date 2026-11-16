using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace MebKoordinatorluk.Core.Services;

public class EOkulParsedTcStudent
{
    public string StudentNumber { get; set; } = string.Empty;
    public string TcKimlikNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Classroom { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string FatherName { get; set; } = string.Empty;
    public string MotherName { get; set; } = string.Empty;
}

public class EOkulTcParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<EOkulParsedTcStudent> Students { get; set; } = [];
    public int TotalStudentsCount => Students.Count;
}

public static class EOkulTcKimlikParser
{
    static EOkulTcKimlikParser()
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

    public static EOkulTcParseResult ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new EOkulTcParseResult { Success = false, ErrorMessage = "Dosya bulunamadı." };
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return ParseStream(stream, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            return new EOkulTcParseResult { Success = false, ErrorMessage = $"Dosya açılamadı: {ex.Message}" };
        }
    }

    public static EOkulTcParseResult ParseStream(Stream stream, string fileName)
    {
        try
        {
            var grid = ExtractGrid(stream, fileName);
            if (grid.Count == 0)
            {
                return new EOkulTcParseResult { Success = false, ErrorMessage = "Dosya içerisinde okunabilir veri satırı bulunamadı." };
            }

            return ParseGrid(grid);
        }
        catch (Exception ex)
        {
            return new EOkulTcParseResult { Success = false, ErrorMessage = $"Ayrıştırma hatası: {ex.Message}" };
        }
    }

    public static EOkulTcParseResult ParseGrid(List<List<string>> grid)
    {
        var result = new EOkulTcParseResult();

        // 1. Blok / Kart Formatını Dene (Kullanıcının paylaştığı e-Okul raporu)
        var blockStudents = ParseBlockFormat(grid);
        if (blockStudents.Count > 0)
        {
            result.Success = true;
            result.Students = blockStudents;
            return result;
        }

        // 2. Standart Tablo / Sütun Formatını Dene
        var tableStudents = ParseTableFormat(grid);
        if (tableStudents.Count > 0)
        {
            result.Success = true;
            result.Students = tableStudents;
            return result;
        }

        result.Success = false;
        result.ErrorMessage = "Excel dosyasında öğrenci numarası ve T.C. Kimlik No içeren geçerli kayıt bulunamadı.";
        return result;
    }

    /// <summary>
    /// e-Okul öğrenci bilgi kartı / blok formatı:
    /// Her öğrenci için art arda satırlarda "Öğrenci Numarası....:", "T.C. Kimlik No........:", "Adı Soyadı...........:" vb. etiketler bulunur.
    /// </summary>
    public static List<EOkulParsedTcStudent> ParseBlockFormat(List<List<string>> grid)
    {
        var list = new List<EOkulParsedTcStudent>();
        var culture = new CultureInfo("tr-TR");

        // Çoklu sütun yerleşimlerini de desteklemek için sütun indeksine göre aktif öğrencileri tutar
        var activeStudents = new Dictionary<int, EOkulParsedTcStudent>();

        for (int r = 0; r < grid.Count; r++)
        {
            var row = grid[r];
            if (row.Count == 0) continue;

            for (int c = 0; c < row.Count; c++)
            {
                var rawCell = row[c]?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(rawCell)) continue;

                var normCell = NormalizeLabel(rawCell, culture);

                if (IsStudentNumberLabel(normCell))
                {
                    // Eğer bu sütunda önceden tamamlanmış bir öğrenci varsa listeye ekle
                    if (activeStudents.TryGetValue(c, out var prevStudent) && IsValidStudent(prevStudent))
                    {
                        list.Add(prevStudent);
                    }

                    var newStudent = new EOkulParsedTcStudent();
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: false);
                    newStudent.StudentNumber = CleanNumber(val);
                    activeStudents[c] = newStudent;
                }
                else if (IsTcKimlikLabel(normCell))
                {
                    // En yakın veya ilgili sütundaki aktif öğrenciyi bul
                    var student = GetOrCreateActiveStudent(activeStudents, c);
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: true);
                    student.TcKimlikNo = CleanTc(val);
                }
                else if (IsNameLabel(normCell))
                {
                    var student = GetOrCreateActiveStudent(activeStudents, c);
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: false);
                    if (!string.IsNullOrWhiteSpace(val)) student.FullName = CleanText(val);
                }
                else if (IsClassLabel(normCell))
                {
                    var student = GetOrCreateActiveStudent(activeStudents, c);
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: false);
                    if (!string.IsNullOrWhiteSpace(val)) student.Classroom = CleanText(val);
                }
                else if (IsBirthDateLabel(normCell))
                {
                    var student = GetOrCreateActiveStudent(activeStudents, c);
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: false);
                    if (!string.IsNullOrWhiteSpace(val)) student.BirthDate = CleanText(val);
                }
                else if (IsGenderLabel(normCell))
                {
                    var student = GetOrCreateActiveStudent(activeStudents, c);
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: false);
                    if (!string.IsNullOrWhiteSpace(val)) student.Gender = CleanText(val);
                }
                else if (IsFatherNameLabel(normCell))
                {
                    var student = GetOrCreateActiveStudent(activeStudents, c);
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: false);
                    if (!string.IsNullOrWhiteSpace(val)) student.FatherName = CleanText(val);
                }
                else if (IsMotherNameLabel(normCell))
                {
                    var student = GetOrCreateActiveStudent(activeStudents, c);
                    var val = FindValueInRowOrCell(row, c, rawCell, isTc: false);
                    if (!string.IsNullOrWhiteSpace(val)) student.MotherName = CleanText(val);
                }
            }
        }

        // Kalan aktif öğrencileri topla
        foreach (var student in activeStudents.Values)
        {
            if (IsValidStudent(student))
            {
                list.Add(student);
            }
        }

        // Tekrarlı numaraları ayıkla (aynı numaradan birden fazla geldiyse ilki veya en dolusu)
        return list
            .GroupBy(s => s.StudentNumber)
            .Select(g => g.OrderByDescending(s => !string.IsNullOrWhiteSpace(s.TcKimlikNo) ? 1 : 0)
                          .ThenByDescending(s => !string.IsNullOrWhiteSpace(s.FullName) ? 1 : 0)
                          .First())
            .ToList();
    }

    /// <summary>
    /// Standart Tablo formatı: Başlık satırında "Okul No", "TC Kimlik No", "Adı Soyadı" olan tablolar.
    /// </summary>
    public static List<EOkulParsedTcStudent> ParseTableFormat(List<List<string>> grid)
    {
        var list = new List<EOkulParsedTcStudent>();
        var culture = new CultureInfo("tr-TR");

        int headerRow = -1;
        int colNo = -1;
        int colTc = -1;
        int colName = -1;
        int colClass = -1;

        for (int r = 0; r < Math.Min(grid.Count, 25); r++)
        {
            var row = grid[r];
            for (int c = 0; c < row.Count; c++)
            {
                var norm = NormalizeLabel(row[c] ?? "", culture);
                if (IsStudentNumberLabel(norm) || norm == "NO" || norm == "OKUL NO" || norm == "ÖĞRENCİ NO")
                {
                    colNo = c;
                }
                else if (IsTcKimlikLabel(norm) || norm == "TC" || norm == "TC NO" || norm == "TC KİMLİK")
                {
                    colTc = c;
                }
                else if (IsNameLabel(norm) || norm == "AD SOYAD" || norm == "ADI VE SOYADI" || norm == "ÖĞRENCİ ADI SOYADI")
                {
                    colName = c;
                }
                else if (IsClassLabel(norm) || norm == "SINIF" || norm == "ŞUBE")
                {
                    colClass = c;
                }
            }

            if (colNo >= 0 && colTc >= 0)
            {
                headerRow = r;
                break;
            }
        }

        if (headerRow == -1 || colNo == -1 || colTc == -1)
        {
            return list;
        }

        for (int r = headerRow + 1; r < grid.Count; r++)
        {
            var row = grid[r];
            string noVal = colNo < row.Count ? row[colNo] : "";
            string tcVal = colTc < row.Count ? row[colTc] : "";
            string nameVal = colName >= 0 && colName < row.Count ? row[colName] : "";
            string classVal = colClass >= 0 && colClass < row.Count ? row[colClass] : "";

            string cleanNo = CleanNumber(noVal);
            string cleanTc = CleanTc(tcVal);

            if (!string.IsNullOrWhiteSpace(cleanNo) && !string.IsNullOrWhiteSpace(cleanTc))
            {
                list.Add(new EOkulParsedTcStudent
                {
                    StudentNumber = cleanNo,
                    TcKimlikNo = cleanTc,
                    FullName = CleanText(nameVal),
                    Classroom = CleanText(classVal)
                });
            }
        }

        return list;
    }

    private static string NormalizeLabel(string text, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var s = text.Replace(".", "").Replace(":", "").Replace("-", "").Replace(";", "").Trim();
        s = Regex.Replace(s, @"\s+", " ");
        return s.ToUpper(culture);
    }

    private static bool IsStudentNumberLabel(string norm)
    {
        return norm.StartsWith("ÖĞRENCİ NUMARASI") ||
               norm.StartsWith("ÖĞRENCİ NO") ||
               norm.StartsWith("OKUL NUMARASI") ||
               norm.StartsWith("OKUL NO") ||
               norm == "ÖĞRENCİ NO" ||
               norm == "OKUL NO";
    }

    private static bool IsTcKimlikLabel(string norm)
    {
        return norm.StartsWith("TC KİMLİK NO") ||
               norm.StartsWith("TC KİMLİK") ||
               norm.StartsWith("TC NO") ||
               norm.StartsWith("TÜRKİYE CUMHURİYETİ KİMLİK") ||
               norm.StartsWith("TCKİMLİK");
    }

    private static bool IsNameLabel(string norm)
    {
        return norm.StartsWith("ADI SOYADI") ||
               norm.StartsWith("AD SOYAD") ||
               norm.StartsWith("ADI VE SOYADI") ||
               norm.StartsWith("ÖĞRENCİ ADI");
    }

    private static bool IsClassLabel(string norm)
    {
        return norm.StartsWith("SINIFI/ŞUBESİ") ||
               norm.StartsWith("SINIF/ŞUBE") ||
               norm.StartsWith("SINIFI") ||
               norm.StartsWith("ŞUBESİ");
    }

    private static bool IsBirthDateLabel(string norm)
    {
        return norm.StartsWith("DOĞUM TARİHİ") ||
               norm.StartsWith("D TARİHİ");
    }

    private static bool IsGenderLabel(string norm)
    {
        return norm.StartsWith("CİNSİYETİ") ||
               norm.StartsWith("CİNSİYET");
    }

    private static bool IsFatherNameLabel(string norm)
    {
        return norm.StartsWith("BABA ADI");
    }

    private static bool IsMotherNameLabel(string norm)
    {
        return norm.StartsWith("ANNE ADI");
    }

    private static bool IsValidStudent(EOkulParsedTcStudent s)
    {
        return !string.IsNullOrWhiteSpace(s.StudentNumber) &&
               !string.IsNullOrWhiteSpace(s.TcKimlikNo) &&
               s.TcKimlikNo.Length == 11;
    }

    private static EOkulParsedTcStudent GetOrCreateActiveStudent(Dictionary<int, EOkulParsedTcStudent> activeStudents, int colIdx)
    {
        if (activeStudents.TryGetValue(colIdx, out var student))
        {
            return student;
        }

        // En yakın önceki sütundaki aktif öğrenciyi bul (blok genellikle aynı sütun civarındadır)
        if (activeStudents.Count > 0)
        {
            var closestCol = activeStudents.Keys.OrderBy(k => Math.Abs(k - colIdx)).First();
            if (Math.Abs(closestCol - colIdx) <= 5)
            {
                return activeStudents[closestCol];
            }
        }

        var newSt = new EOkulParsedTcStudent();
        activeStudents[colIdx] = newSt;
        return newSt;
    }

    /// <summary>
    /// Hücre metninin içinde veya satırın devamındaki sütunlarda değeri arar.
    /// </summary>
    private static string FindValueInRowOrCell(List<string> row, int labelColIdx, string rawLabelCell, bool isTc)
    {
        // 1. Hücrenin kendisi iki nokta içeriyorsa (örn: "Öğrenci Numarası: 121" veya "T.C. Kimlik No: 10274844088")
        if (rawLabelCell.Contains(':'))
        {
            var parts = rawLabelCell.Split(':', 2);
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var candidate = parts[1].Trim();
                if (isTc)
                {
                    var tcMatch = Regex.Match(candidate, @"\b\d{11}\b");
                    if (tcMatch.Success) return tcMatch.Value;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
                }
            }
        }

        // 2. Satırın sonraki sütunlarını tara (etiketten sonraki sütunlar)
        string? fallbackNonEmpty = null;
        for (int c = labelColIdx + 1; c < row.Count; c++)
        {
            var cellVal = row[c]?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(cellVal)) continue;

            // Eğer başka bir etiket başladıysa bu bloğun değer alanını geçtik demektir
            if (cellVal.Contains("....") || cellVal.EndsWith(":")) break;

            if (isTc)
            {
                var tcMatch = Regex.Match(cellVal, @"\b\d{11}\b");
                if (tcMatch.Success) return tcMatch.Value;

                // Sadece rakamlardan oluşuyorsa ve uzunluğu 11 ise
                var digitsOnly = Regex.Replace(cellVal, @"\D", "");
                if (digitsOnly.Length == 11) return digitsOnly;
            }
            else
            {
                fallbackNonEmpty ??= cellVal;
            }
        }

        return fallbackNonEmpty ?? string.Empty;
    }

    private static string CleanNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var m = Regex.Match(text, @"\b\d+\b");
        return m.Success ? m.Value : text.Trim();
    }

    private static string CleanTc(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var digits = Regex.Replace(text, @"\D", "");
        if (digits.Length == 11) return digits;
        var m = Regex.Match(text, @"\b\d{11}\b");
        return m.Success ? m.Value : digits;
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Regex.Replace(text.Trim(), @"\s+", " ");
    }

    private static List<List<string>> ExtractGrid(Stream stream, string fileName)
    {
        var grid = new List<List<string>>();

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
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            string fullText = reader.ReadToEnd();
            grid = ParseHtmlTable(fullText);
            if (grid.Count > 0) return grid;
        }

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
            // If binary reader failed, try HTML/XML fallback
            if (stream.CanSeek)
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string text = reader.ReadToEnd();
                grid = ParseHtmlTable(text);
            }
        }

        return grid;
    }

    private static List<List<string>> ParseHtmlTable(string html)
    {
        var rows = new List<List<string>>();
        var trMatches = Regex.Matches(html, @"<tr[^>]*>(.*?)<\/tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match tr in trMatches)
        {
            var rowList = new List<string>();
            var tdMatches = Regex.Matches(tr.Groups[1].Value, @"<t[dh][^>]*>(.*?)<\/t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match td in tdMatches)
            {
                string cellText = Regex.Replace(td.Groups[1].Value, @"<[^>]+>", " ");
                cellText = System.Net.WebUtility.HtmlDecode(cellText).Trim();
                cellText = Regex.Replace(cellText, @"\s+", " ");
                rowList.Add(cellText);
            }

            if (rowList.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                rows.Add(rowList);
            }
        }

        return rows;
    }
}
