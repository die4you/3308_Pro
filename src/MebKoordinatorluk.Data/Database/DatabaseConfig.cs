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
    }

    public string ConnectionString =>
        $"Data Source={DatabasePath};Mode={(IsArchiveReadOnly ? "ReadOnly" : "ReadWriteCreate")};Foreign Keys=True;";

    public static string SanitizeYear(string academicYear)
    {
        if (string.IsNullOrWhiteSpace(academicYear)) return "current";
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

        var targetPath = GetDbPathForYear(academicYear);

        // Geriye dönük uyumluluk: Eğer targetPath henüz yoksa ama varsayılan meb_koordinatorluk.db varsa:
        if (!File.Exists(targetPath) && File.Exists(DefaultDatabasePath))
        {
            try
            {
                File.Copy(DefaultDatabasePath, targetPath, false);
            }
            catch
            {
                targetPath = DefaultDatabasePath;
            }
        }

        DatabasePath = targetPath;
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
