using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class SettingRepository : ISettingRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public SettingRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Setting>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Setting>("SELECT * FROM Settings ORDER BY Key ASC;");
    }

    public async Task<string?> GetValueAsync(string key)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<string?>(
            "SELECT Value FROM Settings WHERE Key = @Key;", new { Key = key });
    }

    public async Task SetValueAsync(string key, string value, string? description = null)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO Settings (Key, Value, Description, UpdatedDate)
            VALUES (@Key, @Value, @Description, @UpdatedDate)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                Description = COALESCE(excluded.Description, Settings.Description),
                UpdatedDate = excluded.UpdatedDate;
        ";
        await conn.ExecuteAsync(sql, new
        {
            Key = key,
            Value = value,
            Description = description,
            UpdatedDate = DateTime.UtcNow.ToString("o")
        });
    }

    public async Task<AppSettings> GetAppSettingsAsync()
    {
        var settings = new AppSettings();
        var all = await GetAllAsync();
        var dict = all.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        if (dict.TryGetValue("Valilik", out var val)) settings.Valilik = val;
        if (dict.TryGetValue("DistrictMem", out val) || dict.TryGetValue("IlceMem", out val))
        {
            settings.DistrictMem = val;
        }
        else
        {
            // Eğer veritabanında henüz DistrictMem yoksa ve Valilik içinde "/" varsa otomatik ayır!
            // Örn: "NİKSAR KAYMAKAMLIĞI / NİKSAR İLÇE MEM"
            if (!string.IsNullOrWhiteSpace(settings.Valilik) && settings.Valilik.Contains('/'))
            {
                var parts = settings.Valilik.Split('/', 2, StringSplitOptions.TrimEntries);
                settings.Valilik = parts[0];
                settings.DistrictMem = parts.Length > 1 ? parts[1] : "İlçe Millî Eğitim Müdürlüğü";
            }
        }
        if (dict.TryGetValue("KurumKodu", out val) || dict.TryGetValue("SchoolCode", out val)) settings.KurumKodu = val;
        if (dict.TryGetValue("SchoolName", out val)) settings.SchoolName = val;
        if (dict.TryGetValue("SayiNo", out val)) settings.SayiNo = val;
        if (dict.TryGetValue("FormDate", out val)) settings.FormDate = val;
        if (dict.TryGetValue("ValidityDate", out val)) settings.ValidityDate = val;
        if (dict.TryGetValue("TeacherTitle", out val)) settings.TeacherTitle = val;
        if (dict.TryGetValue("AcademicYear", out val)) settings.AcademicYear = val;
        if (dict.TryGetValue("Term", out val)) settings.Term = val;
        if (dict.TryGetValue("FormBodyText", out val)) settings.FormBodyText = val;
        if (dict.TryGetValue("PrincipalName", out val)) settings.PrincipalName = val;
        if (dict.TryGetValue("VicePrincipalName", out val)) settings.VicePrincipalName = val;
        if (dict.TryGetValue("IncludeSaturday", out val) && bool.TryParse(val, out var bVal)) settings.IncludeSaturday = bVal;
        if (dict.TryGetValue("MaxHoursPerTeacher", out val) && int.TryParse(val, out var iVal)) settings.MaxHoursPerTeacher = iVal;
        if (dict.TryGetValue("MinimumWage", out val) && decimal.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dVal)) settings.MinimumWage = dVal;
        if (dict.TryGetValue("GrossMinimumWage", out val) && decimal.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var grossVal)) settings.GrossMinimumWage = grossVal;
        if (dict.TryGetValue("MinimumWageYear", out val) && int.TryParse(val, out iVal)) settings.MinimumWageYear = iVal;
        if (dict.TryGetValue("OrgunStartDate", out val)) settings.OrgunStartDate = val;
        if (dict.TryGetValue("OrgunEndDate", out val)) settings.OrgunEndDate = val;
        if (dict.TryGetValue("FirstMidtermStartDate", out val)) settings.FirstMidtermStartDate = val;
        if (dict.TryGetValue("FirstMidtermEndDate", out val)) settings.FirstMidtermEndDate = val;
        if (dict.TryGetValue("SecondMidtermStartDate", out val)) settings.SecondMidtermStartDate = val;
        if (dict.TryGetValue("SecondMidtermEndDate", out val)) settings.SecondMidtermEndDate = val;
        if (dict.TryGetValue("Username", out val)) settings.Username = val;
        if (dict.TryGetValue("Password", out val)) settings.Password = val;
        if (dict.TryGetValue("LastBackupDate", out val)) settings.LastBackupDate = val;
        if (dict.TryGetValue("TeacherCanEditData", out val) && bool.TryParse(val, out var tEdit)) settings.TeacherCanEditData = tEdit;

        if (dict.TryGetValue("SgkSicilNo", out val)) settings.SgkSicilNo = val;
        if (dict.TryGetValue("SgkKontrolNo", out val)) settings.SgkKontrolNo = val;
        if (dict.TryGetValue("SgkAraciNo", out val)) settings.SgkAraciNo = val;
        if (dict.TryGetValue("VergiNo", out val)) settings.VergiNo = val;
        if (dict.TryGetValue("SgkNaceKodu", out val)) settings.SgkNaceKodu = val;
        if (dict.TryGetValue("SgkSonSinifBildirgeKodu", out val)) settings.SgkSonSinifBildirgeKodu = val;
        if (dict.TryGetValue("SgkYetkiliAdSoyad", out val)) settings.SgkYetkiliAdSoyad = val;
        if (dict.TryGetValue("SgkYetkiliUnvan", out val)) settings.SgkYetkiliUnvan = val;
        if (dict.TryGetValue("SgkYetkiliEposta", out val)) settings.SgkYetkiliEposta = val;
        if (dict.TryGetValue("SgkYetkiliTelefon", out val)) settings.SgkYetkiliTelefon = val;
        if (dict.TryGetValue("SgkBildirimNotu", out val)) settings.SgkBildirimNotu = val;

        if (dict.TryGetValue("OrgunSigortaKolu", out val)) settings.OrgunSigortaKolu = val;
        if (dict.TryGetValue("OrgunOgrenimKodu", out val)) settings.OrgunOgrenimKodu = val;
        if (dict.TryGetValue("OrgunMeslekKodu", out val)) settings.OrgunMeslekKodu = val;
        if (dict.TryGetValue("OrgunGorevKodu", out val)) settings.OrgunGorevKodu = val;

        // Otomatik düzeltme: Öğrenim kodu ve Meslek kodu önceki hatalı tanımdan kalmışsa güncelle
        if (settings.OrgunOgrenimKodu == "9901.02" && settings.OrgunMeslekKodu == "37")
        {
            settings.OrgunOgrenimKodu = "3";
            settings.OrgunMeslekKodu = "9901.02";
        }

        if (dict.TryGetValue("MesemSigortaKolu", out val)) settings.MesemSigortaKolu = val;
        if (dict.TryGetValue("MesemOgrenimKodu", out val)) settings.MesemOgrenimKodu = val;
        if (dict.TryGetValue("MesemMeslekKodu", out val)) settings.MesemMeslekKodu = val;
        if (dict.TryGetValue("MesemGorevKodu", out val)) settings.MesemGorevKodu = val;

        if (dict.TryGetValue("SchoolAddress", out val)) settings.SchoolAddress = val;
        if (dict.TryGetValue("SchoolCity", out val)) settings.SchoolCity = val;
        if (dict.TryGetValue("SchoolDistrict", out val)) settings.SchoolDistrict = val;

        return settings;
    }

    public async Task SaveAppSettingsAsync(AppSettings settings)
    {
        await SetValueAsync("Valilik", settings.Valilik);
        await SetValueAsync("DistrictMem", settings.DistrictMem);
        await SetValueAsync("KurumKodu", settings.KurumKodu);
        await SetValueAsync("SchoolName", settings.SchoolName);
        await SetValueAsync("SayiNo", settings.SayiNo);
        await SetValueAsync("FormDate", settings.FormDate);
        await SetValueAsync("ValidityDate", settings.ValidityDate);
        await SetValueAsync("TeacherTitle", settings.TeacherTitle);
        await SetValueAsync("AcademicYear", settings.AcademicYear);
        await SetValueAsync("Term", settings.Term);
        await SetValueAsync("FormBodyText", settings.FormBodyText);
        await SetValueAsync("PrincipalName", settings.PrincipalName);
        await SetValueAsync("VicePrincipalName", settings.VicePrincipalName);
        await SetValueAsync("IncludeSaturday", settings.IncludeSaturday.ToString().ToLowerInvariant());
        await SetValueAsync("MaxHoursPerTeacher", settings.MaxHoursPerTeacher.ToString());
        await SetValueAsync("MinimumWage", settings.MinimumWage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await SetValueAsync("GrossMinimumWage", settings.GrossMinimumWage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await SetValueAsync("MinimumWageYear", settings.MinimumWageYear.ToString());
        await SetValueAsync("OrgunStartDate", settings.OrgunStartDate);
        await SetValueAsync("OrgunEndDate", settings.OrgunEndDate);
        await SetValueAsync("FirstMidtermStartDate", settings.FirstMidtermStartDate);
        await SetValueAsync("FirstMidtermEndDate", settings.FirstMidtermEndDate);
        await SetValueAsync("SecondMidtermStartDate", settings.SecondMidtermStartDate);
        await SetValueAsync("SecondMidtermEndDate", settings.SecondMidtermEndDate);
        await SetValueAsync("Username", settings.Username);
        await SetValueAsync("Password", settings.Password);
        await SetValueAsync("TeacherCanEditData", settings.TeacherCanEditData.ToString().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(settings.LastBackupDate))
            await SetValueAsync("LastBackupDate", settings.LastBackupDate);

        await SetValueAsync("SgkSicilNo", settings.SgkSicilNo);
        await SetValueAsync("SgkKontrolNo", settings.SgkKontrolNo);
        await SetValueAsync("SgkAraciNo", settings.SgkAraciNo);
        await SetValueAsync("VergiNo", settings.VergiNo);
        await SetValueAsync("SgkNaceKodu", settings.SgkNaceKodu);
        await SetValueAsync("SgkSonSinifBildirgeKodu", settings.SgkSonSinifBildirgeKodu);
        await SetValueAsync("SgkYetkiliAdSoyad", settings.SgkYetkiliAdSoyad);
        await SetValueAsync("SgkYetkiliUnvan", settings.SgkYetkiliUnvan);
        await SetValueAsync("SgkYetkiliEposta", settings.SgkYetkiliEposta);
        await SetValueAsync("SgkYetkiliTelefon", settings.SgkYetkiliTelefon);
        await SetValueAsync("SgkBildirimNotu", settings.SgkBildirimNotu);

        await SetValueAsync("OrgunSigortaKolu", settings.OrgunSigortaKolu);
        await SetValueAsync("OrgunOgrenimKodu", settings.OrgunOgrenimKodu);
        await SetValueAsync("OrgunMeslekKodu", settings.OrgunMeslekKodu);
        await SetValueAsync("OrgunGorevKodu", settings.OrgunGorevKodu);

        await SetValueAsync("MesemSigortaKolu", settings.MesemSigortaKolu);
        await SetValueAsync("MesemOgrenimKodu", settings.MesemOgrenimKodu);
        await SetValueAsync("MesemMeslekKodu", settings.MesemMeslekKodu);
        await SetValueAsync("MesemGorevKodu", settings.MesemGorevKodu);

        await SetValueAsync("SchoolAddress", settings.SchoolAddress);
        await SetValueAsync("SchoolCity", settings.SchoolCity);
        await SetValueAsync("SchoolDistrict", settings.SchoolDistrict);
    }

    public async Task<IEnumerable<YearlyMinimumWage>> GetYearlyMinimumWagesAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<YearlyMinimumWage>(
            "SELECT Year, NetMinimumWage, GrossMinimumWage, UpdatedDate FROM YearlyMinimumWages ORDER BY Year DESC;");
    }

    public async Task<YearlyMinimumWage?> GetYearlyMinimumWageAsync(int year)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<YearlyMinimumWage>(
            "SELECT Year, NetMinimumWage, GrossMinimumWage, UpdatedDate FROM YearlyMinimumWages WHERE Year = @Year;",
            new { Year = year });
    }

    public async Task<YearlyMinimumWage> GetEffectiveWageForYearAsync(int year)
    {
        using var conn = _factory.CreateConnection();
        // 1. İstenen yıla tam eşleşme ara
        var exact = await conn.QueryFirstOrDefaultAsync<YearlyMinimumWage>(
            "SELECT Year, NetMinimumWage, GrossMinimumWage, UpdatedDate FROM YearlyMinimumWages WHERE Year = @Year;",
            new { Year = year });
        if (exact != null) return exact;

        // 2. Tam eşleşme yoksa o yıldan önceki en yakın yılı bul (örn: 2028 istendi ama henüz 2027 tanımlı)
        var previous = await conn.QueryFirstOrDefaultAsync<YearlyMinimumWage>(
            "SELECT Year, NetMinimumWage, GrossMinimumWage, UpdatedDate FROM YearlyMinimumWages WHERE Year <= @Year ORDER BY Year DESC LIMIT 1;",
            new { Year = year });
        if (previous != null) return previous;

        // 3. Mevcut en son kaydı getir
        var fallback = await conn.QueryFirstOrDefaultAsync<YearlyMinimumWage>(
            "SELECT Year, NetMinimumWage, GrossMinimumWage, UpdatedDate FROM YearlyMinimumWages ORDER BY Year DESC LIMIT 1;");
        if (fallback != null) return fallback;

        // 4. Tablo boşsa varsayılan
        return new YearlyMinimumWage
        {
            Year = year,
            NetMinimumWage = 28075.50m,
            GrossMinimumWage = 33030.00m,
            UpdatedDate = DateTime.UtcNow.ToString("o")
        };
    }

    public async Task SaveYearlyMinimumWageAsync(YearlyMinimumWage wage)
    {
        using var conn = _factory.CreateConnection();
        wage.GrossMinimumWage = YearlyMinimumWage.CalculateGrossFromNet(wage.NetMinimumWage);
        wage.UpdatedDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        const string sql = @"
            INSERT INTO YearlyMinimumWages (Year, NetMinimumWage, GrossMinimumWage, UpdatedDate)
            VALUES (@Year, @NetMinimumWage, @GrossMinimumWage, @UpdatedDate)
            ON CONFLICT(Year) DO UPDATE SET
                NetMinimumWage = excluded.NetMinimumWage,
                GrossMinimumWage = excluded.GrossMinimumWage,
                UpdatedDate = excluded.UpdatedDate;
        ";
        await conn.ExecuteAsync(sql, wage);

        // Genel ayarları en son yıl ile senkronize et
        var latest = await conn.QueryFirstOrDefaultAsync<YearlyMinimumWage>(
            "SELECT Year, NetMinimumWage, GrossMinimumWage FROM YearlyMinimumWages ORDER BY Year DESC LIMIT 1;");
        if (latest != null)
        {
            await SetValueAsync("MinimumWage", latest.NetMinimumWage.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await SetValueAsync("GrossMinimumWage", latest.GrossMinimumWage.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await SetValueAsync("MinimumWageYear", latest.Year.ToString());
        }
    }

    public async Task<bool> DeleteYearlyMinimumWageAsync(int year)
    {
        using var conn = _factory.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM YearlyMinimumWages;");
        if (count <= 1)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync("DELETE FROM YearlyMinimumWages WHERE Year = @Year;", new { Year = year });
        return affected > 0;
    }
}
