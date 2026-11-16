using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class CoordinationVisitRepository : ICoordinationVisitRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public CoordinationVisitRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<CoordinationVisit>> GetAllAsync(
        int? assignmentId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            SELECT cv.*,
                   t.FirstName || ' ' || t.LastName AS TeacherFullName,
                   e.Name AS EnterpriseName
            FROM CoordinationVisits cv
            INNER JOIN CoordinatorAssignments ca ON cv.AssignmentId = ca.Id
            INNER JOIN Teachers t ON ca.TeacherId = t.Id
            INNER JOIN Enterprises e ON ca.EnterpriseId = e.Id
            WHERE (@AssignmentId IS NULL OR cv.AssignmentId = @AssignmentId)
              AND (@Start IS NULL OR cv.VisitDate >= @Start)
              AND (@End IS NULL OR cv.VisitDate <= @End)
            ORDER BY cv.VisitDate DESC, t.FirstName, e.Name;
        ";
        return await conn.QueryAsync<CoordinationVisit>(sql, new
        {
            AssignmentId = assignmentId,
            Start = startDate?.ToString("yyyy-MM-dd"),
            End = endDate?.ToString("yyyy-MM-dd")
        });
    }

    public async Task<CoordinationVisit?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT cv.*,
                   t.FirstName || ' ' || t.LastName AS TeacherFullName,
                   e.Name AS EnterpriseName
            FROM CoordinationVisits cv
            INNER JOIN CoordinatorAssignments ca ON cv.AssignmentId = ca.Id
            INNER JOIN Teachers t ON ca.TeacherId = t.Id
            INNER JOIN Enterprises e ON ca.EnterpriseId = e.Id
            WHERE cv.Id = @Id;
        ";
        return await conn.QueryFirstOrDefaultAsync<CoordinationVisit>(sql, new { Id = id });
    }

    public async Task<int> AddAsync(CoordinationVisit visit)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO CoordinationVisits (AssignmentId, VisitDate, Status, FormNumber, ObservationNotes, CreatedAt)
            VALUES (@AssignmentId, @VisitDate, @Status, @FormNumber, @ObservationNotes, @CreatedAt);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            visit.AssignmentId,
            VisitDate = visit.VisitDate.ToString("yyyy-MM-dd"),
            Status = (int)visit.Status,
            visit.FormNumber,
            visit.ObservationNotes,
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
    }

    public async Task<bool> UpdateAsync(CoordinationVisit visit)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE CoordinationVisits SET
                AssignmentId = @AssignmentId,
                VisitDate = @VisitDate,
                Status = @Status,
                FormNumber = @FormNumber,
                ObservationNotes = @ObservationNotes
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new
        {
            visit.Id,
            visit.AssignmentId,
            VisitDate = visit.VisitDate.ToString("yyyy-MM-dd"),
            Status = (int)visit.Status,
            visit.FormNumber,
            visit.ObservationNotes
        });
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "DELETE FROM CoordinationVisits WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<int> BatchGenerateVisitsForMonthAsync(int academicYearId, int year, int month)
    {
        using var conn = _factory.CreateConnection();

        // 1. O dönemdeki aktif tüm görevlendirmeleri al
        const string assignSql = @"
            SELECT * FROM CoordinatorAssignments
            WHERE AcademicYearId = @AcademicYearId AND IsActive = 1;";
        var assignments = (await conn.QueryAsync<CoordinatorAssignment>(assignSql, new { AcademicYearId = academicYearId })).ToList();

        if (assignments.Count == 0) return 0;

        int daysInMonth = DateTime.DaysInMonth(year, month);
        int generatedCount = 0;

        foreach (var assignment in assignments)
        {
            // assignment.DayOfWeek: 1: Pazartesi, 2: Salı, 3: Çarşamba, 4: Perşembe, 5: Cuma
            var targetDayOfWeek = assignment.DayOfWeek switch
            {
                DayOfWeekTurkish.Pazartesi => DayOfWeek.Monday,
                DayOfWeekTurkish.Sali => DayOfWeek.Tuesday,
                DayOfWeekTurkish.Carsamba => DayOfWeek.Wednesday,
                DayOfWeekTurkish.Persembe => DayOfWeek.Thursday,
                DayOfWeekTurkish.Cuma => DayOfWeek.Friday,
                DayOfWeekTurkish.Cumartesi => DayOfWeek.Saturday,
                _ => DayOfWeek.Monday
            };

            for (int day = 1; day <= daysInMonth; day++)
            {
                var currentDate = new DateTime(year, month, day);
                if (currentDate.DayOfWeek == targetDayOfWeek)
                {
                    var dateStr = currentDate.ToString("yyyy-MM-dd");

                    // Daha önce oluşturulmuş mu?
                    var exists = await conn.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*) FROM CoordinationVisits
                        WHERE AssignmentId = @AssignmentId AND VisitDate = @Date;",
                        new { AssignmentId = assignment.Id, Date = dateStr });

                    if (exists == 0)
                    {
                        const string insertSql = @"
                            INSERT INTO CoordinationVisits (AssignmentId, VisitDate, Status, FormNumber, ObservationNotes, CreatedAt)
                            VALUES (@AssignmentId, @Date, 1, @FormNo, 'Aylık plan dahilinde olağan koordinatörlük ziyareti.', @CreatedAt);";

                        await conn.ExecuteAsync(insertSql, new
                        {
                            AssignmentId = assignment.Id,
                            Date = dateStr,
                            FormNo = $"ZF-{year}{month:D2}-{day:D2}-{assignment.Id}",
                            CreatedAt = DateTime.UtcNow.ToString("o")
                        });

                        generatedCount++;
                    }
                }
            }
        }

        return generatedCount;
    }
}
