using System;
using System.IO;
using System.Linq;

namespace MebKoordinatorluk.Data.Database;

public class DatabaseConfig
{
    public string DatabasePath { get; set; }
    public string DataDirectory { get; }
    public string DefaultDatabasePath => GetDbPathForYear(CurrentAcademicYear);
    public string CurrentAcademicYear { get; private set; } = "2026-2027";
    public bool IsArchiveReadOnly { get; private set; } = false;

    public DatabaseConfig(string? customPath = null)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        DataDirectory = Path.Combine(baseDir, "Data");
        if (!Directory.Exists(DataDirectory))
        {
            Directory.CreateDirectory(DataDirectory);
        }

        if (!string.IsNullOrWhiteSpace(customPath))
        {
            DatabasePath = customPath;
        }
        else
        {
            MigrateLegacyKoordinatorlukDbIfPresent();
            // Data klasöründeki mevcut en son meb_*.db dosyasını tespit et (yoksa varsayılan meb_2026_2027.db)
            var detectedYear = DetectLatestAcademicYear();
            CurrentAcademicYear = detectedYear;
            DatabasePath = GetDbPathForYear(CurrentAcademicYear);
        }
    }

    private void MigrateLegacyKoordinatorlukDbIfPresent()
    {
        try
        {
            if (!Directory.Exists(DataDirectory)) return;

            var oldKoordinatorlukDb = Path.Combine(DataDirectory, "meb_koordinatorluk.db");
            var target2026Db = Path.Combine(DataDirectory, "meb_2026_2027.db");

            if (File.Exists(oldKoordinatorlukDb))
            {
                if (!File.Exists(target2026Db))
                {
                    File.Move(oldKoordinatorlukDb, target2026Db);
                }
                else
                {
                    var oldInfo = new FileInfo(oldKoordinatorlukDb);
                    var targetInfo = new FileInfo(target2026Db);
                    if (oldInfo.LastWriteTimeUtc > targetInfo.LastWriteTimeUtc && oldInfo.Length > 0)
                    {
                        File.Copy(oldKoordinatorlukDb, target2026Db, true);
                    }
                    try { File.Delete(oldKoordinatorlukDb); } catch { }
                }
            }
        }
        catch { }
    }

    public string DetectLatestAcademicYear()
    {
        try
        {
            if (Directory.Exists(DataDirectory))
            {
                var files = Directory.GetFiles(DataDirectory, "meb_*.db");
                var years = files
                    .Select(Path.GetFileNameWithoutExtension)
                    .OfType<string>()
                    .Where(f => f.StartsWith("meb_") && f != "meb_koordinatorluk" && f.Length > 4)
                    .Select(f => f[4..].Replace('_', '-'))
                    .OrderByDescending(y => y)
                    .ToList();

                if (years.Count > 0)
                {
                    return years[0];
                }
            }
        }
        catch { }

        return "2026-2027";
    }

    public string ConnectionString =>
        $"Data Source={DatabasePath};Mode={(IsArchiveReadOnly ? "ReadOnly" : "ReadWriteCreate")};Foreign Keys=True;";

    public static string SanitizeYear(string academicYear)
    {
        if (string.IsNullOrWhiteSpace(academicYear)) return "2026_2027";
        return academicYear.Replace("-", "_").Replace("/", "_").Replace(" ", "").Trim();
    }

    public string GetDbPathForYear(string academicYear)
    {
        var clean = SanitizeYear(academicYear);
        return Path.Combine(DataDirectory, $"meb_{clean}.db");
    }

    public void SwitchToYear(string academicYear, bool isReadOnly)
    {
        CurrentAcademicYear = academicYear;
        IsArchiveReadOnly = isReadOnly;
        DatabasePath = GetDbPathForYear(academicYear);

        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        catch { }
    }

    public void SetDatabasePathDirectly(string path, string academicYear, bool isReadOnly)
    {
        CurrentAcademicYear = academicYear;
        IsArchiveReadOnly = isReadOnly;
        DatabasePath = path;
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        catch { }
    }
}
