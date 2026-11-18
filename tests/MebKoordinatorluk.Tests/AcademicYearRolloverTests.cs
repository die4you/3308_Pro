using System;
using System.IO;
using System.Threading.Tasks;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Services;
using Microsoft.Data.Sqlite;
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

    [Fact]
    public void DatabaseConfig_SwitchToYear_ActiveYear_ShouldPointToYearDatabase()
    {
        var config = new DatabaseConfig();
        config.SwitchToYear("2026-2027", false);

        Assert.Equal(config.GetDbPathForYear("2026-2027"), config.DatabasePath);
        Assert.False(config.IsArchiveReadOnly);
        Assert.Equal("2026-2027", config.CurrentAcademicYear);
        Assert.EndsWith("meb_2026_2027.db", config.DatabasePath);
    }

    [Fact]
    public void DatabaseConfig_SwitchToYear_ArchiveYear_ShouldPointToPastYearDatabase()
    {
        var config = new DatabaseConfig();
        config.SwitchToYear("2024-2025", true);

        Assert.True(config.IsArchiveReadOnly);
        Assert.EndsWith("meb_2024_2025.db", config.DatabasePath);
        Assert.Equal("2024-2025", config.CurrentAcademicYear);

        // Switch back to active year
        config.SwitchToYear("2026-2027", false);
        Assert.EndsWith("meb_2026_2027.db", config.DatabasePath);
        Assert.False(config.IsArchiveReadOnly);
    }

    [Fact]
    public async Task DatabaseBackupService_CreateBackupAsync_ShouldUseAcademicYearInFileName()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"meb_2026_2027_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(tempDb);
        config.SwitchToYear("2026-2027", false);
        var factory = new SqliteConnectionFactory(config);
        var initializer = new DatabaseInitializer(factory);
        initializer.Initialize();

        var backupService = new DatabaseBackupService(factory, config);
        var backupPath = await backupService.CreateBackupAsync();

        try
        {
            Assert.True(File.Exists(backupPath));
            var fileName = Path.GetFileName(backupPath);
            Assert.StartsWith("meb_2026_2027_", fileName);
            Assert.EndsWith(".bak", fileName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb)) try { File.Delete(tempDb); } catch { }
            if (File.Exists(backupPath)) try { File.Delete(backupPath); } catch { }
        }
    }
}
