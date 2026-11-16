using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class EnterpriseRepository : IEnterpriseRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public EnterpriseRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Enterprise>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT e.*,
                   ag.Name AS AddressGroupName,
                   t.FirstName || ' ' || t.LastName AS TeacherName
            FROM Enterprises e
            LEFT JOIN AddressGroups ag ON e.AddressGroupId = ag.Id
            LEFT JOIN Teachers t ON e.TeacherId = t.Id
            WHERE e.IsActive = 1
            ORDER BY e.Name ASC;";
        return await conn.QueryAsync<Enterprise>(sql);
    }

    public async Task<Enterprise?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT e.*,
                   ag.Name AS AddressGroupName,
                   t.FirstName || ' ' || t.LastName AS TeacherName
            FROM Enterprises e
            LEFT JOIN AddressGroups ag ON e.AddressGroupId = ag.Id
            LEFT JOIN Teachers t ON e.TeacherId = t.Id
            WHERE e.Id = @Id;";
        return await conn.QueryFirstOrDefaultAsync<Enterprise>(sql, new { Id = id });
    }

    public async Task<int> AddAsync(Enterprise enterprise)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO Enterprises (Name, AddressGroupId, AddressGroupName, Sector, ContactPerson, Phone, Email, Address, District, City, DistanceType, DistanceText, Capacity, ActiveDays, TeacherId, TeacherName, TaxNumber, Iban, SgkSicilNo, MasterTrainerName, OptOutStateContribution, EmployeeCount, Notes, IsActive)
            VALUES (@Name, @AddressGroupId, @AddressGroupName, @Sector, @ContactPerson, @Phone, @Email, @Address, @District, @City, @DistanceType, @DistanceText, @Capacity, @ActiveDays, @TeacherId, @TeacherName, @TaxNumber, @Iban, @SgkSicilNo, @MasterTrainerName, @OptOutStateContribution, @EmployeeCount, @Notes, @IsActive);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, enterprise);
    }

    public async Task<bool> UpdateAsync(Enterprise enterprise)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE Enterprises SET
                Name = @Name,
                AddressGroupId = @AddressGroupId,
                AddressGroupName = @AddressGroupName,
                Sector = @Sector,
                ContactPerson = @ContactPerson,
                Phone = @Phone,
                Email = @Email,
                Address = @Address,
                District = @District,
                City = @City,
                DistanceType = @DistanceType,
                DistanceText = @DistanceText,
                Capacity = @Capacity,
                ActiveDays = @ActiveDays,
                TeacherId = @TeacherId,
                TeacherName = @TeacherName,
                TaxNumber = @TaxNumber,
                Iban = @Iban,
                SgkSicilNo = @SgkSicilNo,
                MasterTrainerName = @MasterTrainerName,
                OptOutStateContribution = @OptOutStateContribution,
                EmployeeCount = @EmployeeCount,
                Notes = @Notes,
                IsActive = @IsActive
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, enterprise);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "UPDATE Enterprises SET IsActive = 0 WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
