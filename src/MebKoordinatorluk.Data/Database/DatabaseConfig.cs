using System;
using System.IO;

namespace MebKoordinatorluk.Data.Database;

public class DatabaseConfig
{
    public string DatabasePath { get; set; }
    public string DataDirectory { get; }
    public string DefaultDatabasePath { get; }
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
        DefaultDatabasePath = Path.Combine(DataDirectory, "meb_koordinatorluk.db");

        if (!string.IsNullOrWhiteSpace(customPath))
        {
            DatabasePath = customPath;
        }
        else
        {
            DatabasePath = DefaultDatabasePath;
        }

        MigrateLegacyYearClonesIfPresent();
    }

    private void MigrateLegacyYearClonesIfPresent()
    {
        try
        {
            if (!Directory.Exists(DataDirectory)) return;

            // Önceki sürümlerden kalan meb_2026_2027.db gibi klon dosyaları tespit et ve birleştir
            var files = Directory.GetFiles(DataDirectory, "meb_*.db");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Equals("meb_koordinatorluk", StringComparison.OrdinalIgnoreCase))
                    continue;

                var cleanCurrent = SanitizeYear(CurrentAcademicYear);
                if (fileName.Equals($"meb_{cleanCurrent}", StringComparison.OrdinalIgnoreCase))
                {
                    var fileInfo = new FileInfo(file);
                    if (!File.Exists(DefaultDatabasePath))
                    {
                        File.Copy(file, DefaultDatabasePath, false);
                    }
                    else
                    {
                        var defaultInfo = new FileInfo(DefaultDatabasePath);
                        if (fileInfo.LastWriteTimeUtc > defaultInfo.LastWriteTimeUtc && fileInfo.Length > 0)
                        {
                            File.Copy(file, DefaultDatabasePath, true);
                        }
                    }

                    var bakPath = file + ".migrated.bak";
                    if (File.Exists(bakPath)) File.Delete(bakPath);
                    File.Move(file, bakPath);
                }
            }
        }
        catch { }
    }

    public string ConnectionString =>
        $"Data Source={DatabasePath};Mode={(IsArchiveReadOnly ? "ReadOnly" : "ReadWriteCreate")};Foreign Keys=True;";

    public static string SanitizeYear(string academicYear)
    {
        if (string.IsNullOrWhiteSpace(academicYear)) return "current";
        return academicYear.Replace("-", "_").Replace("/", "_").Replace(" ", "").Trim();
    }

    public string GetArchiveDbPath(string academicYear)
    {
        var clean = SanitizeYear(academicYear);
        var primaryArchive = Path.Combine(DataDirectory, $"arsiv_meb_{clean}.db");
        if (File.Exists(primaryArchive)) return primaryArchive;

        var legacyArchive = Path.Combine(DataDirectory, $"meb_{clean}.db");
        if (File.Exists(legacyArchive)) return legacyArchive;

        var altArchive = Path.Combine(DataDirectory, $"meb_{clean}_arsiv.db");
        if (File.Exists(altArchive)) return altArchive;

        return primaryArchive;
    }

    public string GetDbPathForYear(string academicYear)
    {
        if (string.Equals(academicYear, CurrentAcademicYear, StringComparison.OrdinalIgnoreCase) || !IsArchiveReadOnly)
        {
            return DefaultDatabasePath;
        }
        return GetArchiveDbPath(academicYear);
    }

    public void SwitchToYear(string academicYear, bool isReadOnly)
    {
        CurrentAcademicYear = academicYear;
        IsArchiveReadOnly = isReadOnly;

        if (isReadOnly)
        {
            DatabasePath = GetArchiveDbPath(academicYear);
        }
        else
        {
            DatabasePath = DefaultDatabasePath;
        }

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
