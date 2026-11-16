using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class DocumentTrackingRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;
    private readonly DocumentTrackingRepository _repo;

    public DocumentTrackingRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_doc_test_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(_tempDbPath);
        _factory = new SqliteConnectionFactory(config);
        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
        _repo = new DocumentTrackingRepository(_factory);
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
    public async Task SaveRecordAsync_WithTransitions_ShouldPersistCorrectly()
    {
        var record = new DocumentTrackingRecord
        {
            Year = 2026,
            Month = 10,
            TeacherId = 1,
            EnterpriseId = 2,
            TransitionStart = true,
            Week1 = true,
            Week2 = false,
            Week3 = true,
            Week4 = false,
            TransitionEnd = true,
            MonthlyReport = true,
            AttendanceSheet = false,
            Notes = "Ekim Geçiş Testi"
        };

        await _repo.SaveRecordAsync(record);

        var list = await _repo.GetForMonthAsync(2026, 10);
        Assert.Single(list);

        var saved = list[0];
        Assert.Equal(1, saved.TeacherId);
        Assert.Equal(2, saved.EnterpriseId);
        Assert.True(saved.TransitionStart);
        Assert.True(saved.TransitionStartDelivered);
        Assert.True(saved.Week1);
        Assert.False(saved.Week2);
        Assert.True(saved.Week3);
        Assert.False(saved.Week4);
        Assert.True(saved.TransitionEnd);
        Assert.True(saved.TransitionEndDelivered);
        Assert.True(saved.MonthlyReport);
        Assert.False(saved.AttendanceSheet);
        Assert.Equal("Ekim Geçiş Testi", saved.Notes);
    }

    [Fact]
    public async Task BulkSetAllAsync_ShouldSetAllColumnsIncludingTransitions()
    {
        var pairs = new List<(int TeacherId, int EnterpriseId)>
        {
            (1, 10),
            (2, 20)
        };

        await _repo.BulkSetAllAsync(2026, 10, true, pairs);

        var list = await _repo.GetForMonthAsync(2026, 10);
        Assert.Equal(2, list.Count);
        foreach (var r in list)
        {
            Assert.True(r.TransitionStart);
            Assert.True(r.Week1);
            Assert.True(r.Week2);
            Assert.True(r.Week3);
            Assert.True(r.Week4);
            Assert.True(r.TransitionEnd);
            Assert.True(r.MonthlyReport);
            Assert.True(r.AttendanceSheet);
        }

        // Test resetting to false
        await _repo.BulkSetAllAsync(2026, 10, false, pairs);
        var resetList = await _repo.GetForMonthAsync(2026, 10);
        Assert.Equal(2, resetList.Count);
        foreach (var r in resetList)
        {
            Assert.False(r.TransitionStart);
            Assert.False(r.Week1);
            Assert.False(r.Week2);
            Assert.False(r.Week3);
            Assert.False(r.Week4);
            Assert.False(r.TransitionEnd);
            Assert.False(r.MonthlyReport);
            Assert.False(r.AttendanceSheet);
        }
    }
}
