using MebKoordinatorluk.Data;
using MebKoordinatorluk.Data.Database;
using Microsoft.Extensions.DependencyInjection;

namespace MebKoordinatorluk.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Türkçe Kültür Ayarlarını Uygulama Genelinde Varsayılan Yap
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = trCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = trCulture;
        Thread.CurrentThread.CurrentCulture = trCulture;
        Thread.CurrentThread.CurrentUICulture = trCulture;

        // Yüksek DPI ve varsayılan font desteği
        ApplicationConfiguration.Initialize();

        // Dependency Injection Konfigürasyonu
        var services = new ServiceCollection();

        // Veri ve İş Mantığı Servislerini Kaydet
        services.AddMebDataServices();

        // WinForms Formlarını Kaydet
        services.AddTransient<MainForm>();

        var serviceProvider = services.BuildServiceProvider();

        // Lisans UI Denetleyicisini Başlat
        try
        {
            Common.LicenseUIHelper.LicenseService = serviceProvider.GetRequiredService<Core.Interfaces.ILicenseService>();
        }
        catch { }

        // 1. SQLite Veritabanını Başlat ve Şemaları Doğrula
        try
        {
            var dbInitializer = serviceProvider.GetRequiredService<DatabaseInitializer>();
            dbInitializer.Initialize();
        }
        catch (Exception ex)
        {
            // Otomatik Kurtarma Mekanizması: Sağlam en son yedeği bulup geri yüklemeyi dene
            var backupService = serviceProvider.GetRequiredService<Core.Interfaces.IDatabaseBackupService>();
            var recoveredFile = backupService.TryAutoRecoverCorruptedDatabaseAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(recoveredFile))
            {
                try
                {
                    var retryDbInit = serviceProvider.GetRequiredService<DatabaseInitializer>();
                    retryDbInit.Initialize();
                    MessageBox.Show(
                        $"Veritabanı başlatılırken sorun tespit edildi, ancak otomatik kurtarma sistemi en son sağlam yedekten ('{recoveredFile}') verilerinizi başarıyla geri yükledi.",
                        "Otomatik Veri Kurtarma Başarılı — 3308 Pro",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception retryEx)
                {
                    MessageBox.Show(
                        $"Otomatik kurtarma sonrası veritabanı başlatılamadı:\n{retryEx.Message}",
                        "Kritik Veritabanı Hatası",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                MessageBox.Show(
                    $"SQLite Veritabanı başlatılırken kritik bir hata oluştu:\n{ex.Message}\n\nİç Hata: {ex.InnerException?.Message}",
                    "Veritabanı Başlatma Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        // 2. Kullanıcı Giriş & Oturum Döngüsü
        while (true)
        {
            using (var loginDlg = new Dialogs.LoginDialog(
                serviceProvider.GetRequiredService<Core.Interfaces.IUserRepository>(),
                serviceProvider.GetRequiredService<Core.Interfaces.ISettingRepository>()))
            {
                var result = loginDlg.ShowDialog();
                loginDlg.Hide();
                if (result != DialogResult.OK)
                {
                    break; // Giriş penceresi kapatıldı veya iptal edildi
                }
            }

            using (var mainForm = serviceProvider.GetRequiredService<MainForm>())
            {
                mainForm.WindowState = FormWindowState.Maximized;
                Application.Run(mainForm);
            }

            // Kullanıcı "Oturumu Kapat" diyerek tekrar giriş ekranına dönmek istedi mi?
            if (!Core.Security.CurrentUserSession.RequestedRelogin)
            {
                break; // Normal kapatma işlemi
            }

            Core.Security.CurrentUserSession.RequestedRelogin = false;
            Core.Security.CurrentUserSession.Logout();
        }

        // 3. Uygulama Tamamen Kapanırken Otomatik Günlük Yedek Al (Arka planda sessizce)
        try
        {
            var backupService = serviceProvider.GetRequiredService<Core.Interfaces.IDatabaseBackupService>();
            backupService.PerformAutoBackupOnExit();
        }
        catch { }
    }
}