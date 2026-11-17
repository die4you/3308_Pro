using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.Data.Database;
using Microsoft.Win32;

namespace MebKoordinatorluk.Data.Services;

public class LicenseService : ILicenseService
{
    private const string RsaPublicKeyXml = @"<RSAKeyValue><Modulus>zuEkm1pcrkGl23cjvm6JwasDvQLWpBYCYyW+SjzlEdfhNbfst0Hhln1uWlAsj87c71K+VeGkW387naM5UYuLJ8xVmEV3+W5Z7SaT4EpGIY4U495wQhmTNMJ6VgaDWCpBCgAS+c/tHLLrhmFfZ9J8rjROsu6TYzLXNkm/HGupqR1ccsjhqs1Wbccv2aHBVHvuRD8v8hQIHkzIOrCuPsCO7S2lEk3yfZn7WoZ4QXUzIAgSz2tNeAfpH1lLceIZmEDYMCoY/Y7V87qyljwxV29Nvj4YklKQJubqKKb6BVuilC+FmvfCE8gQXmvYNea7eyVVQ+wARJVcHesL97iYmCulxQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    private const string RegistrySubKey = @"Software\3308Pro\License";

    private readonly ISqliteConnectionFactory _connectionFactory;
    private LicenseInfo _cachedLicense;

    public string MachineCode => HardwareIdentifier.GetMachineCode();
    public LicenseInfo CurrentLicense => _cachedLicense;

    public LicenseService(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        _cachedLicense = new LicenseInfo
        {
            MachineCode = MachineCode,
            Status = LicenseStatus.Demo,
            Note = "Lisanssız Deneme Sürümü"
        };
    }

    #region Kalıcı Makine Düzeyi Lisans Depolama (Registry + AppData + ProgramData + Data)

    /// <summary>
    /// Lisans anahtarını Windows Kayıt Defteri (HKCU), LocalAppData, ProgramData ve Data klasörüne kaydeder.
    /// Böylece program yeniden kurulsa, veritabanı silinse veya eski bir yedek geri yüklense dahi lisans kaybolmaz.
    /// </summary>
    public static void SaveLicenseToMachineStorage(string licenseKey, string machineCode, string schoolName = "")
    {
        if (string.IsNullOrWhiteSpace(licenseKey)) return;
        var cleanKey = licenseKey.Trim();

        // 1. Windows Kayıt Defteri (HKCU - Kullanıcı yetkisi yeterlidir, yönetici izni gerektirmez)
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
                key?.SetValue("LicenseKey", cleanKey);
                key?.SetValue("MachineCode", machineCode);
                key?.SetValue("SchoolName", schoolName ?? string.Empty);
                key?.SetValue("SavedAt", DateTime.UtcNow.ToString("o"));
            }
            catch { }
        }

        // 2. LocalAppData (%LOCALAPPDATA%\3308Pro\license.lic)
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                var dir = Path.Combine(localAppData, "3308Pro");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "license.lic"), cleanKey, Encoding.UTF8);
            }
        }
        catch { }

        // 3. ProgramData (%PROGRAMDATA%\3308Pro\license.lic - Ortak makine klasörü)
        try
        {
            var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(commonAppData))
            {
                var dir = Path.Combine(commonAppData, "3308Pro");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "license.lic"), cleanKey, Encoding.UTF8);
            }
        }
        catch { }

        // 4. Data klasörü (Uygulama dizini altındaki kalıcı license.lic)
        try
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "license.lic"), cleanKey, Encoding.UTF8);
        }
        catch { }
    }

    /// <summary>
    /// Kalıcı depolardan (Registry, LocalAppData, ProgramData veya Data\license.lic) makineye ait lisans anahtarını okur.
    /// </summary>
    public static string? GetLicenseKeyFromMachineStorage()
    {
        // 1. Windows Registry
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
                var val = key?.GetValue("LicenseKey")?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
            catch { }
        }

        // 2. LocalAppData
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                var p = Path.Combine(localAppData, "3308Pro", "license.lic");
                if (File.Exists(p))
                {
                    var text = File.ReadAllText(p, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
        }
        catch { }

        // 3. ProgramData
        try
        {
            var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(commonAppData))
            {
                var p = Path.Combine(commonAppData, "3308Pro", "license.lic");
                if (File.Exists(p))
                {
                    var text = File.ReadAllText(p, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
        }
        catch { }

        // 4. Uygulama Data klasörü (Data\license.lic)
        try
        {
            var p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "license.lic");
            if (File.Exists(p))
            {
                var text = File.ReadAllText(p, Encoding.UTF8).Trim();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Lisans iptal edildiğinde tüm kalıcı depoları temizler.
    /// </summary>
    public static void ClearMachineLicenseStorage()
    {
        if (OperatingSystem.IsWindows())
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(RegistrySubKey, false); } catch { }
        }
        try
        {
            var p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "3308Pro", "license.lic");
            if (File.Exists(p1)) File.Delete(p1);
        } catch { }
        try
        {
            var p2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "3308Pro", "license.lic");
            if (File.Exists(p2)) File.Delete(p2);
        } catch { }
        try
        {
            var p3 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "license.lic");
            if (File.Exists(p3)) File.Delete(p3);
        } catch { }
    }

    #endregion

    public async Task<LicenseInfo> RefreshLicenseStatusAsync()
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT * FROM Licenses ORDER BY Id DESC LIMIT 1");

            // Eğer veritabanında lisans yoksa veya boşsa, makinenin kalıcı lisans deposuna (Registry / AppData) bak:
            if (row == null || string.IsNullOrWhiteSpace((string?)row.LicenseKey))
            {
                var machineKey = GetLicenseKeyFromMachineStorage();
                if (!string.IsNullOrWhiteSpace(machineKey))
                {
                    var autoVerify = VerifyLicenseKeyInternal(machineKey, MachineCode);
                    if (autoVerify.Success && autoVerify.Payload != null && DateTime.Today <= autoVerify.Payload.EndDate.Date)
                    {
                        // Makineye ait geçerli lisans bulundu! Otomatik olarak geçerli SQLite veritabanına aktar:
                        var nowUtc = DateTime.UtcNow.ToString("o");
                        await conn.ExecuteAsync(@"
                            DELETE FROM Licenses;
                            INSERT INTO Licenses (MachineCode, SchoolName, CityDistrict, StartDate, EndDate, LicenseKey, ActivatedAt, LastVerifiedAt, Status, Note)
                            VALUES (@MachineCode, @SchoolName, @CityDistrict, @StartDate, @EndDate, @LicenseKey, @ActivatedAt, @LastVerifiedAt, @Status, @Note);
                        ", new
                        {
                            MachineCode = MachineCode,
                            SchoolName = autoVerify.Payload.SchoolName,
                            CityDistrict = autoVerify.Payload.CityDistrict,
                            StartDate = autoVerify.Payload.StartDate.ToString("yyyy-MM-dd"),
                            EndDate = autoVerify.Payload.EndDate.ToString("yyyy-MM-dd"),
                            LicenseKey = machineKey,
                            ActivatedAt = nowUtc,
                            LastVerifiedAt = nowUtc,
                            Status = (int)LicenseStatus.Active,
                            Note = autoVerify.Payload.Note ?? "Otomatik Kurtarılan Kalıcı Makine Lisansı"
                        });

                        // Tekrar oku
                        row = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT * FROM Licenses ORDER BY Id DESC LIMIT 1");
                    }
                }
            }

            if (row == null)
            {
                _cachedLicense = new LicenseInfo
                {
                    MachineCode = MachineCode,
                    Status = LicenseStatus.Demo,
                    Note = "Lisanssız Deneme Sürümü (En fazla 5 öğrenci / 3 işletme)"
                };
                return _cachedLicense;
            }

            var lic = new LicenseInfo
            {
                Id = (int)row.Id,
                MachineCode = (string)row.MachineCode,
                SchoolName = (string?)row.SchoolName ?? string.Empty,
                CityDistrict = (string?)row.CityDistrict ?? string.Empty,
                LicenseKey = (string?)row.LicenseKey ?? string.Empty,
                Note = (string?)row.Note
            };

            if (DateTime.TryParse((string?)row.StartDate, out var sDate)) lic.StartDate = sDate;
            if (DateTime.TryParse((string?)row.EndDate, out var eDate)) lic.EndDate = eDate;
            if (DateTime.TryParse((string?)row.ActivatedAt, out var aDate)) lic.ActivatedAt = aDate;
            if (DateTime.TryParse((string?)row.LastVerifiedAt, out var lvDate)) lic.LastVerifiedAt = lvDate;

            // Lisans anahtarını doğrula
            if (!string.IsNullOrWhiteSpace(lic.LicenseKey))
            {
                var verifyResult = VerifyLicenseKeyInternal(lic.LicenseKey, MachineCode);
                if (verifyResult.Success && verifyResult.Payload != null)
                {
                    // Tarih hilesi kontrolü: Sistem saati veritabanındaki son doğrulamadan 24 saatten fazla geride mi?
                    if (DateTime.UtcNow < lic.LastVerifiedAt.AddHours(-24))
                    {
                        lic.Status = LicenseStatus.TamperedClock;
                        lic.Note = "⚠️ Sistem saati geriye alınmış tespit edildi! Lisans askıya alındı.";
                    }
                    else if (DateTime.Today > verifyResult.Payload.EndDate.Date)
                    {
                        lic.Status = LicenseStatus.Expired;
                        lic.Note = $"Lisans süresi {verifyResult.Payload.EndDate:dd.MM.yyyy} tarihinde sona erdi.";
                    }
                    else
                    {
                        lic.Status = LicenseStatus.Active;
                        lic.SchoolName = verifyResult.Payload.SchoolName;
                        lic.CityDistrict = verifyResult.Payload.CityDistrict;
                        lic.StartDate = verifyResult.Payload.StartDate;
                        lic.EndDate = verifyResult.Payload.EndDate;

                        // Kalıcı depoyu da daima güncel tut
                        SaveLicenseToMachineStorage(lic.LicenseKey, MachineCode, lic.SchoolName);
                    }
                }
                else
                {
                    lic.Status = LicenseStatus.Invalid;
                    lic.Note = verifyResult.ErrorMessage ?? "Geçersiz Lisans Anahtarı";
                }
            }
            else
            {
                lic.Status = LicenseStatus.Demo;
                lic.Note = "Lisanssız Deneme Sürümü";
            }

            // Son doğrulama zamanını güncelle
            lic.LastVerifiedAt = DateTime.UtcNow;
            await conn.ExecuteAsync(
                "UPDATE Licenses SET LastVerifiedAt = @LastVerifiedAt, Status = @Status WHERE Id = @Id",
                new { LastVerifiedAt = lic.LastVerifiedAt.ToString("o"), Status = (int)lic.Status, Id = lic.Id });

            _cachedLicense = lic;
            return _cachedLicense;
        }
        catch
        {
            _cachedLicense = new LicenseInfo
            {
                MachineCode = MachineCode,
                Status = LicenseStatus.Demo,
                Note = "Lisans durumu sorgulanamadı (Demo modu aktif)"
            };
            return _cachedLicense;
        }
    }

    public async Task<(bool Success, string Message)> ApplyLicenseKeyAsync(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return (false, "Lütfen bir lisans anahtarı giriniz.");

        licenseKey = licenseKey.Trim();
        var verifyResult = VerifyLicenseKeyInternal(licenseKey, MachineCode);

        if (!verifyResult.Success || verifyResult.Payload == null)
            return (false, verifyResult.ErrorMessage ?? "Lisans anahtarı doğrulanamadı.");

        var payload = verifyResult.Payload;

        if (DateTime.Today > payload.EndDate.Date)
            return (false, $"Bu lisansın kullanım süresi ({payload.EndDate:dd.MM.yyyy}) geçmiş.");

        try
        {
            using var conn = _connectionFactory.CreateConnection();
            var nowUtc = DateTime.UtcNow.ToString("o");

            // Licenses tablosunu güncelle veya ekle
            await conn.ExecuteAsync(@"
                DELETE FROM Licenses;
                INSERT INTO Licenses (MachineCode, SchoolName, CityDistrict, StartDate, EndDate, LicenseKey, ActivatedAt, LastVerifiedAt, Status, Note)
                VALUES (@MachineCode, @SchoolName, @CityDistrict, @StartDate, @EndDate, @LicenseKey, @ActivatedAt, @LastVerifiedAt, @Status, @Note);
            ", new
            {
                MachineCode = MachineCode,
                SchoolName = payload.SchoolName,
                CityDistrict = payload.CityDistrict,
                StartDate = payload.StartDate.ToString("yyyy-MM-dd"),
                EndDate = payload.EndDate.ToString("yyyy-MM-dd"),
                LicenseKey = licenseKey,
                ActivatedAt = nowUtc,
                LastVerifiedAt = nowUtc,
                Status = (int)LicenseStatus.Active,
                Note = payload.Note ?? "Aktif Lisans"
            });

            // Lisans geçmişine kaydet
            await conn.ExecuteAsync(@"
                INSERT INTO LicenseHistory (MachineCode, SchoolName, LicenseKey, StartDate, EndDate, ActivatedAt, Status, Note)
                VALUES (@MachineCode, @SchoolName, @LicenseKey, @StartDate, @EndDate, @ActivatedAt, @Status, @Note);
            ", new
            {
                MachineCode = MachineCode,
                SchoolName = payload.SchoolName,
                LicenseKey = licenseKey,
                StartDate = payload.StartDate.ToString("yyyy-MM-dd"),
                EndDate = payload.EndDate.ToString("yyyy-MM-dd"),
                ActivatedAt = nowUtc,
                Status = "Aktif",
                Note = $"Bitiş: {payload.EndDate:dd.MM.yyyy}"
            });

            // Kalıcı makine depolarına (Registry, AppData, ProgramData, Data/license.lic) kaydet
            SaveLicenseToMachineStorage(licenseKey, MachineCode, payload.SchoolName);

            await RefreshLicenseStatusAsync();
            return (true, $"Tebrikler! Yazılımınız {payload.EndDate:dd.MM.yyyy} tarihine kadar başarıyla lisanslandı.");
        }
        catch (Exception ex)
        {
            return (false, "Lisans kaydedilirken veritabanı hatası oluştu: " + ex.Message);
        }
    }

    public async Task RevokeLicenseToDemoAsync(string reason)
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            await conn.ExecuteAsync("DELETE FROM Licenses;");

            var nowUtc = DateTime.UtcNow.ToString("o");
            await conn.ExecuteAsync(@"
                INSERT INTO LicenseHistory (MachineCode, SchoolName, LicenseKey, StartDate, EndDate, ActivatedAt, Status, Note)
                VALUES (@MachineCode, @SchoolName, '', @StartDate, @EndDate, @ActivatedAt, 'İptal / Demo', @Note);
            ", new
            {
                MachineCode = MachineCode,
                SchoolName = _cachedLicense.SchoolName,
                StartDate = DateTime.Today.ToString("yyyy-MM-dd"),
                EndDate = DateTime.Today.ToString("yyyy-MM-dd"),
                ActivatedAt = nowUtc,
                Note = reason
            });

            ClearMachineLicenseStorage();

            _cachedLicense = new LicenseInfo
            {
                MachineCode = MachineCode,
                Status = LicenseStatus.Demo,
                Note = reason
            };
        }
        catch { }
    }

    public bool CanAddStudent(int currentCount)
    {
        if (_cachedLicense.IsActive) return true;
        return currentCount < 5; // Demo modunda maksimum 5 öğrenci
    }

    public bool CanAddEnterprise(int currentCount)
    {
        if (_cachedLicense.IsActive) return true;
        return currentCount < 3; // Demo modunda maksimum 3 işletme
    }

    public bool CanAddTeacher(int currentCount)
    {
        if (_cachedLicense.IsActive) return true;
        return currentCount < 1; // Demo modunda maksimum 1 öğretmen
    }

    public string GetWatermarkText()
    {
        if (_cachedLicense.IsActive) return string.Empty;
        return "3308 Pro - Lisanssız Deneme Sürümü (Demo)";
    }

    public async Task<List<LicenseHistory>> GetLicenseHistoryAsync()
    {
        try
        {
            using var conn = _connectionFactory.CreateConnection();
            var rows = await conn.QueryAsync<dynamic>("SELECT * FROM LicenseHistory ORDER BY Id DESC");
            var list = new List<LicenseHistory>();

            foreach (var r in rows)
            {
                var h = new LicenseHistory
                {
                    Id = (int)r.Id,
                    MachineCode = (string)r.MachineCode,
                    SchoolName = (string?)r.SchoolName ?? string.Empty,
                    LicenseKey = (string)r.LicenseKey,
                    Status = (string)r.Status,
                    Note = (string?)r.Note
                };
                if (DateTime.TryParse((string?)r.StartDate, out var s)) h.StartDate = s;
                if (DateTime.TryParse((string?)r.EndDate, out var e)) h.EndDate = e;
                if (DateTime.TryParse((string?)r.ActivatedAt, out var a)) h.ActivatedAt = a;
                list.Add(h);
            }
            return list;
        }
        catch
        {
            return [];
        }
    }

    public AcademicYearAccessStatus GetAcademicYearAccess(AcademicYear year)
    {
        return GetAcademicYearAccess(year.Name, year.StartDate, year.EndDate);
    }

    public AcademicYearAccessStatus GetAcademicYearAccess(string yearName, DateTime? startDate = null, DateTime? endDate = null)
    {
        var today = DateTime.Today;
        DateTime effectiveStart = startDate ?? DateTime.MinValue;
        DateTime effectiveEnd = endDate ?? DateTime.MinValue;

        if (effectiveStart == DateTime.MinValue || effectiveEnd == DateTime.MinValue)
        {
            var parsed = ParseYearDates(yearName);
            if (effectiveStart == DateTime.MinValue) effectiveStart = parsed.StartDate;
            if (effectiveEnd == DateTime.MinValue) effectiveEnd = parsed.EndDate;
        }

        // 1. Geçmiş Eğitim Öğretim Yılı:
        // Eğer öğretim yılı bitmişse (effectiveEnd < today), kullanıcı eski kayıtlarına daima erişebilir -> Arşiv Salt Okunur
        if (effectiveEnd.Date < today)
        {
            return AcademicYearAccessStatus.ArchiveReadOnly;
        }

        // 2. Gelecek Eğitim Öğretim Yılı:
        // Eğer öğretim yılı henüz başlamamışsa (effectiveStart > today)
        if (effectiveStart.Date > today)
        {
            // Lisans aktif değilse veya lisans bitiş tarihi bu yılın başlangıcını kapsamıyorsa -> Kilitli
            if (!_cachedLicense.IsActive || !_cachedLicense.EndDate.HasValue || _cachedLicense.EndDate.Value.Date < effectiveStart.Date)
            {
                return AcademicYearAccessStatus.FutureLocked;
            }
            return AcademicYearAccessStatus.ActiveFull;
        }

        // 3. Mevcut / Aktif Eğitim Öğretim Yılı:
        if (!_cachedLicense.IsActive)
        {
            return AcademicYearAccessStatus.DemoAccess;
        }

        return AcademicYearAccessStatus.ActiveFull;
    }

    public static (DateTime StartDate, DateTime EndDate) ParseYearDates(string yearName)
    {
        try
        {
            var parts = yearName.Split('-', '/', '_');
            if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out int y1) && int.TryParse(parts[1].Trim(), out int y2))
            {
                return (new DateTime(y1, 9, 1), new DateTime(y2, 6, 30));
            }
        }
        catch { }

        var cur = DateTime.Today.Year;
        return (new DateTime(cur, 9, 1), new DateTime(cur + 1, 6, 30));
    }

    // ─────────────────────────────────────────────────────────────
    // RSA ŞİFRE VE İMZA DOĞRULAMA (OFFLINE)
    // ─────────────────────────────────────────────────────────────
    public record LicensePayload(
        string Hwid,
        string SchoolName,
        string CityDistrict,
        DateTime StartDate,
        DateTime EndDate,
        string? Note);

    public record VerifyResult(bool Success, LicensePayload? Payload, string? ErrorMessage);

    public static VerifyResult VerifyLicenseKeyInternal(string licenseKey, string currentHwid)
    {
        try
        {
            if (!licenseKey.StartsWith("LIC-"))
                return new VerifyResult(false, null, "Geçersiz lisans formatı (LIC- ile başlamalıdır).");

            var trimmed = licenseKey[4..];
            var parts = trimmed.Split('.');
            if (parts.Length != 2)
                return new VerifyResult(false, null, "Bozuk lisans anahtarı yapısı.");

            var payloadBytes = Convert.FromBase64String(parts[0].Replace('-', '+').Replace('_', '/').PadRight((parts[0].Length + 3) / 4 * 4, '='));
            var signatureBytes = Convert.FromBase64String(parts[1].Replace('-', '+').Replace('_', '/').PadRight((parts[1].Length + 3) / 4 * 4, '='));

            using var rsa = RSA.Create();
            rsa.FromXmlString(RsaPublicKeyXml);

            bool isValidSignature = rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!isValidSignature)
                return new VerifyResult(false, null, "Lisans anahtarı dijital imzası geçersiz veya sahte!");

            var json = Encoding.UTF8.GetString(payloadBytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string hwid = root.GetProperty("H").GetString() ?? "";
            string school = root.GetProperty("S").GetString() ?? "";
            string city = root.TryGetProperty("C", out var cProp) ? cProp.GetString() ?? "" : "";
            DateTime startDate = DateTime.Parse(root.GetProperty("B").GetString()!);
            DateTime endDate = DateTime.Parse(root.GetProperty("E").GetString()!);
            string? note = root.TryGetProperty("N", out var nProp) ? nProp.GetString() : null;

            if (!string.Equals(hwid.Trim(), currentHwid.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new VerifyResult(false, null, "Bu lisans anahtarı farklı bir bilgisayara (Makine Koduna) aittir!");
            }

            var payload = new LicensePayload(hwid, school, city, startDate, endDate, note);
            return new VerifyResult(true, payload, null);
        }
        catch (Exception ex)
        {
            return new VerifyResult(false, null, "Lisans çözümlenemedi: " + ex.Message);
        }
    }
}
