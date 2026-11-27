using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class MidtermBreakTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;
    private readonly SettingRepository _settingRepo;

    public MidtermBreakTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_midterm_test_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(_tempDbPath);
        _factory = new SqliteConnectionFactory(config);
        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
        _settingRepo = new SettingRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public void OverlapsWithMidtermBreak_ShouldDetectFirstMidtermOverlap()
    {
        var settings = new AppSettings
        {
            FirstMidtermStartDate = "2024-11-11",
            FirstMidtermEndDate = "2024-11-15",
            SecondMidtermStartDate = "2025-04-14",
            SecondMidtermEndDate = "2025-04-18"
        };

        // Exact match (11-15 Nov)
        Assert.True(settings.OverlapsWithMidtermBreak(new DateTime(2024, 11, 11), new DateTime(2024, 11, 15)));

        // Week containing the break (10-17 Nov)
        Assert.True(settings.OverlapsWithMidtermBreak(new DateTime(2024, 11, 10), new DateTime(2024, 11, 17)));

        // Non-overlapping week before (3-7 Nov)
        Assert.False(settings.OverlapsWithMidtermBreak(new DateTime(2024, 11, 3), new DateTime(2024, 11, 7)));

        // Non-overlapping week after (18-22 Nov)
        Assert.False(settings.OverlapsWithMidtermBreak(new DateTime(2024, 11, 18), new DateTime(2024, 11, 22)));
    }

    [Fact]
    public void OverlapsWithMidtermBreak_ShouldDetectSecondMidtermOverlap()
    {
        var settings = new AppSettings
        {
            FirstMidtermStartDate = "2024-11-11",
            FirstMidtermEndDate = "2024-11-15",
            SecondMidtermStartDate = "2025-04-14",
            SecondMidtermEndDate = "2025-04-18"
        };

        // Exact match in April
        Assert.True(settings.OverlapsWithMidtermBreak(new DateTime(2025, 4, 14), new DateTime(2025, 4, 18)));

        // Overlapping week
        Assert.True(settings.OverlapsWithMidtermBreak(new DateTime(2025, 4, 13), new DateTime(2025, 4, 20)));

        // Non-overlapping week in March or late April
        Assert.False(settings.OverlapsWithMidtermBreak(new DateTime(2025, 3, 10), new DateTime(2025, 3, 14)));
        Assert.False(settings.OverlapsWithMidtermBreak(new DateTime(2025, 4, 21), new DateTime(2025, 4, 25)));
    }

    [Fact]
    public async Task SettingRepository_ShouldPersistMidtermBreakDates()
    {
        var settings = await _settingRepo.GetAppSettingsAsync();
        Assert.NotNull(settings);

        settings.FirstMidtermStartDate = "2026-11-09";
        settings.FirstMidtermEndDate = "2026-11-13";
        settings.SecondMidtermStartDate = "2027-04-12";
        settings.SecondMidtermEndDate = "2027-04-16";

        await _settingRepo.SaveAppSettingsAsync(settings);

        var loaded = await _settingRepo.GetAppSettingsAsync();
        Assert.Equal("2026-11-09", loaded.FirstMidtermStartDate);
        Assert.Equal("2026-11-13", loaded.FirstMidtermEndDate);
        Assert.Equal("2027-04-12", loaded.SecondMidtermStartDate);
        Assert.Equal("2027-04-16", loaded.SecondMidtermEndDate);
    }
}
