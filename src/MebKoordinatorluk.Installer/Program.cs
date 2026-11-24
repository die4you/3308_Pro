using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using Microsoft.Win32;

namespace MebKoordinatorluk.Installer;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }
}

public class InstallerForm : Form
{
    private TextBox _txtDir = null!;
    private Button _btnBrowse = null!;
    private CheckBox _chkDesktop = null!;
    private CheckBox _chkStartMenu = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblStatus = null!;
    private Button _btnInstall = null!;
    private Button _btnCancel = null!;
    private CheckBox _chkLaunch = null!;
    private PictureBox _picLogo = null!;

    public InstallerForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "3308 Pro - Kurulum Sihirbazı v2.9";
        ClientSize = new Size(640, 520);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252); // slate-50
        Font = new Font("Segoe UI", 9f);

        try
        {
            if (File.Exists("app.ico"))
                Icon = new Icon("app.ico");
            else if (!string.IsNullOrEmpty(Application.ExecutablePath))
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }

        // 1. Header Banner (#1E3A8A MEB Derin Lacivert)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 95,
            BackColor = Color.FromArgb(30, 58, 138)
        };

        var lblTitle = new Label
        {
            Text = "3308 Pro - Kurulum Sihirbazı",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(24, 18),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "İşletmelerde Beceri Eğitimi ve Koordinatörlük Sistemi (Sürüm 2.9)",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(191, 219, 254),
            Location = new Point(25, 52),
            AutoSize = true,
            UseMnemonic = false
        };

        // Sağ Üst Logo
        _picLogo = new PictureBox
        {
            Location = new Point(535, 14),
            Size = new Size(68, 68),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var logoStream = asm.GetManifestResourceStream("Logo.jpg");
            if (logoStream != null)
            {
                _picLogo.Image = Image.FromStream(logoStream);
            }
            else if (Icon != null)
            {
                _picLogo.Image = Icon.ToBitmap();
            }
        }
        catch { }

        pnlHeader.Controls.AddRange([lblTitle, lblSub, _picLogo]);

        // 2. Body Card
        var pnlCard = new Panel
        {
            Location = new Point(24, 110),
            Size = new Size(578, 360),
            BackColor = Color.White
        };
        pnlCard.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
        };

        int y = 18;

        var lblWelcome = new Label
        {
            Text = "Bu sihirbaz 3308 Pro uygulamasını bilgisayarınıza kuracaktır.\nDevam etmek için hedef kurulum klasörünü ve seçenekleri belirleyiniz:",
            Location = new Point(20, y),
            Size = new Size(538, 36),
            Font = new Font("Segoe UI", 9f)
        };
        pnlCard.Controls.Add(lblWelcome);
        y += 44;

        // Hedef Klasör
        var lblDir = new Label
        {
            Text = "Hedef Kurulum Klasörü:",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        pnlCard.Controls.Add(lblDir);
        y += 24;

        _txtDir = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(436, 27),
            Text = @"C:\3308_Pro",
            Font = new Font("Segoe UI", 9.5f)
        };
        _btnBrowse = new Button
        {
            Text = "Gözat...",
            Location = new Point(466, y - 1),
            Size = new Size(92, 29),
            BackColor = Color.FromArgb(243, 244, 246),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand
        };
        _btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        _btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog { SelectedPath = _txtDir.Text };
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                _txtDir.Text = fbd.SelectedPath;
            }
        };
        pnlCard.Controls.AddRange([_txtDir, _btnBrowse]);
        y += 34;

        var lblDirHint = new Label
        {
            Text = "💡 SQLite veritabanı izinlerinin sorunsuz çalışması için varsayılan C:\\3308_Pro önerilir.",
            Location = new Point(20, y),
            Size = new Size(538, 18),
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        pnlCard.Controls.Add(lblDirHint);
        y += 26;

        // Seçenekler
        _chkDesktop = new CheckBox
        {
            Text = "Masaüstüne 3308 Pro kısayol simgesi oluştur",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand
        };
        pnlCard.Controls.Add(_chkDesktop);
        y += 26;

        _chkStartMenu = new CheckBox
        {
            Text = "Başlat menüsüne '3308 Pro' klasörü ve kısayolunu ekle",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand
        };
        pnlCard.Controls.Add(_chkStartMenu);
        y += 34;

        // İlerleme ve Durum
        _lblStatus = new Label
        {
            Text = "Kuruluma hazır. 'Kurulumu Başlat' butonuna tıklayınız.",
            Location = new Point(20, y),
            Size = new Size(538, 20),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        pnlCard.Controls.Add(_lblStatus);
        y += 24;

        _progressBar = new ProgressBar
        {
            Location = new Point(20, y),
            Size = new Size(538, 22),
            Style = ProgressBarStyle.Blocks,
            Value = 0
        };
        pnlCard.Controls.Add(_progressBar);
        y += 28;

        _chkLaunch = new CheckBox
        {
            Text = "🚀 Kurulum tamamlandığında 3308 Pro'yu şimdi başlat",
            Location = new Point(20, y),
            AutoSize = true,
            Checked = true,
            Visible = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(5, 150, 105),
            Cursor = Cursors.Hand
        };
        pnlCard.Controls.Add(_chkLaunch);

        // Alt Butonlar (Genişletilmiş ve Kesilmeye Karşı Güvenli)
        _btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(270, 308),
            Size = new Size(100, 38),
            BackColor = Color.FromArgb(243, 244, 246),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        _btnCancel.Click += (s, e) => Close();

        _btnInstall = new Button
        {
            Text = "🚀 Kurulumu Başlat",
            Location = new Point(378, 308),
            Size = new Size(180, 38),
            BackColor = Color.FromArgb(37, 99, 235), // Blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += BtnInstall_Click;

        pnlCard.Controls.AddRange([_btnCancel, _btnInstall]);

        Controls.Add(pnlCard);
        Controls.Add(pnlHeader);
    }

    private async void BtnInstall_Click(object? sender, EventArgs e)
    {
        if (_btnInstall.Text == "Son" || _btnInstall.Text == "Tamamla")
        {
            if (_chkLaunch.Checked)
            {
                var exePath = Path.Combine(_txtDir.Text.Trim(), "3308_Pro.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = _txtDir.Text.Trim() });
                }
            }
            Close();
            return;
        }

        var targetDir = _txtDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("Lütfen geçerli bir kurulum klasörü seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _btnInstall.Enabled = false;
            _btnBrowse.Enabled = false;
            _txtDir.Enabled = false;
            _btnCancel.Enabled = false;

            _lblStatus.Text = "Klasör oluşturuluyor...";
            Directory.CreateDirectory(targetDir);

            // 1. Zip Kaynağını Aç
            _lblStatus.Text = "Program dosyaları ayıklanıyor...";
            var assembly = Assembly.GetExecutingAssembly();
            await using (var zipStream = assembly.GetManifestResourceStream("Payload.zip"))
            {
                if (zipStream == null)
                {
                    throw new InvalidOperationException("Kurulum veri paketi (Payload.zip) bulunamadı!");
                }

                using var archive = new ZipArchive(zipStream);
                int totalEntries = archive.Entries.Count;
                int current = 0;

                await Task.Run(() =>
                {
                    foreach (var entry in archive.Entries)
                    {
                        var destPath = Path.Combine(targetDir, entry.FullName);
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destPath);
                        }
                        else
                        {
                            var parent = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                            entry.ExtractToFile(destPath, overwrite: true);
                        }

                        current++;
                        int progress = (int)((current / (double)totalEntries) * 85);
                        Invoke(new Action(() =>
                        {
                            _progressBar.Value = progress;
                            _lblStatus.Text = $"Dosyalar kopyalanıyor: {entry.Name}";
                        }));
                    }
                });
            }

            // 2. Kısayolları Oluştur
            _lblStatus.Text = "Masaüstü ve başlat menüsü kısayolları oluşturuluyor...";
            _progressBar.Value = 90;

            var exePath = Path.Combine(targetDir, "3308_Pro.exe");
            var iconPath = Path.Combine(targetDir, "app.ico");
            if (!File.Exists(iconPath)) iconPath = exePath;

            if (_chkDesktop.Checked)
            {
                var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var desktopLnk = Path.Combine(desktopDir, "3308 Pro.lnk");
                CreateWindowsShortcut(exePath, desktopLnk, iconPath, "3308 Pro - MEB Koordinatörlük Sistemi");
            }

            if (_chkStartMenu.Checked)
            {
                var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "3308 Pro");
                Directory.CreateDirectory(startMenuDir);
                var startLnk = Path.Combine(startMenuDir, "3308 Pro.lnk");
                CreateWindowsShortcut(exePath, startLnk, iconPath, "3308 Pro - MEB Koordinatörlük Sistemi");
            }

            // 3. Kaldırıcı (Uninstaller) Oluştur
            CreateUninstaller(targetDir);

            _progressBar.Value = 100;
            _lblStatus.Text = "✅ Kurulum başarıyla tamamlandı!";
            _lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            _chkLaunch.Visible = true;
            _btnInstall.Text = "Tamamla";
            _btnInstall.BackColor = Color.FromArgb(5, 150, 105);
            _btnInstall.Enabled = true;
        }
        catch (Exception ex)
        {
            _progressBar.Value = 0;
            _lblStatus.Text = "❌ Kurulum hatası: " + ex.Message;
            _lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
            MessageBox.Show($"Kurulum sırasında bir hata oluştu:\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnInstall.Enabled = true;
            _btnCancel.Enabled = true;
        }
    }

    private static void CreateWindowsShortcut(string targetPath, string shortcutPath, string iconPath, string description)
    {
        try
        {
            var vbsPath = Path.Combine(Path.GetTempPath(), $"mk_lnk_{Guid.NewGuid():N}.vbs");
            var script = $@"Set oWS = WScript.CreateObject(""WScript.Shell"")
Set oLink = oWS.CreateShortcut(""{shortcutPath}"")
oLink.TargetPath = ""{targetPath}""
oLink.WorkingDirectory = ""{Path.GetDirectoryName(targetPath)}""
oLink.Description = ""{description}""
if ""{iconPath}"" <> """" Then oLink.IconLocation = ""{iconPath},0""
oLink.Save";
            File.WriteAllText(vbsPath, script, Encoding.Unicode);
            using var proc = Process.Start(new ProcessStartInfo("cscript.exe", $"//nologo \"{vbsPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(3000);
            try { File.Delete(vbsPath); } catch { }
        }
        catch { }
    }

    private static void CreateUninstaller(string targetDir)
    {
        try
        {
            var uninstBat = $@"@echo off
chcp 65001 >nul
title 3308 Pro - Program Kaldirici
echo ========================================================
echo   3308 Pro - Isletmelerde Beceri Egitimi Sistemi
echo   Sistemden Kaldirma Sihirbazi
echo ========================================================
echo.
echo Bu islem 3308 Pro masaustu ve baslat menusu kisayollarini
echo ve sistem kayitlarini kaldiracaktir.
echo.
set /p onay=Devam etmek istiyor musunuz? (E/H): 
if /i not ""%onay%""==""E"" exit

taskkill /f /im 3308_Pro.exe >nul 2>&1
timeout /t 1 >nul

del /q ""%USERPROFILE%\Desktop\3308 Pro.lnk"" >nul 2>&1
rmdir /s /q ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\3308 Pro"" >nul 2>&1

reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\3308Pro"" /f >nul 2>&1

echo.
echo [BASARILI] 3308 Pro kisayollari ve sistem kayitlari kaldirildi.
echo.
echo NOT: Giris yaptiginiz okul verilerini korumak amaciyla
echo '{targetDir}' klasoru ve veritabani silinmemistir.
echo Dilerseniz bu klasoru el ile silebilirsiniz.
echo.
pause
";
            var uninstPath = Path.Combine(targetDir, "Programi_Kaldir.bat");
            File.WriteAllText(uninstPath, uninstBat, Encoding.GetEncoding(1254));

            var iconPath = Path.Combine(targetDir, "app.ico");
            if (!File.Exists(iconPath)) iconPath = Path.Combine(targetDir, "3308_Pro.exe");

            var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "3308 Pro");
            if (Directory.Exists(startMenuDir))
            {
                var uninstLnk = Path.Combine(startMenuDir, "3308 Pro'yu Kaldır.lnk");
                CreateWindowsShortcut(uninstPath, uninstLnk, iconPath, "3308 Pro Sistemden Kaldır");
            }

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\3308Pro");
            if (key != null)
            {
                key.SetValue("DisplayName", "3308 Pro - MEB Koordinatörlük Sistemi");
                key.SetValue("DisplayVersion", "2.9");
                key.SetValue("Publisher", "TRGY CYMZ");
                key.SetValue("DisplayIcon", iconPath);
                key.SetValue("UninstallString", $"\"{uninstPath}\"");
                key.SetValue("InstallLocation", targetDir);
            }
        }
        catch { }
    }
}
