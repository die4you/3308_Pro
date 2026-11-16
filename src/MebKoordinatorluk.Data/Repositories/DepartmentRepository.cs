using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public DepartmentRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Department>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Department>("SELECT * FROM Departments WHERE IsActive = 1 ORDER BY Name, SubBranch;");
    }

    public async Task<Department?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Department>("SELECT * FROM Departments WHERE Id = @Id;", new { Id = id });
    }

    public async Task<int> AddAsync(Department department)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO Departments (Code, Name, SubBranch, IsActive)
            VALUES (@Code, @Name, @SubBranch, @IsActive);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, department);
    }

    public async Task<bool> UpdateAsync(Department department)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE Departments SET
                Code = @Code,
                Name = @Name,
                SubBranch = @SubBranch,
                IsActive = @IsActive
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, department);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "UPDATE Departments SET IsActive = 0 WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
