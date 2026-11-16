using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MebKoordinatorluk.Core.Security;

/// <summary>
/// Bilgisayara özgü, kalıcı ve tekil Donanım Kimliği (HWID / Kurulum Kodu) üreteci.
/// </summary>
public static class HardwareIdentifier
{
    private static string? _cachedHwid;

    public static string GetMachineCode()
    {
        if (!string.IsNullOrEmpty(_cachedHwid))
            return _cachedHwid;

        var sb = new StringBuilder();

        // 1. Windows MachineGuid (Benzersiz Sistem Kurulum GUID'i)
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                       .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(guid))
            {
                sb.Append("GUID:" + guid.Trim() + ";");
            }
        }
        catch
        {
            // İzin kısıtında sessizce devam et
        }

        // 2. Sistem Sürücüsü Bilgileri (C: Volume / Serial)
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(systemDrive))
            {
                var driveInfo = new DriveInfo(systemDrive);
                sb.Append("VOL:" + driveInfo.TotalSize + ";" + driveInfo.DriveFormat + ";");
            }
        }
        catch
        {
        }

        // 3. İşlemci ve Makine Özellikleri
        try
        {
            sb.Append("CPU:" + Environment.ProcessorCount + ";");
            sb.Append("ARCH:" + Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") + ";");
            sb.Append("ID:" + Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") + ";");
            sb.Append("PC:" + Environment.MachineName + ";");
        }
        catch
        {
        }

        // Fallback: Yetersiz veri varsa yedek kimlik
        if (sb.Length < 10)
        {
            sb.Append("FALLBACK:" + Environment.UserName + "@" + Environment.MachineName);
        }

        // SHA-256 ile özetle ve 3308-XXXX-XXXX-XXXX-XXXX formatına dönüştür
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        var hex = Convert.ToHexString(hash);

        // İlk 16 karakteri 4'erli bloklara böl (3308-XXXX-XXXX-XXXX-XXXX)
        _cachedHwid = $"3308-{hex[..4]}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}";
        return _cachedHwid;
    }
}
