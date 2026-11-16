using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class SchoolClassRepository : ISchoolClassRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SchoolClassRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<SchoolClass>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM SchoolClasses WHERE IsActive = 1 ORDER BY Name;";
        return await connection.QueryAsync<SchoolClass>(sql);
    }

    public async Task<SchoolClass?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM SchoolClasses WHERE Id = @Id;";
        return await connection.QuerySingleOrDefaultAsync<SchoolClass>(sql, new { Id = id });
    }

    public async Task<int> AddAsync(SchoolClass schoolClass)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO SchoolClasses (Name, FieldId, FieldName, InstitutionType, EducationType, InternshipDays, StartDate, EndDate, IgnoreHalfDayDeduction, HasInternship, IsActive)
            VALUES (@Name, @FieldId, @FieldName, @InstitutionType, @EducationType, @InternshipDays, @StartDate, @EndDate, @IgnoreHalfDayDeduction, @HasInternship, @IsActive);
            SELECT last_insert_rowid();";
        return await connection.ExecuteScalarAsync<int>(sql, schoolClass);
    }

    public async Task<bool> UpdateAsync(SchoolClass schoolClass)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE SchoolClasses 
            SET Name = @Name, FieldId = @FieldId, FieldName = @FieldName, 
                InstitutionType = @InstitutionType, EducationType = @EducationType, 
                InternshipDays = @InternshipDays, StartDate = @StartDate, EndDate = @EndDate, 
                IgnoreHalfDayDeduction = @IgnoreHalfDayDeduction, HasInternship = @HasInternship, IsActive = @IsActive
            WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, schoolClass);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE SchoolClasses SET IsActive = 0 WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task<bool> DeleteAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM SchoolClasses;";
        var rows = await connection.ExecuteAsync(sql);
        return rows >= 0;
    }

    public async Task<bool> ExistsAsync(string name, string? fieldName, int? excludeId = null)
    {
        var allClasses = await GetAllAsync();
        var cleanName = (name ?? string.Empty).Trim();
        var cleanField = (fieldName ?? string.Empty).Trim();

        return allClasses.Any(c =>
            c.Id != (excludeId ?? 0) &&
            string.Equals(c.Name?.Trim(), cleanName, StringComparison.CurrentCultureIgnoreCase) &&
            string.Equals((c.FieldName ?? string.Empty).Trim(), cleanField, StringComparison.CurrentCultureIgnoreCase));
    }
}
