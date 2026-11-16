using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class MonthlyPayrollRepository : IMonthlyPayrollRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public MonthlyPayrollRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<MonthlyPayroll>> GetAllAsync(int? academicYearId = null)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT mp.*
            FROM MonthlyPayrolls mp
            WHERE (@YearId IS NULL OR mp.AcademicYearId = @YearId)
            ORDER BY mp.Year DESC, mp.Month DESC;
        ";
        return await conn.QueryAsync<MonthlyPayroll>(sql, new { YearId = academicYearId });
    }

    public async Task<MonthlyPayroll?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "SELECT * FROM MonthlyPayrolls WHERE Id = @Id;";
        return await conn.QueryFirstOrDefaultAsync<MonthlyPayroll>(sql, new { Id = id });
    }

    public async Task<MonthlyPayroll?> GetByPeriodAsync(int academicYearId, int year, int month)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT * FROM MonthlyPayrolls
            WHERE AcademicYearId = @AcademicYearId AND Year = @Year AND Month = @Month;";
        return await conn.QueryFirstOrDefaultAsync<MonthlyPayroll>(sql, new
        {
            AcademicYearId = academicYearId,
            Year = year,
            Month = month
        });
    }

    public async Task<int> CreatePayrollAsync(int academicYearId, int year, int month)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO MonthlyPayrolls (AcademicYearId, Year, Month, Status, CreatedDate)
            VALUES (@AcademicYearId, @Year, @Month, 1, @CreatedDate);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            AcademicYearId = academicYearId,
            Year = year,
            Month = month,
            CreatedDate = DateTime.UtcNow.ToString("o")
        });
    }

    public async Task<bool> UpdateStatusAsync(int id, PayrollStatus status)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE MonthlyPayrolls SET
                Status = @Status,
                ApprovedDate = CASE WHEN @Status = 2 THEN @Now ELSE ApprovedDate END
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            Status = (int)status,
            Now = DateTime.UtcNow.ToString("o")
        });
        return affected > 0;
    }

    public async Task<IEnumerable<PayrollItem>> GetPayrollItemsAsync(int payrollId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT pi.*,
                   t.FirstName || ' ' || t.LastName AS TeacherFullName,
                   t.TcKimlikNo AS TeacherTc,
                   d.Name AS DepartmentName
            FROM PayrollItems pi
            INNER JOIN Teachers t ON pi.TeacherId = t.Id
            LEFT JOIN Departments d ON t.DepartmentId = d.Id
            WHERE pi.PayrollId = @PayrollId
            ORDER BY t.FirstName, t.LastName;
        ";
        return await conn.QueryAsync<PayrollItem>(sql, new { PayrollId = payrollId });
    }

    public async Task GeneratePayrollItemsAsync(int payrollId, decimal hourlyRate)
    {
        using var conn = _factory.CreateConnection();

        var payroll = await GetByIdAsync(payrollId);
        if (payroll == null) return;

        // 1. Önceki hesaplanmış kalemleri temizle
        await conn.ExecuteAsync("DELETE FROM PayrollItems WHERE PayrollId = @PayrollId;", new { PayrollId = payrollId });

        // 2. Dönemdeki aktif öğretmenleri ve görevlendirmeleri al
        const string assignSql = @"
            SELECT ca.*, t.Id AS TeacherId, t.FirstName, t.LastName, t.DepartmentId
            FROM CoordinatorAssignments ca
            INNER JOIN Teachers t ON ca.TeacherId = t.Id
            WHERE ca.AcademicYearId = @YearId AND ca.IsActive = 1;";

        var assignments = (await conn.QueryAsync<dynamic>(assignSql, new { YearId = payroll.AcademicYearId })).ToList();
        if (assignments.Count == 0) return;

        // Öğretmen bazında grupla
        var teacherGroups = assignments.GroupBy(a => (int)a.TeacherId);
        int daysInMonth = DateTime.DaysInMonth(payroll.Year, payroll.Month);

        foreach (var group in teacherGroups)
        {
            int teacherId = group.Key;
            double w1 = 0, w2 = 0, w3 = 0, w4 = 0, w5 = 0;

            // Öğretmenin tüm görevleri
            foreach (var assign in group)
            {
                var assignDayOfWeek = (DayOfWeekTurkish)assign.DayOfWeek;
                var targetDay = assignDayOfWeek switch
                {
                    DayOfWeekTurkish.Pazartesi => DayOfWeek.Monday,
                    DayOfWeekTurkish.Sali => DayOfWeek.Tuesday,
                    DayOfWeekTurkish.Carsamba => DayOfWeek.Wednesday,
                    DayOfWeekTurkish.Persembe => DayOfWeek.Thursday,
                    DayOfWeekTurkish.Cuma => DayOfWeek.Friday,
                    _ => DayOfWeek.Monday
                };

                double hours = (double)assign.CalculatedHours;

                for (int day = 1; day <= daysInMonth; day++)
                {
                    var dt = new DateTime(payroll.Year, payroll.Month, day);
                    if (dt.DayOfWeek == targetDay)
                    {
                        // MEB Standart Haftalık Bölümleme (1-7: H1, 8-14: H2, 15-21: H3, 22-28: H4, 29-31: H5)
                        if (day <= 7) w1 += hours;
                        else if (day <= 14) w2 += hours;
                        else if (day <= 21) w3 += hours;
                        else if (day <= 28) w4 += hours;
                        else w5 += hours;
                    }
                }
            }

            double totalHours = w1 + w2 + w3 + w4 + w5;
            decimal totalAmount = (decimal)totalHours * hourlyRate;

            const string insertItemSql = @"
                INSERT INTO PayrollItems (PayrollId, TeacherId, Week1Hours, Week2Hours, Week3Hours, Week4Hours, Week5Hours, TotalHours, HourlyRate, TotalAmount, Notes)
                VALUES (@PayrollId, @TeacherId, @W1, @W2, @W3, @W4, @W5, @TotalHours, @HourlyRate, @TotalAmount, @Notes);";

            await conn.ExecuteAsync(insertItemSql, new
            {
                PayrollId = payrollId,
                TeacherId = teacherId,
                W1 = w1,
                W2 = w2,
                W3 = w3,
                W4 = w4,
                W5 = w5,
                TotalHours = totalHours,
                HourlyRate = hourlyRate,
                TotalAmount = totalAmount,
                Notes = $"{payroll.Year}/{payroll.Month:D2} Dönemi koordinatörlük ek ders puantajı"
            });
        }
    }

    public async Task<bool> DeletePayrollAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "DELETE FROM MonthlyPayrolls WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
