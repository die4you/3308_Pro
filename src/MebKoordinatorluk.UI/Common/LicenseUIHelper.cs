using System.Windows.Forms;
using MebKoordinatorluk.Core.Interfaces;

namespace MebKoordinatorluk.UI.Common;

/// <summary>
/// 3308 Pro Demo ve Lisans Kısıtlamalarını UI Katmanında Merkezi Yöneten Yardımcı Sınıf.
/// </summary>
public static class LicenseUIHelper
{
    public static ILicenseService? LicenseService { get; set; }

    /// <summary>
    /// Programın aktif ve geçerli bir lisansa sahip olup olmadığını döner.
    /// </summary>
    public static bool IsLicensed => LicenseService?.CurrentLicense.IsActive ?? false;

    /// <summary>
    /// Belirtilen özelliğin lisanslı olup olmadığını denetler. Demo modundaysa kullanıcıya şık bir uyarı penceresi gösterir.
    /// </summary>
    public static bool CheckFeature(string featureName, IWin32Window? owner = null)
    {
        if (IsLicensed) return true;

        MessageBox.Show(
            owner,
            $"🔒 {featureName} Özelliği Tam Sürüme (Lisanslı) Özeldir.\n\n" +
            "3308 Pro Demo sürümünde bu özellik kısıtlanmıştır.\n" +
            "Tüm sınırlamaları kaldırmak ve programın tüm özelliklerini sınırsız kullanabilmek için lütfen lisansınızı etkinleştiriniz.",
            "Lisans Gerekli — 3308 Pro",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);

        return false;
    }

    /// <summary>
    /// Öğrenci ekleme limitini denetler. (Demo modunda en fazla 5)
    /// </summary>
    public static bool CanAddStudent(int currentCount, IWin32Window? owner = null)
    {
        if (IsLicensed) return true;

        if (LicenseService != null && !LicenseService.CanAddStudent(currentCount))
        {
            MessageBox.Show(
                owner,
                "⚠️ Demo Sürüm Kısıtlaması:\n\n" +
                "3308 Pro Deneme sürümünde en fazla 5 öğrenci kaydedilebilir.\n" +
                "Tüm okul öğrencilerinizi sınırsız olarak kaydedebilmek için lütfen tam sürüm lisansı edininiz.",
                "Demo Sınırına Ulaşıldı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// İşletme ekleme limitini denetler. (Demo modunda en fazla 3)
    /// </summary>
    public static bool CanAddEnterprise(int currentCount, IWin32Window? owner = null)
    {
        if (IsLicensed) return true;

        if (LicenseService != null && !LicenseService.CanAddEnterprise(currentCount))
        {
            MessageBox.Show(
                owner,
                "⚠️ Demo Sürüm Kısıtlaması:\n\n" +
                "3308 Pro Deneme sürümünde en fazla 3 işletme kaydedilebilir.\n" +
                "Tüm işletmelerinizi sınırsız olarak kaydedebilmek için lütfen tam sürüm lisansı edininiz.",
                "Demo Sınırına Ulaşıldı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Öğretmen ekleme limitini denetler. (Demo modunda en fazla 1)
    /// </summary>
    public static bool CanAddTeacher(int currentCount, IWin32Window? owner = null)
    {
        if (IsLicensed) return true;

        if (LicenseService != null && !LicenseService.CanAddTeacher(currentCount))
        {
            MessageBox.Show(
                owner,
                "⚠️ Demo Sürüm Kısıtlaması:\n\n" +
                "3308 Pro Deneme sürümünde en fazla 1 koordinatör öğretmen kaydedilebilir.\n" +
                "Tüm okul koordinatör öğretmen kadronuzu sınırsız olarak eklemek için lütfen tam sürüm lisansı edininiz.",
                "Demo Sınırına Ulaşıldı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }
}
