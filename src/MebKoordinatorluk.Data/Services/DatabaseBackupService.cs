using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;
using Microsoft.Data.Sqlite;

namespace MebKoordinatorluk.Data.Services;

public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly DatabaseConfig _config;

    public DatabaseBackupService(ISqliteConnectionFactory connectionFactory, DatabaseConfig config)
    {
        _connectionFactory = connectionFactory;
        _config = config;
    }

    public string GetCurrentDatabasePath() => _config.DatabasePath;

    public string GetBackupDirectory()
    {
        var dbDir = Path.GetDirectoryName(_config.DatabasePath);
        if (string.IsNullOrWhiteSpace(dbDir))
        {
            dbDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        }
        var backupDir = Path.Combine(dbDir, "Yedekler");
        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }
        return backupDir;
    }

    public async Task<string> CreateBackupAsync(string? customFileName = null, string? destinationFolder = null)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(_config.DatabasePath))
            {
                throw new FileNotFoundException("Veritabanı dosyası bulunamadı!", _config.DatabasePath);
            }

            // 1. WAL Günlüklerini Diske Yaz (PASSIVE: asla diğer işlemleri beklemez veya kilitlemez)
            try
            {
                using var conn = (SqliteConnection)_connectionFactory.CreateConnection();
                conn.Execute("PRAGMA wal_checkpoint(PASSIVE);");
            }
            catch
            {
                // Bağlantı alınamazsa bile dosya kopyalamayı dene
            }

            var targetDir = !string.IsNullOrWhiteSpace(destinationFolder) ? destinationFolder : GetBackupDirectory();
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var cleanYear = DatabaseConfig.SanitizeYear(_config.CurrentAcademicYear);
            var fileName = !string.IsNullOrWhiteSpace(customFileName)
                ? customFileName
                : $"meb_{cleanYear}_{DateTime.Now:yyyy-MM-dd_HHmm}.bak";

            if (!fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".bak";
            }

            var destPath = Path.Combine(targetDir, fileName);

            // 2. SQLite Online Backup API ile güvenli, canlı ve kilitlenmeyen anlık görüntü
            try
            {
                using var sourceConn = new SqliteConnection(_config.ConnectionString);
                sourceConn.Open();
                using var destConn = new SqliteConnection($"Data Source={destPath};");
                destConn.Open();
                sourceConn.BackupDatabase(destConn);
            }
            catch
            {
                File.Copy(_config.DatabasePath, destPath, overwrite: true);
            }

            // 3. Döngüsel Saklama (Varsayılan klasörde 30 günden eski yedekleri temizle)
            if (string.Equals(targetDir, GetBackupDirectory(), StringComparison.OrdinalIgnoreCase))
            {
                CleanOldBackups(targetDir, retentionDays: 30);
            }

            return destPath;
        });
    }

    public List<BackupFileInfo> GetExistingBackups()
    {
        var backupDir = GetBackupDirectory();
        if (!Directory.Exists(backupDir)) return new List<BackupFileInfo>();

        var dirInfo = new DirectoryInfo(backupDir);
        return dirInfo.GetFiles("*.bak")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new BackupFileInfo(f.Name, f.FullName, f.Length, f.LastWriteTime))
            .ToList();
    }

    public async Task<bool> RestoreBackupAsync(string backupFilePath)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException("Geri yüklenecek yedek dosyası bulunamadı!", backupFilePath);
            }

            // 0. Önce doğrudan seçilen yedek dosyasının bütünlüğünü test et (mevcut aktif veritabanına dokunmadan!)
            try
            {
                using var checkConn = new SqliteConnection($"Data Source={backupFilePath};Mode=ReadOnly;");
                checkConn.Open();
                var checkRes = checkConn.ExecuteScalar<string>("PRAGMA quick_check;");
                if (!string.Equals(checkRes, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            // 1. Tüm açık SQLite bağlantı havuzlarını serbest bırak
            SqliteConnection.ClearAllPools();

            // 2. Mevcut veritabanının güvenlik yedeğini al (Hata durumunda geri dönebilmek için)
            string? safetyPath = null;
            if (File.Exists(_config.DatabasePath))
            {
                var safetyDir = GetBackupDirectory();
                var activeDbName = Path.GetFileNameWithoutExtension(_config.DatabasePath);
                safetyPath = Path.Combine(safetyDir, $"{activeDbName}_pre_restore_safety.bak");
                try
                {
                    File.Copy(_config.DatabasePath, safetyPath, overwrite: true);
                }
                catch { }
            }

            // 3. Mevcut WAL ve SHM geçici dosyalarını temizle (artık sayfa çakışmasını önlemek için)
            var walFile = _config.DatabasePath + "-wal";
            var shmFile = _config.DatabasePath + "-shm";
            if (File.Exists(walFile)) { try { File.Delete(walFile); } catch { } }
            if (File.Exists(shmFile)) { try { File.Delete(shmFile); } catch { } }

            // 4. Yedek dosyasını ana veritabanı dosyasının üzerine yaz
            File.Copy(backupFilePath, _config.DatabasePath, overwrite: true);

            // 5. Geri yüklenen dosyanın sağlamlığını doğrula
            try
            {
                using var conn = new SqliteConnection(_config.ConnectionString);
                conn.Open();
                var result = conn.ExecuteScalar<string>("PRAGMA quick_check;");
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    if (safetyPath != null && File.Exists(safetyPath))
                    {
                        try { File.Copy(safetyPath, _config.DatabasePath, overwrite: true); } catch { }
                    }
                    return false;
                }

                // 6. Geri yüklenen veritabanında lisans koruması:
                // Eğer geri yüklenen yedek lisanslanmadan önceki bir tarihe aitse (Licenses tablosu boşsa),
                // bu makineye ait kalıcı lisans anahtarını geri yüklenen veritabanına otomatik aktar.
                try
                {
                    var licCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Licenses WHERE Status = 1;");
                    if (licCount == 0)
                    {
                        var machineKey = LicenseService.GetLicenseKeyFromMachineStorage();
                        if (!string.IsNullOrWhiteSpace(machineKey))
                        {
                            var hwid = Core.Security.HardwareIdentifier.GetMachineCode();
                            var verified = LicenseService.VerifyLicenseKeyInternal(machineKey, hwid);
                            if (verified.Success && verified.Payload != null && DateTime.Today <= verified.Payload.EndDate.Date)
                            {
                                var nowUtc = DateTime.UtcNow.ToString("o");
                                conn.Execute(@"
                                    DELETE FROM Licenses;
                                    INSERT INTO Licenses (MachineCode, SchoolName, CityDistrict, StartDate, EndDate, LicenseKey, ActivatedAt, LastVerifiedAt, Status, Note)
                                    VALUES (@MachineCode, @SchoolName, @CityDistrict, @StartDate, @EndDate, @LicenseKey, @ActivatedAt, @LastVerifiedAt, 1, @Note);
                                ", new
                                {
                                    MachineCode = hwid,
                                    SchoolName = verified.Payload.SchoolName,
                                    CityDistrict = verified.Payload.CityDistrict,
                                    StartDate = verified.Payload.StartDate.ToString("yyyy-MM-dd"),
                                    EndDate = verified.Payload.EndDate.ToString("yyyy-MM-dd"),
                                    LicenseKey = machineKey,
                                    ActivatedAt = nowUtc,
                                    LastVerifiedAt = nowUtc,
                                    Note = "Yedek Geri Yükleme Sonrası Otomatik Kurtarılan Lisans"
                                });
                            }
                        }
                    }
                }
                catch { }

                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public void PerformAutoBackupOnExit()
    {
        try
        {
            if (!File.Exists(_config.DatabasePath)) return;

            var backupDir = GetBackupDirectory();
            var cleanYear = DatabaseConfig.SanitizeYear(_config.CurrentAcademicYear);
            var todayBackupName = $"meb_{cleanYear}_{DateTime.Today:yyyy-MM-dd}_gunluk.bak";
            var destPath = Path.Combine(backupDir, todayBackupName);

            // Son 30 dakika içinde zaten günlük yedek alınmışsa tekrar beklemeden anında çık
            if (File.Exists(destPath))
            {
                var fi = new FileInfo(destPath);
                if (DateTime.Now - fi.LastWriteTime < TimeSpan.FromMinutes(30))
                {
                    return;
                }
            }

            // Hızlı, canlı ve kilitlenmeyen SQLite Backup
            using var sourceConn = new SqliteConnection(_config.ConnectionString);
            sourceConn.Open();
            using var destConn = new SqliteConnection($"Data Source={destPath};");
            destConn.Open();
            sourceConn.BackupDatabase(destConn);

            CleanOldBackups(backupDir, retentionDays: 30);
        }
        catch
        {
            // Kapanışta sessiz hata yakalama (asla kullanıcıyı engellemez veya dondurmaz)
        }
    }

    public async Task PerformAutoBackupOnExitAsync()
    {
        await Task.Run(PerformAutoBackupOnExit);
    }

    public async Task<string?> TryAutoRecoverCorruptedDatabaseAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var backups = GetExistingBackups();
                foreach (var b in backups)
                {
                    // En yeni yedekten başlayarak sağlam olan ilk yedeği ara
                    try
                    {
                        var testConnStr = $"Data Source={b.FullPath};Mode=ReadOnly;";
                        using var conn = new SqliteConnection(testConnStr);
                        conn.Open();
                        var check = conn.ExecuteScalar<string>("PRAGMA quick_check;");
                        if (string.Equals(check, "ok", StringComparison.OrdinalIgnoreCase))
                        {
                            // Sağlam yedek bulundu, geri yükle
                            SqliteConnection.ClearAllPools();
                            var walFile = _config.DatabasePath + "-wal";
                            var shmFile = _config.DatabasePath + "-shm";
                            if (File.Exists(walFile)) { try { File.Delete(walFile); } catch { } }
                            if (File.Exists(shmFile)) { try { File.Delete(shmFile); } catch { } }

                            File.Copy(b.FullPath, _config.DatabasePath, overwrite: true);
                            return b.FileName;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch { }

            return null;
        });
    }

    private static void CleanOldBackups(string dir, int retentionDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            var dirInfo = new DirectoryInfo(dir);
            foreach (var f in dirInfo.GetFiles("*.bak"))
            {
                // Güvenlik yedeklerini hemen silme, normal tarihli yedekleri temizle
                if (f.LastWriteTime < cutoff && !f.Name.Contains("safety", StringComparison.OrdinalIgnoreCase))
                {
                    try { f.Delete(); } catch { }
                }
            }
        }
        catch { }
    }

    public async Task<string> ResetDatabaseAsync(bool keepUsersAndSettings = true)
    {
        // 1. Önce otomatik güvenlik yedeği al
        var activeDbName = Path.GetFileNameWithoutExtension(_config.DatabasePath);
        var backupPath = await CreateBackupAsync($"{activeDbName}_pre_reset_safety_{DateTime.Now:yyyy-MM-dd_HHmmss}.bak");

        await Task.Run(() =>
        {
            using var conn = (SqliteConnection)_connectionFactory.CreateConnection();
            conn.Open();

            // Yabancı anahtar denetimini geçici olarak kapat
            try
            {
                conn.Execute("PRAGMA foreign_keys = OFF;");
            }
            catch { }

            using var tx = conn.BeginTransaction();

            // Çocuk tablolar / İşlem ve Hareket Tabloları
            conn.Execute("DELETE FROM FeeReceipts;", transaction: tx);
            conn.Execute("DELETE FROM PayrollItems;", transaction: tx);
            conn.Execute("DELETE FROM MonthlyPayrolls;", transaction: tx);
            conn.Execute("DELETE FROM CoordinationVisits;", transaction: tx);
            conn.Execute("DELETE FROM CoordinatorAssignments;", transaction: tx);
            conn.Execute("DELETE FROM StudentPlacements;", transaction: tx);
            conn.Execute("DELETE FROM DocumentTrackingRecords;", transaction: tx);
            conn.Execute("DELETE FROM AttendanceEntries;", transaction: tx);

            // Ana Tanım ve Varlık Tabloları
            conn.Execute("DELETE FROM Students;", transaction: tx);
            conn.Execute("DELETE FROM Enterprises;", transaction: tx);
            conn.Execute("DELETE FROM Teachers;", transaction: tx);
            conn.Execute("DELETE FROM AddressGroups;", transaction: tx);
            conn.Execute("DELETE FROM SchoolClasses;", transaction: tx);
            conn.Execute("DELETE FROM Fields;", transaction: tx);
            conn.Execute("DELETE FROM Departments;", transaction: tx);
            conn.Execute("DELETE FROM AcademicYears;", transaction: tx);

            if (!keepUsersAndSettings)
            {
                conn.Execute("DELETE FROM YearlyMinimumWages;", transaction: tx);
                conn.Execute("DELETE FROM Settings;", transaction: tx);
                conn.Execute("DELETE FROM Users;", transaction: tx);

                // Varsayılan yönetici hesabını yeniden ekle (admin / admin123)
                conn.Execute(@"
                    INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                    VALUES ('admin', 'admin123', 'Sistem Yöneticisi', 'Admin', 1, CURRENT_TIMESTAMP);
                ", transaction: tx);

                // Temel varsayılan kurum ayarını ekle
                conn.Execute(@"
                    INSERT INTO Settings (Key, Value, Description, UpdatedDate) VALUES
                    ('Valilik', 'İSTANBUL VALİLİĞİ', 'İl Valiliği / Kaymakamlık', CURRENT_TIMESTAMP),
                    ('DistrictMem', 'İlçe Millî Eğitim Müdürlüğü', 'İl / İlçe Millî Eğitim Müdürlüğü', CURRENT_TIMESTAMP),
                    ('SchoolName', 'MESLEKİ VE TEKNİK ANADOLU LİSESİ', 'Okul / Kurum Resmi Adı', CURRENT_TIMESTAMP),
                    ('AcademicYear', '2025-2026', 'Eğitim Öğretim Yılı', CURRENT_TIMESTAMP),
                    ('Term', '1.', 'Dönem', CURRENT_TIMESTAMP);
                ", transaction: tx);
            }

            // AUTOINCREMENT sayaçlarını sıfırla
            if (keepUsersAndSettings)
            {
                conn.Execute(@"
                    DELETE FROM sqlite_sequence 
                    WHERE name NOT IN ('Users', 'Settings', 'YearlyMinimumWages');
                ", transaction: tx);
            }
            else
            {
                conn.Execute("DELETE FROM sqlite_sequence;", transaction: tx);
            }

            tx.Commit();

            try
            {
                conn.Execute("PRAGMA foreign_keys = ON;");
            }
            catch { }
        });

        // Veritabanını küçült (VACUUM)
        try
        {
            using var vacConn = (SqliteConnection)_connectionFactory.CreateConnection();
            vacConn.Open();
            vacConn.Execute("VACUUM;");
        }
        catch { }

        return backupPath;
    }
}
