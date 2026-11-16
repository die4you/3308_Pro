using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class CoordinatorAssignmentRepository : ICoordinatorAssignmentRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public CoordinatorAssignmentRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<CoordinatorAssignment>> GetAllAsync(int? academicYearId = null)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT ca.*,
                   t.FirstName || ' ' || t.LastName AS TeacherFullName,
                   e.Name AS EnterpriseName,
                   e.District AS EnterpriseDistrict,
                   e.DistanceType AS DistanceType,
                   ay.Name || ' - ' || ay.Term || '. Dönem' AS AcademicYearName
            FROM CoordinatorAssignments ca
            INNER JOIN Teachers t ON ca.TeacherId = t.Id
            INNER JOIN Enterprises e ON ca.EnterpriseId = e.Id
            INNER JOIN AcademicYears ay ON ca.AcademicYearId = ay.Id
            WHERE (@YearId IS NULL OR ca.AcademicYearId = @YearId)
              AND ca.IsActive = 1
            ORDER BY t.FirstName, t.LastName, ca.DayOfWeek;
        ";
        return await conn.QueryAsync<CoordinatorAssignment>(sql, new { YearId = academicYearId });
    }

    public async Task<CoordinatorAssignment?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT ca.*,
                   t.FirstName || ' ' || t.LastName AS TeacherFullName,
                   e.Name AS EnterpriseName,
                   e.District AS EnterpriseDistrict,
                   e.DistanceType AS DistanceType,
                   ay.Name || ' - ' || ay.Term || '. Dönem' AS AcademicYearName
            FROM CoordinatorAssignments ca
            INNER JOIN Teachers t ON ca.TeacherId = t.Id
            INNER JOIN Enterprises e ON ca.EnterpriseId = e.Id
            INNER JOIN AcademicYears ay ON ca.AcademicYearId = ay.Id
            WHERE ca.Id = @Id;
        ";
        return await conn.QueryFirstOrDefaultAsync<CoordinatorAssignment>(sql, new { Id = id });
    }

    public async Task<double> GetTeacherTotalAssignedHoursAsync(int teacherId, int academicYearId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT COALESCE(SUM(CalculatedHours), 0)
            FROM CoordinatorAssignments
            WHERE TeacherId = @TeacherId
              AND AcademicYearId = @AcademicYearId
              AND IsActive = 1;
        ";
        return await conn.ExecuteScalarAsync<double>(sql, new { TeacherId = teacherId, AcademicYearId = academicYearId });
    }

    public async Task<int> AddAsync(CoordinatorAssignment assignment)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO CoordinatorAssignments (AcademicYearId, TeacherId, EnterpriseId, DayOfWeek, StudentCount, CalculatedHours, Notes, IsActive)
            VALUES (@AcademicYearId, @TeacherId, @EnterpriseId, @DayOfWeek, @StudentCount, @CalculatedHours, @Notes, @IsActive);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            assignment.AcademicYearId,
            assignment.TeacherId,
            assignment.EnterpriseId,
            DayOfWeek = (int)assignment.DayOfWeek,
            assignment.StudentCount,
            assignment.CalculatedHours,
            assignment.Notes,
            IsActive = assignment.IsActive ? 1 : 0
        });
    }

    public async Task<bool> UpdateAsync(CoordinatorAssignment assignment)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE CoordinatorAssignments SET
                AcademicYearId = @AcademicYearId,
                TeacherId = @TeacherId,
                EnterpriseId = @EnterpriseId,
                DayOfWeek = @DayOfWeek,
                StudentCount = @StudentCount,
                CalculatedHours = @CalculatedHours,
                Notes = @Notes,
                IsActive = @IsActive
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new
        {
            assignment.Id,
            assignment.AcademicYearId,
            assignment.TeacherId,
            assignment.EnterpriseId,
            DayOfWeek = (int)assignment.DayOfWeek,
            assignment.StudentCount,
            assignment.CalculatedHours,
            assignment.Notes,
            IsActive = assignment.IsActive ? 1 : 0
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "UPDATE CoordinatorAssignments SET IsActive = 0 WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
