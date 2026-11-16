using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class TeacherRepository : ITeacherRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public TeacherRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Teacher>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT t.*, 
                   COALESCE(t.Branch, d.Name, '') AS DepartmentName
            FROM Teachers t
            LEFT JOIN Departments d ON t.DepartmentId = d.Id
            ORDER BY t.FirstName, t.LastName;
        ";
        return await conn.QueryAsync<Teacher>(sql);
    }

    public async Task<Teacher?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT t.*, 
                   COALESCE(t.Branch, d.Name, '') AS DepartmentName
            FROM Teachers t
            LEFT JOIN Departments d ON t.DepartmentId = d.Id
            WHERE t.Id = @Id;
        ";
        return await conn.QueryFirstOrDefaultAsync<Teacher>(sql, new { Id = id });
    }

    public async Task<int> AddAsync(Teacher teacher)
    {
        using var conn = _factory.CreateConnection();
        var cleanTc = string.IsNullOrWhiteSpace(teacher.TcKimlikNo) ? null : teacher.TcKimlikNo.Trim();
        const string sql = @"
            INSERT INTO Teachers (TcKimlikNo, FirstName, LastName, DepartmentId, Branch, Email, Phone, AvailableDays, EmploymentType, MaxCoordinationHours, TargetHours, CoordinatorFeeHours, IsActive)
            VALUES (@TcKimlikNo, @FirstName, @LastName, @DepartmentId, @Branch, @Email, @Phone, @AvailableDays, @EmploymentType, @MaxCoordinationHours, @TargetHours, @CoordinatorFeeHours, @IsActive);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            TcKimlikNo = cleanTc,
            teacher.FirstName,
            teacher.LastName,
            DepartmentId = teacher.DepartmentId > 0 ? teacher.DepartmentId : 1,
            teacher.Branch,
            teacher.Email,
            teacher.Phone,
            teacher.AvailableDays,
            teacher.EmploymentType,
            teacher.MaxCoordinationHours,
            teacher.TargetHours,
            teacher.CoordinatorFeeHours,
            teacher.IsActive
        });
    }

    public async Task<bool> UpdateAsync(Teacher teacher)
    {
        using var conn = _factory.CreateConnection();
        var cleanTc = string.IsNullOrWhiteSpace(teacher.TcKimlikNo) ? null : teacher.TcKimlikNo.Trim();
        const string sql = @"
            UPDATE Teachers SET
                TcKimlikNo = @TcKimlikNo,
                FirstName = @FirstName,
                LastName = @LastName,
                DepartmentId = @DepartmentId,
                Branch = @Branch,
                Email = @Email,
                Phone = @Phone,
                AvailableDays = @AvailableDays,
                EmploymentType = @EmploymentType,
                MaxCoordinationHours = @MaxCoordinationHours,
                TargetHours = @TargetHours,
                CoordinatorFeeHours = @CoordinatorFeeHours,
                IsActive = @IsActive
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new
        {
            teacher.Id,
            TcKimlikNo = cleanTc,
            teacher.FirstName,
            teacher.LastName,
            DepartmentId = teacher.DepartmentId > 0 ? teacher.DepartmentId : 1,
            teacher.Branch,
            teacher.Email,
            teacher.Phone,
            teacher.AvailableDays,
            teacher.EmploymentType,
            teacher.MaxCoordinationHours,
            teacher.TargetHours,
            teacher.CoordinatorFeeHours,
            teacher.IsActive
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "DELETE FROM Teachers WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
