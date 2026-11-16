using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class AcademicYearRepository : IAcademicYearRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public AcademicYearRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<AcademicYear>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<AcademicYear>("SELECT * FROM AcademicYears ORDER BY Name DESC, Term DESC;");
    }

    public async Task<AcademicYear?> GetActiveYearAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<AcademicYear>(
            "SELECT * FROM AcademicYears WHERE IsActive = 1 ORDER BY Id DESC LIMIT 1;");
    }

    public async Task<AcademicYear?> GetByNameAsync(string name)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<AcademicYear>(
            "SELECT * FROM AcademicYears WHERE Name = @Name ORDER BY Id DESC LIMIT 1;", new { Name = name });
    }

    public async Task<int> AddAsync(AcademicYear year)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO AcademicYears (Name, Term, StartDate, EndDate, IsActive)
            VALUES (@Name, @Term, @StartDate, @EndDate, @IsActive);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            year.Name,
            year.Term,
            StartDate = year.StartDate.ToString("yyyy-MM-dd"),
            EndDate = year.EndDate.ToString("yyyy-MM-dd"),
            IsActive = year.IsActive ? 1 : 0
        });
    }

    public async Task UpdateActiveYearAsync(string name)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("UPDATE AcademicYears SET IsActive = 0;");
        await conn.ExecuteAsync("UPDATE AcademicYears SET IsActive = 1 WHERE Name = @Name;", new { Name = name });
    }

    public async Task EnsureYearExistsAsync(string name, DateTime startDate, DateTime endDate)
    {
        using var conn = _factory.CreateConnection();
        var existing = await conn.QueryFirstOrDefaultAsync<AcademicYear>(
            "SELECT * FROM AcademicYears WHERE Name = @Name LIMIT 1;", new { Name = name });
        if (existing == null)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO AcademicYears (Name, Term, StartDate, EndDate, IsActive)
                VALUES (@Name, 1, @StartDate, @EndDate, 0);",
                new { Name = name, StartDate = startDate.ToString("yyyy-MM-dd"), EndDate = endDate.ToString("yyyy-MM-dd") });
        }
    }
}
