using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class EOkulTcKimlikParserTests
{
    [Fact]
    public void ParseBlockFormat_ShouldExtractStudentsMatchingUserScreenshot()
    {
        // Arrange - Kullanıcının paylaştığı e-Okul ekran görüntüsündeki sütun ve etiket yapısı
        // E sütunu: Etiketler (Öğrenci Numarası....:, T.C. Kimlik No........:, vb.)
        // G sütunu: Adı Soyadı, Baba Adı, Anne Adı, Cinsiyeti
        // H sütunu: Öğrenci No değeri, TC Kimlik No değeri, Doğum Tarihi
        var grid = new List<List<string>>
        {
            // Öğrenci 1: 121 / 10274844088 / SEDA HAN
            new() { "", "", "", "", "Öğrenci Numarası....:", "", "", "121" },
            new() { "", "", "", "", "T.C. Kimlik No........:", "", "", "10274844088" },
            new() { "", "", "", "", "Sınıfı/Şubesi........:", "", "", "" },
            new() { "", "", "", "", "Adı Soyadı...........:", "", "SEDA HAN", "" },
            new() { "", "", "", "", "Baba Adı.............:", "", "DURMUŞ", "" },
            new() { "", "", "", "", "Anne Adı.............:", "", "SATİ", "" },
            new() { "", "", "", "", "Cinsiyeti............:", "", "Kız", "" },
            new() { "", "", "", "", "Doğum Tarihi.........:", "", "", "22/08/1997" },

            // Öğrenci 2: 186 / 50920488006 / NAZLI BUDAK
            new() { "", "", "", "", "Öğrenci Numarası....:", "", "", "186" },
            new() { "", "", "", "", "T.C. Kimlik No........:", "", "", "50920488006" },
            new() { "", "", "", "", "Sınıfı/Şubesi........:", "", "", "" },
            new() { "", "", "", "", "Adı Soyadı...........:", "", "NAZLI BUDAK", "" },
            new() { "", "", "", "", "Baba Adı.............:", "", "MEHMET", "" },
            new() { "", "", "", "", "Anne Adı.............:", "", "HATUN", "" },
            new() { "", "", "", "", "Cinsiyeti............:", "", "Kız", "" },
            new() { "", "", "", "", "Doğum Tarihi.........:", "", "", "01/01/2001" },

            // Öğrenci 3: 159 / 55798694238 / KADER KOÇAK
            new() { "", "", "", "", "Öğrenci Numarası....:", "", "", "159" },
            new() { "", "", "", "", "T.C. Kimlik No........:", "", "", "55798694238" },
            new() { "", "", "", "", "Sınıfı/Şubesi........:", "", "", "" },
            new() { "", "", "", "", "Adı Soyadı...........:", "", "KADER KOÇAK", "" },
            new() { "", "", "", "", "Baba Adı.............:", "", "EROL", "" },
            new() { "", "", "", "", "Anne Adı.............:", "", "NESLİHAN", "" },
            new() { "", "", "", "", "Cinsiyeti............:", "", "Kız", "" },
            new() { "", "", "", "", "Doğum Tarihi.........:", "", "", "16/07/2012" }
        };

        // Act
        var result = EOkulTcKimlikParser.ParseGrid(grid);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalStudentsCount);

        var st1 = result.Students.FirstOrDefault(s => s.StudentNumber == "121");
        Assert.NotNull(st1);
        Assert.Equal("10274844088", st1.TcKimlikNo);
        Assert.Equal("SEDA HAN", st1.FullName);
        Assert.Equal("Kız", st1.Gender);
        Assert.Equal("22/08/1997", st1.BirthDate);

        var st2 = result.Students.FirstOrDefault(s => s.StudentNumber == "186");
        Assert.NotNull(st2);
        Assert.Equal("50920488006", st2.TcKimlikNo);
        Assert.Equal("NAZLI BUDAK", st2.FullName);

        var st3 = result.Students.FirstOrDefault(s => s.StudentNumber == "159");
        Assert.NotNull(st3);
        Assert.Equal("55798694238", st3.TcKimlikNo);
        Assert.Equal("KADER KOÇAK", st3.FullName);
    }

    [Fact]
    public void ParseTableFormat_ShouldExtractStudents()
    {
        // Arrange - Standart Tablo Formatı
        var grid = new List<List<string>>
        {
            new() { "Sıra No", "Okul No", "Adı Soyadı", "T.C. Kimlik No", "Sınıf" },
            new() { "1", "121", "Seda Han", "10274844088", "12-A" },
            new() { "2", "186", "Nazlı Budak", "50920488006", "12-B" },
            new() { "3", "159", "Kader Koçak", "55798694238", "12-A" }
        };

        // Act
        var result = EOkulTcKimlikParser.ParseGrid(grid);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalStudentsCount);

        var st1 = result.Students.First(s => s.StudentNumber == "121");
        Assert.Equal("10274844088", st1.TcKimlikNo);
        Assert.Equal("Seda Han", st1.FullName);
        Assert.Equal("12-A", st1.Classroom);
    }

    [Fact]
    public void ParseTableFormat_ShouldExtractStudents_WithBirthDate()
    {
        // Arrange - Standart Tablo Formatı Doğum Tarihi ile
        var grid = new List<List<string>>
        {
            new() { "Sıra No", "Okul No", "Adı Soyadı", "T.C. Kimlik No", "Doğum Tarihi", "Sınıf" },
            new() { "1", "121", "Seda Han", "10274844088", "22.08.1997", "12-A" },
            new() { "2", "186", "Nazlı Budak", "50920488006", "01/01/2001", "12-B" }
        };

        // Act
        var result = EOkulTcKimlikParser.ParseGrid(grid);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.TotalStudentsCount);

        var st1 = result.Students.First(s => s.StudentNumber == "121");
        Assert.Equal("10274844088", st1.TcKimlikNo);
        Assert.Equal("22.08.1997", st1.BirthDate);

        var parsedDt1 = EOkulTcKimlikParser.ParseTurkishDate(st1.BirthDate);
        Assert.NotNull(parsedDt1);
        Assert.Equal(1997, parsedDt1.Value.Year);
        Assert.Equal(8, parsedDt1.Value.Month);
        Assert.Equal(22, parsedDt1.Value.Day);

        var st2 = result.Students.First(s => s.StudentNumber == "186");
        Assert.Equal("01/01/2001", st2.BirthDate);
        var parsedDt2 = EOkulTcKimlikParser.ParseTurkishDate(st2.BirthDate);
        Assert.NotNull(parsedDt2);
        Assert.Equal(2001, parsedDt2.Value.Year);
        Assert.Equal(1, parsedDt2.Value.Month);
        Assert.Equal(1, parsedDt2.Value.Day);
    }

    [Theory]
    [InlineData("22/08/1997", 1997, 8, 22)]
    [InlineData("01.01.2001", 2001, 1, 1)]
    [InlineData("16-07-2012", 2012, 7, 16)]
    [InlineData("22/08/1997 ANKARA", 1997, 8, 22)]
    [InlineData("05.10.2005 / İSTANBUL", 2005, 10, 5)]
    public void ParseTurkishDate_ShouldParseVariousFormats(string input, int year, int month, int day)
    {
        var dt = EOkulTcKimlikParser.ParseTurkishDate(input);
        Assert.NotNull(dt);
        Assert.Equal(year, dt.Value.Year);
        Assert.Equal(month, dt.Value.Month);
        Assert.Equal(day, dt.Value.Day);
    }
}
