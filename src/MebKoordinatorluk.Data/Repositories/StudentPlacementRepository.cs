using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class StudentPlacementRepository : IStudentPlacementRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public StudentPlacementRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<StudentPlacement>> GetAllAsync(int? academicYearId = null)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT sp.*,
                   s.FirstName || ' ' || s.LastName AS StudentFullName,
                   s.StudentNumber AS StudentNumber,
                   e.Name AS EnterpriseName,
                   ay.Name || ' - ' || ay.Term || '. Dönem' AS AcademicYearName,
                   d.Name || CASE WHEN d.SubBranch IS NOT NULL AND d.SubBranch <> '' THEN ' / ' || d.SubBranch ELSE '' END AS DepartmentName
            FROM StudentPlacements sp
            INNER JOIN Students s ON sp.StudentId = s.Id
            INNER JOIN Enterprises e ON sp.EnterpriseId = e.Id
            INNER JOIN AcademicYears ay ON sp.AcademicYearId = ay.Id
            LEFT JOIN Departments d ON s.DepartmentId = d.Id
            WHERE (@YearId IS NULL OR sp.AcademicYearId = @YearId)
            ORDER BY e.Name, s.FirstName, s.LastName;
        ";
        return await conn.QueryAsync<StudentPlacement>(sql, new { YearId = academicYearId });
    }

    public async Task<StudentPlacement?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT sp.*,
                   s.FirstName || ' ' || s.LastName AS StudentFullName,
                   s.StudentNumber AS StudentNumber,
                   e.Name AS EnterpriseName,
                   ay.Name || ' - ' || ay.Term || '. Dönem' AS AcademicYearName,
                   d.Name || CASE WHEN d.SubBranch IS NOT NULL AND d.SubBranch <> '' THEN ' / ' || d.SubBranch ELSE '' END AS DepartmentName
            FROM StudentPlacements sp
            INNER JOIN Students s ON sp.StudentId = s.Id
            INNER JOIN Enterprises e ON sp.EnterpriseId = e.Id
            INNER JOIN AcademicYears ay ON sp.AcademicYearId = ay.Id
            LEFT JOIN Departments d ON s.DepartmentId = d.Id
            WHERE sp.Id = @Id;
        ";
        return await conn.QueryFirstOrDefaultAsync<StudentPlacement>(sql, new { Id = id });
    }

    public async Task<int> GetStudentCountInEnterpriseAsync(int enterpriseId, int academicYearId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT COUNT(*)
            FROM StudentPlacements
            WHERE EnterpriseId = @EnterpriseId
              AND AcademicYearId = @AcademicYearId
              AND Status = 1; -- 1: Aktif stajyer
        ";
        return await conn.ExecuteScalarAsync<int>(sql, new { EnterpriseId = enterpriseId, AcademicYearId = academicYearId });
    }

    public async Task<int> AddAsync(StudentPlacement placement)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO StudentPlacements (AcademicYearId, StudentId, EnterpriseId, StartDate, EndDate, InternshipDays, ContractNo, Status)
            VALUES (@AcademicYearId, @StudentId, @EnterpriseId, @StartDate, @EndDate, @InternshipDays, @ContractNo, @Status);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            placement.AcademicYearId,
            placement.StudentId,
            placement.EnterpriseId,
            StartDate = placement.StartDate.ToString("yyyy-MM-dd"),
            EndDate = placement.EndDate.ToString("yyyy-MM-dd"),
            placement.InternshipDays,
            placement.ContractNo,
            Status = (int)placement.Status
        });
    }

    public async Task<bool> UpdateAsync(StudentPlacement placement)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE StudentPlacements SET
                AcademicYearId = @AcademicYearId,
                StudentId = @StudentId,
                EnterpriseId = @EnterpriseId,
                StartDate = @StartDate,
                EndDate = @EndDate,
                InternshipDays = @InternshipDays,
                ContractNo = @ContractNo,
                Status = @Status
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new
        {
            placement.Id,
            placement.AcademicYearId,
            placement.StudentId,
            placement.EnterpriseId,
            StartDate = placement.StartDate.ToString("yyyy-MM-dd"),
            EndDate = placement.EndDate.ToString("yyyy-MM-dd"),
            placement.InternshipDays,
            placement.ContractNo,
            Status = (int)placement.Status
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "DELETE FROM StudentPlacements WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
