using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class SettingsView : UserControl
{
    private readonly ISettingRepository _settingRepo;
    private readonly IUserRepository? _userRepo;
    private readonly Action<string>? _onNavigate;
    private readonly IDatabaseBackupService? _backupService;
    private readonly ILicenseService? _licenseService;
    private AppSettings _settings = new();

    // License Form Controls
    private TextBox _txtLicHwid = null!;
    private Label _lblHwidTit = null!;
    private Button _btnCopyHwid = null!;
    private Label _lblLicStatusBadge = null!;
    private Label _lblLicSchool = null!;
    private Label _lblLicEndDate = null!;
    private Label _lblLicInfo = null!;
    private Label _lblLicEnd = null!;
    private Button _btnOpenActivation = null!;
    private Panel _pnlRenewalNotice = null!;
    private Label _lblRenewalNoticeText = null!;
    private Panel _pnlLicense = null!;

    // Auth Form
    private TextBox _txtCurrentPassword = null!;
    private TextBox _txtNewUsername = null!;
    private TextBox _txtNewPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Label _lblCurrentUserBadge = null!;
    private CheckBox _chkTeacherCanEdit = null!;

    // Institution Form
    private TextBox _txtValilik = null!;
    private TextBox _txtDistrictMem = null!;
    private TextBox _txtSchoolName = null!;
    private TextBox _txtSchoolAddress = null!;
    private TextBox _txtSchoolCity = null!;
    private TextBox _txtSchoolDistrict = null!;
    private TextBox _txtAcademicYear = null!;
    private TextBox _txtTerm = null!;
    private TextBox _txtSayiNo = null!;
    private TextBox _txtFormDate = null!;
    private TextBox _txtValidityDate = null!;
    private TextBox _txtTeacherTitle = null!;
    private TextBox _txtVicePrincipal = null!;
    private TextBox _txtPrincipal = null!;
    private TextBox _txtFormBody = null!;
    private NumericUpDown _numMaxHours = null!;
    private TextBox _txtMinimumWage = null!;
    private TextBox _txtGrossMinimumWage = null!;
    private DateTimePicker _dtpOrgunStart = null!;
    private DateTimePicker _dtpOrgunEnd = null!;

    // Yıl Bazlı Asgari Ücret Kontrolleri
    private NumericUpDown _numWageYear = null!;
    private TextBox _txtWageNet = null!;
    private TextBox _txtWageGross = null!;
    private Button _btnSaveWage = null!;
    private DataGridView _dgvYearlyWages = null!;
    private DataGridViewButtonColumn _colWageDelete = null!;

    private Panel _pnlHeaderContent = null!;
    private Panel _pnlBody = null!;
    private Panel _pnlSettingsContainer = null!;

    public SettingsView(
        ISettingRepository settingRepo,
        IUserRepository? userRepo = null,
        Action<string>? onNavigate = null,
        IDatabaseBackupService? backupService = null,
        ILicenseService? licenseService = null)
    {
        _settingRepo = settingRepo;
        _userRepo = userRepo;
        _onNavigate = onNavigate;
        _backupService = backupService;
        _licenseService = licenseService;
        InitializeComponent();
        LoadSettingsAsync();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Section
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.Transparent
        };

        _pnlHeaderContent = new Panel
        {
            Location = new Point(24, 4),
            Size = new Size(840, 56),
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = "🏫 Okul Bilgileri ve Genel Ayarlar",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Resmi evrak başlıkları, imza yetkilileri, mevzuat metinleri ve güvenlik",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(2, 28),
            AutoSize = true
        };

        _pnlHeaderContent.Controls.AddRange([lblTitle, lblSubtitle]);
        pnlHeader.Controls.Add(_pnlHeaderContent);

        // Main Scrollable Body
        _pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 0, 0, 24)
        };

        _pnlSettingsContainer = new Panel
        {
            Location = new Point(24, 0),
            Size = new Size(840, 948),
            BackColor = Color.Transparent
        };

        // 1. GÜVENLİK AYARLARI KARTI (Turuncu Başlık Bannerlı - SGK Renk Sıralamasına Uygun)
        var pnlAuth = new Panel
        {
            Location = new Point(0, 8),
            Size = new Size(840, 220),
            BackColor = Color.White
        };
        pnlAuth.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(245, 158, 11), 1.5f); // Amber / Turuncu #F59E0B
            e.Graphics.DrawRectangle(pen, 0, 0, pnlAuth.Width - 1, pnlAuth.Height - 1);
        };

        var pnlAuthBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(245, 158, 11) // Amber / Turuncu #F59E0B
        };

        var lblAuthTitle = new Label { Text = "🔐  Giriş ve Güvenlik Ayarları", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(14, 7), AutoSize = true };

        var pnlBadgeRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 5, 12, 0),
            Margin = Padding.Empty
        };

        _lblCurrentUserBadge = new Label
        {
            Text = "👤 Aktif Kullanıcı: admin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(245, 158, 11),
            BackColor = Color.White,
            AutoSize = true,
            Padding = new Padding(8, 3, 8, 3)
        };
        pnlBadgeRight.Controls.Add(_lblCurrentUserBadge);

        pnlAuthBanner.Controls.AddRange([lblAuthTitle, pnlBadgeRight]);

        var lblCurPass = new Label { Text = "Mevcut Şifre *", Location = new Point(20, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtCurrentPassword = new TextBox { Location = new Point(20, 72), Width = 380, UseSystemPasswordChar = true };

        var lblNewUser = new Label { Text = "Yeni Kullanıcı Adı", Location = new Point(430, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtNewUsername = new TextBox { Location = new Point(430, 72), Width = 380, Text = "admin" };

        var lblNewPass = new Label { Text = "Yeni Şifre", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtNewPassword = new TextBox { Location = new Point(20, 128), Width = 380, UseSystemPasswordChar = true };

        var lblConfPass = new Label { Text = "Yeni Şifre (Tekrar)", Location = new Point(430, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtConfirmPassword = new TextBox { Location = new Point(430, 128), Width = 380, UseSystemPasswordChar = true };

        var btnSaveAuth = new Button
        {
            Text = "💾 Giriş Bilgilerini Güncelle",
            Location = new Point(590, 170),
            Size = new Size(220, 34),
            BackColor = Color.FromArgb(245, 158, 11),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSaveAuth.FlatAppearance.BorderSize = 0;
        btnSaveAuth.Click += BtnSaveAuth_Click;

        var btnManageUsers = new Button
        {
            Text = "👥 Tüm Kullanıcıları ve Yetkileri Yönet",
            Location = new Point(20, 170),
            Size = new Size(280, 34),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = _onNavigate != null && MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin
        };
        btnManageUsers.FlatAppearance.BorderSize = 0;
        btnManageUsers.Click += (s, e) => _onNavigate?.Invoke("settings-users");

        _chkTeacherCanEdit = new CheckBox
        {
            Text = "Öğretmenler Veri Değişikliği Yapabilsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            Location = new Point(310, 178),
            AutoSize = true,
            Cursor = Cursors.Hand,
            Visible = MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin
        };

        pnlAuth.Controls.AddRange([pnlAuthBanner, lblCurPass, _txtCurrentPassword, lblNewUser, _txtNewUsername, lblNewPass, _txtNewPassword, lblConfPass, _txtConfirmPassword, btnManageUsers, _chkTeacherCanEdit, btnSaveAuth]);
        _pnlSettingsContainer.Controls.Add(pnlAuth);

        // 2. KURUM VE DÖNEM BİLGİLERİ KARTI (Mavi Başlık Bannerlı)
        var pnlInst = new Panel
        {
            Location = new Point(0, 244),
            Size = new Size(840, 680),
            BackColor = Color.White
        };
        pnlInst.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(37, 99, 235), 1.5f); // #2563EB
            e.Graphics.DrawRectangle(pen, 0, 0, pnlInst.Width - 1, pnlInst.Height - 1);
        };

        var pnlInstBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(37, 99, 235) // Blue #2563EB
        };
        var lblInstTitle = new Label { Text = "🏫  Kurum ve Dönem Bilgileri (Resmi Form Başlığı)", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(14, 7), AutoSize = true };
        pnlInstBanner.Controls.Add(lblInstTitle);

        int iy = 50;

        // 1. Satır: Valilik / Kaymakamlık & İl / İlçe MEM
        var lblValilik = new Label { Text = "İl Valiliği / Kaymakamlık", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblDistrictMem = new Label { Text = "İl / İlçe Millî Eğitim Müdürlüğü", Location = new Point(430, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtValilik = new TextBox { Location = new Point(20, iy), Width = 380, PlaceholderText = "Örn: TOKAT VALİLİĞİ veya NİKSAR KAYMAKAMLIĞI" };
        _txtDistrictMem = new TextBox { Location = new Point(430, iy), Width = 380, PlaceholderText = "Örn: İlçe Millî Eğitim Müdürlüğü veya NİKSAR İLÇE MEM" };
        iy += 36;

        // 2. Satır: Okul Adı & İl / İlçe
        var lblSchool = new Label { Text = "Okul / Kurum Adı", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblCity = new Label { Text = "İl", Location = new Point(430, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblDistrict = new Label { Text = "İlçe", Location = new Point(630, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtSchoolName = new TextBox { Location = new Point(20, iy), Width = 380, PlaceholderText = "Örn: KIRKKIZLAR MESLEKİ VE TEKNİK ANADOLU LİSESİ" };
        _txtSchoolCity = new TextBox { Location = new Point(430, iy), Width = 180, PlaceholderText = "TOKAT" };
        _txtSchoolDistrict = new TextBox { Location = new Point(630, iy), Width = 180, PlaceholderText = "NİKSAR" };
        iy += 36;

        // 3. Satır: Okul Açık Adresi (Tam Genişlik)
        var lblAddr = new Label { Text = "Okul Açık Adresi", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtSchoolAddress = new TextBox { Location = new Point(20, iy), Width = 790, PlaceholderText = "Örn: Atatürk Mah. Cumhuriyet Cad. No:1" };
        iy += 36;

        // Eğitim Yılı / Dönem / Sayı No / Tarih
        var lblYear = new Label { Text = "Eğitim Öğretim Yılı 🔒 (Üst Menüden Seçilir)", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105) };
        var lblTerm = new Label { Text = "Dönem", Location = new Point(220, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblSayi = new Label { Text = "Resmi Yazı Sayı No", Location = new Point(430, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblDate = new Label { Text = "Yazı Tarihi", Location = new Point(640, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtAcademicYear = new TextBox { Location = new Point(20, iy), Width = 180, PlaceholderText = "2026-2027", ReadOnly = true, BackColor = Color.FromArgb(243, 244, 246) };
        _txtTerm = new TextBox { Location = new Point(220, iy), Width = 180, PlaceholderText = "1." };
        _txtSayiNo = new TextBox { Location = new Point(430, iy), Width = 190, PlaceholderText = "E-84125896-..." };
        _txtFormDate = new TextBox { Location = new Point(640, iy), Width = 170, PlaceholderText = "11/09/2026" };
        iy += 36;

        // Geçerlilik Tarihi / Hitap Ünvanı
        var lblValDate = new Label { Text = "Geçerlilik / Başlama Tarihi", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblTTitle = new Label { Text = "Öğretmen Alt Ünvanı (Hitap Kısmı)", Location = new Point(430, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtValidityDate = new TextBox { Location = new Point(20, iy), Width = 380, PlaceholderText = "15/09/2026" };
        _txtTeacherTitle = new TextBox { Location = new Point(430, iy), Width = 380, PlaceholderText = "Otomatik: [Öğretmenin Alanı] Öğretmeni" };
        iy += 36;

        // Müdür Yrd / Okul Müdürü
        var lblVice = new Label { Text = "Koordinatör Müdür Yardımcısı (İmza)", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblPrinc = new Label { Text = "Okul Müdürü (İmza Alanı)", Location = new Point(430, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtVicePrincipal = new TextBox { Location = new Point(20, iy), Width = 380 };
        _txtPrincipal = new TextBox { Location = new Point(430, iy), Width = 380 };
        iy += 36;

        // Görev Bildirim Metni
        var lblBodyTitle = new Label { Text = "📝 Görev Bildirim Formu Metni (Mevzuat ve Görevlendirme Paragrafı)", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtFormBody = new TextBox
        {
            Location = new Point(20, iy),
            Width = 790,
            Height = 65,
            Multiline = true,
            Font = new Font("Segoe UI", 8.5f),
            ScrollBars = ScrollBars.Vertical
        };
        iy += 76;

        // Azami Saat & Örgün Eğitim Başlama - Bitiş Tarihleri
        var lblHours = new Label { Text = "Öğretmen Başına Azami Haftalık Saat", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblOrgunStart = new Label { Text = "Eğitim Yılı Başlangıç Tarihi", Location = new Point(280, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblOrgunEnd = new Label { Text = "Eğitim Yılı Bitiş Tarihi", Location = new Point(540, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _numMaxHours = new NumericUpDown { Location = new Point(20, iy), Width = 240, Minimum = 1, Maximum = 40, Value = 16 };
        _dtpOrgunStart = new DateTimePicker { Location = new Point(280, iy), Width = 240, Format = DateTimePickerFormat.Short, Enabled = true };
        _dtpOrgunEnd = new DateTimePicker { Location = new Point(540, iy), Width = 270, Format = DateTimePickerFormat.Short, Enabled = true };
        iy += 38;

        // Yıl Bazlı Asgari Ücret ve SGK PEK Tanımları Paneli
        var grpWages = new GroupBox
        {
            Text = " 💰 Yıl Bazlı Asgari Ücret ve SGK PEK Tanımları ",
            Location = new Point(20, iy),
            Size = new Size(790, 265),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.Primary
        };

        var lblWageDesc = new Label
        {
            Text = "Farklı yıllara ait bordro ve e-bildirgelerde o yılın geçerli asgari ücreti esas alınır. Brüt asgari ücret 'Net / 0.85' formülüyle otomatik hesaplanır ve pasiftir.",
            Location = new Point(16, 22),
            Size = new Size(758, 20),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(107, 114, 128)
        };

        var lblYearInput = new Label { Text = "Yıl", Location = new Point(16, 46), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = UITheme.TextPrimary };
        var lblNetInput = new Label { Text = "Net Asgari Ücret (₺)", Location = new Point(126, 46), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = UITheme.TextPrimary };
        var lblGrossInput = new Label { Text = "Brüt Asgari Ücret (Net / 0.85 — Pasif/Otomatik)", Location = new Point(316, 46), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = UITheme.TextSecondary };

        _numWageYear = new NumericUpDown
        {
            Location = new Point(16, 68),
            Width = 95,
            Minimum = 2020,
            Maximum = 2040,
            Value = DateTime.Now.Year,
            Font = new Font("Segoe UI", 9f)
        };

        _txtWageNet = new TextBox
        {
            Location = new Point(126, 68),
            Width = 175,
            Font = new Font("Segoe UI", 9f),
            Text = "28.075,50 ₺"
        };

        _txtWageGross = new TextBox
        {
            Location = new Point(316, 68),
            Width = 240,
            Font = new Font("Segoe UI", 9f),
            ReadOnly = true,
            TabStop = false,
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(75, 85, 99),
            Text = "33.030,00 ₺"
        };

        // Otomatik hesaplama: Net değiştikçe Brüt = Net / 0.85
        _txtWageNet.TextChanged += (s, e) =>
        {
            var raw = _txtWageNet.Text.Replace("₺", "").Replace(".", "").Replace(",", ".").Trim();
            if (decimal.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var nWage) && nWage > 0)
            {
                decimal autoGross = YearlyMinimumWage.CalculateGrossFromNet(nWage);
                _txtWageGross.Text = autoGross.ToString("N2") + " ₺";
            }
            else
            {
                _txtWageGross.Text = "0,00 ₺";
            }
            _txtMinimumWage.Text = _txtWageNet.Text;
            _txtGrossMinimumWage.Text = _txtWageGross.Text;
        };

        _btnSaveWage = new Button
        {
            Text = "➕ Ekle / Güncelle",
            Location = new Point(570, 67),
            Size = new Size(204, 28),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnSaveWage.FlatAppearance.BorderSize = 0;
        _btnSaveWage.Click += BtnSaveWage_Click;

        // Mini DataGridView
        _dgvYearlyWages = new DataGridView
        {
            Location = new Point(16, 104),
            Size = new Size(758, 145),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextPrimary,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 26
        };
        _dgvYearlyWages.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(243, 244, 246);
        _dgvYearlyWages.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(55, 65, 81);
        _dgvYearlyWages.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        _dgvYearlyWages.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colYear",
            HeaderText = "Yıl",
            Width = 85,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        _dgvYearlyWages.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colNet",
            HeaderText = "Net Asgari Ücret",
            Width = 180,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(5, 150, 105) }
        });
        _dgvYearlyWages.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colGross",
            HeaderText = "Brüt Asgari Ücret (Net / 0.85)",
            Width = 200,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(75, 85, 99) }
        });
        _dgvYearlyWages.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colDate",
            HeaderText = "Son Güncelleme",
            Width = 170,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _colWageDelete = new DataGridViewButtonColumn
        {
            Name = "colDelete",
            HeaderText = "İşlem",
            Text = "🗑️ Sil",
            UseColumnTextForButtonValue = true,
            Width = 90,
            FlatStyle = FlatStyle.Flat
        };
        _colWageDelete.DefaultCellStyle.ForeColor = Color.FromArgb(220, 38, 38);
        _colWageDelete.DefaultCellStyle.SelectionForeColor = Color.FromArgb(220, 38, 38);
        _dgvYearlyWages.Columns.Add(_colWageDelete);

        _dgvYearlyWages.CellContentClick += DgvYearlyWages_CellContentClick;
        _dgvYearlyWages.CellClick += DgvYearlyWages_CellClick;

        grpWages.Controls.AddRange([
            lblWageDesc,
            lblYearInput, lblNetInput, lblGrossInput,
            _numWageYear, _txtWageNet, _txtWageGross, _btnSaveWage,
            _dgvYearlyWages
        ]);

        iy += 272;

        _txtMinimumWage = new TextBox { Visible = false };
        _txtGrossMinimumWage = new TextBox { Visible = false };

        var btnSaveInst = new Button
        {
            Text = "💾 Kurum Ayarlarını Kaydet",
            Location = new Point(600, iy),
            Size = new Size(210, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSaveInst.FlatAppearance.BorderSize = 0;
        btnSaveInst.Click += BtnSaveInst_Click;

        pnlInst.Height = iy + 55;

        pnlInst.Controls.AddRange([
            pnlInstBanner,
            lblValilik, lblDistrictMem, _txtValilik, _txtDistrictMem,
            lblSchool, _txtSchoolName,
            lblCity, lblDistrict, _txtSchoolCity, _txtSchoolDistrict,
            lblAddr, _txtSchoolAddress,
            lblYear, lblTerm, lblSayi, lblDate, _txtAcademicYear, _txtTerm, _txtSayiNo, _txtFormDate,
            lblValDate, lblTTitle, _txtValidityDate, _txtTeacherTitle,
            lblVice, lblPrinc, _txtVicePrincipal, _txtPrincipal,
            lblBodyTitle, _txtFormBody,
            lblHours, lblOrgunStart, lblOrgunEnd, _numMaxHours, _dtpOrgunStart, _dtpOrgunEnd,
            grpWages,
            _txtMinimumWage, _txtGrossMinimumWage,
            btnSaveInst
        ]);
        _pnlSettingsContainer.Controls.Add(pnlInst);

        // 3. VERİTABANI GÜVENLİĞİ VE YEDEKLEME KARTI (Zümrüt Yeşili Bannerlı)
        var pnlBackup = new Panel
        {
            Location = new Point(0, pnlInst.Bottom + 16),
            Size = new Size(840, 155),
            BackColor = Color.White
        };
        pnlBackup.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(16, 185, 129), 1.5f); // Emerald green #10B981
            e.Graphics.DrawRectangle(pen, 0, 0, pnlBackup.Width - 1, pnlBackup.Height - 1);
        };

        var pnlBackupBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(16, 185, 129)
        };
        var lblBackupTitle = new Label
        {
            Text = "🛡️  Veritabanı Güvenliği, Otomatik Yedekleme ve Kurtarma",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 7),
            AutoSize = true
        };
        pnlBackupBanner.Controls.Add(lblBackupTitle);

        var lblBackupDesc = new Label
        {
            Text = "Veri güvenliğiniz için sistem her program kapanışında son 30 günün yedeklerini döngüsel olarak otomatik arşivler.\nİstediğiniz zaman güvenli anlık yedek alabilir, geçmiş bir yedeğe geri dönebilir veya veritabanını dışa aktarabilirsiniz.",
            Location = new Point(20, 48),
            Size = new Size(800, 36),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary
        };

        var lblAutoBadge = new Label
        {
            Text = "🟢 Otomatik Kapanış Yedeği: Aktif",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(5, 150, 105),
            BackColor = Color.FromArgb(236, 253, 245),
            Location = new Point(20, 100),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4)
        };

        var btnQuickBackup = new Button
        {
            Text = "⚡ Hızlı Yedek Al",
            Location = new Point(310, 95),
            Size = new Size(140, 36),
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(55, 65, 81),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnQuickBackup.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        btnQuickBackup.Click += async (s, e) =>
        {
            if (_backupService == null) return;
            try
            {
                btnQuickBackup.Enabled = false;
                btnQuickBackup.Text = "Alınıyor...";
                var path = await _backupService.CreateBackupAsync();
                MessageBox.Show($"Veritabanı yedeği başarıyla oluşturuldu:\n\n{path}", "Yedek Alındı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yedek alınırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnQuickBackup.Enabled = true;
                btnQuickBackup.Text = "⚡ Hızlı Yedek Al";
            }
        };

        var btnManageBackups = new Button
        {
            Text = "⚙️ Yedek Yönetimi & Kurtarma",
            Location = new Point(460, 95),
            Size = new Size(205, 36),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnManageBackups.FlatAppearance.BorderSize = 0;
        btnManageBackups.Click += (s, e) =>
        {
            if (_backupService == null)
            {
                MessageBox.Show("Yedekleme servisi yüklenemedi.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using var dlg = new DatabaseBackupDialog(_backupService, () => LoadSettingsAsync());
            dlg.ShowDialog(this);
        };

        var btnResetDatabase = new Button
        {
            Text = "⚠️ Verileri Sıfırla",
            Location = new Point(675, 95),
            Size = new Size(145, 36),
            BackColor = Color.FromArgb(254, 242, 242),
            ForeColor = Color.FromArgb(185, 28, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnResetDatabase.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
        btnResetDatabase.Click += async (s, e) =>
        {
            if (_backupService == null)
            {
                MessageBox.Show("Yedekleme ve veritabanı servisi yüklenemedi.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new DatabaseResetDialog(_backupService, _userRepo);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                LoadSettingsAsync();
            }
        };

        pnlBackup.Controls.AddRange([pnlBackupBanner, lblBackupDesc, lblAutoBadge, btnQuickBackup, btnManageBackups, btnResetDatabase]);
        _pnlSettingsContainer.Controls.Add(pnlBackup);

        // 4. LİSANS VE AKTİVASYON YÖNETİMİ (İndigo Bannerlı)
        _pnlLicense = new Panel
        {
            Location = new Point(0, pnlBackup.Bottom + 16),
            Size = new Size(840, 110),
            BackColor = Color.White
        };
        _pnlLicense.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(99, 102, 241), 1.5f); // Indigo #6366F1
            e.Graphics.DrawRectangle(pen, 0, 0, _pnlLicense.Width - 1, _pnlLicense.Height - 1);
        };

        var pnlLicBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(79, 70, 229) // Indigo-600 #4F46E5
        };
        var lblLicTitle = new Label
        {
            Text = "🔑  Yazılım Lisansı ve Aktivasyon Yönetimi",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 7),
            AutoSize = true
        };
        pnlLicBanner.Controls.Add(lblLicTitle);

        // Satır 1: Durum Rozeti ve Bitiş Tarihi (Lisanslı iken SADECE bunlar görünür)
        _lblLicStatusBadge = new Label
        {
            Text = "Durum Kontrol Ediliyor...",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(20, 52),
            AutoSize = true,
            Padding = new Padding(10, 5, 10, 5)
        };

        _lblLicEnd = new Label
        {
            Text = "Bitiş Tarihi:",
            Location = new Point(320, 56),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        _lblLicEndDate = new Label
        {
            Text = "-",
            Location = new Point(405, 56),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };

        _lblLicInfo = new Label
        {
            Text = "Lisanslı Kurum:",
            Location = new Point(530, 56),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Visible = false
        };
        _lblLicSchool = new Label
        {
            Text = "-",
            Location = new Point(630, 56),
            Size = new Size(190, 20),
            Font = new Font("Segoe UI", 8.5f),
            Visible = false
        };

        // Uyarı Paneli (Son 30 gün kaldığında veya Demo/Süresi Dolduğunda görünür)
        _pnlRenewalNotice = new Panel
        {
            Location = new Point(20, 92),
            Size = new Size(800, 44),
            BackColor = Color.FromArgb(254, 243, 199),
            Visible = false
        };
        _pnlRenewalNotice.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(245, 158, 11), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, _pnlRenewalNotice.Width - 1, _pnlRenewalNotice.Height - 1);
        };
        _lblRenewalNoticeText = new Label
        {
            Text = "⚠️ Lisansınızın kullanım süresi sona ermektedir. (İletişim: 3308pro@gmail.com)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14),
            Location = new Point(12, 13),
            AutoSize = true
        };
        _pnlRenewalNotice.Controls.Add(_lblRenewalNoticeText);

        // Satır 2: Makine Kodu (HWID) ve Yenileme Butonu (Son 30 gün veya Demo durumunda görünür)
        _lblHwidTit = new Label
        {
            Text = "Bilgisayar Makine Kodu (HWID):",
            Location = new Point(20, 148),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Visible = false
        };
        _txtLicHwid = new TextBox
        {
            Location = new Point(20, 170),
            Width = 360,
            ReadOnly = true,
            Font = new Font("Consolas", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(243, 244, 246),
            Visible = false
        };
        _btnCopyHwid = new Button
        {
            Text = "📋 Kopyala",
            Location = new Point(386, 168),
            Size = new Size(95, 27),
            BackColor = Color.FromArgb(243, 244, 246),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnCopyHwid.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        _btnCopyHwid.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtLicHwid.Text))
            {
                Clipboard.SetText(_txtLicHwid.Text);
                MessageBox.Show("Makine kodu panoya kopyalandı.", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        _btnOpenActivation = new Button
        {
            Text = "🔑 Yeni Lisans Gir / Yenile...",
            Location = new Point(510, 167),
            Size = new Size(250, 30),
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnOpenActivation.FlatAppearance.BorderSize = 0;
        _btnOpenActivation.Click += (s, e) =>
        {
            if (_licenseService == null) return;
            using var dlg = new LicenseActivationDialog(_licenseService, () => _ = LoadLicenseInfoAsync());
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _ = LoadLicenseInfoAsync();
            }
        };

        _pnlLicense.Controls.AddRange([
            pnlLicBanner, _lblLicStatusBadge, _lblLicEnd, _lblLicEndDate,
            _lblLicInfo, _lblLicSchool, _pnlRenewalNotice,
            _lblHwidTit, _txtLicHwid, _btnCopyHwid, _btnOpenActivation
        ]);
        _pnlSettingsContainer.Controls.Add(_pnlLicense);

        _pnlSettingsContainer.Height = _pnlLicense.Bottom + 30;
        _pnlBody.Controls.Add(_pnlSettingsContainer);

        _pnlBody.Resize += (s, e) => CenterSettings();
        pnlHeader.Resize += (s, e) => CenterSettings();

        Controls.Add(_pnlBody);
        Controls.Add(pnlHeader);

        CenterSettings();
    }

    private void CenterSettings()
    {
        if (_pnlBody == null || _pnlBody.ClientSize.Width <= 0 || _pnlSettingsContainer == null)
            return;

        int scrollY = -_pnlBody.AutoScrollPosition.Y;
        _pnlBody.AutoScrollPosition = new Point(0, 0);

        int targetX = Math.Max(24, (_pnlBody.ClientSize.Width - _pnlSettingsContainer.Width) / 2);
        _pnlSettingsContainer.Location = new Point(targetX, 0);

        if (_pnlHeaderContent != null)
        {
            _pnlHeaderContent.Location = new Point(targetX, 4);
        }

        if (scrollY > 0)
        {
            _pnlBody.AutoScrollPosition = new Point(0, scrollY);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CenterSettings();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            CenterSettings();
        }
    }

    private async void LoadSettingsAsync()
    {
        _settings = await _settingRepo.GetAppSettingsAsync();

        var currentUser = MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser;
        if (currentUser != null)
        {
            _lblCurrentUserBadge.Text = $"👤 Aktif: {currentUser.FullName} ({currentUser.Username})";
            _txtNewUsername.Text = currentUser.Username;
            _txtNewUsername.Enabled = currentUser.IsAdmin;
        }
        else
        {
            _lblCurrentUserBadge.Text = $"👤 Aktif: {_settings.AdminUsername}";
            _txtNewUsername.Text = _settings.AdminUsername;
        }

        _chkTeacherCanEdit.Checked = _settings.TeacherCanEditData;
        _txtValilik.Text = _settings.DistrictGovernorate;
        _txtDistrictMem.Text = _settings.DistrictMem;
        _txtSchoolName.Text = _settings.SchoolName;
        _txtSchoolAddress.Text = _settings.SchoolAddress;
        _txtSchoolCity.Text = _settings.SchoolCity;
        _txtSchoolDistrict.Text = _settings.SchoolDistrict;
        _txtAcademicYear.Text = _settings.AcademicYear;
        _txtTerm.Text = _settings.Term;
        _txtSayiNo.Text = _settings.OfficialLetterNumber;
        _txtFormDate.Text = _settings.LetterDate;
        _txtValidityDate.Text = _settings.ValidityDate;
        _txtTeacherTitle.Text = _settings.TeacherTitle;
        _txtVicePrincipal.Text = _settings.VicePrincipalName;
        _txtPrincipal.Text = _settings.PrincipalName;
        _txtFormBody.Text = _settings.FormBodyText;
        _numMaxHours.Value = Math.Max(1, _settings.MaxWeeklyCoordinatorHours);
        _txtMinimumWage.Text = _settings.StatutoryMinimumWage.ToString("N2") + " ₺";
        decimal grossDisplay = _settings.GrossMinimumWage > 0 ? _settings.GrossMinimumWage : _settings.EffectiveGrossMinimumWage;
        _txtGrossMinimumWage.Text = grossDisplay.ToString("N2") + " ₺";

        if (DateTime.TryParse(_settings.OrgunStartDate, out var sDate)) _dtpOrgunStart.Value = sDate;
        if (DateTime.TryParse(_settings.OrgunEndDate, out var eDate)) _dtpOrgunEnd.Value = eDate;

        await LoadYearlyWagesAsync();
        await LoadLicenseInfoAsync();
    }

    private async Task LoadLicenseInfoAsync()
    {
        if (_licenseService == null) return;

        try
        {
            _txtLicHwid.Text = _licenseService.MachineCode;
            var lic = await _licenseService.RefreshLicenseStatusAsync();

            if (lic.IsActive)
            {
                if (lic.RemainingDays > 30)
                {
                    // LİSANSLI VE 30 GÜNDEN FAZLA: Sade, sadeleştirilmiş görünüm (Sadece Lisanslı ve Bitiş Tarihi)
                    _lblLicStatusBadge.Text = "🟢 LİSANSLI";
                    _lblLicStatusBadge.ForeColor = Color.FromArgb(5, 150, 105);
                    _lblLicStatusBadge.BackColor = Color.FromArgb(236, 253, 245);

                    _lblLicEnd.Text = "Bitiş Tarihi:";
                    _lblLicEndDate.Text = lic.EndDate.HasValue ? lic.EndDate.Value.ToString("dd.MM.yyyy") : "-";

                    // Kullanıcı talebi: Başka hiçbir bilgi ve tablo görünmez
                    _lblLicInfo.Visible = false;
                    _lblLicSchool.Visible = false;
                    _pnlRenewalNotice.Visible = false;
                    _lblHwidTit.Visible = false;
                    _txtLicHwid.Visible = false;
                    _btnCopyHwid.Visible = false;
                    _btnOpenActivation.Visible = false;

                    _pnlLicense.Height = 105;
                }
                else
                {
                    // LİSANSLI VE SON 30 GÜN (1 AY KALA): Yenileme modu aktif
                    _lblLicStatusBadge.Text = $"⚠️ LİSANS BİTİYOR ({lic.RemainingDays} GÜN KALDI)";
                    _lblLicStatusBadge.ForeColor = Color.FromArgb(180, 83, 9); // amber-700
                    _lblLicStatusBadge.BackColor = Color.FromArgb(254, 243, 199); // amber-100

                    _lblLicEnd.Text = "Bitiş Tarihi:";
                    _lblLicEndDate.Text = lic.EndDate.HasValue ? lic.EndDate.Value.ToString("dd.MM.yyyy") : "-";

                    _lblLicInfo.Visible = false;
                    _lblLicSchool.Visible = false;

                    // Uyarı bandını ve yenileme kontrollerini göster
                    _pnlRenewalNotice.Visible = true;
                    _pnlRenewalNotice.BackColor = Color.FromArgb(254, 243, 199);
                    _lblRenewalNoticeText.ForeColor = Color.FromArgb(146, 64, 14);
                    _lblRenewalNoticeText.Text = $"⚠️ Lisansınızın kullanım süresi {lic.EndDate:dd.MM.yyyy} tarihinde ({lic.RemainingDays} gün sonra) sona erecektir. Kesintisiz kullanım için lisansınızı yenileyiniz. (İletişim: 3308pro@gmail.com)";

                    _lblHwidTit.Visible = true;
                    _txtLicHwid.Visible = true;
                    _btnCopyHwid.Visible = true;
                    _btnOpenActivation.Visible = true;
                    _btnOpenActivation.Text = "🔑 Lisansı Yenile (Yeni Anahtar Gir)...";
                    _btnOpenActivation.BackColor = Color.FromArgb(217, 119, 6); // amber-600

                    _pnlLicense.Height = 215;
                }
            }
            else
            {
                // LİSANSSIZ / DEMO / SÜRESİ DOLDU
                if (lic.Status == MebKoordinatorluk.Core.Enums.LicenseStatus.Expired)
                {
                    _lblLicStatusBadge.Text = "⚠️ SÜRESİ DOLDU (DEMO MOD)";
                    _lblLicStatusBadge.ForeColor = Color.FromArgb(220, 38, 38);
                    _lblLicStatusBadge.BackColor = Color.FromArgb(254, 242, 242);

                    _lblRenewalNoticeText.Text = "⚠️ Lisans kullanım süreniz dolmuştur. Tam sürüm özelliklerini kullanmaya devam etmek için lisansınızı yenileyiniz. (İletişim: 3308pro@gmail.com)";
                    _pnlRenewalNotice.BackColor = Color.FromArgb(254, 242, 242);
                    _lblRenewalNoticeText.ForeColor = Color.FromArgb(185, 28, 28);
                }
                else if (lic.Status == MebKoordinatorluk.Core.Enums.LicenseStatus.TamperedClock)
                {
                    _lblLicStatusBadge.Text = "⛔ TARİH HİLESİ (KİLİTLİ)";
                    _lblLicStatusBadge.ForeColor = Color.FromArgb(185, 28, 28);
                    _lblLicStatusBadge.BackColor = Color.FromArgb(254, 242, 242);

                    _lblRenewalNoticeText.Text = "⛔ Sistem saatinde geriye dönük değişiklik algılandı. Lisans kilitlendi. Düzeltme için: 3308pro@gmail.com";
                    _pnlRenewalNotice.BackColor = Color.FromArgb(254, 242, 242);
                    _lblRenewalNoticeText.ForeColor = Color.FromArgb(185, 28, 28);
                }
                else
                {
                    _lblLicStatusBadge.Text = "⚠️ DEMO SÜRÜM (KISITLI)";
                    _lblLicStatusBadge.ForeColor = Color.FromArgb(217, 119, 6);
                    _lblLicStatusBadge.BackColor = Color.FromArgb(254, 243, 199);

                    _lblRenewalNoticeText.Text = "ℹ️ Sistem deneme (demo) modunda çalışmaktadır. Lisans satın almak ve tam sürüme geçmek için iletişime geçiniz: 3308pro@gmail.com";
                    _pnlRenewalNotice.BackColor = Color.FromArgb(254, 243, 199);
                    _lblRenewalNoticeText.ForeColor = Color.FromArgb(146, 64, 14);
                }

                _lblLicEnd.Text = "Bitiş Tarihi:";
                _lblLicEndDate.Text = lic.EndDate.HasValue ? lic.EndDate.Value.ToString("dd.MM.yyyy") : "-";

                _lblLicInfo.Visible = false;
                _lblLicSchool.Visible = false;

                _pnlRenewalNotice.Visible = true;
                _lblHwidTit.Visible = true;
                _txtLicHwid.Visible = true;
                _btnCopyHwid.Visible = true;
                _btnOpenActivation.Visible = true;
                _btnOpenActivation.Text = "🔑 Lisans Anahtarı Gir / Aktifleştir...";
                _btnOpenActivation.BackColor = Color.FromArgb(79, 70, 229); // indigo-600

                _pnlLicense.Height = 215;
            }

            _pnlSettingsContainer.Height = _pnlLicense.Bottom + 30;
        }
        catch
        {
            // safe ignore
        }
    }

    private async void BtnSaveAuth_Click(object? sender, EventArgs e)
    {
        var curPass = _txtCurrentPassword.Text.Trim();
        var newPass = _txtNewPassword.Text.Trim();
        var confPass = _txtConfirmPassword.Text.Trim();
        var newUser = _txtNewUsername.Text.Trim();

        var currentUser = MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser;
        if (currentUser != null && _userRepo != null)
        {
            var freshUser = await _userRepo.GetByIdAsync(currentUser.Id);
            if (freshUser == null || !MebKoordinatorluk.Core.Security.PasswordSecurity.VerifyPassword(curPass, freshUser.PasswordHash, freshUser.PasswordSalt))
            {
                MessageBox.Show("Mevcut şifreniz hatalı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _txtCurrentPassword.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(newPass))
            {
                if (newPass.Length < 4)
                {
                    MessageBox.Show("Yeni şifre en az 4 karakter olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (newPass != confPass)
                {
                    MessageBox.Show("Yeni şifreler birbiriyle uyuşmuyor!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                await _userRepo.ChangePasswordAsync(freshUser.Id, newPass);
            }

            if (currentUser.IsAdmin)
            {
                if (!string.IsNullOrWhiteSpace(newUser) && !newUser.Equals(freshUser.Username, StringComparison.OrdinalIgnoreCase))
                {
                    freshUser.Username = newUser;
                    await _userRepo.UpdateAsync(freshUser);
                    currentUser.Username = newUser;
                }
                _settings.TeacherCanEditData = _chkTeacherCanEdit.Checked;
                MebKoordinatorluk.Core.Security.CurrentUserSession.TeacherCanEditData = _chkTeacherCanEdit.Checked;
                _settings.AdminUsername = freshUser.Username;
                if (!string.IsNullOrWhiteSpace(newPass)) _settings.AdminPassword = newPass;
                await _settingRepo.SaveAppSettingsAsync(_settings);
            }

            _txtCurrentPassword.Clear();
            _txtNewPassword.Clear();
            _txtConfirmPassword.Clear();
            _lblCurrentUserBadge.Text = $"👤 Aktif: {freshUser.FullName} ({freshUser.Username})";
            MessageBox.Show("Güvenlik ve giriş bilgileri başarıyla güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            if (curPass != _settings.AdminPassword)
            {
                MessageBox.Show("Mevcut şifreniz hatalı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _txtCurrentPassword.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(newPass))
            {
                if (newPass.Length < 4)
                {
                    MessageBox.Show("Yeni şifre en az 4 karakter olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (newPass != confPass)
                {
                    MessageBox.Show("Yeni şifreler birbiriyle uyuşmuyor!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _settings.AdminPassword = newPass;
            }

            if (!string.IsNullOrWhiteSpace(newUser))
            {
                _settings.AdminUsername = newUser;
            }

            await _settingRepo.SaveAppSettingsAsync(_settings);
            _txtCurrentPassword.Clear();
            _txtNewPassword.Clear();
            _txtConfirmPassword.Clear();
            _lblCurrentUserBadge.Text = $"Aktif Kullanıcı: {_settings.AdminUsername}";

            MessageBox.Show("Güvenlik ve giriş bilgileri başarıyla güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void BtnSaveInst_Click(object? sender, EventArgs e)
    {
        _settings.DistrictGovernorate = _txtValilik.Text.Trim();
        _settings.DistrictMem = _txtDistrictMem.Text.Trim();
        _settings.SchoolName = _txtSchoolName.Text.Trim();
        _settings.SchoolAddress = _txtSchoolAddress.Text.Trim();
        _settings.SchoolCity = _txtSchoolCity.Text.Trim();
        _settings.SchoolDistrict = _txtSchoolDistrict.Text.Trim();
        _settings.AcademicYear = _txtAcademicYear.Text.Trim();
        _settings.Term = _txtTerm.Text.Trim();
        _settings.OfficialLetterNumber = _txtSayiNo.Text.Trim();
        _settings.LetterDate = _txtFormDate.Text.Trim();
        _settings.ValidityDate = _txtValidityDate.Text.Trim();
        _settings.TeacherTitle = _txtTeacherTitle.Text.Trim();
        _settings.VicePrincipalName = _txtVicePrincipal.Text.Trim();
        _settings.PrincipalName = _txtPrincipal.Text.Trim();
        _settings.FormBodyText = _txtFormBody.Text.Trim();
        _settings.MaxWeeklyCoordinatorHours = (int)_numMaxHours.Value;

        var wageRaw = _txtWageNet.Text.Replace("₺", "").Replace(".", "").Replace(",", ".").Trim();
        if (decimal.TryParse(wageRaw, System.Globalization.CultureInfo.InvariantCulture, out var wage) && wage > 0)
        {
            _settings.StatutoryMinimumWage = wage;
            _settings.GrossMinimumWage = YearlyMinimumWage.CalculateGrossFromNet(wage);
        }

        _settings.OrgunStartDate = _dtpOrgunStart.Value.ToString("yyyy-MM-dd");
        _settings.OrgunEndDate = _dtpOrgunEnd.Value.ToString("yyyy-MM-dd");

        await _settingRepo.SaveAppSettingsAsync(_settings);
        MessageBox.Show("Kurum ve dönem ayarları başarıyla kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task LoadYearlyWagesAsync()
    {
        var list = (await _settingRepo.GetYearlyMinimumWagesAsync()).OrderByDescending(w => w.Year).ToList();
        _dgvYearlyWages.Rows.Clear();
        foreach (var w in list)
        {
            int idx = _dgvYearlyWages.Rows.Add(
                w.Year.ToString(),
                w.NetMinimumWage.ToString("N2") + " ₺",
                w.GrossMinimumWage.ToString("N2") + " ₺",
                w.UpdatedDate ?? "-",
                "🗑️ Sil"
            );
            _dgvYearlyWages.Rows[idx].Tag = w;
        }

        var activeWage = list.FirstOrDefault(w => w.Year == DateTime.Now.Year) ?? list.FirstOrDefault();
        if (activeWage != null)
        {
            _numWageYear.Value = activeWage.Year;
            _txtWageNet.Text = activeWage.NetMinimumWage.ToString("N2") + " ₺";
            _txtWageGross.Text = activeWage.GrossMinimumWage.ToString("N2") + " ₺";
            _txtMinimumWage.Text = _txtWageNet.Text;
            _txtGrossMinimumWage.Text = _txtWageGross.Text;
        }
    }

    private async void BtnSaveWage_Click(object? sender, EventArgs e)
    {
        int year = (int)_numWageYear.Value;
        var raw = _txtWageNet.Text.Replace("₺", "").Replace(".", "").Replace(",", ".").Trim();
        if (!decimal.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var net) || net <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir Net Asgari Ücret tutarı giriniz!", "Hatalı Tutar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtWageNet.Focus();
            return;
        }

        decimal gross = YearlyMinimumWage.CalculateGrossFromNet(net);
        _txtWageGross.Text = gross.ToString("N2") + " ₺";

        var wage = new YearlyMinimumWage
        {
            Year = year,
            NetMinimumWage = net,
            GrossMinimumWage = gross,
            UpdatedDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
        };

        await _settingRepo.SaveYearlyMinimumWageAsync(wage);
        await LoadYearlyWagesAsync();

        MessageBox.Show($"{year} yılı asgari ücreti kaydedildi:\n\nNet: {net:N2} ₺\nBrüt (Net / 0.85): {gross:N2} ₺", "Kayıt Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void DgvYearlyWages_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _colWageDelete.Index) return;

        if (_dgvYearlyWages.Rows.Count <= 1)
        {
            MessageBox.Show("Sistemde en az bir yıl için asgari ücret tanımı bulunmalıdır!\nBu kayıt silinemez.",
                "Güvenlik Uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_dgvYearlyWages.Rows[e.RowIndex].Tag is YearlyMinimumWage wage)
        {
            var confirm = MessageBox.Show(
                $"{wage.Year} yılı asgari ücret tanımını ({wage.NetMinimumWage:N2} ₺) silmek istediğinize emin misiniz?",
                "Asgari Ücret Kaydı Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                var deleted = await _settingRepo.DeleteYearlyMinimumWageAsync(wage.Year);
                if (deleted)
                {
                    await LoadYearlyWagesAsync();
                    MessageBox.Show($"{wage.Year} yılı asgari ücret kaydı silindi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }

    private void DgvYearlyWages_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex == _colWageDelete.Index) return;

        if (_dgvYearlyWages.Rows[e.RowIndex].Tag is YearlyMinimumWage wage)
        {
            _numWageYear.Value = wage.Year;
            _txtWageNet.Text = wage.NetMinimumWage.ToString("N2") + " ₺";
            _txtWageGross.Text = wage.GrossMinimumWage.ToString("N2") + " ₺";
        }
    }
}
