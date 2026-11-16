using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class YearlyMinimumWageTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;

    public YearlyMinimumWageTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_wage_test_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(_tempDbPath);
        _factory = new SqliteConnectionFactory(config);

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Theory]
    [InlineData(28075.50, 33030.00)]
    [InlineData(28000.00, 32941.18)]
    [InlineData(22104.00, 26004.71)]
    [InlineData(0, 0)]
    public void CalculateGrossFromNet_ShouldDivideBy085_AndRoundTo2Decimals(double netInput, double expectedGross)
    {
        decimal net = (decimal)netInput;
        decimal gross = YearlyMinimumWage.CalculateGrossFromNet(net);

        Assert.Equal((decimal)expectedGross, gross);
    }

    [Fact]
    public async Task YearlyMinimumWage_SeedingAndEffectiveLookup_ShouldWork()
    {
        var repo = new SettingRepository(_factory);

        // 1. Initial seed should have 2025 and 2026
        var wages = (await repo.GetYearlyMinimumWagesAsync()).ToList();
        Assert.NotEmpty(wages);
        Assert.Contains(wages, w => w.Year == 2026);

        // 2. Exact match lookup for 2026
        var wage2026 = await repo.GetEffectiveWageForYearAsync(2026);
        Assert.Equal(2026, wage2026.Year);
        Assert.True(wage2026.NetMinimumWage > 0);
        Assert.Equal(YearlyMinimumWage.CalculateGrossFromNet(wage2026.NetMinimumWage), wage2026.GrossMinimumWage);

        // 3. Fallback lookup for unconfigured future year 2028 should return closest previous (2026)
        var wage2028Fallback = await repo.GetEffectiveWageForYearAsync(2028);
        Assert.Equal(2026, wage2028Fallback.Year);

        // 4. Add 2027 wage
        var newWage2027 = new YearlyMinimumWage
        {
            Year = 2027,
            NetMinimumWage = 35000.00m
        };
        await repo.SaveYearlyMinimumWageAsync(newWage2027);

        var retrieved2027 = await repo.GetYearlyMinimumWageAsync(2027);
        Assert.NotNull(retrieved2027);
        Assert.Equal(35000.00m, retrieved2027.NetMinimumWage);
        Assert.Equal(Math.Round(35000.00m / 0.85m, 2), retrieved2027.GrossMinimumWage);

        // 5. Fallback for 2028 should now pick 2027
        var wage2028AfterAdd = await repo.GetEffectiveWageForYearAsync(2028);
        Assert.Equal(2027, wage2028AfterAdd.Year);
    }

    [Fact]
    public async Task DeleteYearlyMinimumWage_ShouldEnforceSafetyLimit()
    {
        var repo = new SettingRepository(_factory);

        // Ensure we have 2025, 2026, 2027
        await repo.SaveYearlyMinimumWageAsync(new YearlyMinimumWage { Year = 2027, NetMinimumWage = 35000m });

        // Deleting 2027 should succeed
        bool deleted2027 = await repo.DeleteYearlyMinimumWageAsync(2027);
        Assert.True(deleted2027);

        // Deleting 2025 should succeed
        bool deleted2025 = await repo.DeleteYearlyMinimumWageAsync(2025);
        Assert.True(deleted2025);

        // Only 1 record (2026) left in DB -> Deleting the last record must fail (return false)
        bool deletedLast = await repo.DeleteYearlyMinimumWageAsync(2026);
        Assert.False(deletedLast);

        // Record must still exist
        var remaining = await repo.GetYearlyMinimumWageAsync(2026);
        Assert.NotNull(remaining);
    }
}
