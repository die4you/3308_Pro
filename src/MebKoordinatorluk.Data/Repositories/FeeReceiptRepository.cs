using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class FeeReceiptRepository : IFeeReceiptRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public FeeReceiptRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<FeeReceipt>> GetForPeriodAsync(string period)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM FeeReceipts WHERE Period = @Period;";
        return await connection.QueryAsync<FeeReceipt>(sql, new { Period = period });
    }

    public async Task<FeeReceipt?> GetByStudentAndPeriodAsync(int studentId, string period)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM FeeReceipts WHERE StudentId = @StudentId AND Period = @Period LIMIT 1;";
        return await connection.QueryFirstOrDefaultAsync<FeeReceipt>(sql, new { StudentId = studentId, Period = period });
    }

    public async Task<int> AddOrUpdateAsync(FeeReceipt receipt)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO FeeReceipts (StudentId, Period, FileName, ArchivePath, AuditDate, MatchReason)
            VALUES (@StudentId, @Period, @FileName, @ArchivePath, @AuditDate, @MatchReason)
            ON CONFLICT(StudentId, Period) DO UPDATE SET
                FileName = excluded.FileName,
                ArchivePath = excluded.ArchivePath,
                AuditDate = excluded.AuditDate,
                MatchReason = excluded.MatchReason;
            SELECT last_insert_rowid();";
        return await connection.ExecuteScalarAsync<int>(sql, receipt);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM FeeReceipts WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task<bool> DeleteByStudentAndPeriodAsync(int studentId, string period)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM FeeReceipts WHERE StudentId = @StudentId AND Period = @Period;";
        var rows = await connection.ExecuteAsync(sql, new { StudentId = studentId, Period = period });
        return rows > 0;
    }
}
