using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MebKoordinatorluk.Core.Interfaces;

public record BackupFileInfo(string FileName, string FullPath, long FileSizeBytes, DateTime CreatedAt);

public interface IDatabaseBackupService
{
    Task<string> CreateBackupAsync(string? customFileName = null, string? destinationFolder = null);
    List<BackupFileInfo> GetExistingBackups();
    Task<bool> RestoreBackupAsync(string backupFilePath);
    Task PerformAutoBackupOnExitAsync();
    void PerformAutoBackupOnExit();
    Task<string?> TryAutoRecoverCorruptedDatabaseAsync();
    string GetBackupDirectory();
    Task<string> ResetDatabaseAsync(bool keepUsersAndSettings = true);
}
