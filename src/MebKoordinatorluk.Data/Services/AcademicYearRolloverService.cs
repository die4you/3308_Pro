using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;
using Microsoft.Data.Sqlite;

namespace MebKoordinatorluk.Data.Services;

public class AcademicYearRolloverService : IAcademicYearRolloverService
{
    private readonly DatabaseConfig _dbConfig;
    private readonly DatabaseInitializer _dbInitializer;
    private readonly ILicenseService _licenseService;

    public AcademicYearRolloverService(
        DatabaseConfig dbConfig,
        DatabaseInitializer dbInitializer,
        ILicenseService licenseService)
    {
        _dbConfig = dbConfig;
        _dbInitializer = dbInitializer;
        _licenseService = licenseService;
    }

    public async Task<AcademicYearRolloverResult> ExecuteRolloverAsync(
        AcademicYearRolloverOptions options,
        IProgress<string>? progress = null)
    {
        var result = new AcademicYearRolloverResult();

        if (string.IsNullOrWhiteSpace(options.SourceYear) || string.IsNullOrWhiteSpace(options.TargetYear))
        {
            result.Success = false;
            result.Message = "Kaynak ve hedef eğitim-öğretim yılları belirtilmelidir.";
            return result;
        }

        // 1. Kaynak ve Hedef Veritabanı Yollarını Belirle
        var sourceDbPath = _dbConfig.GetDbPathForYear(options.SourceYear);
        if (!File.Exists(sourceDbPath))
        {
            if (File.Exists(_dbConfig.DatabasePath))
                sourceDbPath = _dbConfig.DatabasePath;
            else if (File.Exists(_dbConfig.DefaultDatabasePath))
                sourceDbPath = _dbConfig.DefaultDatabasePath;
            else
            {
                result.Success = false;
                result.Message = $"Kaynak veritabanı bulunamadı: {options.SourceYear}";
                return result;
            }
        }

        var targetDbPath = _dbConfig.GetDbPathForYear(options.TargetYear);

        try
        {
            progress?.Report("Hedef veritabanı dosyası hazırlanıyor...");

            // Hedef dosya varsa temiz bir başlangıç için sıfırla
            if (File.Exists(targetDbPath))
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    File.Delete(targetDbPath);
                }
                catch { }
            }

            // 2. Hedef Veritabanı Şemasını Başlat
            using (var targetInitConn = new SqliteConnection($"Data Source={targetDbPath};Mode=ReadWriteCreate;Foreign Keys=True;"))
            {
                await targetInitConn.OpenAsync();
                _dbInitializer.Initialize(targetInitConn);
            }

            progress?.Report("Temel veriler ve sistem ayarları kopyalanıyor...");

            using var srcConn = new SqliteConnection($"Data Source={sourceDbPath};Mode=ReadOnly;Foreign Keys=False;");
            await srcConn.OpenAsync();

            using var dstConn = new SqliteConnection($"Data Source={targetDbPath};Mode=ReadWriteCreate;Foreign Keys=False;");
            await dstConn.OpenAsync();

            // A. Lisans ve Kullanıcı Hesaplarını Kopyala
            var licenses = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Licenses")).ToList();
            if (licenses.Count > 0)
            {
                await dstConn.ExecuteAsync("DELETE FROM Licenses;");
                foreach (var lic in licenses)
                {
                    await dstConn.ExecuteAsync(@"
                        INSERT INTO Licenses (MachineCode, SchoolName, CityDistrict, StartDate, EndDate, LicenseKey, ActivatedAt, LastVerifiedAt, Status, Note)
                        VALUES (@MachineCode, @SchoolName, @CityDistrict, @StartDate, @EndDate, @LicenseKey, @ActivatedAt, @LastVerifiedAt, @Status, @Note);",
                        (object)lic);
                }
            }

            var licenseHistories = (await srcConn.QueryAsync<dynamic>("SELECT * FROM LicenseHistory")).ToList();
            foreach (var lh in licenseHistories)
            {
                await dstConn.ExecuteAsync(@"
                    INSERT INTO LicenseHistory (MachineCode, SchoolName, LicenseKey, StartDate, EndDate, ActivatedAt, Status, Note)
                    VALUES (@MachineCode, @SchoolName, @LicenseKey, @StartDate, @EndDate, @ActivatedAt, @Status, @Note);",
                    (object)lh);
            }

            var users = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Users")).ToList();
            if (users.Count > 0)
            {
                await dstConn.ExecuteAsync("DELETE FROM Users;");
                foreach (var u in users)
                {
                    await dstConn.ExecuteAsync(@"
                        INSERT INTO Users (Username, PasswordHash, PasswordSalt, FullName, Role, TeacherId, IsActive, CreatedAt, LastLoginAt)
                        VALUES (@Username, @PasswordHash, @PasswordSalt, @FullName, @Role, @TeacherId, @IsActive, @CreatedAt, @LastLoginAt);",
                        (object)u);
                }
            }

            // B. Sistem Ayarlarını Kopyala ve Güncelle
            var settings = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Settings")).ToList();
            var targetStartDateStr = options.TargetStartDate.ToString("yyyy-MM-dd");
            var targetEndDateStr = options.TargetEndDate.ToString("yyyy-MM-dd");
            var targetFormDateStr = options.TargetStartDate.ToString("dd/MM/yyyy");

            foreach (var s in settings)
            {
                string key = (string)s.Key;
                string val = (string)s.Value;

                if (key.Equals("AcademicYear", StringComparison.OrdinalIgnoreCase))
                    val = options.TargetYear;
                else if (key.Equals("OrgunStartDate", StringComparison.OrdinalIgnoreCase))
                    val = targetStartDateStr;
                else if (key.Equals("OrgunEndDate", StringComparison.OrdinalIgnoreCase))
                    val = targetEndDateStr;
                else if (key.Equals("FormDate", StringComparison.OrdinalIgnoreCase) || key.Equals("ValidityDate", StringComparison.OrdinalIgnoreCase))
                    val = targetFormDateStr;

                await dstConn.ExecuteAsync(@"
                    INSERT OR REPLACE INTO Settings (Key, Value, Description, UpdatedDate)
                    VALUES (@Key, @Value, @Description, @UpdatedDate);",
                    new { Key = key, Value = val, Description = (string?)s.Description, UpdatedDate = DateTime.UtcNow.ToString("o") });
            }

            // İdari Görev Takvimi Şablonunu Aktar
            try
            {
                var calendarTasks = (await srcConn.QueryAsync<dynamic>("SELECT * FROM CalendarTasks")).ToList();
                if (calendarTasks.Count > 0)
                {
                    await dstConn.ExecuteAsync("DELETE FROM CalendarTasks;");
                    foreach (var ct in calendarTasks)
                    {
                        await dstConn.ExecuteAsync(@"
                            INSERT INTO CalendarTasks (Id, Title, Subtitle, StartDay, EndDay, RangeText, FrequencyType, TargetMonth, TargetYearMonth, NavTarget, NavBtnText, IsSystemDefault, IsActive, OrderIndex)
                            VALUES (@Id, @Title, @Subtitle, @StartDay, @EndDay, @RangeText, @FrequencyType, @TargetMonth, @TargetYearMonth, @NavTarget, @NavBtnText, @IsSystemDefault, @IsActive, @OrderIndex);",
                            (object)ct);
                    }
                }
            }
            catch { }

            // C. Alan ve Dallar (Fields & Departments)
            if (options.CopyFields)
            {
                progress?.Report("Alan ve dal havuzu aktarılıyor...");
                var fields = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Fields")).ToList();
                foreach (var f in fields)
                {
                    await dstConn.ExecuteAsync(@"
                        INSERT OR IGNORE INTO Fields (Id, Name, Description, Branches, IsActive)
                        VALUES (@Id, @Name, @Description, @Branches, @IsActive);",
                        (object)f);
                }

                var depts = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Departments")).ToList();
                foreach (var d in depts)
                {
                    await dstConn.ExecuteAsync(@"
                        INSERT OR IGNORE INTO Departments (Id, Code, Name, SubBranch, IsActive)
                        VALUES (@Id, @Code, @Name, @SubBranch, @IsActive);",
                        (object)d);
                }
            }

            // D. Adres Grupları (AddressGroups)
            if (options.CopyAddressGroups)
            {
                progress?.Report("Bölge ve adres grupları aktarılıyor...");
                var groups = (await srcConn.QueryAsync<dynamic>("SELECT * FROM AddressGroups")).ToList();
                foreach (var g in groups)
                {
                    await dstConn.ExecuteAsync(@"
                        INSERT OR IGNORE INTO AddressGroups (Id, Name, Region, Description, Color, IsActive)
                        VALUES (@Id, @Name, @Region, @Description, @Color, @IsActive);",
                        (object)g);
                }
            }

            // E. Öğretmenler (Teachers)
            var teacherIdMap = new Dictionary<int, int>();
            if (options.CopyTeachers)
            {
                progress?.Report("Koordinatör öğretmen kadrosu aktarılıyor...");
                var teachers = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Teachers WHERE IsActive = 1")).ToList();
                foreach (var t in teachers)
                {
                    await dstConn.ExecuteAsync(@"
                        INSERT INTO Teachers (TcKimlikNo, FirstName, LastName, DepartmentId, Branch, Email, Phone, AvailableDays, EmploymentType, MaxCoordinationHours, TargetHours, CoordinatorFeeHours, IsActive)
                        VALUES (@TcKimlikNo, @FirstName, @LastName, @DepartmentId, @Branch, @Email, @Phone, @AvailableDays, @EmploymentType, @MaxCoordinationHours, @TargetHours, @CoordinatorFeeHours, @IsActive);",
                        (object)t);

                    int newTid = await dstConn.ExecuteScalarAsync<int>("SELECT last_insert_rowid();");
                    teacherIdMap[(int)t.Id] = newTid;
                    result.CopiedTeachersCount++;
                }
            }

            // F. Staj İşletmeleri (Enterprises)
            var enterpriseIdMap = new Dictionary<int, int>();
            if (options.CopyEnterprises)
            {
                progress?.Report("Staj işletmeleri havuzu aktarılıyor...");
                var enterprises = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Enterprises WHERE IsActive = 1")).ToList();
                foreach (var ent in enterprises)
                {
                    int? origTeacherId = (int?)ent.TeacherId;
                    int? mappedTeacherId = (origTeacherId.HasValue && teacherIdMap.TryGetValue(origTeacherId.Value, out var mId)) ? mId : null;

                    await dstConn.ExecuteAsync(@"
                        INSERT INTO Enterprises (Name, AddressGroupId, AddressGroupName, Sector, ContactPerson, Phone, Email, Address, District, City, DistanceType, DistanceText, Capacity, ActiveDays, TeacherId, TeacherName, TaxNumber, Iban, SgkSicilNo, MasterTrainerName, Notes, IsActive)
                        VALUES (@Name, @AddressGroupId, @AddressGroupName, @Sector, @ContactPerson, @Phone, @Email, @Address, @District, @City, @DistanceType, @DistanceText, @Capacity, @ActiveDays, @TeacherId, @TeacherName, @TaxNumber, @Iban, @SgkSicilNo, @MasterTrainerName, @Notes, @IsActive);",
                        new
                        {
                            ent.Name,
                            ent.AddressGroupId,
                            ent.AddressGroupName,
                            ent.Sector,
                            ent.ContactPerson,
                            ent.Phone,
                            ent.Email,
                            ent.Address,
                            ent.District,
                            ent.City,
                            ent.DistanceType,
                            ent.DistanceText,
                            ent.Capacity,
                            ent.ActiveDays,
                            TeacherId = mappedTeacherId,
                            TeacherName = ent.TeacherName,
                            ent.TaxNumber,
                            ent.Iban,
                            ent.SgkSicilNo,
                            ent.MasterTrainerName,
                            ent.Notes,
                            ent.IsActive
                        });

                    int newEid = await dstConn.ExecuteScalarAsync<int>("SELECT last_insert_rowid();");
                    enterpriseIdMap[(int)ent.Id] = newEid;
                    result.CopiedEnterprisesCount++;
                }
            }

            // G. Sınıf ve Öğrenci Terfisi (Class & Student Promotion)
            var classIdMap = new Dictionary<int, int>();
            if (options.PromoteClassesAndStudents)
            {
                progress?.Report("Sınıflar bir üst seviyeye terfi ettiriliyor (11 ➔ 12, 10 ➔ 11, 9 ➔ 10)...");
                var classes = (await srcConn.QueryAsync<dynamic>("SELECT * FROM SchoolClasses WHERE IsActive = 1")).ToList();

                foreach (var cls in classes)
                {
                    string oldClassName = (string)cls.Name;

                    // 12. sınıflar mezun olduğu için yeni yıla kopyalanmaz
                    if (IsClass12th(oldClassName))
                    {
                        continue;
                    }

                    // 11 -> 12, 10 -> 11, 9 -> 10
                    string newClassName = PromoteClassName(oldClassName);

                    await dstConn.ExecuteAsync(@"
                        INSERT INTO SchoolClasses (Name, FieldId, FieldName, InstitutionType, EducationType, InternshipDays, StartDate, EndDate, IgnoreHalfDayDeduction, HasInternship, IsActive)
                        VALUES (@Name, @FieldId, @FieldName, @InstitutionType, @EducationType, @InternshipDays, @StartDate, @EndDate, @IgnoreHalfDayDeduction, @HasInternship, @IsActive);",
                        new
                        {
                            Name = newClassName,
                            cls.FieldId,
                            cls.FieldName,
                            cls.InstitutionType,
                            cls.EducationType,
                            cls.InternshipDays,
                            StartDate = targetStartDateStr,
                            EndDate = targetEndDateStr,
                            cls.IgnoreHalfDayDeduction,
                            cls.HasInternship,
                            cls.IsActive
                        });

                    int newCid = await dstConn.ExecuteScalarAsync<int>("SELECT last_insert_rowid();");
                    classIdMap[(int)cls.Id] = newCid;
                    result.PromotedClassesCount++;
                }

                progress?.Report("Öğrenciler terfi ettiriliyor ve mezunlar arşivleniyor...");
                var students = (await srcConn.QueryAsync<dynamic>("SELECT * FROM Students WHERE IsActive = 1")).ToList();

                foreach (var s in students)
                {
                    string grade = (string?)s.Grade ?? "";
                    string classroom = (string?)s.Classroom ?? "";

                    bool is12th = grade == "12" || IsClass12th(classroom);
                    bool is11th = grade == "11" || IsClass11th(classroom);
                    bool is10th = grade == "10" || IsClass10th(classroom);
                    bool is9th = grade == "9" || IsClass9th(classroom);

                    // 12. Sınıflar Mezun Oldu: Yeni yıla aktarılmaz (Eski veritabanında arşivde güvende kalır)
                    if (is12th)
                    {
                        result.GraduatedStudentsCount++;
                        continue;
                    }

                    string newGrade;
                    string newClassroom = PromoteClassName(classroom);
                    int? newClassId = null;

                    int? origClassId = (int?)s.ClassId;
                    if (origClassId.HasValue && classIdMap.TryGetValue(origClassId.Value, out int mappedCid))
                    {
                        newClassId = mappedCid;
                    }

                    int? newEnterpriseId = null;
                    string? newEnterpriseName = null;
                    int? newTeacherId = null;
                    string? newTeacherName = null;
                    string educationPlace = (string?)s.EducationPlace ?? "İşletmede (Staj)";

                    if (is11th)
                    {
                        newGrade = "12";
                        educationPlace = "İşletmede (Staj)";

                        // 11'den 12'ye geçenler için işletme eşleşmeleri tercihe göre sıfırlanır
                        if (!options.ResetStudentEnterprisesForPromoted)
                        {
                            int? origEnterpriseId = (int?)s.EnterpriseId;
                            if (origEnterpriseId.HasValue && enterpriseIdMap.TryGetValue(origEnterpriseId.Value, out int mappedEid))
                            {
                                newEnterpriseId = mappedEid;
                                newEnterpriseName = (string?)s.EnterpriseName;
                            }

                            int? origTeacherId = (int?)s.TeacherId;
                            if (origTeacherId.HasValue && teacherIdMap.TryGetValue(origTeacherId.Value, out int mappedTid))
                            {
                                newTeacherId = mappedTid;
                                newTeacherName = (string?)s.TeacherName;
                            }
                        }
                    }
                    else if (is10th)
                    {
                        newGrade = "11";
                    }
                    else if (is9th)
                    {
                        newGrade = "10";
                    }
                    else
                    {
                        newGrade = grade;
                    }

                    await dstConn.ExecuteAsync(@"
                        INSERT INTO Students (TcKimlikNo, Iban, StudentNumber, FirstName, LastName, DepartmentId, ClassId, Classroom, Grade, FieldId, FieldName, EnterpriseId, EnterpriseName, TeacherId, TeacherName, InternshipDays, DutyDays, FeeCategory, EducationType, BirthDate, SgkEntryDate, SgkExitDate, SgkPrimKodu, Gender, HasFamilyHealthInsurance, EducationPlace, Phone, ParentPhone, Notes, IsActive)
                        VALUES (@TcKimlikNo, @Iban, @StudentNumber, @FirstName, @LastName, @DepartmentId, @ClassId, @Classroom, @Grade, @FieldId, @FieldName, @EnterpriseId, @EnterpriseName, @TeacherId, @TeacherName, @InternshipDays, @DutyDays, @FeeCategory, @EducationType, @BirthDate, @SgkEntryDate, @SgkExitDate, @SgkPrimKodu, @Gender, @HasFamilyHealthInsurance, @EducationPlace, @Phone, @ParentPhone, @Notes, 1);",
                        new
                        {
                            s.TcKimlikNo,
                            s.Iban,
                            s.StudentNumber,
                            s.FirstName,
                            s.LastName,
                            s.DepartmentId,
                            ClassId = newClassId,
                            Classroom = newClassroom,
                            Grade = newGrade,
                            s.FieldId,
                            s.FieldName,
                            EnterpriseId = newEnterpriseId,
                            EnterpriseName = newEnterpriseName,
                            TeacherId = newTeacherId,
                            TeacherName = newTeacherName,
                            s.InternshipDays,
                            s.DutyDays,
                            s.FeeCategory,
                            s.EducationType,
                            s.BirthDate,
                            s.SgkEntryDate,
                            s.SgkExitDate,
                            s.SgkPrimKodu,
                            s.Gender,
                            s.HasFamilyHealthInsurance,
                            EducationPlace = educationPlace,
                            s.Phone,
                            s.ParentPhone,
                            s.Notes
                        });

                    result.PromotedStudentsCount++;
                }
            }

            // H. Eğitim-Öğretim Yılları Kaydını Ekle
            await dstConn.ExecuteAsync(@"
                INSERT INTO AcademicYears (Name, Term, StartDate, EndDate, IsActive)
                VALUES (@Name, 1, @StartDate, @EndDate, 1);",
                new
                {
                    Name = options.TargetYear,
                    StartDate = targetStartDateStr,
                    EndDate = targetEndDateStr
                });

            // Kaynak veritabanına da yeni yılın varlığını ekle
            try
            {
                using var srcWriteConn = new SqliteConnection($"Data Source={sourceDbPath};Mode=ReadWrite;Foreign Keys=False;");
                await srcWriteConn.OpenAsync();
                var existsInSrc = await srcWriteConn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM AcademicYears WHERE Name = @Name;", new { Name = options.TargetYear });
                if (existsInSrc == 0)
                {
                    await srcWriteConn.ExecuteAsync(@"
                        INSERT INTO AcademicYears (Name, Term, StartDate, EndDate, IsActive)
                        VALUES (@Name, 1, @StartDate, @EndDate, 0);",
                        new
                        {
                            Name = options.TargetYear,
                            StartDate = targetStartDateStr,
                            EndDate = targetEndDateStr
                        });
                }
            }
            catch { }

            progress?.Report("Devir işlemi başarıyla tamamlandı.");

            result.Success = true;
            result.Message = $"🎉 {options.TargetYear} Eğitim-Öğretim Yılı başarıyla oluşturuldu!\n\n" +
                             $"• {result.PromotedStudentsCount} öğrenci bir üst sınıfa terfi ettirildi.\n" +
                             $"• {result.GraduatedStudentsCount} 12. sınıf öğrencisi mezun olarak eski yıl arşivinde korundu.\n" +
                             $"• {result.PromotedClassesCount} sınıf şubesi güncellendi.\n" +
                             $"• {result.CopiedTeachersCount} koordinatör öğretmen ve {result.CopiedEnterprisesCount} işletme aktarıldı.\n" +
                             $"• Devamsızlık ve puantaj çizelgeleri yeni yıl için tertemiz sıfırlandı.";

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Eğitim-öğretim yılı devir işlemi sırasında hata oluştu:\n{ex.Message}";
            return result;
        }
    }

    public static string PromoteClassName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;

        // 11 -> 12
        if (Regex.IsMatch(name, @"(^|\b|\D)11(\b|\D|$)"))
            return Regex.Replace(name, @"(^|\b|\D)11(\b|\D|$)", "${1}12${2}");

        // 10 -> 11
        if (Regex.IsMatch(name, @"(^|\b|\D)10(\b|\D|$)"))
            return Regex.Replace(name, @"(^|\b|\D)10(\b|\D|$)", "${1}11${2}");

        // 9 -> 10
        if (Regex.IsMatch(name, @"(^|\b|\D)9(\b|\D|$)"))
            return Regex.Replace(name, @"(^|\b|\D)9(\b|\D|$)", "${1}10${2}");

        return name;
    }

    private static bool IsClass12th(string name) => Regex.IsMatch(name, @"(^|\b|\D)12(\b|\D|$)");
    private static bool IsClass11th(string name) => Regex.IsMatch(name, @"(^|\b|\D)11(\b|\D|$)");
    private static bool IsClass10th(string name) => Regex.IsMatch(name, @"(^|\b|\D)10(\b|\D|$)");
    private static bool IsClass9th(string name) => Regex.IsMatch(name, @"(^|\b|\D)9(\b|\D|$)");
}
