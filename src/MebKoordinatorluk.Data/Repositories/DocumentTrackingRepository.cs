using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class DocumentTrackingRepository : IDocumentTrackingRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public DocumentTrackingRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<DocumentTrackingRecord>> GetForMonthAsync(int year, int month)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM DocumentTrackingRecords 
            WHERE Year = @Year AND Month = @Month;";
        var results = await connection.QueryAsync<DocumentTrackingRecord>(sql, new { Year = year, Month = month });
        return results.ToList();
    }

    public async Task SaveRecordAsync(DocumentTrackingRecord record)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string safeSql = @"
            DELETE FROM DocumentTrackingRecords 
            WHERE Year = @Year AND Month = @Month AND TeacherId = @TeacherId AND EnterpriseId = @EnterpriseId;

            INSERT INTO DocumentTrackingRecords (Year, Month, TeacherId, EnterpriseId, TransitionStart, Week1, Week2, Week3, Week4, TransitionEnd, MonthlyReport, AttendanceSheet, Notes)
            VALUES (@Year, @Month, @TeacherId, @EnterpriseId, @TransitionStart, @Week1, @Week2, @Week3, @Week4, @TransitionEnd, @MonthlyReport, @AttendanceSheet, @Notes);";
        await connection.ExecuteAsync(safeSql, record);
    }

    public async Task BulkSetAllAsync(int year, int month, bool completed, IEnumerable<(int TeacherId, int EnterpriseId)> pairs)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var tx = connection.BeginTransaction();

        int val = completed ? 1 : 0;
        foreach (var p in pairs)
        {
            const string sql = @"
                DELETE FROM DocumentTrackingRecords 
                WHERE Year = @Year AND Month = @Month AND TeacherId = @TeacherId AND EnterpriseId = @EnterpriseId;

                INSERT INTO DocumentTrackingRecords (Year, Month, TeacherId, EnterpriseId, TransitionStart, Week1, Week2, Week3, Week4, TransitionEnd, MonthlyReport, AttendanceSheet)
                VALUES (@Year, @Month, @TeacherId, @EnterpriseId, @Val, @Val, @Val, @Val, @Val, @Val, @Val, @Val);";
            await connection.ExecuteAsync(sql, new { Year = year, Month = month, TeacherId = p.TeacherId, EnterpriseId = p.EnterpriseId, Val = val }, transaction: tx);
        }

        tx.Commit();
    }
}
