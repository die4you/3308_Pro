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


    // Institution Form
    private TextBox _txtValilik = null!;
    private TextBox _txtDistrictMem = null!;
    private TextBox _txtKurumKodu = null!;
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
    private DateTimePicker _dtpMidterm1Start = null!;
    private DateTimePicker _dtpMidterm1End = null!;
    private DateTimePicker _dtpMidterm2Start = null!;
    private DateTimePicker _dtpMidterm2End = null!;

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

        // 1. KURUM VE DÖNEM BİLGİLERİ KARTI (Mavi Başlık Bannerlı)
        var pnlInst = new Panel
        {
            Location = new Point(0, 8),
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

        // 2. Satır: Kurum Kodu, Okul Adı & İl / İlçe
        var lblKurumKodu = new Label { Text = "Kurum Kodu", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblSchool = new Label { Text = "Okul / Kurum Resmi Adı", Location = new Point(165, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblCity = new Label { Text = "İl", Location = new Point(560, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblDistrict = new Label { Text = "İlçe", Location = new Point(690, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _txtKurumKodu = new TextBox { Location = new Point(20, iy), Width = 130, PlaceholderText = "Örn: 763421" };
        _txtSchoolName = new TextBox { Location = new Point(165, iy), Width = 380, PlaceholderText = "Örn: KIRKKIZLAR MESLEKİ VE TEKNİK ANADOLU LİSESİ" };
        _txtSchoolCity = new TextBox { Location = new Point(560, iy), Width = 115, PlaceholderText = "TOKAT" };
        _txtSchoolDistrict = new TextBox { Location = new Point(690, iy), Width = 120, PlaceholderText = "NİKSAR" };
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

        // 1. ve 2. Ara Tatil Tarihleri (Örgün Eğitim)
        var lblMidterm1Start = new Label { Text = "1. Ara Tatil Başlangıç", Location = new Point(20, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblMidterm1End = new Label { Text = "1. Ara Tatil Bitiş", Location = new Point(220, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblMidterm2Start = new Label { Text = "2. Ara Tatil Başlangıç", Location = new Point(430, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var lblMidterm2End = new Label { Text = "2. Ara Tatil Bitiş", Location = new Point(630, iy), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        iy += 20;
        _dtpMidterm1Start = new DateTimePicker { Location = new Point(20, iy), Width = 185, Format = DateTimePickerFormat.Short, Enabled = true };
        _dtpMidterm1End = new DateTimePicker { Location = new Point(220, iy), Width = 185, Format = DateTimePickerFormat.Short, Enabled = true };
        _dtpMidterm2Start = new DateTimePicker { Location = new Point(430, iy), Width = 185, Format = DateTimePickerFormat.Short, Enabled = true };
        _dtpMidterm2End = new DateTimePicker { Location = new Point(630, iy), Width = 180, Format = DateTimePickerFormat.Short, Enabled = true };
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
            lblKurumKodu, _txtKurumKodu, lblSchool, _txtSchoolName,
            lblCity, lblDistrict, _txtSchoolCity, _txtSchoolDistrict,
            lblAddr, _txtSchoolAddress,
            lblYear, lblTerm, lblSayi, lblDate, _txtAcademicYear, _txtTerm, _txtSayiNo, _txtFormDate,
            lblValDate, lblTTitle, _txtValidityDate, _txtTeacherTitle,
            lblVice, lblPrinc, _txtVicePrincipal, _txtPrincipal,
            lblBodyTitle, _txtFormBody,
            lblHours, lblOrgunStart, lblOrgunEnd, _numMaxHours, _dtpOrgunStart, _dtpOrgunEnd,
            lblMidterm1Start, lblMidterm1End, lblMidterm2Start, lblMidterm2End,
            _dtpMidterm1Start, _dtpMidterm1End, _dtpMidterm2Start, _dtpMidterm2End,
            grpWages,
            _txtMinimumWage, _txtGrossMinimumWage,
            btnSaveInst
        ]);
        _pnlSettingsContainer.Controls.Add(pnlInst);

        // 2. VERİTABANI GÜVENLİĞİ VE YEDEKLEME KARTI (Zümrüt Yeşili Bannerlı)
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

        _pnlSettingsContainer.Height = pnlBackup.Bottom + 30;
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

        _txtValilik.Text = _settings.DistrictGovernorate;
        _txtDistrictMem.Text = _settings.DistrictMem;
        _txtKurumKodu.Text = _settings.KurumKodu;
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

        if (DateTime.TryParse(_settings.FirstMidtermStartDate, out var m1Start)) _dtpMidterm1Start.Value = m1Start;
        if (DateTime.TryParse(_settings.FirstMidtermEndDate, out var m1End)) _dtpMidterm1End.Value = m1End;
        if (DateTime.TryParse(_settings.SecondMidtermStartDate, out var m2Start)) _dtpMidterm2Start.Value = m2Start;
        if (DateTime.TryParse(_settings.SecondMidtermEndDate, out var m2End)) _dtpMidterm2End.Value = m2End;

        await LoadYearlyWagesAsync();
    }

    private async void BtnSaveInst_Click(object? sender, EventArgs e)
    {
        _settings.DistrictGovernorate = _txtValilik.Text.Trim();
        _settings.DistrictMem = _txtDistrictMem.Text.Trim();
        _settings.KurumKodu = _txtKurumKodu.Text.Trim();
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

        _settings.FirstMidtermStartDate = _dtpMidterm1Start.Value.ToString("yyyy-MM-dd");
        _settings.FirstMidtermEndDate = _dtpMidterm1End.Value.ToString("yyyy-MM-dd");
        _settings.SecondMidtermStartDate = _dtpMidterm2Start.Value.ToString("yyyy-MM-dd");
        _settings.SecondMidtermEndDate = _dtpMidterm2End.Value.ToString("yyyy-MM-dd");

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
