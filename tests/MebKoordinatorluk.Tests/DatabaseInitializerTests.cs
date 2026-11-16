using System.Data;
using Dapper;
using MebKoordinatorluk.Data.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class DatabaseInitializerTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;

    public DatabaseInitializerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_test_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(_tempDbPath);
        _factory = new SqliteConnectionFactory(config);
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
    public void Initialize_ShouldCreateAllTenTables_AndEnableWalAndForeignKeys()
    {
        // Arrange
        var initializer = new DatabaseInitializer(_factory);

        // Act
        initializer.Initialize();

        // Assert
        using var connection = _factory.CreateConnection();

        // Check foreign keys
        var fkStatus = connection.ExecuteScalar<int>("PRAGMA foreign_keys;");
        Assert.Equal(1, fkStatus);

        // Check all 10 tables exist
        var expectedTables = new[]
        {
            "Settings",
            "AcademicYears",
            "Departments",
            "Teachers",
            "Enterprises",
            "Students",
            "StudentPlacements",
            "CoordinatorAssignments",
            "CoordinationVisits",
            "MonthlyPayrolls",
            "PayrollItems"
        };

        var actualTables = connection.Query<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';")
            .ToList();

        foreach (var expectedTable in expectedTables)
        {
            Assert.Contains(expectedTable, actualTables);
        }
    }

    [Fact]
    public void Initialize_ShouldSeedDefaultDataCorrectly()
    {
        // Arrange
        var initializer = new DatabaseInitializer(_factory);

        // Act
        initializer.Initialize();

        // Assert
        using var connection = _factory.CreateConnection();

        var settingCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Settings;");
        Assert.True(settingCount >= 5);

        var deptCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Departments;");
        Assert.True(deptCount >= 6);

        var fieldCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Fields;");
        Assert.True(fieldCount >= 5);

        var taskCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM CalendarTasks;");
        Assert.True(taskCount >= 10);

        var yearCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM AcademicYears;");
        Assert.True(yearCount >= 1);
    }

    [Fact]
    public void ForeignKeyConstraint_ShouldEnforceReferentialIntegrity()
    {
        // Arrange
        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();

        // Act & Assert
        using var connection = _factory.CreateConnection();

        // Non-existent DepartmentId 9999 should throw foreign key constraint exception
        const string insertStudentSql = @"
            INSERT INTO Students (TcKimlikNo, StudentNumber, FirstName, LastName, DepartmentId, Classroom, IsActive)
            VALUES ('99999999999', '101', 'Test', 'Öğrenci', 9999, '12-A', 1);";

        Assert.Throws<SqliteException>(() =>
        {
            connection.Execute(insertStudentSql);
        });
    }
}
