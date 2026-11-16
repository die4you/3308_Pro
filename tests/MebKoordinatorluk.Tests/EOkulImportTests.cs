using System.Text;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class EOkulImportTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;

    public EOkulImportTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_eokul_test_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(_tempDbPath);
        _factory = new SqliteConnectionFactory(config);

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
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
    public void EOkulClassListParser_HtmlFormat_ShouldParseCorrectly()
    {
        // Arrange: MEB e-Okul HTML format (often saved with .xls extension)
        var htmlContent = @"
<html>
<body>
<table>
    <tr><td colspan='7'>T.C. MİLLÎ EĞİTİM BAKANLIĞI</td></tr>
    <tr><td colspan='7'>AMP - 9. Sınıf / A Şubesi (BİLİŞİM TEKNOLOJİLERİ ALANI) Sınıf Listesi</td></tr>
    <tr>
        <td>S.No</td>
        <td>Öğrenci No</td>
        <td>Adı</td>
        <td>Soyadı</td>
        <td>Cinsiyeti</td>
        <td>Doğum Tarihi</td>
        <td>Yaşı</td>
    </tr>
    <tr>
        <td>1</td>
        <td>101</td>
        <td>ALİ MERT</td>
        <td>YILMAZ</td>
        <td>Erkek</td>
        <td>15.05.2008</td>
        <td>16</td>
    </tr>
    <tr>
        <td>2</td>
        <td>102</td>
        <td>AYŞE</td>
        <td>KAYA</td>
        <td>Kız</td>
        <td>20.08.2008</td>
        <td>16</td>
    </tr>
    <tr>
        <td colspan='7'>Toplam Öğrenci Sayısı: 2</td>
    </tr>
</table>
</body>
</html>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));

        // Act
        var result = EOkulClassListParser.ParseStream(stream, "sinif_listesi.xls");

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.TotalStudentsCount);
        Assert.Equal(1, result.TotalClassesCount);

        var first = result.AllStudents[0];
        Assert.Equal("101", first.StudentNumber);
        Assert.Equal("Ali Mert", first.FirstName);
        Assert.Equal("YILMAZ", first.LastName);
        Assert.Equal("Ali Mert YILMAZ", first.FullName);
        Assert.Equal("Erkek", first.Gender);
        Assert.Equal(new DateTime(2008, 5, 15), first.BirthDate);
        Assert.Equal("9-A", first.Classroom);
        Assert.Equal("9", first.Grade);
        Assert.Equal("A", first.Branch);
        Assert.Equal("Bilişim Teknolojileri", first.FieldName);
        Assert.Equal("Örgün Eğitim", first.EducationType);

        var second = result.AllStudents[1];
        Assert.Equal("102", second.StudentNumber);
        Assert.Equal("Ayşe", second.FirstName);
        Assert.Equal("KAYA", second.LastName);
        Assert.Equal("Ayşe KAYA", second.FullName);
        Assert.Equal("Kız", second.Gender);
        Assert.Equal(new DateTime(2008, 8, 20), second.BirthDate);
        Assert.Equal("9-A", second.Classroom);
    }

    [Fact]
    public async Task StudentRepository_DeleteAllAsync_ShouldClearAllStudents()
    {
        // Arrange
        var repo = new StudentRepository(_factory);
        await repo.AddAsync(new Student
        {
            StudentNumber = "201",
            FirstName = "Can",
            LastName = "Demir",
            Classroom = "10-A",
            Grade = "10",
            IsActive = true
        });
        await repo.AddAsync(new Student
        {
            StudentNumber = "202",
            FirstName = "Zeynep",
            LastName = "Çelik",
            Classroom = "10-A",
            Grade = "10",
            IsActive = true
        });

        var initialList = (await repo.GetAllAsync()).ToList();
        Assert.True(initialList.Count >= 2);

        // Act
        var result = await repo.DeleteAllAsync();

        // Assert
        Assert.True(result);
        var afterList = (await repo.GetAllAsync()).ToList();
        Assert.Empty(afterList);
    }

    [Fact]
    public async Task SchoolClassRepository_DeleteAllAsync_ShouldClearAllClasses()
    {
        // Arrange
        var repo = new SchoolClassRepository(_factory);
        await repo.AddAsync(new SchoolClass { Name = "12-A" });
        var initialList = (await repo.GetAllAsync()).ToList();
        Assert.NotEmpty(initialList);

        // Act
        var result = await repo.DeleteAllAsync();

        // Assert
        Assert.True(result);
        var afterList = (await repo.GetAllAsync()).ToList();
        Assert.Empty(afterList);
    }

    [Fact]
    public async Task FieldRepository_DeleteAllAsync_ShouldClearAllFields()
    {
        // Arrange
        var repo = new FieldRepository(_factory);
        var initialList = (await repo.GetAllAsync()).ToList();
        Assert.NotEmpty(initialList);

        // Act
        var result = await repo.DeleteAllAsync();

        // Assert
        Assert.True(result);
        var afterList = (await repo.GetAllAsync()).ToList();
        Assert.Empty(afterList);
    }

    [Fact]
    public void EOkulParsedStudent_InternshipDays_RuleValidation()
    {
        // 1. Örgün Eğitim 9, 10, 11. sınıflar -> Boş olmalı
        var orgun9 = new EOkulParsedStudent { Grade = "9", EducationType = "Örgün Eğitim", Classroom = "9-A" };
        var orgun10 = new EOkulParsedStudent { Grade = "10", EducationType = "Örgün Eğitim", Classroom = "10-A" };
        var orgun11 = new EOkulParsedStudent { Grade = "11", EducationType = "Örgün Eğitim", Classroom = "11-A" };

        Assert.Equal(string.Empty, orgun9.InternshipDays);
        Assert.Equal(string.Empty, orgun10.InternshipDays);
        Assert.Equal(string.Empty, orgun11.InternshipDays);

        // 2. Örgün Eğitim 12. sınıf -> İşletme günleri dolu olmalı
        var orgun12 = new EOkulParsedStudent { Grade = "12", EducationType = "Örgün Eğitim", Classroom = "12-A" };
        Assert.Equal("Pazartesi,Salı,Çarşamba", orgun12.InternshipDays);

        // 3. MESEM 9, 10, 11, 12 -> Hepsinde işletme günleri dolu olmalı
        var mesem9 = new EOkulParsedStudent { Grade = "9", EducationType = "MESEM", Classroom = "MESEM 9-A" };
        var mesem10 = new EOkulParsedStudent { Grade = "10", EducationType = "MESEM", Classroom = "MESEM 10-A" };
        var mesem11 = new EOkulParsedStudent { Grade = "11", EducationType = "MESEM", Classroom = "MESEM 11-A" };
        var mesem12 = new EOkulParsedStudent { Grade = "12", EducationType = "MESEM", Classroom = "MESEM 12-A" };

        Assert.False(string.IsNullOrWhiteSpace(mesem9.InternshipDays));
        Assert.False(string.IsNullOrWhiteSpace(mesem10.InternshipDays));
        Assert.False(string.IsNullOrWhiteSpace(mesem11.InternshipDays));
        Assert.False(string.IsNullOrWhiteSpace(mesem12.InternshipDays));
    }

    [Fact]
    public void ExtractGrid_StreamAndFile_ShouldExtractRows()
    {
        var csvContent = "Yıl;Dönem;Boş;NUMARASI;ADI SOYADI;Boş;Boş;Boş;Boş;TARİHİ;Boş;TÜRÜ\n2026-2027;I. Dönem;;231;MELİSA EŞSİZ;;;;;16/09/2026;;D";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        var grid = EOkulClassListParser.ExtractGrid(stream, "test.csv");

        Assert.Equal(2, grid.Count);
        Assert.Equal("NUMARASI", grid[0][3]);
        Assert.Equal("231", grid[1][3]);
        Assert.Equal("16/09/2026", grid[1][9]);
        Assert.Equal("D", grid[1][11]);
    }
}
