using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class VisitAndPayrollTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;

    public VisitAndPayrollTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_visit_test_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(_tempDbPath);
        _factory = new SqliteConnectionFactory(config);

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();

        using var conn = _factory.CreateConnection();
        conn.Execute("INSERT INTO Teachers (Id, FirstName, LastName, DepartmentId, IsActive) VALUES (1, 'Hakan', 'Öztürk', 1, 1);");
        conn.Execute("INSERT INTO Enterprises (Id, Name, DistanceType, IsActive) VALUES (1, 'Aselsan A.Ş.', 1, 1);");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task CoordinationVisitRepository_AddAndUpdate_ShouldWorkCorrectly()
    {
        // Arrange
        var assignRepo = new CoordinatorAssignmentRepository(_factory);
        var visitRepo = new CoordinationVisitRepository(_factory);

        var assignId = await assignRepo.AddAsync(new CoordinatorAssignment
        {
            AcademicYearId = 1,
            TeacherId = 1,
            EnterpriseId = 1,
            DayOfWeek = DayOfWeekTurkish.Pazartesi,
            StudentCount = 12,
            CalculatedHours = 4.0,
            IsActive = true
        });

        var visit = new CoordinationVisit
        {
            AssignmentId = assignId,
            VisitDate = new DateTime(2024, 10, 7),
            Status = VisitStatus.Yapilmadi,
            FormNumber = "ZF-202410-01",
            ObservationNotes = "İlk ziyaret planlandı"
        };

        // Act
        var visitId = await visitRepo.AddAsync(visit);
        var retrieved = await visitRepo.GetByIdAsync(visitId);

        Assert.NotNull(retrieved);
        Assert.Equal(VisitStatus.Yapilmadi, retrieved.Status);

        // Update to Yapıldı
        retrieved.Status = VisitStatus.Yapildi;
        retrieved.ObservationNotes = "Ziyaret başarıyla icra edildi.";
        var updated = await visitRepo.UpdateAsync(retrieved);

        var finalState = await visitRepo.GetByIdAsync(visitId);

        // Assert
        Assert.True(updated);
        Assert.NotNull(finalState);
        Assert.Equal(VisitStatus.Yapildi, finalState.Status);
        Assert.Equal("Ziyaret başarıyla icra edildi.", finalState.ObservationNotes);
    }

    [Fact]
    public async Task CoordinationVisitRepository_BatchGenerate_ShouldCreateVisitsForMondays()
    {
        // Arrange
        var assignRepo = new CoordinatorAssignmentRepository(_factory);
        var visitRepo = new CoordinationVisitRepository(_factory);

        // Ekim 2024'te Pazartesi günleri: 7, 14, 21, 28 (4 adet Pazartesi)
        await assignRepo.AddAsync(new CoordinatorAssignment
        {
            AcademicYearId = 1,
            TeacherId = 1,
            EnterpriseId = 1,
            DayOfWeek = DayOfWeekTurkish.Pazartesi,
            StudentCount = 10,
            CalculatedHours = 4.0,
            IsActive = true
        });

        // Act - Ekim 2024 için toplu ziyaret planı üret
        var count = await visitRepo.BatchGenerateVisitsForMonthAsync(1, 2024, 10);
        var allVisits = (await visitRepo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(4, count);
        Assert.True(allVisits.Count >= 4);
    }

    [Fact]
    public async Task MonthlyPayrollRepository_GeneratePayroll_ShouldCalculateWeeksAndAmount()
    {
        // Arrange
        var assignRepo = new CoordinatorAssignmentRepository(_factory);
        var payrollRepo = new MonthlyPayrollRepository(_factory);

        // Öğretmen 1 için görevlendirme: Pazartesi günleri 4.0 saat
        await assignRepo.AddAsync(new CoordinatorAssignment
        {
            AcademicYearId = 1,
            TeacherId = 1,
            EnterpriseId = 1,
            DayOfWeek = DayOfWeekTurkish.Pazartesi,
            StudentCount = 12,
            CalculatedHours = 4.0,
            IsActive = true
        });

        // Act - Ekim 2024 (2024-10) için bordro oluştur
        var payrollId = await payrollRepo.CreatePayrollAsync(1, 2024, 10);
        const decimal hourlyRate = 125.50m;
        await payrollRepo.GeneratePayrollItemsAsync(payrollId, hourlyRate);

        var items = (await payrollRepo.GetPayrollItemsAsync(payrollId)).ToList();
        var teacher1Item = items.FirstOrDefault(i => i.TeacherId == 1);

        // Assert
        Assert.NotNull(teacher1Item);
        // Ekim 2024 Pazartesi günleri: 7 (H1: 4s), 14 (H2: 4s), 21 (H3: 4s), 28 (H4: 4s) -> Toplam: 16 saat
        Assert.Equal(16.0, teacher1Item.TotalHours);
        Assert.Equal(hourlyRate, teacher1Item.HourlyRate);
        Assert.Equal(16.0m * hourlyRate, teacher1Item.TotalAmount); // 16 * 125.50 = 2008.00 TL
    }

    [Fact]
    public async Task MonthlyPayrollRepository_StatusFlow_ShouldUpdateCorrectly()
    {
        // Arrange
        var payrollRepo = new MonthlyPayrollRepository(_factory);
        var payrollId = await payrollRepo.CreatePayrollAsync(1, 2024, 11);

        var initial = await payrollRepo.GetByIdAsync(payrollId);
        Assert.NotNull(initial);
        Assert.Equal(PayrollStatus.Taslak, initial.Status);

        // Act & Assert 1: Onayla
        await payrollRepo.UpdateStatusAsync(payrollId, PayrollStatus.Onaylandi);
        var approved = await payrollRepo.GetByIdAsync(payrollId);
        Assert.NotNull(approved);
        Assert.Equal(PayrollStatus.Onaylandi, approved.Status);
        Assert.NotNull(approved.ApprovedDate);

        // Act & Assert 2: Kilitle
        await payrollRepo.UpdateStatusAsync(payrollId, PayrollStatus.Kilitlendi);
        var locked = await payrollRepo.GetByIdAsync(payrollId);
        Assert.NotNull(locked);
        Assert.Equal(PayrollStatus.Kilitlendi, locked.Status);
    }
}
