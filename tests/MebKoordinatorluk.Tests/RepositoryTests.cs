using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class RepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;

    public RepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_repo_test_{Guid.NewGuid():N}.db");
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
    public async Task TeacherRepository_AddAndGetAll_ShouldWorkCorrectly()
    {
        // Arrange
        var repo = new TeacherRepository(_factory);
        var teacher = new Teacher
        {
            TcKimlikNo = "98765432101",
            FirstName = "Selim",
            LastName = "Yıldız",
            DepartmentId = 1,
            Email = "selim.yildiz@meb.k12.tr",
            Phone = "05320000000",
            EmploymentType = EmploymentType.Kadrolu,
            MaxCoordinationHours = 16,
            IsActive = true
        };

        // Act
        var newId = await repo.AddAsync(teacher);
        var all = (await repo.GetAllAsync()).ToList();
        var retrieved = await repo.GetByIdAsync(newId);

        // Assert
        Assert.True(newId > 0);
        Assert.NotNull(retrieved);
        Assert.Equal("Selim", retrieved.FirstName);
        Assert.Equal("Yıldız", retrieved.LastName);
        Assert.Contains(all, t => t.TcKimlikNo == "98765432101");
    }

    [Fact]
    public async Task TeacherRepository_AddMultipleTeachersWithEmptyTc_ShouldNotFailUniqueConstraint()
    {
        var repo = new TeacherRepository(_factory);
        var t1 = new Teacher
        {
            TcKimlikNo = "",
            FirstName = "Ali",
            LastName = "Veli",
            Branch = "Bilişim",
            IsActive = true
        };
        var t2 = new Teacher
        {
            TcKimlikNo = "",
            FirstName = "Ahmet",
            LastName = "Can",
            Branch = "Elektrik",
            IsActive = true
        };

        var id1 = await repo.AddAsync(t1);
        var id2 = await repo.AddAsync(t2);

        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
    }

    [Fact]
    public async Task SettingRepository_GetAndSet_ShouldPersistValue()
    {
        // Arrange
        var repo = new SettingRepository(_factory);

        // Act
        await repo.SetValueAsync("TestParam", "TestValue123", "Birim test açıklaması");
        var val = await repo.GetValueAsync("TestParam");

        // Assert
        Assert.Equal("TestValue123", val);
    }

    [Fact]
    public async Task EnterpriseRepository_AddAndSoftDelete_ShouldWorkCorrectly()
    {
        // Arrange
        var repo = new EnterpriseRepository(_factory);
        var ent = new Enterprise
        {
            Name = "Örnek Yazılım Sanayi",
            TaxNumber = "1122334455",
            Iban = "TR120006200000000123456789",
            SgkSicilNo = "21234010112345670341234",
            ContactPerson = "Barış Kaya",
            Phone = "02120001122",
            District = "Kadıköy",
            City = "İstanbul",
            DistanceType = DistanceType.MucavirAlanIci,
            Sector = "Bilişim",
            IsActive = true
        };

        // Act
        var id = await repo.AddAsync(ent);
        var fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.True(fetched.IsActive);
        Assert.Equal("TR120006200000000123456789", fetched.Iban);
        Assert.Equal("21234010112345670341234", fetched.SgkSicilNo);

        await repo.DeleteAsync(id);
        var deleted = await repo.GetByIdAsync(id);

        // Assert
        Assert.NotNull(deleted);
        Assert.False(deleted.IsActive);
    }

    [Fact]
    public async Task DepartmentRepository_AddUpdateAndDelete_ShouldWorkCorrectly()
    {
        // Arrange
        var repo = new DepartmentRepository(_factory);
        var dept = new Department
        {
            Code = "GRA",
            Name = "Grafik ve Fotoğraf",
            SubBranch = "Grafik Tasarım",
            IsActive = true
        };

        // Act - Add
        var id = await repo.AddAsync(dept);
        var retrieved = await repo.GetByIdAsync(id);
        Assert.NotNull(retrieved);
        Assert.Equal("GRA", retrieved.Code);

        // Act - Update
        retrieved.SubBranch = "Animasyon ve Çizgi Film";
        var updated = await repo.UpdateAsync(retrieved);
        Assert.True(updated);

        var updatedDept = await repo.GetByIdAsync(id);
        Assert.NotNull(updatedDept);
        Assert.Equal("Animasyon ve Çizgi Film", updatedDept.SubBranch);

        // Act - Soft Delete
        var deleted = await repo.DeleteAsync(id);
        Assert.True(deleted);

        var activeDepts = await repo.GetAllAsync();
        Assert.DoesNotContain(activeDepts, d => d.Id == id);
    }

    [Fact]
    public async Task StudentRepository_AddAndGetWithSgkFields_ShouldPersistCorrectly()
    {
        // Arrange
        var repo = new StudentRepository(_factory);
        var birthDate = DateTime.Today.AddYears(-21); // 21 yaşında
        var student = new Student
        {
            StudentNumber = "1234",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            TcKimlikNo = "12345678901",
            BirthDate = birthDate,
            Gender = "Erkek",
            HasFamilyHealthInsurance = false,
            EducationPlace = "İşletmede (Staj)",
            SgkPrimKodu = "42 Nolu Belge Türü",
            IsActive = true
        };

        // Act
        var id = await repo.AddAsync(student);
        var retrieved = await repo.GetByIdAsync(id);

        // Assert
        Assert.True(id > 0);
        Assert.NotNull(retrieved);
        Assert.Equal("12345678901", retrieved.TcKimlikNo);
        Assert.Equal("Erkek", retrieved.Gender);
        Assert.False(retrieved.HasFamilyHealthInsurance);
        Assert.Equal("İşletmede (Staj)", retrieved.EducationPlace);
        Assert.Equal("42 Nolu Belge Türü", retrieved.SgkPrimKodu);
        Assert.Equal(21, retrieved.Age);

        // Test update
        retrieved.Gender = "Kız";
        retrieved.HasFamilyHealthInsurance = true;
        retrieved.EducationPlace = "Okulda";
        retrieved.SgkPrimKodu = "49 Nolu Belge Türü";
        var updated = await repo.UpdateAsync(retrieved);
        Assert.True(updated);

        var retrievedAfterUpdate = await repo.GetByIdAsync(id);
        Assert.NotNull(retrievedAfterUpdate);
        Assert.Equal("Kız", retrievedAfterUpdate.Gender);
        Assert.True(retrievedAfterUpdate.HasFamilyHealthInsurance);
        Assert.Equal("Okulda", retrievedAfterUpdate.EducationPlace);
        Assert.Equal("49 Nolu Belge Türü", retrievedAfterUpdate.SgkPrimKodu);
    }

    [Fact]
    public async Task AttendanceRepository_SaveAndGetBlankCodes_ShouldPersistCorrectly()
    {
        // Arrange
        var repo = new AttendanceRepository(_factory);
        var entries = new List<AttendanceEntry>
        {
            new() { Year = 2024, Month = 9, StudentId = 1, DayOfMonth = 1, Code = "" },
            new() { Year = 2024, Month = 9, StudentId = 1, DayOfMonth = 2, Code = "" },
            new() { Year = 2024, Month = 9, StudentId = 1, DayOfMonth = 9, Code = "X" },
            new() { Year = 2024, Month = 9, StudentId = 1, DayOfMonth = 10, Code = "D" }
        };

        // Act
        await repo.SaveEntriesAsync(entries);
        var fetched = await repo.GetForMonthAsync(2024, 9);

        // Assert
        Assert.Equal(4, fetched.Count);
        Assert.Equal("", fetched.First(e => e.DayOfMonth == 1).Code);
        Assert.Equal("", fetched.First(e => e.DayOfMonth == 2).Code);
        Assert.Equal("X", fetched.First(e => e.DayOfMonth == 9).Code);
        Assert.Equal("D", fetched.First(e => e.DayOfMonth == 10).Code);
    }

    [Fact]
    public async Task AttendanceRepository_SyncClassAttendanceEntriesAsync_ShouldUpdateMesemEntriesAndPreserveAbsences()
    {
        // Arrange
        var repo = new AttendanceRepository(_factory);
        var schoolClass = new SchoolClass { Id = 1, Name = "10-A", EducationType = "MESEM" };
        var student = new Student { Id = 10, FirstName = "Aysima", LastName = "Apohan", Classroom = "10-A", Grade = "10" };

        // Save a pre-existing absence on Sept 10
        await repo.SaveEntryAsync(new AttendanceEntry { Year = 2024, Month = 9, StudentId = 10, DayOfMonth = 10, Code = "R" });

        // Act - Sync to MESEM
        await repo.SyncClassAttendanceEntriesAsync(schoolClass, new[] { student }, toMesem: true);

        // Assert
        var fetched = await repo.GetForMonthAsync(2024, 9);
        var studentEntries = fetched.Where(e => e.StudentId == 10).ToList();
        Assert.NotEmpty(studentEntries);

        // Raporlu day should be preserved
        var day10 = studentEntries.FirstOrDefault(e => e.DayOfMonth == 10);
        Assert.NotNull(day10);
        Assert.Equal("R", day10.Code);

        // Sept 9 was Monday -> should be 'X' in MESEM
        var day9 = studentEntries.FirstOrDefault(e => e.DayOfMonth == 9);
        Assert.NotNull(day9);
        Assert.Equal("X", day9.Code);

        // Sept 8 was Sunday -> should be '-'
        var day8 = studentEntries.FirstOrDefault(e => e.DayOfMonth == 8);
        Assert.NotNull(day8);
        Assert.Equal("-", day8.Code);
    }

    [Fact]
    public async Task StudentRepository_SyncClassInfoToStudentsAsync_ShouldUpdateStudentsInternshipDays()
    {
        // Arrange
        var classRepo = new SchoolClassRepository(_factory);
        var studentRepo = new StudentRepository(_factory);

        var schoolClass = new SchoolClass
        {
            Name = "12-Z",
            FieldId = 1,
            FieldName = "Bilişim Teknolojileri",
            EducationType = "Örgün Eğitim",
            InternshipDays = "Pazartesi,Salı"
        };
        var classId = await classRepo.AddAsync(schoolClass);
        schoolClass.Id = classId;

        var st1 = new Student
        {
            StudentNumber = "9901",
            FirstName = "Ali",
            LastName = "Can",
            ClassId = classId,
            Classroom = "12-Z",
            InternshipDays = "Pazartesi,Salı",
            DepartmentId = 1
        };
        var st2 = new Student
        {
            StudentNumber = "9902",
            FirstName = "Veli",
            LastName = "Han",
            Classroom = "12-Z", // Only classroom matches
            InternshipDays = "Pazartesi,Salı",
            DepartmentId = 1
        };
        await studentRepo.AddAsync(st1);
        await studentRepo.AddAsync(st2);

        // Act - Sınıfın işletme günleri değişiyor: "Çarşamba,Perşembe,Cuma"
        schoolClass.InternshipDays = "Çarşamba,Perşembe,Cuma";
        await classRepo.UpdateAsync(schoolClass);
        var affected = await studentRepo.SyncClassInfoToStudentsAsync(schoolClass);

        // Assert
        Assert.Equal(2, affected);

        var updatedSt1 = (await studentRepo.GetAllAsync()).First(s => s.StudentNumber == "9901");
        var updatedSt2 = (await studentRepo.GetAllAsync()).First(s => s.StudentNumber == "9902");

        Assert.Equal("Çarşamba,Perşembe,Cuma", updatedSt1.InternshipDays);
        Assert.Equal("Çarşamba,Perşembe,Cuma", updatedSt2.InternshipDays);
        Assert.Equal(classId, updatedSt2.ClassId);
    }

    [Fact]
    public async Task StudentRepository_SyncClassInfoAndSgkCodesToStudentsAsync_UpdateField_ShouldUpdateWhenTrueAndKeepWhenFalse()
    {
        // Arrange
        var classRepo = new SchoolClassRepository(_factory);
        var studentRepo = new StudentRepository(_factory);

        var schoolClass = new SchoolClass
        {
            Name = "12-E",
            FieldId = 1,
            FieldName = "Elektrik-Elektronik Teknolojisi",
            EducationType = "Örgün Eğitim",
            InternshipDays = "Pazartesi,Salı,Çarşamba"
        };
        var classId = await classRepo.AddAsync(schoolClass);
        schoolClass.Id = classId;

        var st = new Student
        {
            StudentNumber = "8801",
            FirstName = "Can",
            LastName = "Demir",
            ClassId = classId,
            Classroom = "12-E",
            FieldId = 1,
            FieldName = "Elektrik-Elektronik Teknolojisi",
            DepartmentId = 1
        };
        await studentRepo.AddAsync(st);

        // Act 1: Sınıf alanı Makine olarak değişti ama updateField = false
        schoolClass.FieldId = 2;
        schoolClass.FieldName = "Makine ve Tasarım Teknolojisi";
        await classRepo.UpdateAsync(schoolClass);

        await studentRepo.SyncClassInfoAndSgkCodesToStudentsAsync(schoolClass, null, updateSgkCodes: false, toMesem: false, updateField: false);

        var studentAfterFalse = (await studentRepo.GetAllAsync()).First(s => s.StudentNumber == "8801");
        Assert.Equal("Elektrik-Elektronik Teknolojisi", studentAfterFalse.FieldName);
        Assert.Equal(1, studentAfterFalse.FieldId);

        // Act 2: updateField = true
        await studentRepo.SyncClassInfoAndSgkCodesToStudentsAsync(schoolClass, null, updateSgkCodes: false, toMesem: false, updateField: true);

        var studentAfterTrue = (await studentRepo.GetAllAsync()).First(s => s.StudentNumber == "8801");
        Assert.Equal("Makine ve Tasarım Teknolojisi", studentAfterTrue.FieldName);
        Assert.Equal(2, studentAfterTrue.FieldId);
    }

    [Fact]
    public async Task StudentRepository_SyncClassInfoAndSgkCodesToStudentsAsync_ToMesem_ShouldSet42WhenNoHealthInsurance()
    {
        // Arrange
        var classRepo = new SchoolClassRepository(_factory);
        var studentRepo = new StudentRepository(_factory);

        var schoolClass = new SchoolClass
        {
            Name = "11-M",
            EducationType = "MESEM",
            InstitutionType = "MESEM"
        };
        var classId = await classRepo.AddAsync(schoolClass);
        schoolClass.Id = classId;

        var stWithInsurance = new Student
        {
            StudentNumber = "8001",
            FirstName = "Ahmet",
            LastName = "Kaya",
            ClassId = classId,
            Classroom = "11-M",
            HasFamilyHealthInsurance = true,
            SgkPrimKodu = "49"
        };
        var stWithoutInsurance = new Student
        {
            StudentNumber = "8002",
            FirstName = "Mehmet",
            LastName = "Demir",
            ClassId = classId,
            Classroom = "11-M",
            HasFamilyHealthInsurance = false,
            SgkPrimKodu = "50"
        };

        var id1 = await studentRepo.AddAsync(stWithInsurance);
        var id2 = await studentRepo.AddAsync(stWithoutInsurance);

        // Act - Sync to MESEM with updateSgkCodes = true, toMesem = true
        await studentRepo.SyncClassInfoAndSgkCodesToStudentsAsync(schoolClass, null, updateSgkCodes: true, toMesem: true);

        // Assert
        var updated1 = await studentRepo.GetByIdAsync(id1);
        var updated2 = await studentRepo.GetByIdAsync(id2);

        Assert.NotNull(updated1);
        Assert.NotNull(updated2);
        Assert.Equal("MESEM", updated1.EducationType);
        Assert.Equal("MESEM", updated2.EducationType);
        Assert.Equal("7", updated1.SgkPrimKodu);
        Assert.Equal("42", updated2.SgkPrimKodu); // Must be 42, not 50!
    }

    [Fact]
    public async Task SchoolClassRepository_EducationTypes_ShouldBeDistinctAndRetrievable()
    {
        // Arrange
        var classRepo = new SchoolClassRepository(_factory);
        var c1 = new SchoolClass { Name = "10-M1", EducationType = "MESEM", InstitutionType = "MESEM" };
        var c2 = new SchoolClass { Name = "10-M2", EducationType = "MESEM", InstitutionType = "MESEM" };
        var c3 = new SchoolClass { Name = "10-A", EducationType = "Örgün Eğitim", InstitutionType = "Anadolu Meslek Programı" };

        await classRepo.AddAsync(c1);
        await classRepo.AddAsync(c2);
        await classRepo.AddAsync(c3);

        // Act
        var classes = (await classRepo.GetAllAsync()).ToList();
        var eduTypes = classes
            .Select(c => c.EducationType?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e)
            .ToList();

        // Assert
        Assert.Contains("MESEM", eduTypes);
        Assert.Contains("Örgün Eğitim", eduTypes);
    }

    [Fact]
    public async Task StudentRepository_UpdateTcKimlikNo_ShouldPersistCorrectly()
    {
        // Arrange
        var studentRepo = new StudentRepository(_factory);
        var st = new Student
        {
            StudentNumber = "8888",
            FirstName = "Test",
            LastName = "Öğrenci",
            TcKimlikNo = "",
            DepartmentId = 1,
            Classroom = "12-A"
        };
        var id = await studentRepo.AddAsync(st);
        st.Id = id;

        // Act - TC Kimlik güncelle
        st.TcKimlikNo = "10274844088";
        var updated = await studentRepo.UpdateAsync(st);

        // Assert
        Assert.True(updated);
        var reloaded = await studentRepo.GetByIdAsync(id);
        Assert.NotNull(reloaded);
        Assert.Equal("10274844088", reloaded.TcKimlikNo);
    }

    [Fact]
    public async Task SchoolClassRepository_ExistsAsync_ShouldDetectDuplicatesAccurately()
    {
        // Arrange
        var repo = new SchoolClassRepository(_factory);
        var sc = new SchoolClass
        {
            Name = "99-TEST",
            FieldName = "Bilişim Teknolojileri",
            EducationType = "Örgün Eğitim",
            InstitutionType = "Anadolu Meslek Programı",
            IsActive = true
        };
        var id = await repo.AddAsync(sc);

        // Act & Assert
        // 1. Birebir aynı isim ve alan
        var existsExact = await repo.ExistsAsync("99-TEST", "Bilişim Teknolojileri");
        Assert.True(existsExact);

        // 2. Büyük/küçük harf ve boşluk duyarsız
        var existsCaseAndSpace = await repo.ExistsAsync("  99-test  ", "  bilişim teknolojileri  ");
        Assert.True(existsCaseAndSpace);

        // 3. Farklı alan adı ile aynı sınıf adı (çakışma yok)
        var existsDiffField = await repo.ExistsAsync("99-TEST", "Elektrik-Elektronik Teknolojisi");
        Assert.False(existsDiffField);

        // 4. Farklı sınıf adı ile aynı alan (çakışma yok)
        var existsDiffClass = await repo.ExistsAsync("98-TEST", "Bilişim Teknolojileri");
        Assert.False(existsDiffClass);

        // 5. Kendi kaydını güncellerken (excludeId) çakışma sayılmamalı
        var existsSelf = await repo.ExistsAsync("99-TEST", "Bilişim Teknolojileri", excludeId: id);
        Assert.False(existsSelf);

        // 6. Silindiğinde aktif kayıtlarda çakışma vermemeli
        await repo.DeleteAsync(id);
        var existsAfterDelete = await repo.ExistsAsync("99-TEST", "Bilişim Teknolojileri");
        Assert.False(existsAfterDelete);
    }

    [Fact]
    public async Task SchoolClassRepository_HasInternship_ShouldPersistCorrectly()
    {
        // Arrange
        var repo = new SchoolClassRepository(_factory);
        var scNoIntern = new SchoolClass
        {
            Name = "9-TEST-NOINTERN",
            FieldName = "Bilişim Teknolojileri",
            EducationType = "Örgün Eğitim",
            InstitutionType = "Anadolu Meslek Programı",
            HasInternship = false
        };

        // Act
        var id = await repo.AddAsync(scNoIntern);
        var fetched = await repo.GetByIdAsync(id);

        // Assert
        Assert.NotNull(fetched);
        Assert.False(fetched.HasInternship);

        // Update to true
        fetched.HasInternship = true;
        await repo.UpdateAsync(fetched);
        var updated = await repo.GetByIdAsync(id);
        Assert.NotNull(updated);
        Assert.True(updated.HasInternship);
    }

    [Fact]
    public async Task StudentRepository_ClearBulkSgkDatesAsync_ShouldClearDatesInDatabase()
    {
        // Arrange
        var studentRepo = new StudentRepository(_factory);
        var st1 = new Student
        {
            TcKimlikNo = "11111111111",
            FirstName = "Ahmet",
            LastName = "Kaya",
            SgkEntryDate = new DateTime(2026, 9, 1),
            SgkExitDate = new DateTime(2026, 9, 30),
            IsActive = true
        };
        var st2 = new Student
        {
            TcKimlikNo = "22222222222",
            FirstName = "Mehmet",
            LastName = "Demir",
            SgkEntryDate = new DateTime(2026, 9, 1),
            SgkExitDate = new DateTime(2026, 9, 30),
            IsActive = true
        };

        var id1 = await studentRepo.AddAsync(st1);
        var id2 = await studentRepo.AddAsync(st2);

        // Act - Clear Entry and Exit for st1 and st2
        var result = await studentRepo.ClearBulkSgkDatesAsync(new[] { id1, id2 }, clearEntry: true, clearExit: true);

        // Assert
        Assert.True(result);

        var reloaded1 = await studentRepo.GetByIdAsync(id1);
        var reloaded2 = await studentRepo.GetByIdAsync(id2);

        Assert.NotNull(reloaded1);
        Assert.NotNull(reloaded2);
        Assert.Null(reloaded1.SgkEntryDate);
        Assert.Null(reloaded1.SgkExitDate);
        Assert.Null(reloaded2.SgkEntryDate);
        Assert.Null(reloaded2.SgkExitDate);
    }

    [Fact]
    public async Task SettingRepository_InsuranceEntryExitParameters_ShouldPersistAndLoadCorrectly()
    {
        // Arrange
        var settingRepo = new SettingRepository(_factory);
        var settings = await settingRepo.GetAppSettingsAsync();

        // Check defaults
        Assert.Equal("37", settings.OrgunSigortaKolu);
        Assert.Equal("3", settings.OrgunOgrenimKodu);
        Assert.Equal("9901.02", settings.OrgunMeslekKodu);
        Assert.Equal("5", settings.OrgunGorevKodu);

        Assert.Equal("7", settings.MesemSigortaKolu);
        Assert.Equal("3", settings.MesemOgrenimKodu);
        Assert.Equal("9901.03", settings.MesemMeslekKodu);
        Assert.Equal("5", settings.MesemGorevKodu);

        // Act - Custom values
        settings.OrgunSigortaKolu = "37";
        settings.OrgunOgrenimKodu = "3";
        settings.OrgunMeslekKodu = "9901.02";
        settings.OrgunGorevKodu = "5";

        settings.MesemSigortaKolu = "7";
        settings.MesemOgrenimKodu = "3";
        settings.MesemMeslekKodu = "9901.03";
        settings.MesemGorevKodu = "5";

        await settingRepo.SaveAppSettingsAsync(settings);

        // Assert
        var reloaded = await settingRepo.GetAppSettingsAsync();
        Assert.Equal("37", reloaded.OrgunSigortaKolu);
        Assert.Equal("3", reloaded.OrgunOgrenimKodu);
        Assert.Equal("9901.02", reloaded.OrgunMeslekKodu);
        Assert.Equal("5", reloaded.OrgunGorevKodu);

        Assert.Equal("7", reloaded.MesemSigortaKolu);
        Assert.Equal("3", reloaded.MesemOgrenimKodu);
        Assert.Equal("9901.03", reloaded.MesemMeslekKodu);
        Assert.Equal("5", reloaded.MesemGorevKodu);
    }

    [Fact]
    public async Task StudentRepository_DeletePermanentlyAsync_ShouldDeleteStudentAndLinkedRecords()
    {
        // Arrange
        var studentRepo = new StudentRepository(_factory);
        var attendanceRepo = new AttendanceRepository(_factory);

        var student = new Student
        {
            StudentNumber = "999-P",
            FirstName = "Arşiv",
            LastName = "Öğrenci",
            Classroom = "12-A",
            DepartmentId = 1,
            IsActive = false
        };

        var studentId = await studentRepo.AddAsync(student);
        Assert.True(studentId > 0);

        // Devamsızlık kaydı ekle
        await attendanceRepo.SaveEntryAsync(new AttendanceEntry
        {
            Year = 2026,
            Month = 10,
            StudentId = studentId,
            DayOfMonth = 1,
            Code = "X"
        });

        // Act - Kalıcı sil
        var deleted = await studentRepo.DeletePermanentlyAsync(studentId);
        Assert.True(deleted);

        // Assert - Öğrenci tamamen silinmiş olmalı
        var fetched = await studentRepo.GetByIdAsync(studentId);
        Assert.Null(fetched);

        var archived = await studentRepo.GetArchivedStudentsAsync();
        Assert.DoesNotContain(archived, s => s.Id == studentId);

        // Devamsızlık kaydı da silinmiş olmalı
        var attendances = await attendanceRepo.GetForMonthAsync(2026, 10);
        Assert.DoesNotContain(attendances, a => a.StudentId == studentId);
    }
}

