using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Services;

namespace MebKoordinatorluk.UI.Dialogs;

public class AcademicYearRolloverDialog : Form
{
    private readonly DatabaseConfig _dbConfig;
    private readonly IAcademicYearRolloverService _rolloverService;
    private readonly ILicenseService _licenseService;
    private readonly ISettingRepository _settingRepo;
    private readonly Action<string>? _onCompleted;

    private Label _lblSourceYear = null!;
    private ComboBox _cmbTargetYear = null!;
    private DateTimePicker _dtpStartDate = null!;
    private DateTimePicker _dtpEndDate = null!;

    private CheckBox _chkPromoteClassesAndStudents = null!;
    private CheckBox _chkArchiveGraduated12 = null!;
    private CheckBox _chkCopyTeachers = null!;
    private CheckBox _chkCopyEnterprises = null!;
    private CheckBox _chkCopyFields = null!;
    private CheckBox _chkResetEnterprisesForPromoted = null!;
    private CheckBox _chkResetAttendance = null!;

    private Panel _pnlLicenseNotice = null!;
    private Label _lblLicenseNotice = null!;

    private Panel _pnlProgress = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblProgressStatus = null!;

    private Button _btnCancel = null!;
    private Button _btnStartRollover = null!;

    public AcademicYearRolloverDialog(
        DatabaseConfig dbConfig,
        IAcademicYearRolloverService rolloverService,
        ILicenseService licenseService,
        ISettingRepository settingRepo,
        Action<string>? onCompleted = null)
    {
        _dbConfig = dbConfig;
        _rolloverService = rolloverService;
        _licenseService = licenseService;
        _settingRepo = settingRepo;
        _onCompleted = onCompleted;

        InitializeComponent();
        LoadCurrentYearData();
    }

    private void InitializeComponent()
    {
        Text = "3308 Pro — Yeni Eğitim-Öğretim Yılı Başlatma ve Sınıf Atlatma Sihirbazı";
        Size = new Size(680, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9f);

        // 1. Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = Color.FromArgb(30, 58, 138),
            Padding = new Padding(20, 12, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = "🎓 Yeni Eğitim-Öğretim Yılı Başlatma ve Devir Sihirbazı",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Mevcut yıl verilerinizi koruyarak yeni yıla temiz bir veritabanı ve sınıf terfileriyle geçiş yapın.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(219, 234, 254),
            Location = new Point(22, 40),
            AutoSize = true,
            UseMnemonic = false
        };

        pnlHeader.Controls.AddRange([lblTitle, lblSub]);

        // Content Area
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20, 15, 20, 15)
        };

        int top = 10;

        // Group 1: Yıl ve Tarih Tanımları
        var grpYear = new GroupBox
        {
            Text = " 📅 Eğitim-Öğretim Yılı ve Çalışma Takvimi ",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            Location = new Point(20, top),
            Size = new Size(620, 140),
            BackColor = Color.White
        };

        var lblSrcTitle = new Label
        {
            Text = "Kaynak (Mevcut) Yıl:",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 30),
            Size = new Size(130, 24)
        };

        _lblSourceYear = new Label
        {
            Text = "2026-2027",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(155, 28),
            Size = new Size(180, 24)
        };

        var lblTargetTitle = new Label
        {
            Text = "Hedef (Yeni) Yıl:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 65),
            Size = new Size(130, 24)
        };

        _cmbTargetYear = new ComboBox
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(155, 62),
            Size = new Size(160, 28),
            DropDownStyle = ComboBoxStyle.DropDown
        };
        _cmbTargetYear.SelectedIndexChanged += (s, e) => ValidateLicenseForTargetYear();
        _cmbTargetYear.TextChanged += (s, e) => ValidateLicenseForTargetYear();

        var lblDates = new Label
        {
            Text = "Örgün Eğitim Başlangıç / Bitiş Tarihleri:",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 102),
            Size = new Size(230, 24)
        };

        _dtpStartDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(255, 99),
            Size = new Size(120, 25)
        };
        _dtpStartDate.ValueChanged += (s, e) => ValidateLicenseForTargetYear();

        var lblDash = new Label
        {
            Text = "—",
            Location = new Point(382, 101),
            Size = new Size(20, 24)
        };

        _dtpEndDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(405, 99),
            Size = new Size(120, 25)
        };
        _dtpEndDate.ValueChanged += (s, e) => ValidateLicenseForTargetYear();

        grpYear.Controls.AddRange([lblSrcTitle, _lblSourceYear, lblTargetTitle, _cmbTargetYear, lblDates, _dtpStartDate, lblDash, _dtpEndDate]);
        pnlBody.Controls.Add(grpYear);

        top += 150;

        // Group 2: Devir Tercihleri
        var grpOptions = new GroupBox
        {
            Text = " ⚙️ Veri Devri ve Sınıf Atlatma Seçenekleri ",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            Location = new Point(20, top),
            Size = new Size(620, 200),
            BackColor = Color.White
        };

        int optTop = 26;

        _chkPromoteClassesAndStudents = new CheckBox
        {
            Text = "Sınıfları ve öğrencileri bir üst seviyeye aktar (11➔12, 10➔11, 9➔10)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Checked = true,
            Location = new Point(20, optTop),
            Size = new Size(580, 22)
        };
        optTop += 24;

        _chkArchiveGraduated12 = new CheckBox
        {
            Text = "12. sınıf mezun öğrencileri yeni yıla aktarma (eski yıl veritabanında arşivlensin)",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Checked = true,
            Location = new Point(40, optTop),
            Size = new Size(560, 20)
        };
        optTop += 24;

        _chkResetEnterprisesForPromoted = new CheckBox
        {
            Text = "11'den 12'ye geçen öğrencilerin işletme eşleşmelerini sıfırla (yeni sözleşmeler için önerilir)",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Checked = false,
            Location = new Point(40, optTop),
            Size = new Size(560, 20)
        };
        optTop += 26;

        _chkCopyTeachers = new CheckBox
        {
            Text = "Koordinatör öğretmen kadrosunu ve çalışma saatlerini yeni yıla aktar",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(15, 23, 42),
            Checked = true,
            Location = new Point(20, optTop),
            Size = new Size(580, 22)
        };
        optTop += 24;

        _chkCopyEnterprises = new CheckBox
        {
            Text = "Staj işletmeleri havuzunu ve usta öğretici tanımlarını yeni yıla aktar",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(15, 23, 42),
            Checked = true,
            Location = new Point(20, optTop),
            Size = new Size(580, 22)
        };
        optTop += 24;

        _chkCopyFields = new CheckBox
        {
            Text = "Mesleki alan, dal ve bölge adres gruplarını yeni yıla aktar",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(15, 23, 42),
            Checked = true,
            Location = new Point(20, optTop),
            Size = new Size(580, 22)
        };
        optTop += 24;

        _chkResetAttendance = new CheckBox
        {
            Text = "Aylık devamsızlık, evrak takip, puantaj ve dekont kayıtlarını sıfırla (temiz başlangıç)",
            Font = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = Color.FromArgb(5, 150, 105), // green-600
            Checked = true,
            Enabled = false,
            Location = new Point(20, optTop),
            Size = new Size(580, 22)
        };

        grpOptions.Controls.AddRange([
            _chkPromoteClassesAndStudents,
            _chkArchiveGraduated12,
            _chkResetEnterprisesForPromoted,
            _chkCopyTeachers,
            _chkCopyEnterprises,
            _chkCopyFields,
            _chkResetAttendance
        ]);
        pnlBody.Controls.Add(grpOptions);

        top += 210;

        // Group 3: Lisans Durumu Uyarısı
        _pnlLicenseNotice = new Panel
        {
            Location = new Point(20, top),
            Size = new Size(620, 56),
            BackColor = Color.FromArgb(239, 246, 255), // blue-50
            Padding = new Padding(12, 8, 12, 8)
        };

        _lblLicenseNotice = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(30, 64, 175),
            Text = "🔍 Lisans durumu kontrol ediliyor...",
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        _pnlLicenseNotice.Controls.Add(_lblLicenseNotice);
        pnlBody.Controls.Add(_pnlLicenseNotice);

        top += 64;

        // Group 4: Progress Panel (Initially hidden)
        _pnlProgress = new Panel
        {
            Location = new Point(20, top),
            Size = new Size(620, 50),
            Visible = false
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 20,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };

        _lblProgressStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            Text = "Veriler aktarılıyor, lütfen bekleyiniz...",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _pnlProgress.Controls.AddRange([_progressBar, _lblProgressStatus]);
        pnlBody.Controls.Add(_pnlProgress);

        Controls.Add(pnlBody);

        // Footer Panel
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 10, 20, 10)
        };

        _btnCancel = new Button
        {
            Text = "İptal",
            Size = new Size(90, 38),
            Dock = DockStyle.Left,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnCancel.Click += (s, e) => Close();

        _btnStartRollover = new Button
        {
            Text = "🚀 Yeni Yılı Başlat ve Verileri Devret",
            Size = new Size(270, 38),
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(5, 150, 105), // emerald-600
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _btnStartRollover.FlatAppearance.BorderSize = 0;
        _btnStartRollover.FlatAppearance.MouseOverBackColor = Color.FromArgb(4, 120, 87);
        _btnStartRollover.Click += BtnStartRollover_Click;

        pnlFooter.Controls.AddRange([_btnCancel, _btnStartRollover]);
        Controls.Add(pnlFooter);
        Controls.Add(pnlHeader);
    }

    private void LoadCurrentYearData()
    {
        try
        {
            var appSettings = _settingRepo.GetAppSettingsAsync().GetAwaiter().GetResult();
            var curYear = !string.IsNullOrWhiteSpace(appSettings.AcademicYear) ? appSettings.AcademicYear : _dbConfig.CurrentAcademicYear;
            _lblSourceYear.Text = curYear;

            // Önerilen hedef yıl: mevcut yılın bir sonrası
            var parsed = LicenseService.ParseYearDates(curYear);
            int nextStartYear = parsed.StartDate.Year + 1;
            int nextEndYear = parsed.EndDate.Year + 1;
            string suggestedTarget = $"{nextStartYear}-{nextEndYear}";

            _cmbTargetYear.Items.Clear();
            _cmbTargetYear.Items.Add(suggestedTarget);
            _cmbTargetYear.Items.Add($"{nextStartYear + 1}-{nextEndYear + 1}");
            _cmbTargetYear.SelectedItem = suggestedTarget;

            _dtpStartDate.Value = new DateTime(nextStartYear, 9, 8);
            _dtpEndDate.Value = new DateTime(nextEndYear, 6, 19);

            ValidateLicenseForTargetYear();
        }
        catch
        {
            _lblSourceYear.Text = "2026-2027";
            _cmbTargetYear.Text = "2027-2028";
            _dtpStartDate.Value = new DateTime(2027, 9, 8);
            _dtpEndDate.Value = new DateTime(2028, 6, 19);
        }
    }

    private void ValidateLicenseForTargetYear()
    {
        string targetYear = _cmbTargetYear.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetYear))
        {
            _lblLicenseNotice.Text = "⚠️ Lütfen bir hedef eğitim-öğretim yılı seçiniz.";
            _pnlLicenseNotice.BackColor = Color.FromArgb(254, 243, 199);
            _lblLicenseNotice.ForeColor = Color.FromArgb(180, 83, 9);
            _btnStartRollover.Enabled = false;
            return;
        }

        var status = _licenseService.GetAcademicYearAccess(targetYear, _dtpStartDate.Value, _dtpEndDate.Value);
        var lic = _licenseService.CurrentLicense;

        if (status == AcademicYearAccessStatus.FutureLocked)
        {
            _pnlLicenseNotice.BackColor = Color.FromArgb(254, 226, 226); // red-100
            _lblLicenseNotice.ForeColor = Color.FromArgb(185, 28, 28);   // red-700
            _lblLicenseNotice.Text = $"🔒 Lisans Kapsamı Dışında: Mevcut lisansınız ({lic.EndDate:dd.MM.yyyy}) bu yılı kapsamamaktadır.\n" +
                                     $"Yeni yılı başlatmak için lütfen 3308pro@gmail.com ile iletişime geçiniz.";
            _btnStartRollover.Enabled = false;
        }
        else if (status == AcademicYearAccessStatus.DemoAccess)
        {
            _pnlLicenseNotice.BackColor = Color.FromArgb(254, 243, 199); // amber-100
            _lblLicenseNotice.ForeColor = Color.FromArgb(180, 83, 9);    // amber-700
            _lblLicenseNotice.Text = "⏳ Deneme Sürümü: Yeni yıl oluşturulacak ve demo limitleriyle (en fazla 5 öğrenci / 3 işletme) kullanılabilecektir.";
            _btnStartRollover.Enabled = true;
        }
        else
        {
            _pnlLicenseNotice.BackColor = Color.FromArgb(236, 253, 245); // emerald-50
            _lblLicenseNotice.ForeColor = Color.FromArgb(4, 120, 87);    // emerald-700
            _lblLicenseNotice.Text = $"✔ Lisans Doğrulandı: Mevcut lisansınız ({lic.EndDate:dd.MM.yyyy}) bu eğitim-öğretim yılını kapsamaktadır.";
            _btnStartRollover.Enabled = true;
        }
    }

    private async void BtnStartRollover_Click(object? sender, EventArgs e)
    {
        string sourceYear = _lblSourceYear.Text.Trim();
        string targetYear = _cmbTargetYear.Text.Trim();

        if (string.Equals(sourceYear, targetYear, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "Kaynak yıl ile hedef yıl aynı olamaz! Lütfen bir sonraki eğitim-öğretim yılını seçiniz.",
                "Uyarı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var targetDbPath = _dbConfig.GetDbPathForYear(targetYear);
        if (File.Exists(targetDbPath))
        {
            var confirmOverwrite = MessageBox.Show(
                $"'{targetYear}' yılına ait veritabanı dosyası ({Path.GetFileName(targetDbPath)}) zaten mevcut!\n\n" +
                $"Yeniden devretmek, hedef yıldaki mevcut verileri sıfırlayacaktır.\n\n" +
                $"Devam etmek istiyor musunuz?",
                "Mevcut Dosya Uyarısı — 3308 Pro",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmOverwrite != DialogResult.Yes)
                return;
        }

        var confirmFinal = MessageBox.Show(
            $"Yeni Eğitim-Öğretim Yılı Devri Başlatılıyor:\n\n" +
            $"• Kaynak Yıl: {sourceYear}\n" +
            $"• Hedef Yıl: {targetYear}\n" +
            $"• Başlangıç: {_dtpStartDate.Value:dd.MM.yyyy} — Bitiş: {_dtpEndDate.Value:dd.MM.yyyy}\n\n" +
            $"İşlem başlatılsın mı?",
            "Devir Onayı — 3308 Pro",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmFinal != DialogResult.Yes)
            return;

        // UI Kilitle
        _btnStartRollover.Enabled = false;
        _btnCancel.Enabled = false;
        _cmbTargetYear.Enabled = false;
        _pnlProgress.Visible = true;

        var options = new AcademicYearRolloverOptions
        {
            SourceYear = sourceYear,
            TargetYear = targetYear,
            TargetStartDate = _dtpStartDate.Value,
            TargetEndDate = _dtpEndDate.Value,
            PromoteClassesAndStudents = _chkPromoteClassesAndStudents.Checked,
            ArchiveGraduated12thGrades = _chkArchiveGraduated12.Checked,
            ResetStudentEnterprisesForPromoted = _chkResetEnterprisesForPromoted.Checked,
            CopyTeachers = _chkCopyTeachers.Checked,
            CopyEnterprises = _chkCopyEnterprises.Checked,
            CopyFields = _chkCopyFields.Checked,
            CopyAddressGroups = _chkCopyFields.Checked,
            ResetAttendancesAndPayrolls = true
        };

        var progress = new Progress<string>(msg =>
        {
            if (!IsDisposed)
            {
                _lblProgressStatus.Text = msg;
            }
        });

        try
        {
            var result = await Task.Run(() => _rolloverService.ExecuteRolloverAsync(options, progress));

            if (result.Success)
            {
                MessageBox.Show(
                    result.Message,
                    "Yeni Yıl Başlatıldı — 3308 Pro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _onCompleted?.Invoke(targetYear);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(
                    result.Message,
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                _btnStartRollover.Enabled = true;
                _btnCancel.Enabled = true;
                _cmbTargetYear.Enabled = true;
                _pnlProgress.Visible = false;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Kritik bir hata oluştu:\n{ex.Message}",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _btnStartRollover.Enabled = true;
            _btnCancel.Enabled = true;
            _cmbTargetYear.Enabled = true;
            _pnlProgress.Visible = false;
        }
    }
}
