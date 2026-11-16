using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class PlacementAndAssignmentTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;

    public PlacementAndAssignmentTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_assign_test_{Guid.NewGuid():N}.db");
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
    public async Task StudentRepository_AddAndFilter_ShouldWorkCorrectly()
    {
        // Arrange
        var repo = new StudentRepository(_factory);
        var student = new Student
        {
            TcKimlikNo = "12345678912",
            StudentNumber = "4501",
            FirstName = "Deniz",
            LastName = "Aydın",
            DepartmentId = 1,
            Grade = "12",
            Classroom = "12-A",
            Phone = "05559998877",
            IsActive = true
        };

        // Act
        var id = await repo.AddAsync(student);
        var fetched = await repo.GetByIdAsync(id);
        var filteredByDept = (await repo.GetAllAsync(departmentId: 1)).ToList();
        var filteredByGrade = (await repo.GetAllAsync(grade: "12")).ToList();

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(fetched);
        Assert.Equal("Deniz", fetched.FirstName);
        Assert.Contains(filteredByDept, s => s.Id == id);
        Assert.Contains(filteredByGrade, s => s.Id == id);
    }

    [Fact]
    public async Task StudentPlacementRepository_AddAndCountInEnterprise_ShouldWorkCorrectly()
    {
        // Arrange
        var studentRepo = new StudentRepository(_factory);
        var placementRepo = new StudentPlacementRepository(_factory);

        var s1Id = await studentRepo.AddAsync(new Student
        {
            TcKimlikNo = "11111111112",
            StudentNumber = "501",
            FirstName = "Ali",
            LastName = "Kaya",
            DepartmentId = 1,
            Grade = "12",
            Classroom = "12-A"
        });

        var s2Id = await studentRepo.AddAsync(new Student
        {
            TcKimlikNo = "11111111114",
            StudentNumber = "502",
            FirstName = "Veli",
            LastName = "Kaya",
            DepartmentId = 1,
            Grade = "12",
            Classroom = "12-A"
        });

        // Act - Place both students in Enterprise 1 for AcademicYear 1
        await placementRepo.AddAsync(new StudentPlacement
        {
            AcademicYearId = 1,
            StudentId = s1Id,
            EnterpriseId = 1,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(4),
            InternshipDays = "Pzt,Sal,Çrş",
            Status = PlacementStatus.Aktif
        });

        await placementRepo.AddAsync(new StudentPlacement
        {
            AcademicYearId = 1,
            StudentId = s2Id,
            EnterpriseId = 1,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(4),
            InternshipDays = "Pzt,Sal,Çrş",
            Status = PlacementStatus.Aktif
        });

        var count = await placementRepo.GetStudentCountInEnterpriseAsync(1, 1);
        var allPlacements = (await placementRepo.GetAllAsync(1)).ToList();

        // Assert
        Assert.Equal(2, count);
        Assert.True(allPlacements.Count >= 2);
    }

    [Fact]
    public async Task CoordinatorAssignmentRepository_TeacherHoursAggregation_ShouldTrackQuotas()
    {
        // Arrange
        var assignRepo = new CoordinatorAssignmentRepository(_factory);
        var calculator = new MebCoordinationCalculator();

        // Öğretmen 1 (Hakan Öztürk) -> İşletme 1 (Mücavir alan içi, 10 öğrenci -> 4 saat)
        var hours1 = calculator.CalculateWeeklyHours(10, DistanceType.MucavirAlanIci);
        Assert.Equal(4.0, hours1);

        await assignRepo.AddAsync(new CoordinatorAssignment
        {
            AcademicYearId = 1,
            TeacherId = 1,
            EnterpriseId = 1,
            DayOfWeek = DayOfWeekTurkish.Pazartesi,
            StudentCount = 10,
            CalculatedHours = hours1,
            IsActive = true
        });

        // Öğretmen 1 -> İşletme 2 (Mücavir alan dışı, 18 öğrenci -> 5 + 2 = 7 saat)
        var hours2 = calculator.CalculateWeeklyHours(18, DistanceType.MucavirAlanDisi);
        Assert.Equal(7.0, hours2);

        await assignRepo.AddAsync(new CoordinatorAssignment
        {
            AcademicYearId = 1,
            TeacherId = 1,
            EnterpriseId = 2,
            DayOfWeek = DayOfWeekTurkish.Carsamba,
            StudentCount = 18,
            CalculatedHours = hours2,
            IsActive = true
        });

        // Act - Calculate total assigned hours for Teacher 1 in AcademicYear 1
        var totalAssigned = await assignRepo.GetTeacherTotalAssignedHoursAsync(1, 1);

        // Assert
        Assert.Equal(11.0, totalAssigned); // 4 + 7 = 11 saat

        // Check if teacher can take another 4 hour assignment within 16 hour quota
        var canTake4More = calculator.CanAssignHours(totalAssigned, 4.0, 16);
        Assert.True(canTake4More); // 11 + 4 = 15 <= 16

        // Check if teacher can take another 6 hour assignment
        var canTake6More = calculator.CanAssignHours(totalAssigned, 6.0, 16);
        Assert.False(canTake6More); // 11 + 6 = 17 > 16
    }

    [Fact]
    public void SmartDistribution_WithTargetStudents_ShouldOnlyDistributeToTarget()
    {
        // Arrange
        var teacher = new Teacher
        {
            Id = 1,
            FullName = "Ahmet Yılmaz",
            Branch = "Bilişim Teknolojileri",
            AvailableDays = "Pazartesi,Salı,Çarşamba",
            IsActive = true
        };

        var enterprise = new Enterprise
        {
            Id = 10,
            Name = "Teknoloji A.Ş.",
            Sector = "Bilişim",
            ActiveDays = "Pazartesi,Salı,Çarşamba",
            Capacity = 10,
            IsActive = true
        };

        var org9Student = new Student
        {
            Id = 101,
            FirstName = "Ali",
            LastName = "9Örgün",
            Grade = "9",
            Classroom = "9-A",
            EducationType = "Örgün Eğitim",
            InternshipDays = "",
            FieldName = "Bilişim Teknolojileri"
        };

        var org12Student = new Student
        {
            Id = 102,
            FirstName = "Veli",
            LastName = "12Örgün",
            Grade = "12",
            Classroom = "12-A",
            EducationType = "Örgün Eğitim",
            InternshipDays = "Pazartesi,Salı,Çarşamba",
            EnterpriseId = enterprise.Id,
            EnterpriseName = enterprise.Name,
            FieldName = "Bilişim Teknolojileri"
        };

        var mesemStudent = new Student
        {
            Id = 103,
            FirstName = "Ayşe",
            LastName = "Mesem",
            Grade = "10",
            Classroom = "MESEM 10-A",
            EducationType = "MESEM",
            InternshipDays = "Pazartesi,Salı,Çarşamba",
            EnterpriseId = enterprise.Id,
            EnterpriseName = enterprise.Name,
            FieldName = "Bilişim Teknolojileri"
        };

        // Hedef ortak havuz: Sadece 12. Sınıf Örgün + MESEM (9. sınıf örgün havuzda yok!)
        var targetStudents = new List<Student> { org12Student, mesemStudent };

        var options = new DistributionOptions
        {
            PrioritizeDayMatch = true,
            PrioritizeBranchMatch = true,
            ClusterEnterprises = true,
            BalanceWorkload = true
        };

        // Act
        var result = SmartDistributionEngine.RunAutoDistribution(options, new List<Teacher> { teacher }, new List<Enterprise> { enterprise }, targetStudents);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.AssignedStudents);

        // Hedef öğrenciler öğretmen ataması aldı
        Assert.Equal(teacher.Id, org12Student.TeacherId);
        Assert.Equal(teacher.Id, mesemStudent.TeacherId);

        // Havuz dışındaki 9. sınıf örgün öğrencisine dokunulmadı
        Assert.Null(org9Student.TeacherId);
    }
}
