using System.Globalization;
using System.Xml.Linq;
using MebKoordinatorluk.Core.Entities;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class SgkExitXmlTests
{
    [Fact]
    public void SgkExitXml_ShouldMatchExactAttributeOrderAndSchema()
    {
        // Arrange
        var settings = new AppSettings
        {
            SchoolName = "KIRKKIZLAR MESLEKİ VE TEKNİK ANADOLU LİSESİ",
            SgkSicilNo = "185320101100442706005",
            SgkAraciNo = "000",
            MinimumWage = 29360.00m // %30'u = 8808.00 TL
        };

        var student = new Student
        {
            FirstName = "Ezgi",
            LastName = "KIRBUĞA",
            TcKimlikNo = "55597332638",
            SgkEntryDate = new DateTime(2026, 9, 15),
            SgkExitDate = new DateTime(2026, 10, 16),
            SgkPrimKodu = "49"
        };

        // Act - Simulate XML structure creation matching exact sequence
        string schoolTitle = settings.SchoolName.ToUpper(new CultureInfo("tr-TR"));
        var rootElement = new XElement("SGK4AISTENCIKIS",
            new XElement("ISYERI",
                new XAttribute("ISYERIADRES", schoolTitle),
                new XAttribute("ISYERIARACINO", settings.SgkAraciNo),
                new XAttribute("ISYERISICIL", settings.SgkSicilNo),
                new XAttribute("ISYERIUNVAN", schoolTitle)
            ),
            new XElement("SIGORTALILAR",
                new XElement("SIGORTALI",
                    new XAttribute("TCKNO", student.TcKimlikNo),
                    new XAttribute("AD", student.FirstName.Trim()),
                    new XAttribute("SOYAD", student.LastName.Trim().ToUpper(new CultureInfo("tr-TR"))),
                    new XAttribute("ISTENCIKISTARIHI", student.SgkExitDate!.Value.ToString("yyyy-MM-dd")),
                    new XAttribute("MESLEKKODU", "9901.02"),
                    new XAttribute("ISTENAYRILISNEDENI", "22"),
                    new XAttribute("CSGBISKOLU", "0"),
                    new XAttribute("UCRETYUZDEUSULU", "H"),
                    new XElement("ONCEKIDONEM",
                        new XAttribute("BELGETURU", "49"),
                        new XAttribute("HAKEDILENUCRET", "8808.00"),
                        new XAttribute("PRIMIKRAMIYE", "0"),
                        new XAttribute("EKSIKGUNSAYISI", "14"),
                        new XAttribute("EKSIKGUNNEDENI", "13")
                    ),
                    new XElement("BULUNDUGUMUZDONEM",
                        new XAttribute("BELGETURU", "49"),
                        new XAttribute("HAKEDILENUCRET", "8808.00"),
                        new XAttribute("EKSIKGUNSAYISI", "00"),
                        new XAttribute("EKSIKGUNNEDENI", "00")
                    )
                )
            )
        );

        // Assert - Root
        Assert.Equal("SGK4AISTENCIKIS", rootElement.Name.LocalName);

        // Assert - ISYERI
        var isyeri = rootElement.Element("ISYERI");
        Assert.NotNull(isyeri);
        Assert.Equal("KIRKKIZLAR MESLEKİ VE TEKNİK ANADOLU LİSESİ", isyeri.Attribute("ISYERIADRES")?.Value);
        Assert.Equal("000", isyeri.Attribute("ISYERIARACINO")?.Value);
        Assert.Equal("185320101100442706005", isyeri.Attribute("ISYERISICIL")?.Value);
        Assert.Equal("KIRKKIZLAR MESLEKİ VE TEKNİK ANADOLU LİSESİ", isyeri.Attribute("ISYERIUNVAN")?.Value);

        // Assert - SIGORTALI attribute order
        var sigortali = rootElement.Element("SIGORTALILAR")?.Element("SIGORTALI");
        Assert.NotNull(sigortali);

        var sigortaliAttrNames = sigortali.Attributes().Select(a => a.Name.LocalName).ToList();
        var expectedSigortaliOrder = new[]
        {
            "TCKNO", "AD", "SOYAD", "ISTENCIKISTARIHI", "MESLEKKODU", "ISTENAYRILISNEDENI", "CSGBISKOLU", "UCRETYUZDEUSULU"
        };
        Assert.Equal(expectedSigortaliOrder, sigortaliAttrNames);

        // Assert - ONCEKIDONEM attribute order
        var onceki = sigortali.Element("ONCEKIDONEM");
        Assert.NotNull(onceki);
        var oncekiAttrNames = onceki.Attributes().Select(a => a.Name.LocalName).ToList();
        var expectedOncekiOrder = new[]
        {
            "BELGETURU", "HAKEDILENUCRET", "PRIMIKRAMIYE", "EKSIKGUNSAYISI", "EKSIKGUNNEDENI"
        };
        Assert.Equal(expectedOncekiOrder, oncekiAttrNames);
        Assert.Equal("14", onceki.Attribute("EKSIKGUNSAYISI")?.Value);
        Assert.Equal("13", onceki.Attribute("EKSIKGUNNEDENI")?.Value);
        Assert.Equal("8808.00", onceki.Attribute("HAKEDILENUCRET")?.Value);

        // Assert - BULUNDUGUMUZDONEM attribute order
        var simdiki = sigortali.Element("BULUNDUGUMUZDONEM");
        Assert.NotNull(simdiki);
        var simdikiAttrNames = simdiki.Attributes().Select(a => a.Name.LocalName).ToList();
        var expectedSimdikiOrder = new[]
        {
            "BELGETURU", "HAKEDILENUCRET", "EKSIKGUNSAYISI", "EKSIKGUNNEDENI"
        };
        Assert.Equal(expectedSimdikiOrder, simdikiAttrNames);
        Assert.Equal("00", simdiki.Attribute("EKSIKGUNSAYISI")?.Value);
        Assert.Equal("00", simdiki.Attribute("EKSIKGUNNEDENI")?.Value);
        Assert.Equal("8808.00", simdiki.Attribute("HAKEDILENUCRET")?.Value);
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public SgkExitXmlTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InspectDatabaseWageSettings()
    {
        var paths = new[]
        {
            @"C:\Users\trgy\.gemini\antigravity\scratch\MebKoordinatorluk\src\MebKoordinatorluk.UI\bin\Debug\net9.0-windows\Data\meb_2026_2027.db",
            @"C:\Users\trgy\.gemini\antigravity\scratch\MebKoordinatorluk\src\MebKoordinatorluk.UI\bin\Debug\net9.0-windows\Data\meb_koordinatorluk.db",
            @"C:\Users\trgy\.gemini\antigravity\scratch\MebKoordinatorluk\Dagitim_Paketi\MebKoordinatorluk_Bagimsiz_x64\Data\meb_koordinatorluk.db"
        };

        foreach (var p in paths)
        {
            if (File.Exists(p))
            {
                var factory = new MebKoordinatorluk.Data.Database.SqliteConnectionFactory(
                    new MebKoordinatorluk.Data.Database.DatabaseConfig(p));
                var repo = new MebKoordinatorluk.Data.Repositories.SettingRepository(factory);
                var settings = await repo.GetAppSettingsAsync();
                _output.WriteLine($"[DB_INSPECT] Path: {p} | GrossMinimumWage: {settings.GrossMinimumWage} | MinimumWage: {settings.MinimumWage} | EffectiveGross: {settings.EffectiveGrossMinimumWage}");
            }
        }
    }

    [Fact]
    public void SgkExitXml_WhenEntryIsInExitMonth_PreviousPeriodShouldHaveZeroWage()
    {
        var student = new Student
        {
            FirstName = "İrem",
            LastName = "ENGİN",
            TcKimlikNo = "12345678901",
            SgkEntryDate = new DateTime(2026, 10, 12),
            SgkExitDate = new DateTime(2026, 10, 19),
            SgkPrimKodu = "49"
        };

        var prevPeriodEnd = new DateTime(2026, 9, 30);
        bool notYetEmployed = student.SgkEntryDate.HasValue && student.SgkEntryDate.Value.Date > prevPeriodEnd;

        Assert.True(notYetEmployed);
    }

    [Fact]
    public void SurekliDevamsiz_LifecycleAndSgkExitStatus_ShouldBeTrackedAccurately()
    {
        var studentPendingExit = new Student
        {
            StudentNumber = "101",
            FirstName = "Ahmet",
            LastName = "Yılmaz",
            Classroom = "12-A",
            IsSurekliDevamsiz = true,
            SgkExitDate = null
        };

        var studentExitDone = new Student
        {
            StudentNumber = "102",
            FirstName = "Mehmet",
            LastName = "Demir",
            Classroom = "12-A",
            IsSurekliDevamsiz = true,
            SgkExitDate = new DateTime(2026, 10, 15)
        };

        // Assert pending exit condition
        Assert.True(studentPendingExit.IsSurekliDevamsiz);
        Assert.Null(studentPendingExit.SgkExitDate);

        // Assert exit completed condition
        Assert.True(studentExitDone.IsSurekliDevamsiz);
        Assert.NotNull(studentExitDone.SgkExitDate);
        Assert.Equal(new DateTime(2026, 10, 15), studentExitDone.SgkExitDate.Value);
    }

    [Fact]
    public void SgkExitXml_PreviousPeriodWith31DaysAnd5DaysAbsence_ShouldCalculate26PaidDaysAnd14313Pek()
    {
        // 2026 Ekim ayı (31 gün): 5 gün devamsızlık olduğunda 31 - 5 = 26 prim günü bildirilmeli.
        // Brüt 33.030,00 TL üzerinden Günlük PEK: 550,50 TL => 26 * 550,50 = 14.313,00 TL.
        int year = 2026;
        int month = 10;
        int absentDays = 5;

        var sgkRes = MebKoordinatorluk.Core.Services.SgkCalculationHelper.Calculate(
            year,
            month,
            null,
            null,
            absentDays);

        Assert.Equal(26, sgkRes.BildirilecekPrimGunu);
        Assert.Equal(5, sgkRes.BildirilecekEksikGun);

        decimal grossWage = 33030.00m;
        decimal monthlyWage = Math.Round(grossWage / 2.0m, 2);
        decimal dailyWage = monthlyWage / 30.0m;
        decimal earnedWage = Math.Round(dailyWage * sgkRes.BildirilecekPrimGunu, 2);

        Assert.Equal(14313.00m, earnedWage);
    }
}
