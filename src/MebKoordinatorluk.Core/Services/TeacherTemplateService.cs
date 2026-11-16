using System.Data;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.Core.Services;

public class ParsedTeacherItem
{
    public int RowIndex { get; set; }
    public string? TcKimlikNo { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string AvailableDays { get; set; } = "Salı";
    public int MaxCoordinationHours { get; set; } = 16;
    public int CoordinatorFeeHours { get; set; } = 16;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class TeacherParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ParsedTeacherItem> Teachers { get; set; } = [];
    public int TotalCount => Teachers.Count;
}

public static class TeacherTemplateService
{
    static TeacherTemplateService()
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

    public static TeacherParseResult ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new TeacherParseResult { Success = false, ErrorMessage = "Dosya bulunamadı." };
        }

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".csv" || ext == ".txt")
            {
                return ParseCsvFile(filePath);
            }

            using var stream = File.OpenRead(filePath);
            return ParseStream(stream, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            return new TeacherParseResult { Success = false, ErrorMessage = $"Dosya okuma hatası: {ex.Message}" };
        }
    }

    public static TeacherParseResult ParseStream(Stream stream, string fileName)
    {
        try
        {
            var grid = ExtractGrid(stream, fileName);
            if (grid.Count == 0)
            {
                return new TeacherParseResult { Success = false, ErrorMessage = "Dosya içerisinde okunabilir veri satırı bulunamadı." };
            }

            return ParseGrid(grid);
        }
        catch (Exception ex)
        {
            return new TeacherParseResult { Success = false, ErrorMessage = $"Ayrıştırma hatası: {ex.Message}" };
        }
    }

    public static List<List<string>> ParseCsvLines(IEnumerable<string> lines)
    {
        var grid = new List<List<string>>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            char separator = ';';
            if (!line.Contains(';') && line.Contains(',')) separator = ',';
            else if (!line.Contains(';') && line.Contains('\t')) separator = '\t';

            var parts = line.Split(separator).Select(p => p.Trim(' ', '"', '\'')).ToList();
            if (parts.Any(p => !string.IsNullOrWhiteSpace(p)))
            {
                grid.Add(parts);
            }
        }
        return grid;
    }

    public static TeacherParseResult ParseCsvFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        return ParseGrid(ParseCsvLines(lines));
    }

    public static TeacherParseResult ParseGrid(List<List<string>> grid)
    {
        var result = new TeacherParseResult();
        if (grid.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Tablo boş.";
            return result;
        }

        int headerRowIdx = -1;
        int idxTc = -1;
        int idxFirst = -1;
        int idxLast = -1;
        int idxFullName = -1;
        int idxBranch = -1;
        int idxDays = -1;
        int idxMaxHours = -1;
        int idxCoordHours = -1;
        int idxPhone = -1;
        int idxEmail = -1;
        int idxActive = -1;

        // Başlık satırı tespiti
        for (int r = 0; r < Math.Min(5, grid.Count); r++)
        {
            var row = grid[r];
            int fFirst = -1, fLast = -1, fFull = -1, fBranch = -1;

            for (int c = 0; c < row.Count; c++)
            {
                var norm = CleanHeaderString(row[c]);
                if (norm.Contains("TC") || norm.Contains("KIMLIK")) idxTc = c;
                else if (norm == "ADI" || norm == "AD" || norm == "FIRSTNAME" || norm.StartsWith("OGRETMEN ADI") || norm.StartsWith("OGRETMEN AD")) fFirst = c;
                else if (norm == "SOYADI" || norm == "SOYAD" || norm == "LASTNAME" || norm.StartsWith("OGRETMEN SOYADI") || norm.StartsWith("OGRETMEN SOYAD")) fLast = c;
                else if (norm.Contains("AD") && norm.Contains("SOYAD")) fFull = c;
                else if (norm.Contains("BRANS") || norm.Contains("ALAN") || norm.Contains("BOLUM")) fBranch = c;
                else if (norm.Contains("GUN") || norm.Contains("GOREV") || norm.Contains("KOORDINATORLUK G")) idxDays = c;
                else if (norm.Contains("AZAMI") || norm.Contains("MAKS") || norm.Contains("MAX") || norm.Contains("YUK")) idxMaxHours = c;
                else if (norm.Contains("ALDIGI") || norm.Contains("UCRET") || norm.Contains("EK DERS") || norm.Contains("SAAT")) idxCoordHours = c;
                else if (norm.Contains("TEL") || norm.Contains("GSM") || norm.Contains("TELEFON")) idxPhone = c;
                else if (norm.Contains("POSTA") || norm.Contains("MAIL")) idxEmail = c;
                else if (norm.Contains("AKTIF") || norm.Contains("DURUM")) idxActive = c;
            }

            if ((fFirst >= 0 && fLast >= 0) || fFull >= 0 || (fBranch >= 0 && (fFirst >= 0 || fFull >= 0)))
            {
                headerRowIdx = r;
                idxFirst = fFirst;
                idxLast = fLast;
                idxFullName = fFull;
                idxBranch = fBranch;
                break;
            }
        }

        // Eğer başlık bulunamadıysa standart varsayılan sütun sırası
        int startRow = headerRowIdx >= 0 ? headerRowIdx + 1 : 0;
        if (headerRowIdx < 0)
        {
            idxTc = 0;
            idxFirst = 1;
            idxLast = 2;
            idxBranch = 3;
            idxDays = 4;
            idxMaxHours = 5;
            idxCoordHours = 6;
            idxPhone = 7;
            idxEmail = 8;
            idxActive = 9;
        }

        for (int r = startRow; r < grid.Count; r++)
        {
            var row = grid[r];
            if (row.Count == 0) continue;

            string GetCol(int colIdx) => colIdx >= 0 && colIdx < row.Count ? row[colIdx]?.Trim() ?? "" : "";

            string rawTc = GetCol(idxTc);
            string first = "";
            string last = "";

            if (idxFirst >= 0 && idxLast >= 0)
            {
                first = GetCol(idxFirst);
                last = GetCol(idxLast);
            }
            else if (idxFullName >= 0)
            {
                var full = GetCol(idxFullName);
                if (!string.IsNullOrWhiteSpace(full))
                {
                    var parts = full.Split(' ', 2);
                    first = parts[0];
                    last = parts.Length > 1 ? parts[1] : "";
                }
            }
            else if (row.Count >= 3)
            {
                first = row[1].Trim();
                last = row[2].Trim();
            }

            if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
                continue;

            // Başlık satırı tekrarı kontrolü
            var normFirst = CleanHeaderString(first);
            if (normFirst == "ADI" || normFirst == "AD" || normFirst == "OGRETMEN ADI")
                continue;

            string cleanTc = Regex.Replace(rawTc, @"\D", "");
            if (cleanTc.Length != 11) cleanTc = "";

            string branch = GetCol(idxBranch);
            if (string.IsNullOrWhiteSpace(branch)) branch = "Bilişim Teknolojileri";

            string days = GetCol(idxDays);
            if (string.IsNullOrWhiteSpace(days)) days = "Salı";
            days = days.Replace(';', ',');

            string maxStr = GetCol(idxMaxHours);
            int maxH = int.TryParse(Regex.Match(maxStr, @"\d+").Value, out var mh) && mh > 0 ? mh : 16;

            string coordStr = GetCol(idxCoordHours);
            int coordH = int.TryParse(Regex.Match(coordStr, @"\d+").Value, out var ch) && ch >= 0 ? ch : maxH;

            string phone = GetCol(idxPhone);
            string email = GetCol(idxEmail);

            string activeStr = GetCol(idxActive);
            bool isActive = true;
            if (!string.IsNullOrWhiteSpace(activeStr))
            {
                var nAct = CleanHeaderString(activeStr);
                if (nAct.Contains("PASIF") || nAct == "0" || nAct == "HAYIR" || nAct == "NO")
                    isActive = false;
            }

            result.Teachers.Add(new ParsedTeacherItem
            {
                RowIndex = r + 1,
                TcKimlikNo = string.IsNullOrWhiteSpace(cleanTc) ? null : cleanTc,
                FirstName = first,
                LastName = last,
                Branch = branch,
                AvailableDays = days,
                MaxCoordinationHours = maxH,
                CoordinatorFeeHours = coordH,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                IsActive = isActive
            });
        }

        if (result.Teachers.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Dosyada geçerli öğretmen bilgisi satırı bulunamadı.";
            return result;
        }

        result.Success = true;
        return result;
    }

    private static string CleanHeaderString(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return string.Empty;
        var s = val.Trim()
            .Replace("ı", "I")
            .Replace("İ", "I")
            .Replace("i", "I")
            .Replace("ğ", "G")
            .Replace("Ğ", "G")
            .Replace("ü", "U")
            .Replace("Ü", "U")
            .Replace("ş", "S")
            .Replace("Ş", "S")
            .Replace("ö", "O")
            .Replace("Ö", "O")
            .Replace("ç", "C")
            .Replace("Ç", "C")
            .ToUpperInvariant();
        return Regex.Replace(s, @"\s+", " ");
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

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".csv" || ext == ".txt")
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var lines = new List<string>();
            string? l;
            while ((l = reader.ReadLine()) != null) lines.Add(l);
            return ParseCsvLines(lines);
        }

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
            if (stream.CanSeek)
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                string text = reader.ReadToEnd();
                grid = ParseHtmlTable(text);
                if (grid.Count == 0)
                {
                    var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    grid = ParseCsvLines(lines);
                }
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

    /// <summary>
    /// Öğretmen ekleme formunun birebir alanlarına uygun örnek Excel (.xlsx) dosyasını oluşturur.
    /// </summary>
    public static byte[] GenerateSampleExcelXlsx()
    {
        var headers = new[]
        {
            "T.C. Kimlik No",
            "Öğretmen Adı",
            "Öğretmen Soyadı",
            "Alanı / Branşı",
            "Koordinatörlük Günleri",
            "Azami Koordinatörlük Yükü",
            "Aldığı Koordinatörlük Saati",
            "Telefon Numarası",
            "E-Posta Adresi",
            "Aktif"
        };

        var sampleRows = new List<string[]>
        {
            new[] { "11111111110", "Ahmet", "YILDIZ", "Bilişim Teknolojileri", "Salı,Çarşamba", "16", "16", "05321112233", "ahmet.yildiz@meb.k12.tr", "Aktif" },
            new[] { "22222222220", "Mehmet", "ÖZTÜRK", "Elektrik-Elektronik Teknolojisi", "Pazartesi,Perşembe", "16", "16", "05332223344", "mehmet.ozturk@meb.k12.tr", "Aktif" },
            new[] { "", "Ayşe", "DEMİR", "Çocuk Gelişimi ve Eğitimi", "Çarşamba,Cuma", "16", "16", "05553334455", "ayse.demir@meb.k12.tr", "Aktif" }
        };

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            WriteZipEntry(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\r\n" +
                "  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\r\n" +
                "  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\r\n" +
                "  <Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>\r\n" +
                "  <Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\r\n" +
                "</Types>");

            WriteZipEntry(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
                "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>\r\n" +
                "</Relationships>");

            WriteZipEntry(archive, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">\r\n" +
                "  <sheets>\r\n" +
                "    <sheet name=\"Ogretmenler\" sheetId=\"1\" r:id=\"rId1\"/>\r\n" +
                "  </sheets>\r\n" +
                "</workbook>");

            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
                "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>\r\n" +
                "</Relationships>");

            var sbSheet = new StringBuilder();
            sbSheet.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sbSheet.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sbSheet.AppendLine("  <sheetData>");

            // Başlık satırı
            sbSheet.AppendLine("    <row r=\"1\">");
            for (int c = 0; c < headers.Length; c++)
            {
                string colLetter = GetExcelColumnLetter(c + 1);
                sbSheet.AppendLine($"      <c r=\"{colLetter}1\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(headers[c])}</t></is></c>");
            }
            sbSheet.AppendLine("    </row>");

            // Örnek veriler
            int rowIdx = 2;
            foreach (var row in sampleRows)
            {
                sbSheet.AppendLine($"    <row r=\"{rowIdx}\">");
                for (int c = 0; c < row.Length; c++)
                {
                    string colLetter = GetExcelColumnLetter(c + 1);
                    sbSheet.AppendLine($"      <c r=\"{colLetter}{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(row[c])}</t></is></c>");
                }
                sbSheet.AppendLine("    </row>");
                rowIdx++;
            }

            sbSheet.AppendLine("  </sheetData>");
            sbSheet.AppendLine("</worksheet>");

            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", sbSheet.ToString());
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Öğretmen ekleme formunun birebir alanlarına uygun örnek CSV içeriği oluşturur.
    /// </summary>
    public static string GenerateSampleCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("T.C. Kimlik No;Öğretmen Adı;Öğretmen Soyadı;Alanı / Branşı;Koordinatörlük Günleri;Azami Koordinatörlük Yükü;Aldığı Koordinatörlük Saati;Telefon Numarası;E-Posta Adresi;Aktif");
        sb.AppendLine("11111111110;Ahmet;YILDIZ;Bilişim Teknolojileri;Salı,Çarşamba;16;16;05321112233;ahmet.yildiz@meb.k12.tr;Aktif");
        sb.AppendLine("22222222220;Mehmet;ÖZTÜRK;Elektrik-Elektronik Teknolojisi;Pazartesi,Perşembe;16;16;05332223344;mehmet.ozturk@meb.k12.tr;Aktif");
        sb.AppendLine(";Ayşe;DEMİR;Çocuk Gelişimi ve Eğitimi;Çarşamba,Cuma;16;16;05553334455;ayse.demir@meb.k12.tr;Aktif");
        return sb.ToString();
    }

    private static string GetExcelColumnLetter(int colIndex)
    {
        string letter = "";
        while (colIndex > 0)
        {
            int mod = (colIndex - 1) % 26;
            letter = (char)('A' + mod) + letter;
            colIndex = (colIndex - mod) / 26;
        }
        return letter;
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }
}
