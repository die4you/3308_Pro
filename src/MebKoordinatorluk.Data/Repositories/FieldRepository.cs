using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class FieldRepository : IFieldRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public FieldRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Field>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM Fields WHERE IsActive = 1 ORDER BY Name;";
        return await connection.QueryAsync<Field>(sql);
    }

    public async Task<Field?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM Fields WHERE Id = @Id;";
        return await connection.QuerySingleOrDefaultAsync<Field>(sql, new { Id = id });
    }

    public async Task<int> AddAsync(Field field)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO Fields (Name, Description, Branches, IsActive)
            VALUES (@Name, @Description, @Branches, @IsActive);
            SELECT last_insert_rowid();";
        return await connection.ExecuteScalarAsync<int>(sql, field);
    }

    public async Task<bool> UpdateAsync(Field field)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Fields 
            SET Name = @Name, Description = @Description, Branches = @Branches, IsActive = @IsActive
            WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, field);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE Fields SET IsActive = 0 WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task<bool> DeleteAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM Fields;";
        var rows = await connection.ExecuteAsync(sql);
        return rows >= 0;
    }
}
