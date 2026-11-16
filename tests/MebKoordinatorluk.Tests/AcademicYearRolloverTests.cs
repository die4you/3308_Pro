using System;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class AcademicYearRolloverTests
{
    [Theory]
    [InlineData("11-A", "12-A")]
    [InlineData("11/B", "12/B")]
    [InlineData("11 C", "12 C")]
    [InlineData("AMP 11-A", "AMP 12-A")]
    [InlineData("10-A", "11-A")]
    [InlineData("10/B", "11/B")]
    [InlineData("9-A", "10-A")]
    [InlineData("9/C", "10/C")]
    public void PromoteClassName_ShouldPromoteGradesCorrectly(string oldName, string expectedNewName)
    {
        var result = AcademicYearRolloverService.PromoteClassName(oldName);
        Assert.Equal(expectedNewName, result);
    }

    [Fact]
    public void ParseYearDates_ShouldParseStandardAcademicYears()
    {
        var (s1, e1) = LicenseService.ParseYearDates("2026-2027");
        Assert.Equal(2026, s1.Year);
        Assert.Equal(9, s1.Month);
        Assert.Equal(2027, e1.Year);
        Assert.Equal(6, e1.Month);

        var (s2, e2) = LicenseService.ParseYearDates("2027_2028");
        Assert.Equal(2027, s2.Year);
        Assert.Equal(2028, e2.Year);
    }

    [Fact]
    public void DatabaseConfig_SanitizeYear_ShouldFormatCorrectly()
    {
        Assert.Equal("2026_2027", DatabaseConfig.SanitizeYear("2026-2027"));
        Assert.Equal("2027_2028", DatabaseConfig.SanitizeYear("2027/2028"));
        Assert.Equal("2028_2029", DatabaseConfig.SanitizeYear(" 2028 - 2029 "));
    }

    [Fact]
    public void LicenseService_GetAcademicYearAccess_PastYear_ShouldBeArchiveReadOnly()
    {
        var pastYear = new AcademicYear
        {
            Name = "2023-2024",
            StartDate = new DateTime(2023, 9, 1),
            EndDate = new DateTime(2024, 6, 20)
        };

        // Pass any dummy factory or mock
        var config = new DatabaseConfig();
        var factory = new SqliteConnectionFactory(config);
        var licenseService = new LicenseService(factory);

        var access = licenseService.GetAcademicYearAccess(pastYear);
        Assert.Equal(AcademicYearAccessStatus.ArchiveReadOnly, access);
    }

    [Fact]
    public void LicenseService_GetAcademicYearAccess_FutureYearWithoutLicense_ShouldBeFutureLocked()
    {
        var futureYear = new AcademicYear
        {
            Name = "2030-2031",
            StartDate = new DateTime(2030, 9, 1),
            EndDate = new DateTime(2031, 6, 20)
        };

        var config = new DatabaseConfig();
        var factory = new SqliteConnectionFactory(config);
        var licenseService = new LicenseService(factory);

        var access = licenseService.GetAcademicYearAccess(futureYear);
        Assert.Equal(AcademicYearAccessStatus.FutureLocked, access);
    }
}
