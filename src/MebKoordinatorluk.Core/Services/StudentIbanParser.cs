using System.Data;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace MebKoordinatorluk.Core.Services;

public enum IbanMatchStatus
{
    WillUpdate,      // Sistemde öğrenci var, IBAN güncellenecek / eklenecek
    AlreadySame,     // Sistemdeki IBAN zaten dosyadaki ile aynı
    NotFoundInDb,    // Okul numarası sistemde bulunamadı
    InvalidIban      // IBAN numarası boş veya geçersiz formatta
}

public class ParsedIbanItem
{
    public int RowIndex { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string RawIban { get; set; } = string.Empty;
    public string CleanedIban { get; set; } = string.Empty;
    public string? OptionalName { get; set; }
}

public class StudentIbanParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ParsedIbanItem> Items { get; set; } = [];
    public int TotalCount => Items.Count;
}

public static class StudentIbanParser
{
    static StudentIbanParser()
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

    /// <summary>
    /// IBAN karakterlerini temizler: Boşluk, çizgi, nokta kaldırılır, büyük harfe çevrilir.
    /// Eğer 24 haneli rakam girilmişse (TR yazılmamışsa) başına TR eklenir.
    /// </summary>
    public static string NormalizeIban(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var clean = Regex.Replace(raw.Trim(), @"[\s\-_.\t]+", "").ToUpperInvariant();

        // TR kodu olmadan 24 basamaklı sadece rakam girilmişse otomatik "TR" ekle
        if (clean.Length == 24 && clean.All(char.IsDigit))
        {
            clean = "TR" + clean;
        }

        return clean;
    }

    /// <summary>
    /// IBAN temel geçerlilik kontrolü (Türkiye için TR + 24 hane = 26 karakter).
    /// </summary>
    public static bool IsValidIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;
        var clean = NormalizeIban(iban);

        // Türkiye IBAN standartı: TR ile başlar ve devamı 24 rakamdır (toplam 26 karakter)
        if (clean.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            return clean.Length == 26 && clean[2..].All(char.IsDigit);
        }

        // Uluslararası genel IBAN formatı (en az 15, en çok 34 karakter)
        return clean.Length >= 15 && clean.Length <= 34 && Regex.IsMatch(clean, @"^[A-Z]{2}[0-9A-Z]{13,32}$");
    }

    /// <summary>
    /// Okul numarasını temizler (boşlukları temizler).
    /// </summary>
    public static string CleanStudentNumber(string? num)
    {
        if (string.IsNullOrWhiteSpace(num)) return string.Empty;
        var trimmed = num.Trim();
        var match = Regex.Match(trimmed, @"^\d+");
        if (match.Success) return match.Value;
        var any = Regex.Match(trimmed, @"\d+");
        return any.Success ? any.Value : trimmed;
    }

    public static StudentIbanParseResult ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new StudentIbanParseResult { Success = false, ErrorMessage = "Dosya bulunamadı." };
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
            return new StudentIbanParseResult { Success = false, ErrorMessage = $"Dosya okuma hatası: {ex.Message}" };
        }
    }

    public static StudentIbanParseResult ParseStream(Stream stream, string fileName)
    {
        try
        {
            var grid = ExtractGrid(stream, fileName);
            if (grid.Count == 0)
            {
                return new StudentIbanParseResult { Success = false, ErrorMessage = "Dosya içerisinde okunabilir veri satırı bulunamadı." };
            }

            return ParseGrid(grid);
        }
        catch (Exception ex)
        {
            return new StudentIbanParseResult { Success = false, ErrorMessage = $"Ayrıştırma hatası: {ex.Message}" };
        }
    }

    public static StudentIbanParseResult ParseCsvFile(string filePath)
    {
        var grid = new List<List<string>>();
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
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

        return ParseGrid(grid);
    }

    public static StudentIbanParseResult ParseGrid(List<List<string>> grid)
    {
        var result = new StudentIbanParseResult();
        if (grid.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Tablo boş.";
            return result;
        }

        int headerRowIdx = -1;
        int noColIdx = 0;
        int ibanColIdx = 1;
        int nameColIdx = -1;

        // Başlık satırı tespiti
        for (int r = 0; r < Math.Min(5, grid.Count); r++)
        {
            var row = grid[r];
            int fNo = -1;
            int fIban = -1;
            int fName = -1;

            for (int c = 0; c < row.Count; c++)
            {
                var norm = CleanHeaderString(row[c]);
                if (fNo < 0 && (norm.Contains("OKUL") || norm.Contains("OGRENCI") || norm.Contains("NO") || norm == "NUMARA"))
                {
                    fNo = c;
                }
                else if (fIban < 0 && (norm.Contains("IBAN") || norm.Contains("HESAP") || norm.Contains("BANKA")))
                {
                    fIban = c;
                }
                else if (fName < 0 && (norm.Contains("AD") || norm.Contains("SOYAD") || norm.Contains("ISIM")))
                {
                    fName = c;
                }
            }

            if (fNo >= 0 && fIban >= 0)
            {
                headerRowIdx = r;
                noColIdx = fNo;
                ibanColIdx = fIban;
                nameColIdx = fName;
                break;
            }
        }

        int startRow = headerRowIdx >= 0 ? headerRowIdx + 1 : 0;

        for (int r = startRow; r < grid.Count; r++)
        {
            var row = grid[r];
            if (row.Count == 0) continue;

            string rawNo = noColIdx < row.Count ? row[noColIdx]?.Trim() ?? "" : "";
            string rawIban = ibanColIdx < row.Count ? row[ibanColIdx]?.Trim() ?? "" : "";
            string rawName = nameColIdx >= 0 && nameColIdx < row.Count ? row[nameColIdx]?.Trim() ?? "" : "";

            // Başlık satırı tekrarları veya boş satırları atla
            if (string.IsNullOrWhiteSpace(rawNo) && string.IsNullOrWhiteSpace(rawIban))
                continue;

            var normNo = CleanHeaderString(rawNo);
            if (normNo.Contains("OKUL NO") || normNo.Contains("OGRENCI NO") || normNo.Contains("IBAN"))
                continue;

            var cleanNo = CleanStudentNumber(rawNo);
            var cleanIban = NormalizeIban(rawIban);

            if (string.IsNullOrWhiteSpace(cleanNo) && string.IsNullOrWhiteSpace(cleanIban))
                continue;

            result.Items.Add(new ParsedIbanItem
            {
                RowIndex = r + 1,
                StudentNumber = cleanNo,
                RawIban = rawIban,
                CleanedIban = cleanIban,
                OptionalName = string.IsNullOrWhiteSpace(rawName) ? null : rawName
            });
        }

        if (result.Items.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Dosyada geçerli Okul No ve IBAN satırı bulunamadı.";
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

    /// <summary>
    /// Kullanıcı için 1. sütun Okul No, 2. sütun IBAN olan örnek Excel (.xlsx) dosyasını oluşturur.
    /// </summary>
    public static byte[] GenerateSampleExcelXlsx()
    {
        var sampleRows = new List<(string No, string Iban, string Note)>
        {
            ("101", "TR120006200000012345678901", "Örnek Öğrenci 1"),
            ("102", "TR340001500000098765432109", "Örnek Öğrenci 2"),
            ("103", "TR560006200000011122233344", "Örnek Öğrenci 3"),
            ("104", "TR890001000000055566677788", "Örnek Öğrenci 4")
        };

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // 1. [Content_Types].xml
            WriteZipEntry(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\r\n" +
                "  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\r\n" +
                "  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\r\n" +
                "  <Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>\r\n" +
                "  <Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\r\n" +
                "</Types>");

            // 2. _rels/.rels
            WriteZipEntry(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
                "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>\r\n" +
                "</Relationships>");

            // 3. xl/workbook.xml
            WriteZipEntry(archive, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">\r\n" +
                "  <sheets>\r\n" +
                "    <sheet name=\"IBAN_Listesi\" sheetId=\"1\" r:id=\"rId1\"/>\r\n" +
                "  </sheets>\r\n" +
                "</workbook>");

            // 4. xl/_rels/workbook.xml.rels
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
                "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>\r\n" +
                "</Relationships>");

            // 5. xl/worksheets/sheet1.xml
            var sbSheet = new StringBuilder();
            sbSheet.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sbSheet.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sbSheet.AppendLine("  <sheetData>");

            // Başlık satırı
            sbSheet.AppendLine("    <row r=\"1\">");
            sbSheet.AppendLine("      <c r=\"A1\" t=\"inlineStr\"><is><t>Okul No</t></is></c>");
            sbSheet.AppendLine("      <c r=\"B1\" t=\"inlineStr\"><is><t>IBAN</t></is></c>");
            sbSheet.AppendLine("    </row>");

            // Örnek veri satırları
            int rowIdx = 2;
            foreach (var (no, iban, _) in sampleRows)
            {
                sbSheet.AppendLine($"    <row r=\"{rowIdx}\">");
                sbSheet.AppendLine($"      <c r=\"A{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(no)}</t></is></c>");
                sbSheet.AppendLine($"      <c r=\"B{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(iban)}</t></is></c>");
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
    /// Kullanıcı için 1. sütun Okul No, 2. sütun IBAN olan örnek CSV içeriğini oluşturur.
    /// </summary>
    public static string GenerateSampleCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Okul No;IBAN");
        sb.AppendLine("101;TR120006200000012345678901");
        sb.AppendLine("102;TR340001500000098765432109");
        sb.AppendLine("103;TR560006200000011122233344");
        sb.AppendLine("104;TR890001000000055566677788");
        return sb.ToString();
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }
}
