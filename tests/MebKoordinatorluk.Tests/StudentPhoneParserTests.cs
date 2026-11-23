using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.Tests;

public class StudentPhoneParserTests
{
    [Theory]
    [InlineData("0555 123 45 67", "0555 123 45 67")]
    [InlineData("5551234567", "0555 123 45 67")]
    [InlineData("+905551234567", "0555 123 45 67")]
    [InlineData("90 555 123 45 67", "0555 123 45 67")]
    [InlineData("(0555) 123-4567", "0555 123 45 67")]
    [InlineData("0212 345 67 89", "0212 345 67 89")]
    [InlineData("2123456789", "0212 345 67 89")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("yok", "")]
    public void NormalizePhone_ShouldCleanAndFormatPhoneCorrectly(string? raw, string expected)
    {
        var result = StudentPhoneParser.NormalizePhone(raw);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0555 123 45 67", true)]
    [InlineData("5551234567", true)]
    [InlineData("+905551234567", true)]
    [InlineData("0212 345 67 89", true)]
    [InlineData("12345", false)] // Çok kısa
    [InlineData("055512345678901", false)] // Çok uzun
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPhone_ShouldValidateTurkishPhones(string? phone, bool expected)
    {
        var result = StudentPhoneParser.IsValidPhone(phone);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  Ahmet   Yılmaz  ", "Ahmet Yılmaz")]
    [InlineData("Fatma Kaya", "Fatma Kaya")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void CleanParentName_ShouldTrimAndCollapseWhitespace(string? raw, string expected)
    {
        var result = StudentPhoneParser.CleanParentName(raw);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseGrid_StandardFiveColumnsWithHeader_ShouldParseCorrectly()
    {
        var grid = new List<List<string>>
        {
            new() { "Okul No", "Öğrenci Adı Soyadı", "Öğrenci Telefonu", "Veli Adı Soyadı", "Veli Telefonu" },
            new() { "101", "Ahmet Yılmaz", "0555 123 45 67", "Mehmet Yılmaz", "0532 987 65 43" },
            new() { "102", "Ayşe Kaya", "+90 542 222 33 44", "Fatma Kaya", "0505 333 44 55" },
            new() { "103", "Can Demir", "5334445566", "", "" }
        };

        var result = StudentPhoneParser.ParseGrid(grid);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalCount);

        Assert.Equal("101", result.Items[0].StudentNumber);
        Assert.Equal("Ahmet Yılmaz", result.Items[0].OptionalName);
        Assert.Equal("0555 123 45 67", result.Items[0].CleanedPhone);
        Assert.Equal("Mehmet Yılmaz", result.Items[0].CleanedParentName);
        Assert.Equal("0532 987 65 43", result.Items[0].CleanedParentPhone);

        Assert.Equal("102", result.Items[1].StudentNumber);
        Assert.Equal("0542 222 33 44", result.Items[1].CleanedPhone);
        Assert.Equal("Fatma Kaya", result.Items[1].CleanedParentName);

        Assert.Equal("103", result.Items[2].StudentNumber);
        Assert.Equal("0533 444 55 66", result.Items[2].CleanedPhone);
        Assert.Empty(result.Items[2].CleanedParentName);
        Assert.Empty(result.Items[2].CleanedParentPhone);
    }

    [Fact]
    public void GenerateSampleExcelXlsx_ShouldProduceValidNonEmptyZip()
    {
        var bytes = StudentPhoneParser.GenerateSampleExcelXlsx();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 500);
    }

    [Fact]
    public void GenerateSampleCsv_ShouldProduceValidCsvContent()
    {
        var csv = StudentPhoneParser.GenerateSampleCsv();
        Assert.False(string.IsNullOrWhiteSpace(csv));
        Assert.Contains("Okul No", csv);
        Assert.Contains("Öğrenci Telefonu", csv);
        Assert.Contains("Veli Telefonu", csv);
        Assert.Contains("101", csv);
    }
}
