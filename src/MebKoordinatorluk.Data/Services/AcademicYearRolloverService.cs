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

        // 1. Aktif Çalışma Veritabanı (meb_koordinatorluk.db)
        var activeDbPath = _dbConfig.DefaultDatabasePath;
        if (!File.Exists(activeDbPath))
        {
            activeDbPath = _dbConfig.DatabasePath;
        }

        if (!File.Exists(activeDbPath))
        {
            result.Success = false;
            result.Message = $"Aktif veritabanı bulunamadı: {activeDbPath}";
            return result;
        }

        // 2. Geçmiş Yıl İçin Arşiv Dosyası Oluştur (arsiv_meb_{cleanSourceYear}.db)
        var cleanSource = DatabaseConfig.SanitizeYear(options.SourceYear);
        var archiveDbPath = Path.Combine(_dbConfig.DataDirectory, $"arsiv_meb_{cleanSource}.db");

        try
        {
            progress?.Report($"'{options.SourceYear}' yılı verileri arşivleniyor ({Path.GetFileName(archiveDbPath)})...");

            // Bağlantı havuzlarını temizle
            SqliteConnection.ClearAllPools();

            // Kaynak veritabanının tam birebir kopyasını arşiv olarak sakla (12. sınıflar, devamsızlıklar ve puantajlar burada korunur)
            File.Copy(activeDbPath, archiveDbPath, true);

            progress?.Report("Aktif veritabanı yeni eğitim-öğretim yılına hazırlanıyor...");

            using var conn = new SqliteConnection($"Data Source={activeDbPath};Mode=ReadWrite;Foreign Keys=False;");
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();

            // A. Sistem Ayarlarını Yeni Yıl Tarihleriyle Güncelle
            var targetStartDateStr = options.TargetStartDate.ToString("yyyy-MM-dd");
            var targetEndDateStr = options.TargetEndDate.ToString("yyyy-MM-dd");
            var targetFormDateStr = options.TargetStartDate.ToString("dd/MM/yyyy");

            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO Settings (Key, Value, UpdatedDate)
                VALUES ('AcademicYear', @AcademicYear, @UpdatedDate);",
                new { AcademicYear = options.TargetYear, UpdatedDate = DateTime.UtcNow.ToString("o") }, trans);

            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO Settings (Key, Value, UpdatedDate)
                VALUES ('OrgunStartDate', @StartDate, @UpdatedDate);",
                new { StartDate = targetStartDateStr, UpdatedDate = DateTime.UtcNow.ToString("o") }, trans);

            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO Settings (Key, Value, UpdatedDate)
                VALUES ('OrgunEndDate', @EndDate, @UpdatedDate);",
                new { EndDate = targetEndDateStr, UpdatedDate = DateTime.UtcNow.ToString("o") }, trans);

            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO Settings (Key, Value, UpdatedDate)
                VALUES ('FormDate', @FormDate, @UpdatedDate);",
                new { FormDate = targetFormDateStr, UpdatedDate = DateTime.UtcNow.ToString("o") }, trans);

            await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO Settings (Key, Value, UpdatedDate)
                VALUES ('ValidityDate', @ValidityDate, @UpdatedDate);",
                new { ValidityDate = targetFormDateStr, UpdatedDate = DateTime.UtcNow.ToString("o") }, trans);

            // B. Sınıf ve Öğrenci Terfisi
            if (options.PromoteClassesAndStudents)
            {
                progress?.Report("Sınıflar terfi ettiriliyor (11 ➔ 12, 10 ➔ 11, 9 ➔ 10)...");
                var classes = (await conn.QueryAsync<dynamic>("SELECT * FROM SchoolClasses WHERE IsActive = 1", null, trans)).ToList();

                foreach (var cls in classes)
                {
                    string oldClassName = (string)cls.Name;
                    int clsId = (int)cls.Id;

                    if (IsClass12th(oldClassName))
                    {
                        // 12. sınıflar mezun olduğu için silinir (arşivde zaten kayıtlı)
                        await conn.ExecuteAsync("DELETE FROM SchoolClasses WHERE Id = @Id;", new { Id = clsId }, trans);
                        continue;
                    }

                    string newClassName = PromoteClassName(oldClassName);
                    await conn.ExecuteAsync(@"
                        UPDATE SchoolClasses 
                        SET Name = @Name, StartDate = @StartDate, EndDate = @EndDate 
                        WHERE Id = @Id;",
                        new { Name = newClassName, StartDate = targetStartDateStr, EndDate = targetEndDateStr, Id = clsId }, trans);

                    result.PromotedClassesCount++;
                }

                progress?.Report("Öğrenciler terfi ettiriliyor ve mezunlar arşivleniyor...");
                var students = (await conn.QueryAsync<dynamic>("SELECT * FROM Students WHERE IsActive = 1", null, trans)).ToList();

                foreach (var s in students)
                {
                    string grade = (string?)s.Grade ?? "";
                    string classroom = (string?)s.Classroom ?? "";
                    int sId = (int)s.Id;

                    bool is12th = grade == "12" || IsClass12th(classroom);
                    bool is11th = grade == "11" || IsClass11th(classroom);
                    bool is10th = grade == "10" || IsClass10th(classroom);
                    bool is9th = grade == "9" || IsClass9th(classroom);

                    if (is12th)
                    {
                        // 12. sınıflar mezun: aktif veritabanından kaldırılır (arşivde güvende)
                        await conn.ExecuteAsync("DELETE FROM Students WHERE Id = @Id;", new { Id = sId }, trans);
                        result.GraduatedStudentsCount++;
                        continue;
                    }

                    string newGrade;
                    string newClassroom = PromoteClassName(classroom);
                    string educationPlace = (string?)s.EducationPlace ?? "İşletmede (Staj)";
                    int? newEnterpriseId = (int?)s.EnterpriseId;
                    string? newEnterpriseName = (string?)s.EnterpriseName;
                    int? newTeacherId = (int?)s.TeacherId;
                    string? newTeacherName = (string?)s.TeacherName;

                    if (is11th)
                    {
                        newGrade = "12";
                        educationPlace = "İşletmede (Staj)";

                        if (options.ResetStudentEnterprisesForPromoted)
                        {
                            newEnterpriseId = null;
                            newEnterpriseName = null;
                            newTeacherId = null;
                            newTeacherName = null;
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

                    await conn.ExecuteAsync(@"
                        UPDATE Students 
                        SET Grade = @Grade, Classroom = @Classroom, EducationPlace = @EducationPlace,
                            EnterpriseId = @EnterpriseId, EnterpriseName = @EnterpriseName,
                            TeacherId = @TeacherId, TeacherName = @TeacherName
                        WHERE Id = @Id;",
                        new
                        {
                            Grade = newGrade,
                            Classroom = newClassroom,
                            EducationPlace = educationPlace,
                            EnterpriseId = newEnterpriseId,
                            EnterpriseName = newEnterpriseName,
                            TeacherId = newTeacherId,
                            TeacherName = newTeacherName,
                            Id = sId
                        }, trans);

                    result.PromotedStudentsCount++;
                }
            }

            // C. Yıllık Değişen Operasyonel Verileri Sıfırla (Devamsızlık, Puantaj vb.)
            if (options.ResetAttendancesAndPayrolls)
            {
                progress?.Report("Devamsızlık ve puantaj çizelgeleri yeni yıl için sıfırlanıyor...");
                await conn.ExecuteAsync("DELETE FROM Attendances;", null, trans);
                await conn.ExecuteAsync("DELETE FROM MonthlyPayrolls;", null, trans);
                await conn.ExecuteAsync("DELETE FROM CoordinatorVisits;", null, trans);
                await conn.ExecuteAsync("DELETE FROM DocumentTrackings;", null, trans);
                await conn.ExecuteAsync("DELETE FROM CoordinatorReports;", null, trans);
                await conn.ExecuteAsync("DELETE FROM InsuranceTransactions;", null, trans);
                await conn.ExecuteAsync("DELETE FROM InsuranceMonthlySummary;", null, trans);
            }

            // D. Akademik Yıllar Tablosunu Güncelle
            await conn.ExecuteAsync("UPDATE AcademicYears SET IsActive = 0;", null, trans);
            var existsInYears = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM AcademicYears WHERE Name = @Name;", new { Name = options.TargetYear }, trans);

            if (existsInYears == 0)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO AcademicYears (Name, Term, StartDate, EndDate, IsActive)
                    VALUES (@Name, 1, @StartDate, @EndDate, 1);",
                    new
                    {
                        Name = options.TargetYear,
                        StartDate = targetStartDateStr,
                        EndDate = targetEndDateStr
                    }, trans);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE AcademicYears 
                    SET IsActive = 1, StartDate = @StartDate, EndDate = @EndDate 
                    WHERE Name = @Name;",
                    new
                    {
                        Name = options.TargetYear,
                        StartDate = targetStartDateStr,
                        EndDate = targetEndDateStr
                    }, trans);
            }

            // E. Alanlar, İşletmeler, Öğretmenler Durumu
            if (!options.CopyTeachers)
            {
                await conn.ExecuteAsync("UPDATE Teachers SET IsActive = 0;", null, trans);
            }
            else
            {
                result.CopiedTeachersCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Teachers WHERE IsActive = 1;", null, trans);
            }

            if (!options.CopyEnterprises)
            {
                await conn.ExecuteAsync("UPDATE Enterprises SET IsActive = 0;", null, trans);
            }
            else
            {
                result.CopiedEnterprisesCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Enterprises WHERE IsActive = 1;", null, trans);
            }

            // İşlemleri kalıcı olarak onayla
            trans.Commit();

            // Aktif yılı güncelle
            _dbConfig.SwitchToYear(options.TargetYear, false);

            progress?.Report("Devir işlemi başarıyla tamamlandı.");

            result.Success = true;
            result.Message = $"🎉 {options.TargetYear} Eğitim-Öğretim Yılı başarıyla başlatıldı!\n\n" +
                             $"• '{options.SourceYear}' yılı verileri '{Path.GetFileName(archiveDbPath)}' olarak arşivlendi.\n" +
                             $"• {result.PromotedStudentsCount} öğrenci bir üst sınıfa terfi ettirildi.\n" +
                             $"• {result.GraduatedStudentsCount} 12. sınıf öğrencisi mezun olarak eski yıl arşivinde korundu.\n" +
                             $"• {result.PromotedClassesCount} sınıf şubesi güncellendi.\n" +
                             $"• {result.CopiedTeachersCount} koordinatör öğretmen ve {result.CopiedEnterprisesCount} işletme aktarıldı.\n" +
                             $"• Devamsızlık ve puantaj çizelgeleri yeni yıl için tertemiz sıfırlandı.\n" +
                             $"• Sistem tek ve aktif 'meb_koordinatorluk.db' üzerinden kesintisiz çalışmaya devam ediyor.";

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
