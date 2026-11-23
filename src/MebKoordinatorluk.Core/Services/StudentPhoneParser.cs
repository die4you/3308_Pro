using System.Data;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace MebKoordinatorluk.Core.Services;

public enum PhoneMatchStatus
{
    WillUpdate,      // Sistemde öğrenci var, telefon/veli bilgisi güncellenecek / eklenecek
    AlreadySame,     // Sistemdeki telefon ve veli bilgileri zaten dosyadaki ile aynı
    NotFoundInDb,    // Okul numarası veya TCKN sistemde bulunamadı
    NoDataProvided,  // Satırda aktarılacak herhangi bir telefon veya veli bilgisi yok
    InvalidData      // Okul numarası boş veya telefon numaraları geçersiz formatta
}

public class ParsedPhoneItem
{
    public int RowIndex { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string? TcKimlikNo { get; set; }
    public string? OptionalName { get; set; }

    public string RawPhone { get; set; } = string.Empty;
    public string CleanedPhone { get; set; } = string.Empty;

    public string RawParentName { get; set; } = string.Empty;
    public string CleanedParentName { get; set; } = string.Empty;

    public string RawParentPhone { get; set; } = string.Empty;
    public string CleanedParentPhone { get; set; } = string.Empty;

    public bool HasAnyData => !string.IsNullOrWhiteSpace(CleanedPhone) ||
                              !string.IsNullOrWhiteSpace(CleanedParentName) ||
                              !string.IsNullOrWhiteSpace(CleanedParentPhone);
}

public class StudentPhoneParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ParsedPhoneItem> Items { get; set; } = [];
    public int TotalCount => Items.Count;
}

public static class StudentPhoneParser
{
    static StudentPhoneParser()
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
    /// Telefon numarasını Türkiye standart formatına dönüştürür (05XX XXX XX XX veya 0XXX XXX XX XX).
    /// Başındaki +90 veya 90 temizlenir, baştaki sıfır yoksa eklenir.
    /// </summary>
    public static string NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Rakam dışındaki tüm karakterleri temizle
        var digits = Regex.Replace(raw.Trim(), @"\D", "");
        if (string.IsNullOrEmpty(digits)) return string.Empty;

        // +90 veya 90 ile başlıyorsa (12 haneli) 90'ı kaldır
        if (digits.Length == 12 && digits.StartsWith("90"))
        {
            digits = digits.Substring(2);
        }
        // Başında 0 olan 11 haneli (05XXXXXXXXX veya 02XXXXXXXXX) ise başındaki 0'ı kaldır
        else if (digits.Length == 11 && digits.StartsWith("0"))
        {
            digits = digits.Substring(1);
        }

        // 10 haneli standart numara (Örn: 5321234567 veya 2123456789)
        if (digits.Length == 10)
        {
            return $"0{digits[..3]} {digits.Substring(3, 3)} {digits.Substring(6, 2)} {digits.Substring(8, 2)}";
        }

        // 11 haneli ve 0 ile başlıyorsa (yukarıda sıyrılmadıysa)
        if (digits.Length == 11 && digits.StartsWith("0"))
        {
            return $"0{digits.Substring(1, 3)} {digits.Substring(4, 3)} {digits.Substring(7, 2)} {digits.Substring(9, 2)}";
        }

        // Standart uzunlukta değilse ve rakam sayısı mantıksız derecede az değilse ham rakamları döndür
        return digits.Length >= 7 ? digits : string.Empty;
    }

    /// <summary>
    /// Telefon numarasının geçerli bir Türkiye cep veya sabit telefon formatında olup olmadığını denetler.
    /// </summary>
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var digits = Regex.Replace(phone, @"\D", "");
        if (digits.Length == 12 && digits.StartsWith("90")) digits = digits.Substring(2);
        else if (digits.Length == 11 && digits.StartsWith("0")) digits = digits.Substring(1);

        return digits.Length == 10;
    }

    /// <summary>
    /// Veli ad soyad alanını temizler (iç ve dış gereksiz boşlukları toparlar).
    /// </summary>
    public static string CleanParentName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var cleaned = Regex.Replace(raw.Trim(), @"\s+", " ");
        return cleaned;
    }

    /// <summary>
    /// Okul numarasını temizler.
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

    /// <summary>
    /// TCKN temizler (11 haneli rakam).
    /// </summary>
    public static string CleanTcNo(string? tc)
    {
        if (string.IsNullOrWhiteSpace(tc)) return string.Empty;
        var digits = Regex.Replace(tc.Trim(), @"\D", "");
        return digits.Length == 11 ? digits : string.Empty;
    }

    public static StudentPhoneParseResult ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new StudentPhoneParseResult { Success = false, ErrorMessage = "Dosya bulunamadı." };
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
            return new StudentPhoneParseResult { Success = false, ErrorMessage = $"Dosya okuma hatası: {ex.Message}" };
        }
    }

    public static StudentPhoneParseResult ParseStream(Stream stream, string fileName)
    {
        try
        {
            var grid = ExtractGrid(stream, fileName);
            if (grid.Count == 0)
            {
                return new StudentPhoneParseResult { Success = false, ErrorMessage = "Dosya içerisinde okunabilir veri satırı bulunamadı." };
            }

            return ParseGrid(grid);
        }
        catch (Exception ex)
        {
            return new StudentPhoneParseResult { Success = false, ErrorMessage = $"Ayrıştırma hatası: {ex.Message}" };
        }
    }

    public static StudentPhoneParseResult ParseCsvFile(string filePath)
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

    public static StudentPhoneParseResult ParseGrid(List<List<string>> grid)
    {
        var result = new StudentPhoneParseResult();
        if (grid.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Tablo boş.";
            return result;
        }

        int headerRowIdx = -1;
        int noColIdx = -1;
        int tcColIdx = -1;
        int nameColIdx = -1;
        int phoneColIdx = -1;
        int parentNameColIdx = -1;
        int parentPhoneColIdx = -1;

        // Başlık satırı tespiti (ilk 5 satırı tara)
        for (int r = 0; r < Math.Min(5, grid.Count); r++)
        {
            var row = grid[r];
            int fNo = -1;
            int fTc = -1;
            int fName = -1;
            int fPhone = -1;
            int fParentName = -1;
            int fParentPhone = -1;

            for (int c = 0; c < row.Count; c++)
            {
                var norm = CleanHeaderString(row[c]);

                // 1. Okul No
                if (fNo < 0 && (norm.Contains("OKUL NO") || norm.Contains("OGRENCI NO") || norm == "NO" || norm == "NUMARA" || norm.Contains("OKUL NUMARASI")))
                {
                    fNo = c;
                }
                // 2. TCKN
                else if (fTc < 0 && (norm.Contains("TCKN") || norm.Contains("TC KIMLIK") || norm == "TC" || norm.Contains("KIMLIK NO")))
                {
                    fTc = c;
                }
                // 3. Veli Telefonu (önce kontrol edilir ki genel telefon ile karışmasın)
                else if (fParentPhone < 0 && (norm.Contains("VELI TEL") || norm.Contains("VELI TELEFON") || norm.Contains("VELI CEP") || norm.Contains("TEL 2") || norm.Contains("TELEFON 2")))
                {
                    fParentPhone = c;
                }
                // 4. Veli Adı
                else if (fParentName < 0 && (norm.Contains("VELI AD") || norm.Contains("VELI ADI") || norm.Contains("VELISI") || norm.Contains("VELI ISIM")))
                {
                    fParentName = c;
                }
                // 5. Öğrenci Telefonu
                else if (fPhone < 0 && (norm.Contains("OGRENCI TEL") || norm.Contains("OGRENCI TELEFON") || norm.Contains("OGRENCI CEP") || norm.Contains("TEL 1") || norm.Contains("TELEFON 1") || norm.Contains("TEL") || norm.Contains("TELEFON") || norm.Contains("CEP")))
                {
                    fPhone = c;
                }
                // 6. Öğrenci Adı Soyadı
                else if (fName < 0 && !norm.Contains("VELI") && (norm.Contains("AD SOYAD") || norm.Contains("ADI SOYADI") || norm.Contains("OGRENCI ADI") || norm == "ISIM"))
                {
                    fName = c;
                }
            }

            // Başlık satırı için en az bir numara kolonu ve en az bir veri kolonu bulunmalı
            if ((fNo >= 0 || fTc >= 0) && (fPhone >= 0 || fParentPhone >= 0 || fParentName >= 0))
            {
                headerRowIdx = r;
                noColIdx = fNo;
                tcColIdx = fTc;
                nameColIdx = fName;
                phoneColIdx = fPhone;
                parentNameColIdx = fParentName;
                parentPhoneColIdx = fParentPhone;
                break;
            }
        }

        // Başlık bulunamadıysa varsayılan pozisyonları tahmin et
        if (headerRowIdx < 0)
        {
            if (grid[0].Count >= 5)
            {
                noColIdx = 0;
                nameColIdx = 1;
                phoneColIdx = 2;
                parentNameColIdx = 3;
                parentPhoneColIdx = 4;
            }
            else if (grid[0].Count == 4)
            {
                noColIdx = 0;
                phoneColIdx = 1;
                parentNameColIdx = 2;
                parentPhoneColIdx = 3;
            }
            else if (grid[0].Count == 3)
            {
                noColIdx = 0;
                phoneColIdx = 1;
                parentPhoneColIdx = 2;
            }
            else if (grid[0].Count == 2)
            {
                noColIdx = 0;
                phoneColIdx = 1;
            }
            else
            {
                noColIdx = 0;
            }
        }

        int startRow = headerRowIdx >= 0 ? headerRowIdx + 1 : 0;

        for (int r = startRow; r < grid.Count; r++)
        {
            var row = grid[r];
            if (row.Count == 0) continue;

            string rawNo = noColIdx >= 0 && noColIdx < row.Count ? row[noColIdx]?.Trim() ?? "" : "";
            string rawTc = tcColIdx >= 0 && tcColIdx < row.Count ? row[tcColIdx]?.Trim() ?? "" : "";
            string rawName = nameColIdx >= 0 && nameColIdx < row.Count ? row[nameColIdx]?.Trim() ?? "" : "";
            string rawPhone = phoneColIdx >= 0 && phoneColIdx < row.Count ? row[phoneColIdx]?.Trim() ?? "" : "";
            string rawParentName = parentNameColIdx >= 0 && parentNameColIdx < row.Count ? row[parentNameColIdx]?.Trim() ?? "" : "";
            string rawParentPhone = parentPhoneColIdx >= 0 && parentPhoneColIdx < row.Count ? row[parentPhoneColIdx]?.Trim() ?? "" : "";

            // Başlık satırı tekrarları veya tamamen boş satırları atla
            if (string.IsNullOrWhiteSpace(rawNo) && string.IsNullOrWhiteSpace(rawTc) && string.IsNullOrWhiteSpace(rawPhone))
                continue;

            var normNo = CleanHeaderString(rawNo);
            if (normNo.Contains("OKUL NO") || normNo.Contains("OGRENCI NO") || normNo.Contains("TELEFON"))
                continue;

            var cleanNo = CleanStudentNumber(rawNo);
            var cleanTc = CleanTcNo(rawTc);
            var cleanPhone = NormalizePhone(rawPhone);
            var cleanParentName = CleanParentName(rawParentName);
            var cleanParentPhone = NormalizePhone(rawParentPhone);

            // Ne öğrenci numarası/TCKN var ne de telefon/veli bilgisi varsa geç
            if (string.IsNullOrWhiteSpace(cleanNo) && string.IsNullOrWhiteSpace(cleanTc))
                continue;

            result.Items.Add(new ParsedPhoneItem
            {
                RowIndex = r + 1,
                StudentNumber = cleanNo,
                TcKimlikNo = string.IsNullOrWhiteSpace(cleanTc) ? null : cleanTc,
                OptionalName = string.IsNullOrWhiteSpace(rawName) ? null : rawName,
                RawPhone = rawPhone,
                CleanedPhone = cleanPhone,
                RawParentName = rawParentName,
                CleanedParentName = cleanParentName,
                RawParentPhone = rawParentPhone,
                CleanedParentPhone = cleanParentPhone
            });
        }

        if (result.Items.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Dosyada geçerli Okul No ve telefon/veli satırı bulunamadı.";
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
    /// Kullanıcı için 5 sütunlu örnek Excel (.xlsx) dosyasını oluşturur:
    /// Okul No | Öğrenci Adı Soyadı | Öğrenci Telefonu | Veli Adı Soyadı | Veli Telefonu
    /// </summary>
    public static byte[] GenerateSampleExcelXlsx()
    {
        var sampleRows = new List<(string No, string Name, string Phone, string ParentName, string ParentPhone)>
        {
            ("101", "Ahmet Yılmaz", "0555 123 45 67", "Mehmet Yılmaz", "0532 987 65 43"),
            ("102", "Ayşe Kaya", "0542 222 33 44", "Fatma Kaya", "0505 333 44 55"),
            ("103", "Can Demir", "0533 444 55 66", "Ali Demir", "0554 666 77 88"),
            ("104", "Zeynep Çelik", "0544 555 66 77", "Emine Çelik", "0535 777 88 99")
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
                "    <sheet name=\"Telefon_Listesi\" sheetId=\"1\" r:id=\"rId1\"/>\r\n" +
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
            sbSheet.AppendLine("      <c r=\"B1\" t=\"inlineStr\"><is><t>Öğrenci Adı Soyadı</t></is></c>");
            sbSheet.AppendLine("      <c r=\"C1\" t=\"inlineStr\"><is><t>Öğrenci Telefonu</t></is></c>");
            sbSheet.AppendLine("      <c r=\"D1\" t=\"inlineStr\"><is><t>Veli Adı Soyadı</t></is></c>");
            sbSheet.AppendLine("      <c r=\"E1\" t=\"inlineStr\"><is><t>Veli Telefonu</t></is></c>");
            sbSheet.AppendLine("    </row>");

            // Örnek veri satırları
            int rowIdx = 2;
            foreach (var (no, name, phone, parentName, parentPhone) in sampleRows)
            {
                sbSheet.AppendLine($"    <row r=\"{rowIdx}\">");
                sbSheet.AppendLine($"      <c r=\"A{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(no)}</t></is></c>");
                sbSheet.AppendLine($"      <c r=\"B{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(name)}</t></is></c>");
                sbSheet.AppendLine($"      <c r=\"C{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(phone)}</t></is></c>");
                sbSheet.AppendLine($"      <c r=\"D{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(parentName)}</t></is></c>");
                sbSheet.AppendLine($"      <c r=\"E{rowIdx}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(parentPhone)}</t></is></c>");
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
    /// Kullanıcı için örnek CSV şablon içeriğini oluşturur.
    /// </summary>
    public static string GenerateSampleCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Okul No;Öğrenci Adı Soyadı;Öğrenci Telefonu;Veli Adı Soyadı;Veli Telefonu");
        sb.AppendLine("101;Ahmet Yılmaz;0555 123 45 67;Mehmet Yılmaz;0532 987 65 43");
        sb.AppendLine("102;Ayşe Kaya;0542 222 33 44;Fatma Kaya;0505 333 44 55");
        sb.AppendLine("103;Can Demir;0533 444 55 66;Ali Demir;0554 666 77 88");
        sb.AppendLine("104;Zeynep Çelik;0544 555 66 77;Emine Çelik;0535 777 88 99");
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
