using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.Tests;

public class StudentIbanParserTests
{
    [Theory]
    [InlineData("TR12 0006 2000 0001 2345 6789 01", "TR120006200000012345678901")]
    [InlineData("tr120006200000012345678901", "TR120006200000012345678901")]
    [InlineData("120006200000012345678901", "TR120006200000012345678901")] // 24 hane rakam girildiğinde TR eklenmeli
    [InlineData(" TR-34 0001 5000 0009 8765 4321 09 ", "TR340001500000098765432109")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeIban_ShouldCleanAndFormatIbanCorrectly(string? raw, string expected)
    {
        var result = StudentIbanParser.NormalizeIban(raw);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("TR120006200000012345678901", true)]
    [InlineData("TR12 0006 2000 0001 2345 6789 01", true)]
    [InlineData("120006200000012345678901", true)] // Otomatik TR ile 26 haneye tamamlanır
    [InlineData("TR12345", false)] // Çok kısa
    [InlineData("TR1200062000000123456789019999", false)] // Çok uzun
    [InlineData("TR12000620000001234567890A", false)] // Rakam harici karakter
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidIban_ShouldValidateTurkishIban(string? iban, bool expected)
    {
        var result = StudentIbanParser.IsValidIban(iban);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseGrid_StandardTwoColumnWithHeader_ShouldParseCorrectly()
    {
        var grid = new List<List<string>>
        {
            new() { "Okul No", "IBAN" },
            new() { "101", "TR12 0006 2000 0001 2345 6789 01" },
            new() { "102", "TR340001500000098765432109" },
            new() { "103", "560006200000011122233344" } // 24 haneli rakam
        };

        var result = StudentIbanParser.ParseGrid(grid);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalCount);

        Assert.Equal("101", result.Items[0].StudentNumber);
        Assert.Equal("TR120006200000012345678901", result.Items[0].CleanedIban);

        Assert.Equal("102", result.Items[1].StudentNumber);
        Assert.Equal("TR340001500000098765432109", result.Items[1].CleanedIban);

        Assert.Equal("103", result.Items[2].StudentNumber);
        Assert.Equal("TR560006200000011122233344", result.Items[2].CleanedIban);
    }

    [Fact]
    public void ParseGrid_ThreeColumnsWithNames_ShouldDetectAndParse()
    {
        var grid = new List<List<string>>
        {
            new() { "Okul No", "Adı Soyadı", "IBAN Numarası" },
            new() { "201", "Ahmet Yılmaz", "TR120006200000012345678901" },
            new() { "202", "Mehmet Kaya", "TR340001500000098765432109" }
        };

        var result = StudentIbanParser.ParseGrid(grid);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("201", result.Items[0].StudentNumber);
        Assert.Equal("Ahmet Yılmaz", result.Items[0].OptionalName);
        Assert.Equal("TR120006200000012345678901", result.Items[0].CleanedIban);
    }

    [Fact]
    public void GenerateSampleExcelXlsx_ShouldProduceValidXlsxStream()
    {
        var bytes = StudentIbanParser.GenerateSampleExcelXlsx();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        using var ms = new MemoryStream(bytes);
        var result = StudentIbanParser.ParseStream(ms, "sample.xlsx");

        Assert.True(result.Success);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal("101", result.Items[0].StudentNumber);
        Assert.StartsWith("TR12", result.Items[0].CleanedIban);
    }

    [Fact]
    public void GenerateSampleCsv_ShouldProduceValidCsvText()
    {
        var csv = StudentIbanParser.GenerateSampleCsv();
        Assert.False(string.IsNullOrWhiteSpace(csv));
        Assert.Contains("Okul No;IBAN", csv);
        Assert.Contains("101;TR120006200000012345678901", csv);
    }
}
