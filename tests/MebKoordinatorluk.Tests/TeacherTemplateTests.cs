using System.Text;
using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class TeacherTemplateTests
{
    [Fact]
    public void GenerateSampleCsv_ShouldContainExpectedHeadersAndDelimiter()
    {
        // Act
        var csv = TeacherTemplateService.GenerateSampleCsv();

        // Assert
        Assert.NotNull(csv);
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2);

        var header = lines[0];
        Assert.Contains("T.C. Kimlik No", header);
        Assert.Contains("Öğretmen Adı", header);
        Assert.Contains("Öğretmen Soyadı", header);
        Assert.Contains("Alanı / Branşı", header);
        Assert.Contains("Koordinatörlük Günleri", header);
        Assert.Contains("Azami Koordinatörlük Yükü", header);
        Assert.Contains("Aldığı Koordinatörlük Saati", header);
        Assert.Contains("Telefon Numarası", header);
        Assert.Contains("E-Posta Adresi", header);
        Assert.Contains("Aktif", header);
    }

    [Fact]
    public void ParseStream_Csv_ShouldCorrectlyExtractTeacherRows()
    {
        // Arrange
        var csvContent = "T.C. Kimlik No;Öğretmen Adı;Öğretmen Soyadı;Alanı / Branşı;Koordinatörlük Günleri;Azami Koordinatörlük Yükü;Aldığı Koordinatörlük Saati;Telefon Numarası;E-Posta Adresi;Aktif\r\n" +
                         "12345678901;Ahmet;Yılmaz;Bilişim Teknolojileri;Pazartesi, Salı;20;16;05551234567;ahmet@meb.k12.tr;Evet\r\n" +
                         ";Ayşe;Kaya;Elektrik-Elektronik Teknolojisi;Çarşamba;18;12;;ayse@meb.k12.tr;1\r\n";

        var bytes = Encoding.UTF8.GetBytes(csvContent);
        using var stream = new MemoryStream(bytes);

        // Act
        var result = TeacherTemplateService.ParseStream(stream, "ogretmenler.csv");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Teachers.Count);

        var r1 = result.Teachers[0];
        Assert.Equal("12345678901", r1.TcKimlikNo);
        Assert.Equal("Ahmet", r1.FirstName);
        Assert.Equal("Yılmaz", r1.LastName);
        Assert.Equal("Bilişim Teknolojileri", r1.Branch);
        Assert.Equal("Pazartesi, Salı", r1.AvailableDays);
        Assert.Equal(20, r1.MaxCoordinationHours);
        Assert.Equal(16, r1.CoordinatorFeeHours);
        Assert.Equal("05551234567", r1.Phone);
        Assert.Equal("ahmet@meb.k12.tr", r1.Email);
        Assert.True(r1.IsActive);

        var r2 = result.Teachers[1];
        Assert.Null(r2.TcKimlikNo);
        Assert.Equal("Ayşe", r2.FirstName);
        Assert.Equal("Kaya", r2.LastName);
        Assert.Equal("Elektrik-Elektronik Teknolojisi", r2.Branch);
        Assert.Equal("Çarşamba", r2.AvailableDays);
        Assert.Equal(18, r2.MaxCoordinationHours);
        Assert.Equal(12, r2.CoordinatorFeeHours);
        Assert.Null(r2.Phone);
        Assert.Equal("ayse@meb.k12.tr", r2.Email);
        Assert.True(r2.IsActive);
    }

    [Fact]
    public void GenerateSampleExcelXlsx_ShouldGenerateValidZipAndParseCorrectly()
    {
        // Act
        var xlsxBytes = TeacherTemplateService.GenerateSampleExcelXlsx();

        // Assert
        Assert.NotNull(xlsxBytes);
        Assert.True(xlsxBytes.Length > 0);

        using var stream = new MemoryStream(xlsxBytes);
        var result = TeacherTemplateService.ParseStream(stream, "test_template.xlsx");

        Assert.True(result.Success);
        Assert.NotEmpty(result.Teachers);
        Assert.True(result.Teachers.Count >= 2);
        Assert.Contains(result.Teachers, r => r.FirstName == "Ahmet" && r.LastName == "YILDIZ");
    }

    [Fact]
    public void ParseGrid_WithoutHeaders_ShouldUsePositionalColumns()
    {
        // Arrange: no header, direct data
        var rawRows = new List<List<string>>
        {
            new() { "11111111110", "Mehmet", "Demir", "Makine Teknolojisi", "Cuma", "16", "10", "05001112233", "mehmet@test.com", "Hayır" }
        };

        // Act
        var result = TeacherTemplateService.ParseGrid(rawRows);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Teachers);
        Assert.Equal("11111111110", result.Teachers[0].TcKimlikNo);
        Assert.Equal("Mehmet", result.Teachers[0].FirstName);
        Assert.Equal("Demir", result.Teachers[0].LastName);
        Assert.Equal("Makine Teknolojisi", result.Teachers[0].Branch);
        Assert.False(result.Teachers[0].IsActive);
    }
}
