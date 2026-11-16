using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class CalendarTaskRepository : ICalendarTaskRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public CalendarTaskRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private async Task EnsureTableAndDefaultsAsync(System.Data.IDbConnection connection)
    {
        const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS CalendarTasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Subtitle TEXT,
                StartDay INTEGER NOT NULL DEFAULT 1,
                EndDay INTEGER NOT NULL DEFAULT 5,
                RangeText TEXT,
                FrequencyType INTEGER NOT NULL DEFAULT 0,
                TargetMonth INTEGER,
                TargetYearMonth TEXT,
                NavTarget TEXT,
                NavBtnText TEXT,
                IsSystemDefault INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                OrderIndex INTEGER NOT NULL DEFAULT 0
            );";
        await connection.ExecuteAsync(createTableSql);

        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CalendarTasks;");
        if (count == 0)
        {
            var defaultTasks = GetDefaultTasks();
            foreach (var task in defaultTasks)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO CalendarTasks 
                    (Title, Subtitle, StartDay, EndDay, RangeText, FrequencyType, TargetMonth, TargetYearMonth, NavTarget, NavBtnText, IsSystemDefault, IsActive, OrderIndex)
                    VALUES 
                    (@Title, @Subtitle, @StartDay, @EndDay, @RangeText, @FrequencyType, @TargetMonth, @TargetYearMonth, @NavTarget, @NavBtnText, @IsSystemDefault, @IsActive, @OrderIndex);",
                    task);
            }
        }
    }

    public async Task<IEnumerable<CalendarTask>> GetAllAsync(bool includeInactive = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        await EnsureTableAndDefaultsAsync(connection);

        string sql = includeInactive
            ? "SELECT * FROM CalendarTasks ORDER BY OrderIndex, StartDay, Id;"
            : "SELECT * FROM CalendarTasks WHERE IsActive = 1 ORDER BY OrderIndex, StartDay, Id;";
        return await connection.QueryAsync<CalendarTask>(sql);
    }

    public async Task<IEnumerable<CalendarTask>> GetTasksForMonthAsync(DateTime month)
    {
        var all = await GetAllAsync(includeInactive: false);
        return all.Where(t => t.IsApplicableForMonth(month))
                  .OrderBy(t => t.StartDay)
                  .ThenBy(t => t.OrderIndex)
                  .ThenBy(t => t.Id);
    }

    public async Task<CalendarTask?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await EnsureTableAndDefaultsAsync(connection);

        const string sql = "SELECT * FROM CalendarTasks WHERE Id = @Id;";
        return await connection.QuerySingleOrDefaultAsync<CalendarTask>(sql, new { Id = id });
    }

    public async Task<int> AddAsync(CalendarTask task)
    {
        using var connection = _connectionFactory.CreateConnection();
        await EnsureTableAndDefaultsAsync(connection);

        const string sql = @"
            INSERT INTO CalendarTasks 
            (Title, Subtitle, StartDay, EndDay, RangeText, FrequencyType, TargetMonth, TargetYearMonth, NavTarget, NavBtnText, IsSystemDefault, IsActive, OrderIndex)
            VALUES 
            (@Title, @Subtitle, @StartDay, @EndDay, @RangeText, @FrequencyType, @TargetMonth, @TargetYearMonth, @NavTarget, @NavBtnText, @IsSystemDefault, @IsActive, @OrderIndex);
            SELECT last_insert_rowid();";
        return await connection.ExecuteScalarAsync<int>(sql, task);
    }

    public async Task<bool> UpdateAsync(CalendarTask task)
    {
        using var connection = _connectionFactory.CreateConnection();
        await EnsureTableAndDefaultsAsync(connection);

        const string sql = @"
            UPDATE CalendarTasks 
            SET Title = @Title,
                Subtitle = @Subtitle,
                StartDay = @StartDay,
                EndDay = @EndDay,
                RangeText = @RangeText,
                FrequencyType = @FrequencyType,
                TargetMonth = @TargetMonth,
                TargetYearMonth = @TargetYearMonth,
                NavTarget = @NavTarget,
                NavBtnText = @NavBtnText,
                IsSystemDefault = @IsSystemDefault,
                IsActive = @IsActive,
                OrderIndex = @OrderIndex
            WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, task);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await EnsureTableAndDefaultsAsync(connection);

        const string sql = "DELETE FROM CalendarTasks WHERE Id = @Id;";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task ResetToDefaultsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await EnsureTableAndDefaultsAsync(connection);

        await connection.ExecuteAsync("DELETE FROM CalendarTasks WHERE IsSystemDefault = 1;");

        var defaultTasks = GetDefaultTasks();
        foreach (var task in defaultTasks)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO CalendarTasks 
                (Title, Subtitle, StartDay, EndDay, RangeText, FrequencyType, TargetMonth, TargetYearMonth, NavTarget, NavBtnText, IsSystemDefault, IsActive, OrderIndex)
                VALUES 
                (@Title, @Subtitle, @StartDay, @EndDay, @RangeText, @FrequencyType, @TargetMonth, @TargetYearMonth, @NavTarget, @NavBtnText, @IsSystemDefault, @IsActive, @OrderIndex);",
                task);
        }
    }

    public static List<CalendarTask> GetDefaultTasks()
    {
        return new List<CalendarTask>
        {
            // 1. Aylık Standart MEB Rutinleri (Her Ay)
            new()
            {
                Title = "Devamsızlıklar kontrol edildi, SGK primleri hesaplandı, e-Bildirge onaylandı ve tahakkuk fişi alındı",
                Subtitle = "Aylık devamsızlık ve SGK e-Bildirge tahakkuk işlemleri",
                StartDay = 1,
                EndDay = 5,
                RangeText = "1 – 5 Arası",
                FrequencyType = TaskFrequencyType.RecurringMonthly,
                NavTarget = "xml-ebildirge",
                NavBtnText = "📑 SGK Bildirge",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 1
            },
            new()
            {
                Title = "SGK Prim Ödenek Talebi Yapıldı",
                Subtitle = "MEBBİS ➔ Ödenek İşlemleri ➔ Personel Giderleri",
                StartDay = 1,
                EndDay = 5,
                RangeText = "1 – 5 Arası",
                FrequencyType = TaskFrequencyType.RecurringMonthly,
                NavTarget = null,
                NavBtnText = null,
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 2
            },
            new()
            {
                Title = "İşletmelerden staj dekontları toplandı ve sisteme girildi",
                Subtitle = "Stajyer öğrenci ücret dekontlarının kontrolü ve sisteme işlenmesi",
                StartDay = 1,
                EndDay = 8,
                RangeText = "1 – 8 Arası",
                FrequencyType = TaskFrequencyType.RecurringMonthly,
                NavTarget = "fees",
                NavBtnText = "💳 Ücret & Dekont",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 3
            },
            new()
            {
                Title = "Devlet Desteği Ödemeleri MYS üzerinden Yapıldı",
                Subtitle = "Harcama Yönetim Sistemi (MYS) üzerinden işletme devlet desteği aktarımı",
                StartDay = 15,
                EndDay = 30,
                RangeText = "15 – 30 Arası",
                FrequencyType = TaskFrequencyType.RecurringMonthly,
                NavTarget = "fees",
                NavBtnText = "📑 Devlet Desteği",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 4
            },
            new()
            {
                Title = "SGK Prim Ödemeleri MYS üzerinden Yapıldı",
                Subtitle = "SGK prim ödemelerinin MYS üzerinden muhasebeleştirilmesi",
                StartDay = 15,
                EndDay = 30,
                RangeText = "15 – 30 Arası",
                FrequencyType = TaskFrequencyType.RecurringMonthly,
                NavTarget = "xml-ebildirge",
                NavBtnText = "⚡ e-Bildirge",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 5
            },

            // 2. Dönemsel / Belirli Aylarda Yapılan Standart MEB Görevleri
            new()
            {
                Title = "İşletmelerle koordinatörlük sözleşmelerinin imzalanması ve sisteme işlenmesi",
                Subtitle = "Dönem başı işletme sözleşmeleri ve stajyer kayıtlarının tamamlanması",
                StartDay = 1,
                EndDay = 15,
                RangeText = "1 – 15 Arası",
                FrequencyType = TaskFrequencyType.SpecificMonthEveryYear,
                TargetMonth = 9, // Eylül
                NavTarget = "students",
                NavBtnText = "🎓 Öğrenciler",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 10
            },
            new()
            {
                Title = "Staja başlayan öğrencilerin SGK işe giriş bildirgelerinin verilmesi",
                Subtitle = "Yeni eğitim dönemi stajyer SGK işe giriş bildirgelerinin onaylanması",
                StartDay = 1,
                EndDay = 15,
                RangeText = "1 – 15 Arası",
                FrequencyType = TaskFrequencyType.SpecificMonthEveryYear,
                TargetMonth = 9, // Eylül
                NavTarget = "toplu-isegiris-cikis",
                NavBtnText = "📅 İşe Girişler",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 11
            },
            new()
            {
                Title = "1. Dönem staj ve koordinatörlük notlarının e-Okul'a girilmesi",
                Subtitle = "1. Dönem koordinatörlük not fişleri ve staj değerlendirmelerinin işlenmesi",
                StartDay = 10,
                EndDay = 20,
                RangeText = "10 – 20 Arası",
                FrequencyType = TaskFrequencyType.SpecificMonthEveryYear,
                TargetMonth = 1, // Ocak
                NavTarget = null,
                NavBtnText = null,
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 12
            },
            new()
            {
                Title = "2. Dönem işletme ve koordinatör öğretmen görevlendirmelerinin onaylanması",
                Subtitle = "2. Dönem koordinatörlük görev onay formlarının güncellenmesi",
                StartDay = 1,
                EndDay = 15,
                RangeText = "1 – 15 Arası",
                FrequencyType = TaskFrequencyType.SpecificMonthEveryYear,
                TargetMonth = 2, // Şubat
                NavTarget = "forms",
                NavBtnText = "📑 Görev Formları",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 13
            },
            new()
            {
                Title = "12. Sınıf staj dosyaları ve not fişlerinin teslim alınması ve arşive kaldırılması",
                Subtitle = "Yıl sonu staj dosyalarının kontrolü, mühürlenmesi ve okul arşivine teslimi",
                StartDay = 1,
                EndDay = 20,
                RangeText = "1 – 20 Arası",
                FrequencyType = TaskFrequencyType.SpecificMonthEveryYear,
                TargetMonth = 6, // Haziran
                NavTarget = null,
                NavBtnText = null,
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 14
            },
            new()
            {
                Title = "Dönem sonu öğrencilerin SGK işten çıkış bildirgelerinin verilmesi",
                Subtitle = "Stajı biten veya mezun olan öğrencilerin SGK işten çıkış bildirgeleri",
                StartDay = 10,
                EndDay = 25,
                RangeText = "10 – 25 Arası",
                FrequencyType = TaskFrequencyType.SpecificMonthEveryYear,
                TargetMonth = 6, // Haziran
                NavTarget = "toplu-isegiris-cikis",
                NavBtnText = "📅 İşe Çıkışlar",
                IsSystemDefault = true,
                IsActive = true,
                OrderIndex = 15
            }
        };
    }
}
