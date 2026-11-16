using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class AddressGroupRepository : IAddressGroupRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public AddressGroupRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<AddressGroup>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM AddressGroups WHERE IsActive = 1 ORDER BY Name;";
        return await connection.QueryAsync<AddressGroup>(sql);
    }

    public async Task<AddressGroup?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM AddressGroups WHERE Id = @Id;";
        return await connection.QuerySingleOrDefaultAsync<AddressGroup>(sql, new { Id = id });
    }

    public async Task<int> AddAsync(AddressGroup addressGroup)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO AddressGroups (Name, Region, Description, Color, IsActive)
            VALUES (@Name, @Region, @Description, @Color, @IsActive);
            SELECT last_insert_rowid();";
        return await connection.ExecuteScalarAsync<int>(sql, addressGroup);
    }

    public async Task<bool> UpdateAsync(AddressGroup addressGroup)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE AddressGroups 
            SET Name = @Name, Region = @Region, Description = @Description, Color = @Color, IsActive = @IsActive
            WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, addressGroup);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE AddressGroups SET IsActive = 0 WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }
}
