using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public AttendanceRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<AttendanceEntry>> GetForMonthAsync(int year, int month)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM AttendanceEntries 
            WHERE Year = @Year AND Month = @Month;";
        var results = await connection.QueryAsync<AttendanceEntry>(sql, new { Year = year, Month = month });
        return results.ToList();
    }

    public async Task SaveEntryAsync(AttendanceEntry entry)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string safeSql = @"
            DELETE FROM AttendanceEntries 
            WHERE Year = @Year AND Month = @Month AND StudentId = @StudentId AND DayOfMonth = @DayOfMonth;

            INSERT INTO AttendanceEntries (Year, Month, StudentId, DayOfMonth, Code)
            VALUES (@Year, @Month, @StudentId, @DayOfMonth, @Code);";
        await connection.ExecuteAsync(safeSql, entry);
    }

    public async Task SaveEntriesAsync(IEnumerable<AttendanceEntry> entries)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var tx = connection.BeginTransaction();
        const string sql = @"
            DELETE FROM AttendanceEntries 
            WHERE Year = @Year AND Month = @Month AND StudentId = @StudentId AND DayOfMonth = @DayOfMonth;

            INSERT INTO AttendanceEntries (Year, Month, StudentId, DayOfMonth, Code)
            VALUES (@Year, @Month, @StudentId, @DayOfMonth, @Code);";
        await connection.ExecuteAsync(sql, entries, transaction: tx);
        tx.Commit();
    }

    public async Task BulkSetDayAsync(int year, int month, int day, string code, IEnumerable<int> studentIds)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var tx = connection.BeginTransaction();

        foreach (var sId in studentIds)
        {
            const string sql = @"
                DELETE FROM AttendanceEntries 
                WHERE Year = @Year AND Month = @Month AND StudentId = @StudentId AND DayOfMonth = @DayOfMonth;

                INSERT INTO AttendanceEntries (Year, Month, StudentId, DayOfMonth, Code)
                VALUES (@Year, @Month, @StudentId, @DayOfMonth, @Code);";
            await connection.ExecuteAsync(sql, new { Year = year, Month = month, StudentId = sId, DayOfMonth = day, Code = code }, transaction: tx);
        }
        tx.Commit();
    }

    public async Task ResetMonthAsync(int year, int month, IEnumerable<Student> students)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var tx = connection.BeginTransaction();

        // Önce seçilen ayın mevcut kayıtlarını temizle
        await connection.ExecuteAsync(
            "DELETE FROM AttendanceEntries WHERE Year = @Year AND Month = @Month;",
            new { Year = year, Month = month },
            transaction: tx);

        int daysInMonth = DateTime.DaysInMonth(year, month);
        var entries = new List<AttendanceEntry>();

        foreach (var student in students)
        {
            for (int d = 1; d <= daysInMonth; d++)
            {
                var dt = new DateTime(year, month, d);
                // Haftaiçi günleri (Pazartesi..Cuma) için varsayılan 'X' (Geldi)
                if (dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday)
                {
                    entries.Add(new AttendanceEntry
                    {
                        Year = year,
                        Month = month,
                        StudentId = student.Id,
                        DayOfMonth = d,
                        Code = "X"
                    });
                }
            }
        }

        const string insertSql = @"
            INSERT INTO AttendanceEntries (Year, Month, StudentId, DayOfMonth, Code)
            VALUES (@Year, @Month, @StudentId, @DayOfMonth, @Code);";
        await connection.ExecuteAsync(insertSql, entries, transaction: tx);
        tx.Commit();
    }

    public async Task SyncClassAttendanceEntriesAsync(SchoolClass schoolClass, IEnumerable<Student> students, bool toMesem, AppSettings? settings = null)
    {
        var studentList = students.ToList();
        if (studentList.Count == 0) return;

        using var connection = _connectionFactory.CreateConnection();

        // 1. Sistemde devamsızlık kaydı bulunan tüm (Yıl, Ay) çiftlerini al
        const string monthsSql = "SELECT DISTINCT Year, Month FROM AttendanceEntries;";
        var existingMonths = (await connection.QueryAsync<(int Year, int Month)>(monthsSql)).ToList();

        // Aktif eğitim yılı ve cari ayı da listeye dahil et
        int currentYear = DateTime.Now.Year;
        int currentMonth = DateTime.Now.Month;
        int schoolYearStart = currentMonth >= 9 ? currentYear : currentYear - 1;

        var targetMonths = new HashSet<(int Year, int Month)>(existingMonths)
        {
            (currentYear, currentMonth),
            (schoolYearStart, 9)
        };

        // Eğer mevcut aylar varsa sadece o ayları ve cari ayı güncelle
        int gradeLevel = FeeCalculator.GetGradeLevel(studentList[0], schoolClass);

        DateTime termStart = new DateTime(schoolYearStart, 9, 9);
        DateTime termEnd = new DateTime(schoolYearStart + 1, 6, 20);
        if (settings != null)
        {
            if (!string.IsNullOrWhiteSpace(settings.OrgunStartDate) && DateTime.TryParse(settings.OrgunStartDate, out var sDate))
                termStart = sDate;
            if (!string.IsNullOrWhiteSpace(settings.OrgunEndDate) && DateTime.TryParse(settings.OrgunEndDate, out var eDate))
                termEnd = eDate;
        }

        using var tx = connection.BeginTransaction();

        foreach (var (year, month) in targetMonths)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);
            var studentIds = studentList.Select(s => s.Id).ToList();

            const string existingEntriesSql = @"
                SELECT * FROM AttendanceEntries 
                WHERE Year = @Year AND Month = @Month AND StudentId IN @Ids;";
            var existingEntries = (await connection.QueryAsync<AttendanceEntry>(
                existingEntriesSql,
                new { Year = year, Month = month, Ids = studentIds },
                transaction: tx
            )).ToDictionary(e => (e.StudentId, e.DayOfMonth), e => e.Code);

            var entriesToUpsert = new List<AttendanceEntry>();

            foreach (var student in studentList)
            {
                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(year, month, d);
                    bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                    string newCode;
                    if (isWeekend)
                    {
                        newCode = "-";
                    }
                    else if (student.SgkEntryDate.HasValue && date < student.SgkEntryDate.Value.Date)
                    {
                        newCode = "";
                    }
                    else if (student.SgkExitDate.HasValue && date > student.SgkExitDate.Value.Date)
                    {
                        newCode = "";
                    }
                    else if (toMesem)
                    {
                        // MESEM: Hafta içi işletmededir ('X'). Gerçek devamsızlıklar (D, R, İ, C, S, SV, SY, ÖY) korunur.
                        if (existingEntries.TryGetValue((student.Id, d), out var oldCode) &&
                            (oldCode is "D" or "R" or "İ" or "I" or "C" or "S" or "SV" or "SY" or "ÖY"))
                        {
                            newCode = oldCode;
                        }
                        else
                        {
                            newCode = "X";
                        }
                    }
                    else
                    {
                        // Örgün Eğitim: 9, 10, 11 staj yapmaz; 12. sınıf dönem içi staj yapar.
                        if (gradeLevel < 12)
                        {
                            newCode = "";
                        }
                        else
                        {
                            if (month == 9 && date < termStart.Date)
                            {
                                newCode = "";
                            }
                            else if (month == 6 && date > termEnd.Date)
                            {
                                newCode = "";
                            }
                            else if (existingEntries.TryGetValue((student.Id, d), out var oldCode) &&
                                     (oldCode is "D" or "R" or "İ" or "I" or "C" or "S" or "SV" or "SY" or "ÖY"))
                            {
                                newCode = oldCode;
                            }
                            else
                            {
                                newCode = "X";
                            }
                        }
                    }

                    entriesToUpsert.Add(new AttendanceEntry
                    {
                        Year = year,
                        Month = month,
                        StudentId = student.Id,
                        DayOfMonth = d,
                        Code = newCode
                    });
                }
            }

            const string upsertSql = @"
                DELETE FROM AttendanceEntries 
                WHERE Year = @Year AND Month = @Month AND StudentId = @StudentId AND DayOfMonth = @DayOfMonth;

                INSERT INTO AttendanceEntries (Year, Month, StudentId, DayOfMonth, Code)
                VALUES (@Year, @Month, @StudentId, @DayOfMonth, @Code);";

            await connection.ExecuteAsync(upsertSql, entriesToUpsert, transaction: tx);
        }

        tx.Commit();
    }
}
