using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ClosedXML.Excel;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;
using MebKoordinatorluk.UI.Views;

namespace MebKoordinatorluk.UI;

public class MainForm : Form
{
    private readonly ITeacherRepository _teacherRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly IAddressGroupRepository _groupRepo;
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IDocumentTrackingRepository _docRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly IFeeReceiptRepository _receiptRepo;
    private readonly IUserRepository _userRepo;
    private readonly IDatabaseBackupService _backupService;
    private readonly ISgkCrossCheckService _crossCheckService;
    private readonly ILicenseService _licenseService;
    private readonly IAcademicYearRepository _academicYearRepo;
    private readonly MebKoordinatorluk.Data.Database.DatabaseConfig _dbConfig;
    private readonly IAcademicYearRolloverService _rolloverService;
    private readonly MebKoordinatorluk.Data.Database.DatabaseInitializer _dbInitializer;

    // Layout Containers
    private Panel _pnlHeader = null!;
    private Panel _pnlSidebar = null!;
    private FlowLayoutPanel _flpSidebarItems = null!;
    private Panel _pnlContent = null!;
    private Panel _pnlArchiveBanner = null!;
    private Label _lblArchiveBannerText = null!;
    private Button _btnReturnToActiveYear = null!;

    private Button _btnToggleSidebar = null!;
    private Button _btnLicenseBadge = null!;
    private Button _btnAcademicYear = null!;
    private bool _isSidebarExpanded = true;
    private const int SidebarExpandedWidth = 240;
    private const int SidebarCollapsedWidth = 60;

    private string _currentTab = "dashboard";
    private string? _openCategoryTitle;

    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, (Panel HeaderPanel, Label HeaderLabel, FlowLayoutPanel ItemsPanel)> _categories = new();

    // Views Cache
    private DashboardView? _dashboardView;
    private StudentsView? _studentsView;
    private StudentArchiveView? _studentArchiveView;
    private TeachersView? _teachersView;
    private EnterprisesView? _enterprisesView;
    private ClassesView? _classesView;
    private FieldsView? _fieldsView;
    private AddressGroupsView? _addressGroupsView;
    private DistributionView? _distributionView;
    private FormsView? _formsView;
    private AttendanceView? _attendanceView;
    private DocumentsView? _documentsView;
    private CoordinatorDocsView? _coordinatorDocsView;
    private FeesView? _feesView;
    private SettingsView? _settingsView;
    private InsuranceSettingsView? _insuranceSettingsView;
    private UsersView? _usersView;
    private CalendarTasksManagementView? _calendarTasksView;
    private ContactSupportView? _contactSupportView;
    private readonly ICalendarTaskRepository _calendarTaskRepo;
    private readonly Dictionary<string, InsuranceOpsView> _insuranceOpsViews = new();

    public MainForm(
        ITeacherRepository teacherRepo,
        IEnterpriseRepository enterpriseRepo,
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        IAddressGroupRepository groupRepo,
        IAttendanceRepository attendanceRepo,
        IDocumentTrackingRepository docRepo,
        ISettingRepository settingRepo,
        IFeeReceiptRepository receiptRepo,
        IUserRepository userRepo,
        IDatabaseBackupService backupService,
        ISgkCrossCheckService crossCheckService,
        ILicenseService licenseService,
        IAcademicYearRepository academicYearRepo,
        MebKoordinatorluk.Data.Database.DatabaseConfig dbConfig,
        IAcademicYearRolloverService rolloverService,
        MebKoordinatorluk.Data.Database.DatabaseInitializer dbInitializer,
        ICalendarTaskRepository calendarTaskRepo)
    {
        _teacherRepo = teacherRepo;
        _enterpriseRepo = enterpriseRepo;
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _groupRepo = groupRepo;
        _attendanceRepo = attendanceRepo;
        _docRepo = docRepo;
        _settingRepo = settingRepo;
        _receiptRepo = receiptRepo;
        _userRepo = userRepo;
        _backupService = backupService;
        _crossCheckService = crossCheckService;
        _licenseService = licenseService;
        _academicYearRepo = academicYearRepo;
        _dbConfig = dbConfig;
        _rolloverService = rolloverService;
        _dbInitializer = dbInitializer;
        _calendarTaskRepo = calendarTaskRepo;

        try
        {
            var appSettings = _settingRepo.GetAppSettingsAsync().GetAwaiter().GetResult();
            CurrentUserSession.TeacherCanEditData = appSettings.TeacherCanEditData;
            if (!string.IsNullOrWhiteSpace(appSettings.AcademicYear))
            {
                _dbConfig.SwitchToYear(appSettings.AcademicYear, false);
                _dbInitializer.Initialize();
            }
        }
        catch { }

        InitializeComponent();
        SwitchTab("dashboard");
        _ = InitializeLicenseAndHeartbeatAsync();
    }

    private void InitializeComponent()
    {
        Text = "3308 Pro - İşletmelerde Beceri Eğitimi & Koordinatörlük Sistemi";
        try
        {
            if (System.IO.File.Exists("app.ico"))
                Icon = new Icon("app.ico");
            else if (!string.IsNullOrEmpty(Application.ExecutablePath))
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }
        Size = new Size(1380, 860);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 9f);
        BackColor = UITheme.Background;

        // 1. Top Header Bar (Deep Navy #1E3A8A)
        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(30, 58, 138), // #1E3A8A
            Padding = new Padding(12, 0, 16, 0)
        }
;

        // Left Header: Toggle Button & Logo / Title
        var pnlHeaderLeft = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 10, 0, 0)
        }
;

        _btnToggleSidebar = new Button
        {
            Text = "≡",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            Size = new Size(38, 36),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 12, 0)
        }
;
        _btnToggleSidebar.FlatAppearance.BorderSize = 0;
        _btnToggleSidebar.Click += (s, e) => ToggleSidebar();

        var lblBrandTitle = new Label
        {
            Text = "3308 PRO",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 7, 0, 0),
            Cursor = Cursors.Hand
        }
;
        lblBrandTitle.Click += (s, e) => SwitchTab("dashboard");

        _btnAcademicYear = new Button
        {
            Text = $"🎓  {_dbConfig.CurrentAcademicYear}  ▾",
            AutoSize = true,
            Height = 36,
            MinimumSize = new Size(160, 36),
            BackColor = Color.FromArgb(29, 78, 216), // blue-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(16, 0, 0, 0),
            Padding = new Padding(12, 0, 12, 0)
        };
        _btnAcademicYear.FlatAppearance.BorderSize = 0;
        _btnAcademicYear.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
        _btnAcademicYear.Click += BtnAcademicYear_Click;

        pnlHeaderLeft.Controls.AddRange([_btnToggleSidebar, lblBrandTitle, _btnAcademicYear]);

        // Right Header: Actions & User Info
        var pnlHeaderRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 10, 0, 0)
        }
;

        var btnLogout = new Button
        {
            Text = "🚪 Çıkış",
            Size = new Size(84, 36),
            BackColor = Color.FromArgb(220, 38, 38), // red-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            Visible = CurrentUserSession.IsAdmin
        }
;
        btnLogout.FlatAppearance.BorderSize = 0;
        btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(185, 28, 28);
        btnLogout.Click += BtnLogout_Click;

        var currentUser = CurrentUserSession.CurrentUser;
        var roleText = currentUser?.Role == "Admin" ? "Yönetici" : "Öğretmen";
        var editBadge = !CurrentUserSession.IsAdmin 
            ? (CurrentUserSession.CanEditData ? " • Düzenleme Yetkili" : " • Salt Okunur") 
            : "";
        var userDisplayName = currentUser != null ? $"{currentUser.FullName} ({roleText}{editBadge})" : "admin (Yönetici)";

        var userMenu = new ContextMenuStrip
        {
            Font = new Font("Segoe UI", 9.5f),
            ShowImageMargin = false,
            BackColor = Color.White
        }
;
        var itemChangePass = userMenu.Items.Add("🔑  Şifremi Değiştir...");
        itemChangePass.Click += (s, e) =>
        {
            if (CurrentUserSession.CurrentUser != null)
            {
                using var dlg = new ChangePasswordDialog(_userRepo, CurrentUserSession.CurrentUser);
                dlg.ShowDialog(this);
            }

        }
;
        userMenu.Items.Add(new ToolStripSeparator());
        var itemLogout = userMenu.Items.Add("🚪  Oturumu Kapat (Farklı Kullanıcı)");
        itemLogout.ForeColor = Color.FromArgb(220, 38, 38);
        itemLogout.Click += (s, e) => BtnSwitchUser_Click(s, e);

        var btnUserBadge = new Button
        {
            Text = $"👤  {userDisplayName}  ▾",
            AutoSize = true,
            Height = 36,
            MinimumSize = new Size(160, 36),
            BackColor = Color.FromArgb(29, 78, 216), // blue-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            Visible = CurrentUserSession.IsAdmin,
            Padding = new Padding(12, 0, 12, 0)
        }
;
        btnUserBadge.FlatAppearance.BorderSize = 0;
        btnUserBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
        btnUserBadge.Click += (s, e) => userMenu.Show(btnUserBadge, new Point(0, btnUserBadge.Height + 2));

        var btnExportExcel = new Button
        {
            Text = "📊 Excel İndir",
            Size = new Size(116, 36),
            BackColor = Color.FromArgb(5, 150, 105), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            Visible = CurrentUserSession.IsAdmin
        }
;
        btnExportExcel.FlatAppearance.BorderSize = 0;
        btnExportExcel.FlatAppearance.MouseOverBackColor = Color.FromArgb(4, 120, 87);
        btnExportExcel.Click += BtnExportEntireDbToExcel_Click;

        _btnLicenseBadge = new Button
        {
            Text = "🔑 Lisans Kontrol...",
            AutoSize = true,
            Height = 36,
            MinimumSize = new Size(130, 36),
            BackColor = Color.FromArgb(55, 65, 81), // gray-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(10, 0, 10, 0)
        };
        _btnLicenseBadge.FlatAppearance.BorderSize = 0;
        _btnLicenseBadge.Click += (s, e) => OpenLicenseActivationDialog();

        var btnSupport = new Button
        {
            Text = "📞 İletişim & Destek",
            AutoSize = true,
            Height = 36,
            MinimumSize = new Size(130, 36),
            BackColor = Color.FromArgb(29, 78, 216), // blue-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(10, 0, 10, 0)
        };
        btnSupport.FlatAppearance.BorderSize = 0;
        btnSupport.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
        btnSupport.Click += (s, e) => SwitchTab("contact-support");

        pnlHeaderRight.Controls.AddRange([btnLogout, btnUserBadge, btnExportExcel, _btnLicenseBadge, btnSupport]);

        _pnlHeader.Controls.Add(pnlHeaderLeft);
        _pnlHeader.Controls.Add(pnlHeaderRight);

        // 2. Left Sidebar Navigation Panel (#1E3A8A MEB Navy Blue)
        _pnlSidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = SidebarExpandedWidth,
            BackColor = Color.FromArgb(30, 58, 138), // #1E3A8A
            Padding = new Padding(0, 8, 0, 8)
        }
;

        _flpSidebarItems = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        }
;

        _pnlSidebar.Controls.Add(_flpSidebarItems);

        // Populate Left Sidebar Navigation Categories
        BuildSidebarMenu();

        // 3. Main Content Container
        _pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UITheme.Background
        };

        // 4. Archive Mode Top Alert Banner
        _pnlArchiveBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(254, 243, 199), // amber-100
            Padding = new Padding(16, 5, 16, 5),
            Visible = _dbConfig.IsArchiveReadOnly
        };

        _lblArchiveBannerText = new Label
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 83, 9), // amber-700
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 4, 0, 0),
            UseMnemonic = false,
            Text = "📁 ARŞİV MODU: Geçmiş eğitim-öğretim yılı kayıtlarını inceliyorsunuz. Geçmiş yıl verileri salt okunurdur, değişiklik yapılamaz."
        };

        _btnReturnToActiveYear = new Button
        {
            Dock = DockStyle.Right,
            Text = "➔ Aktif Eğitim Yılına Dön",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(217, 119, 6), // amber-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Size = new Size(190, 28)
        };
        _btnReturnToActiveYear.FlatAppearance.BorderSize = 0;
        _btnReturnToActiveYear.Click += (s, e) => SwitchToActiveAcademicYear();

        _pnlArchiveBanner.Controls.AddRange([_lblArchiveBannerText, _btnReturnToActiveYear]);

        Controls.Add(_pnlContent);
        Controls.Add(_pnlArchiveBanner);
        Controls.Add(_pnlSidebar);
        Controls.Add(_pnlHeader);
    }

    private void BuildSidebarMenu()
    {
        _flpSidebarItems.Controls.Clear();
        _navButtons.Clear();
        _categories.Clear();

        // Category 1: GENEL BAKIŞ
        AddSidebarCategory("📊 GENEL BAKIŞ", [
            ("dashboard", "📊", "Genel Bakış Paneli", (Action)(() => SwitchTab("dashboard"))),
            ("task-calendar", "📅", "İdari Görev Takvimi", (Action)(() => SwitchTab("task-calendar")))
        ]);

        // Category 2: KAYIT YÖNETİMİ
        AddSidebarCategory("👥 KAYIT YÖNETİMİ", [
            ("students", "🎓", "Öğrenci Listesi", (Action)(() => SwitchTab("students"))),
            ("student-archive", "🗄️", "Öğrenci Arşivi", (Action)(() => SwitchTab("student-archive"))),
            ("teachers", "👨‍🏫", "Koordinatör Öğretmenler", (Action)(() => SwitchTab("teachers"))),
            ("enterprises", "🏢", "Staj İşletmeleri", (Action)(() => SwitchTab("enterprises")))
        ]);

        // Category 3: OKUL & KURUM TANIMLARI
        AddSidebarCategory("🏫 OKUL TANIMLARI", [
            ("classes", "🏫", "Sınıflar & Şubeler", (Action)(() => SwitchTab("classes"))),
            ("fields", "📚", "Alan / Dal Tanımları", (Action)(() => SwitchTab("fields"))),
            ("address-groups", "📍", "Adres Grupları", (Action)(() => SwitchTab("address-groups")))
        ]);

        // Category 4: SİGORTA İŞLEMLERİ
        AddSidebarCategory("🛡️ SİGORTA İŞLEMLERİ", [
            ("toplu-isegiris-cikis", "📅", "Toplu İşe Gir/Çık Tar Ekleme", (Action)(() => SwitchTab("toplu-isegiris-cikis"))),
            ("xml-isegiris", "📄", "XML İşe Giriş / Çıkış", (Action)(() => SwitchTab("xml-isegiris"))),
            ("xml-ebildirge", "📄", "XML e-Bildirge", (Action)(() => SwitchTab("xml-ebildirge"))),
            ("surekli-devamsizlar", "⚠️", "Sürekli Devamsızlar", (Action)(() => SwitchTab("surekli-devamsizlar"))),
            ("yaz-staji", "☀️", "Yaz Stajı Öğrencileri", (Action)(() => SwitchTab("yaz-staji"))),
            ("tatil-kayitlari", "🏖️", "Tatil Kayıtları", (Action)(() => SwitchTab("tatil-kayitlari"))),
            ("settings-insurance", "⚙️", "Sigorta Parametreleri", (Action)(() => SwitchTab("settings-insurance")))
        ]);

        // Category 5: RAPOR & TAKİP
        AddSidebarCategory("📑 RAPOR & TAKİP", [
            ("attendance", "📋", "Aylık Devamsızlık", (Action)(() => SwitchTab("attendance"))),
            ("fees", "💵", "Aylık Ücret (Bordro)", (Action)(() => SwitchTab("fees"))),
            ("documents", "📑", "Evrak Takip", (Action)(() => SwitchTab("documents")))
        ]);

        // Category 6: GÖREV DAĞITIMI
        if (CurrentUserSession.IsAdmin)
        {
            AddSidebarCategory("⚡ GÖREV DAĞITIMI", [
                ("distribution", "🔄", "Akıllı Dağıtım", (Action)(() => SwitchTab("distribution"))),
                ("forms", "🖨️", "Görev Formu", (Action)(() => SwitchTab("forms")))
            ]);
        }

        else
        {
            // Öğretmenler dağıtımı çalıştıramaz ama koordinatörlük görev formunu alabilir
            AddSidebarCategory("⚡ KOORDİNATÖRLÜK", [
                ("forms", "🖨️", "Görev Formu", (Action)(() => SwitchTab("forms")))
            ]);
        }


        // Category 7: KOORDİNATÖR EVRAKLARI (Ayrı Ana Menü)
        AddSidebarCategory("📁 KOORDİNATÖR EVRAKLARI", [
            ("coordinator-docs", "📑", "Aylık Rehberlik Formu (Ek-1)", (Action)(() => SwitchTab("coordinator-docs"))),
            ("coordinator-daily-docs", "📝", "Günlük Rehberlik Formu", (Action)(() => SwitchTab("coordinator-daily-docs"))),
            ("coordinator-attendance-docs", "📊", "Aylık Devamsızlık Çizelgesi", (Action)(() => SwitchTab("coordinator-attendance-docs")))
        ]);

        // Category 8: İSG İŞLEMLERİ
        AddSidebarCategory("🦺 İSG İŞLEMLERİ", [
            ("isg-islemleri", "📋", "İSG Öğrenci Takip Ekranı", (Action)(() => SwitchTab("isg-islemleri")))
        ]);

        // Category 9: VERİ AKTARIMI (Sadece Admin)
        if (CurrentUserSession.IsAdmin)
        {
            AddSidebarCategory("📥 VERİ AKTARIMI", [
                ("import-eokul", "📥", "e-Okul Sınıf Yükle", (Action)OpenEOkulImport),
                ("student-audit", "🔍", "Öğrenci Kontrolü", (Action)OpenStudentAuditDialog),
                ("import-tc", "🆔", "e-Okul TCKN Aktar", (Action)OpenEOkulTcImport),
                ("import-iban", "💳", "Öğrenci IBAN Yükle", (Action)OpenStudentIbanImport),
                ("import-bulk", "📋", "Toplu Öğrenci Ekle", (Action)OpenBulkStudentsDialog)
            ]);
        }


        // Category 10: SİSTEM AYARLARI (Sadece Admin)
        if (CurrentUserSession.IsAdmin)
        {
            AddSidebarCategory("⚙️ SİSTEM AYARLARI", [
                ("settings-school", "🏫", "Genel Okul Ayarları", (Action)(() => SwitchTab("settings-school"))),
                ("settings-users", "👥", "Kullanıcı Yönetimi", (Action)(() => SwitchTab("settings-users")))
            ]);
        }

        // Category 11: YARDIM & DESTEK
        AddSidebarCategory("ℹ️ YARDIM & DESTEK", [
            ("contact-support", "📞", "İletişim & Destek", (Action)(() => SwitchTab("contact-support")))
        ]);

    }

    private static Color GetDefaultNavButtonForeColor(string btnId)
    {
        if (btnId is "yaz-staji" or "tatil-kayitlari")
        {
            return Color.FromArgb(100, 116, 139); // slate-500 pasif renk
        }

        if (btnId == "xml-ebildirge")
        {
            int day = DateTime.Today.Day;
            if (day >= 1 && day <= 8)
            {
                return Color.FromArgb(248, 113, 113); // Günlerden 1-8 arası dikkat çekici kırmızı
            }

        }

        return Color.FromArgb(241, 245, 249); // slate-100 parlak açık renk
    }

    private void AddSidebarCategory(string categoryTitle, List<(string id, string icon, string title, Delegate action)> items)
    {
        var pnlCategoryHeader = new Panel
        {
            Size = new Size(SidebarExpandedWidth - 8, 36),
            Margin = new Padding(4, 3, 4, 1),
            BackColor = Color.FromArgb(30, 58, 138), // Sol zemin ile aynı (#1E3A8A)
            Cursor = Cursors.Hand
        }
;

        var lblCatTitle = new Label
        {
            Text = $"{categoryTitle}   ▶",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(241, 245, 249),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 10, 0),
            Cursor = Cursors.Hand,
            Tag = categoryTitle
        }
;

        pnlCategoryHeader.Controls.Add(lblCatTitle);

        var pnlItemsContainer = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Visible = false // Collapsed by default (Accordion)
        }
;

        // Accordion click handlers
        pnlCategoryHeader.Click += (s, e) => ToggleCategory(categoryTitle);
        lblCatTitle.Click += (s, e) => ToggleCategory(categoryTitle);

        pnlCategoryHeader.MouseEnter += (s, e) => pnlCategoryHeader.BackColor = Color.FromArgb(37, 72, 172);
        pnlCategoryHeader.MouseLeave += (s, e) => pnlCategoryHeader.BackColor = Color.FromArgb(30, 58, 138);
        lblCatTitle.MouseEnter += (s, e) => pnlCategoryHeader.BackColor = Color.FromArgb(37, 72, 172);
        lblCatTitle.MouseLeave += (s, e) => pnlCategoryHeader.BackColor = Color.FromArgb(30, 58, 138);

        foreach (var (id, icon, title, action) in items)
        {
            bool isPassive = id is "yaz-staji" or "tatil-kayitlari";

            var btnItem = new Button
            {
                Size = new Size(SidebarExpandedWidth - 22, 34),
                Text = isPassive ? $"   {icon}   {title} (Pasif)" : $"   {icon}   {title}",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = isPassive ? Color.FromArgb(148, 163, 184) : GetDefaultNavButtonForeColor(id),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = isPassive ? Cursors.Default : Cursors.Hand,
                Enabled = !isPassive,
                Margin = new Padding(16, 1, 4, 1), // İçe girinti (Indentation)
                Padding = new Padding(4, 0, 0, 0),
                Tag = new SidebarItemTag(id, icon, title, categoryTitle)
            }
;
            btnItem.FlatAppearance.BorderSize = 0;
            btnItem.FlatAppearance.MouseOverBackColor = isPassive ? Color.Transparent : Color.FromArgb(37, 72, 172);

            btnItem.Click += (s, e) =>
            {
                if (!isPassive && action is Action act) act.Invoke();
            }
;

            _navButtons[id] = btnItem;
            pnlItemsContainer.Controls.Add(btnItem);
        }


        // İnce zarif ayrım çizgisi (Divider)
        var pnlDivider = new Panel
        {
            Height = 1,
            Width = SidebarExpandedWidth - 24,
            BackColor = Color.FromArgb(48, 86, 175), // İnce ayırıcı çizgi
            Margin = new Padding(12, 3, 12, 3),
            Tag = "divider"
        }
;

        _categories[categoryTitle] = (pnlCategoryHeader, lblCatTitle, pnlItemsContainer);
        _flpSidebarItems.Controls.Add(pnlCategoryHeader);
        _flpSidebarItems.Controls.Add(pnlItemsContainer);
        _flpSidebarItems.Controls.Add(pnlDivider);
    }

    private void ToggleCategory(string categoryTitle)
    {
        bool wasOpen = _openCategoryTitle == categoryTitle;

        // Close all categories
        foreach (var (title, (headerPnl, headerLbl, itemsFlp)) in _categories)
        {
            itemsFlp.Visible = false;
            headerLbl.Text = $"{title}   ▶";
        }


        // Expand clicked category if it was not open
        if (!wasOpen)
        {
            _openCategoryTitle = categoryTitle;
            if (_categories.TryGetValue(categoryTitle, out var cat))
            {
                cat.ItemsPanel.Visible = true;
                cat.HeaderLabel.Text = $"{categoryTitle}   ▼";
            }

        }

        else
        {
            _openCategoryTitle = null;
        }

    }

    private class SidebarItemTag
    {
        public string Id { get; }
        public string Icon { get; }
        public string Title { get; }
        public string CategoryTitle { get; }

        public SidebarItemTag(string id, string icon, string title, string categoryTitle)
        {
            Id = id;
            Icon = icon;
            Title = title;
            CategoryTitle = categoryTitle;
        }

    }

    private void ToggleSidebar()
    {
        _isSidebarExpanded = !_isSidebarExpanded;
        int targetWidth = _isSidebarExpanded ? SidebarExpandedWidth : SidebarCollapsedWidth;

        _pnlSidebar.Width = targetWidth;

        foreach (Control ctrl in _flpSidebarItems.Controls)
        {
            if (ctrl is Panel pnlCat)
            {
                pnlCat.Visible = _isSidebarExpanded;
                if (pnlCat.Tag as string == "divider")
                {
                    pnlCat.Width = targetWidth - 24;
                }

                else
                {
                    pnlCat.Width = targetWidth - 8;
                }

            }

            else if (ctrl is FlowLayoutPanel flpItems)
            {
                flpItems.Width = targetWidth - 8;
                foreach (Control itemCtrl in flpItems.Controls)
                {
                    if (itemCtrl is Button btn && btn.Tag is SidebarItemTag tag)
                    {
                        bool isPassive = tag.Id is "yaz-staji" or "tatil-kayitlari";
                        string displayTitle = isPassive ? $"{tag.Title} (Pasif)" : tag.Title;
                        btn.Text = _isSidebarExpanded ? $"   {tag.Icon}   {displayTitle}" : $" {tag.Icon}";
                        btn.TextAlign = _isSidebarExpanded ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
                        btn.Margin = _isSidebarExpanded ? new Padding(16, 1, 4, 1) : new Padding(2, 1, 2, 1);
                        btn.Padding = _isSidebarExpanded ? new Padding(4, 0, 0, 0) : Padding.Empty;
                        btn.Width = _isSidebarExpanded ? targetWidth - 22 : targetWidth - 4;
                    }

                }

            }

        }

    }

    private void OpenEOkulImport()
    {
        using var dlg = new Dialogs.EOkulImportDialog(_studentRepo, _classRepo, _fieldRepo);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            if (_studentsView != null) _ = _studentsView.LoadLookupsAndDataAsync();
            if (_classesView != null) _ = _classesView.LoadClassesAsync();
            SwitchTab("students");
        }

    }

    private void OpenStudentAuditDialog()
    {
        using var dlg = new Dialogs.StudentAuditDialog(_studentRepo, _classRepo, _fieldRepo, _settingRepo);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ChangesCount > 0)
        {
            if (_studentsView != null) _ = _studentsView.RefreshStudentsAsync();
            if (_classesView != null) _ = _classesView.LoadClassesAsync();
            SwitchTab("students");
        }

        else if (_dashboardView != null)
        {
            _ = _dashboardView.LoadDataAsync();
        }

    }

    private void OpenEOkulTcImport()
    {
        using var dlg = new Dialogs.EOkulTcImportDialog(_studentRepo);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ImportedCount > 0)
        {
            if (_studentsView != null) _ = _studentsView.RefreshStudentsAsync();
            SwitchTab("students");
        }

    }

    private void OpenStudentIbanImport()
    {
        using var dlg = new Dialogs.StudentIbanImportDialog(_studentRepo);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ImportedCount > 0)
        {
            if (_studentsView != null) _ = _studentsView.RefreshStudentsAsync();
            SwitchTab("students");
        }

    }

    private void OpenBulkStudentsDialog()
    {
        using var dlg = new Dialogs.BulkStudentsDialog(_studentRepo, _classRepo, _fieldRepo);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ImportedCount > 0)
        {
            if (_studentsView != null) _ = _studentsView.RefreshStudentsAsync();
            SwitchTab("students");
        }

    }

    private InsuranceOpsView GetOrCreateInsuranceOpsView(string tabId)
    {
        if (!_insuranceOpsViews.TryGetValue(tabId, out var view))
        {
            view = new InsuranceOpsView(tabId, _studentRepo, _classRepo, _settingRepo, _attendanceRepo, SwitchTab, _crossCheckService);
            _insuranceOpsViews[tabId] = view;
        }

        return view;
    }

    public void SwitchTab(string tabId)
    {
        string baseTabId = tabId.Contains(':') ? tabId.Split(':')[0] : tabId;
        string? tabParam = tabId.Contains(':') ? tabId.Split(':')[1] : null;

        if (baseTabId == "student-audit")
        {
            OpenStudentAuditDialog();
            return;
        }


        if (baseTabId is "yaz-staji" or "tatil-kayitlari")
        {
            return;
        }


        _currentTab = baseTabId;

        // Reset sidebar button styles
        foreach (var (bId, btn) in _navButtons)
        {
            btn.BackColor = Color.Transparent;
            btn.ForeColor = GetDefaultNavButtonForeColor(bId);
            btn.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
        }


        // Highlight active button and expand parent category accordion
        if (_navButtons.TryGetValue(baseTabId, out var activeBtn) && activeBtn.Tag is SidebarItemTag tag)
        {
            activeBtn.BackColor = Color.FromArgb(2, 132, 199); // sky-600 vibrant active
            activeBtn.ForeColor = Color.White;
            activeBtn.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);

            if (!string.IsNullOrEmpty(tag.CategoryTitle))
            {
                _openCategoryTitle = tag.CategoryTitle;
                foreach (var (title, (headerPnl, headerLbl, itemsFlp)) in _categories)
                {
                    bool isTarget = title == tag.CategoryTitle;
                    itemsFlp.Visible = isTarget;
                    headerLbl.Text = isTarget ? $"{title}   ▼" : $"{title}   ▶";
                }

            }

        }


        // Yetki Kontrolü: Öğretmen rolü admin yetkisi gerektiren ekranlara doğrudan geçemez
        if (!CurrentUserSession.IsAdmin)
        {
            if (baseTabId is "distribution" or "settings" or "settings-school" or "settings-users" or "settings-insurance")
            {
                MessageBox.Show("Bu bölüme erişim yetkiniz bulunmamaktadır. Sadece yöneticiler erişebilir.", "Erişim Yetkisi Yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SwitchTab("dashboard");
                return;
            }

        }


        // Show View
        _pnlContent.SuspendLayout();
        _pnlContent.Controls.Clear();

        UserControl view = baseTabId switch
        {
            "dashboard" => _dashboardView ??= new DashboardView(_teacherRepo, _enterpriseRepo, _studentRepo, _classRepo, _fieldRepo, _settingRepo, _calendarTaskRepo, SwitchTab, ReloadAllData),
            "task-calendar" => _calendarTasksView ??= new CalendarTasksManagementView(_calendarTaskRepo, SwitchTab),
            "students" => _studentsView ??= new StudentsView(_studentRepo, _classRepo, _fieldRepo, _enterpriseRepo, _teacherRepo, _settingRepo, _licenseService),
            "student-archive" => _studentArchiveView ??= new StudentArchiveView(_studentRepo, _classRepo, _fieldRepo, _settingRepo),
            "teachers" => _teachersView ??= new TeachersView(_teacherRepo, _fieldRepo, _studentRepo, _enterpriseRepo),
            "enterprises" => _enterprisesView ??= new EnterprisesView(_enterpriseRepo, _groupRepo, _fieldRepo, _studentRepo, _settingRepo, _licenseService),
            "classes" => _classesView ??= new ClassesView(_classRepo, _fieldRepo, _studentRepo, _settingRepo, _attendanceRepo),
            "fields" => _fieldsView ??= new FieldsView(_fieldRepo, _teacherRepo, _enterpriseRepo, _studentRepo),
            "address-groups" => _addressGroupsView ??= new AddressGroupsView(_groupRepo, _enterpriseRepo),
            "toplu-isegiris-cikis" or "xml-ebildirge" or "xml-isegiris" or "surekli-devamsizlar" or "yaz-staji" or "tatil-kayitlari" or "isg-islemleri" or "isg-egitimler" or "isg-evraklar" => GetOrCreateInsuranceOpsView(baseTabId),
            "distribution" => _distributionView ??= new DistributionView(_studentRepo, _teacherRepo, _enterpriseRepo, _groupRepo, _settingRepo, _classRepo),
            "forms" => _formsView ??= new FormsView(_teacherRepo, _studentRepo, _enterpriseRepo, _settingRepo, _licenseService),
            "coordinator-docs" or "coordinator-daily-docs" or "coordinator-attendance-docs" => _coordinatorDocsView ??= new CoordinatorDocsView(_teacherRepo, _studentRepo, _enterpriseRepo, _classRepo, _fieldRepo, _settingRepo, _attendanceRepo),
            "attendance" => _attendanceView ??= new AttendanceView(_attendanceRepo, _studentRepo, _classRepo, _fieldRepo, _settingRepo),
            "documents" => _documentsView ??= new DocumentsView(_docRepo, _teacherRepo, _enterpriseRepo, _studentRepo, _fieldRepo, _settingRepo),
            "fees" => _feesView ??= new FeesView(_studentRepo, _attendanceRepo, _classRepo, _enterpriseRepo, _settingRepo, _receiptRepo),
            "settings" or "settings-school" => _settingsView ??= new SettingsView(_settingRepo, _userRepo, SwitchTab, _backupService, _licenseService),
            "settings-insurance" => _insuranceSettingsView ??= new InsuranceSettingsView(_settingRepo),
            "settings-users" => _usersView ??= new UsersView(_userRepo, _teacherRepo, _settingRepo),
            "contact-support" => _contactSupportView ??= new ContactSupportView(_settingRepo, _licenseService, () =>
            {
                UpdateLicenseBadge();
                _ = InitializeHeartbeatAsync();
            }),
            _ => _dashboardView ??= new DashboardView(_teacherRepo, _enterpriseRepo, _studentRepo, _classRepo, _fieldRepo, _settingRepo, _calendarTaskRepo, SwitchTab, ReloadAllData)
        };

        view.Dock = DockStyle.Fill;
        _pnlContent.Controls.Add(view);
        _pnlContent.ResumeLayout();

        // Refresh underlying data if needed
        if (view is DashboardView d) _ = d.LoadDataAsync();
        else if (view is CalendarTasksManagementView ctv) _ = ctv.LoadTasksAsync();
        else if (view is ContactSupportView csv) _ = csv.LoadDataAsync();
        else if (view is StudentsView st)
        {
            if (tabParam == "missing-tc")
            {
                st.FilterByMissingTc();
            }

            else
            {
                _ = st.RefreshStudentsAsync();
            }

        }

        else if (view is StudentArchiveView sav) _ = sav.LoadDataAsync();
        else if (view is TeachersView tv) _ = tv.LoadTeachersAsync();
        else if (view is EnterprisesView ev) _ = ev.RefreshEnterprisesAsync();
        else if (view is ClassesView cv) _ = cv.LoadClassesAsync();
        else if (view is FieldsView fv) _ = fv.LoadFieldsAsync();
        else if (view is AddressGroupsView ag) _ = ag.LoadGroupsAsync();
        else if (view is DistributionView dv) _ = dv.LoadDataAsync();
        else if (view is FormsView fov) _ = fov.LoadDataAsync();
        else if (view is AttendanceView attv) _ = attv.LoadDataAsync();
        else if (view is DocumentsView docv) _ = docv.LoadDataAsync();
        else if (view is CoordinatorDocsView cdv)
        {
            _ = cdv.LoadDataAsync();
            if (baseTabId == "coordinator-daily-docs")
            {
                cdv.SelectDocument(1);
            }

            else if (baseTabId == "coordinator-attendance-docs")
            {
                cdv.SelectDocument(2);
            }

            else if (baseTabId == "coordinator-docs")
            {
                cdv.SelectDocument(0);
            }

        }

        else if (view is FeesView feev) _ = feev.LoadDataAsync();
        else if (view is InsuranceOpsView iov) iov.LoadDataAsync();
        else if (view is UsersView uv) _ = uv.LoadUsersAsync();
    }

    private void ReloadAllData()
    {
        _dashboardView = null;
        _calendarTasksView = null;
        _contactSupportView = null;
        SwitchTab(_currentTab);
    }

    private void BtnLogout_Click(object? sender, EventArgs e)
    {
        var res = MessageBox.Show(
            "Programı kapatmak istediğinize emin misiniz?",
            "Programdan Çıkış",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res == DialogResult.Yes)
        {
            CurrentUserSession.RequestedRelogin = false;
            Application.Exit();
        }

    }

    private void BtnSwitchUser_Click(object? sender, EventArgs e)
    {
        var res = MessageBox.Show(
            "Mevcut oturumu kapatıp kullanıcı giriş ekranına dönmek istiyor musunuz?",
            "Oturumu Kapat",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res == DialogResult.Yes)
        {
            CurrentUserSession.RequestedRelogin = true;
            Close();
        }

    }

    private async void BtnExportEntireDbToExcel_Click(object? sender, EventArgs e)
    {
        // 1. KVKK ve Veri Güvenliği: Yönetici Şifre Doğrulaması
        using var authDlg = new AdminPasswordPromptDialog(_userRepo);
        if (authDlg.ShowDialog(this) != DialogResult.OK)
        {
            return; // Şifre girilmedi veya doğrulanamadı, dışa aktarım durduruldu
        }


        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx",
            FileName = $"3308_Pro_Sistem_Veritabanı_{DateTime.Now:yyyy_MM_dd}.xlsx",
            Title = "Tüm Sistem Verilerini Excel Olarak Dışa Aktar"
        }
;

        if (sfd.ShowDialog() != DialogResult.OK) return;

        try

        {
            var students = (await _studentRepo.GetAllAsync()).OrderBy(s => s.Classroom).ThenBy(s => s.FullName).ToList();
            var teachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();
            var enterprises = (await _enterpriseRepo.GetAllAsync()).OrderBy(e => e.Name).ToList();
            var classes = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();

            using var wb = new XLWorkbook();

            // ── 1. ÖĞRENCİLER SAYFASI ──────────────────────────────────────
            var wsSt = wb.Worksheets.Add("Öğrenciler");
            string[] stHeaders = [
                "No", "T.C. Kimlik No", "Öğrenci No", "Adı Soyadı", "Sınıf / Şube",
                "Mesleki Alan / Dal", "Kayıtlı İşletme", "Koordinatör Öğretmen",
                "Öğretim Türü", "SGK Durumu", "SGK Prim Kodu", "Staj Günleri",
                "Görev Günü", "Ücret Kategorisi", "Durum"
            ];

            ApplySheetHeaderStyle(wsSt, "ÖĞRENCİ GENEL LİSTESİ", stHeaders, XLColor.FromArgb(30, 58, 138)); // blue-900

            int rSt = 4;
            int idxSt = 1;
            foreach (var s in students)
            {
                wsSt.Cell(rSt, 1).SetValue(idxSt++);
                wsSt.Cell(rSt, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 2).SetValue(s.TcKimlikNo ?? "");
                wsSt.Cell(rSt, 2).Style.NumberFormat.Format = "@";
                wsSt.Cell(rSt, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 3).SetValue(s.StudentNumber ?? "");
                wsSt.Cell(rSt, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 4).SetValue(s.FullName);
                wsSt.Cell(rSt, 4).Style.Font.Bold = true;

                wsSt.Cell(rSt, 5).SetValue(s.Classroom ?? "");
                wsSt.Cell(rSt, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 6).SetValue(s.FieldName ?? "");
                wsSt.Cell(rSt, 7).SetValue(s.EnterpriseName ?? "Atanmadı");
                wsSt.Cell(rSt, 8).SetValue(s.TeacherName ?? "Atanmadı");

                wsSt.Cell(rSt, 9).SetValue(s.EducationType ?? "Örgün Eğitim");
                wsSt.Cell(rSt, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                string sgkDurum = s.HasFamilyHealthInsurance ? "Bakmakla Yükümlü (Sağlık Var)" : "Yükümlü Olmayan (Sağlık Yok)";
                wsSt.Cell(rSt, 10).SetValue(sgkDurum);
                wsSt.Cell(rSt, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 11).SetValue(s.SgkPrimKodu ?? "");
                wsSt.Cell(rSt, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 12).SetValue(s.InternshipDays ?? "");
                wsSt.Cell(rSt, 13).SetValue(s.DutyDays ?? "");
                wsSt.Cell(rSt, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 14).SetValue(s.FeeCategory == "kat2" ? "%50" : "%30");
                wsSt.Cell(rSt, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsSt.Cell(rSt, 15).SetValue(s.IsSurekliDevamsiz ? "Sürekli Devamsız" : (s.IsActive ? "Aktif" : "Pasif"));
                wsSt.Cell(rSt, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if ((rSt % 2) == 1) wsSt.Range(rSt, 1, rSt, stHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                wsSt.Row(rSt).Height = 20;
                rSt++;
            }

            FinalizeSheetTable(wsSt, stHeaders.Length, rSt - 1);

            // ── 2. ÖĞRETMENLER SAYFASI ────────────────────────────────────
            var wsTc = wb.Worksheets.Add("Öğretmenler");
            string[] tcHeaders = [
                "No", "T.C. Kimlik No", "Adı Soyadı", "Branş / Alan", "Görev Günü",
                "Sorumlu İşletme Sayısı", "Sorumlu Öğrenci Sayısı", "Hedef Saat", "Ücret Saati",
                "Telefon", "E-Posta"
            ];

            ApplySheetHeaderStyle(wsTc, "KOORDİNATÖR ÖĞRETMEN LİSTESİ", tcHeaders, XLColor.FromArgb(49, 46, 129)); // indigo-900

            int rTc = 4;
            int idxTc = 1;
            foreach (var t in teachers)
            {
                int studentCount = students.Count(s => s.TeacherId == t.Id);
                int enterpriseCount = students.Where(s => s.TeacherId == t.Id && s.EnterpriseId.HasValue).Select(s => s.EnterpriseId!.Value).Distinct().Count();

                wsTc.Cell(rTc, 1).SetValue(idxTc++);
                wsTc.Cell(rTc, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 2).SetValue(t.TcKimlikNo ?? "");
                wsTc.Cell(rTc, 2).Style.NumberFormat.Format = "@";
                wsTc.Cell(rTc, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 3).SetValue(t.FullName);
                wsTc.Cell(rTc, 3).Style.Font.Bold = true;

                wsTc.Cell(rTc, 4).SetValue(t.Branch ?? "");

                wsTc.Cell(rTc, 5).SetValue(t.AvailableDays ?? "");
                wsTc.Cell(rTc, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 6).SetValue(enterpriseCount);
                wsTc.Cell(rTc, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 7).SetValue(studentCount);
                wsTc.Cell(rTc, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 8).SetValue(t.TargetHours);
                wsTc.Cell(rTc, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 9).SetValue(t.CoordinatorFeeHours);
                wsTc.Cell(rTc, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 10).SetValue(t.Phone ?? "");
                wsTc.Cell(rTc, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsTc.Cell(rTc, 11).SetValue(t.Email ?? "");

                if ((rTc % 2) == 1) wsTc.Range(rTc, 1, rTc, tcHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                wsTc.Row(rTc).Height = 20;
                rTc++;
            }

            FinalizeSheetTable(wsTc, tcHeaders.Length, rTc - 1);

            // ── 3. İŞLETMELER SAYFASI ─────────────────────────────────────
            var wsEnt = wb.Worksheets.Add("İşletmeler");
            string[] entHeaders = [
                "No", "İşletme Adı", "Bölge / Grup", "Yetkili Kişi", "Telefon",
                "İl / İlçe", "Koordinatör Öğretmen", "Kayıtlı Öğrenci", "Kapasite",
                "Aktif Günler", "Vergi No", "IBAN", "SGK Sicil No"
            ];

            ApplySheetHeaderStyle(wsEnt, "STAJ İŞLETMELERİ LİSTESİ", entHeaders, XLColor.FromArgb(6, 95, 70)); // emerald-800

            int rEnt = 4;
            int idxEnt = 1;
            foreach (var eItem in enterprises)
            {
                int studentCount = students.Count(s => s.EnterpriseId == eItem.Id);

                wsEnt.Cell(rEnt, 1).SetValue(idxEnt++);
                wsEnt.Cell(rEnt, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsEnt.Cell(rEnt, 2).SetValue(eItem.Name);
                wsEnt.Cell(rEnt, 2).Style.Font.Bold = true;

                wsEnt.Cell(rEnt, 3).SetValue(eItem.AddressGroupName ?? "-");
                wsEnt.Cell(rEnt, 4).SetValue(eItem.ContactPerson ?? "");

                wsEnt.Cell(rEnt, 5).SetValue(eItem.Phone ?? "");
                wsEnt.Cell(rEnt, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsEnt.Cell(rEnt, 6).SetValue($"{eItem.District ?? ""} / {eItem.City ?? ""}".Trim(' ', '/'));

                wsEnt.Cell(rEnt, 7).SetValue(eItem.TeacherName ?? "Atanmadı");

                wsEnt.Cell(rEnt, 8).SetValue(studentCount);
                wsEnt.Cell(rEnt, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsEnt.Cell(rEnt, 8).Style.Font.Bold = true;

                wsEnt.Cell(rEnt, 9).SetValue(eItem.Capacity);
                wsEnt.Cell(rEnt, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsEnt.Cell(rEnt, 10).SetValue(eItem.ActiveDays ?? "");
                wsEnt.Cell(rEnt, 11).SetValue(eItem.TaxNumber ?? "");
                wsEnt.Cell(rEnt, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsEnt.Cell(rEnt, 12).SetValue(eItem.Iban ?? "");
                wsEnt.Cell(rEnt, 13).SetValue(eItem.SgkSicilNo ?? "");
                wsEnt.Cell(rEnt, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if ((rEnt % 2) == 1) wsEnt.Range(rEnt, 1, rEnt, entHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                wsEnt.Row(rEnt).Height = 20;
                rEnt++;
            }

            FinalizeSheetTable(wsEnt, entHeaders.Length, rEnt - 1);

            // ── 4. SINIFLAR SAYFASI ───────────────────────────────────────
            var wsCl = wb.Worksheets.Add("Sınıflar");
            string[] clHeaders = [
                "No", "Sınıf / Şube Adı", "Mesleki Alan / Dal", "Kurum Türü",
                "Öğretim Türü", "Kayıtlı Öğrenci", "İşletmeye Gidiyor mu?",
                "Staj Günleri", "İşe Başlama Tarihi", "İş Bitiş Tarihi", "Yarım Gün Muafiyeti"
            ];

            ApplySheetHeaderStyle(wsCl, "SINIF VE ŞUBE LİSTESİ", clHeaders, XLColor.FromArgb(146, 64, 14)); // amber-800

            int rCl = 4;
            int idxCl = 1;
            foreach (var c in classes)
            {
                int studentCount = students.Count(s => s.ClassId == c.Id || string.Equals(s.Classroom, c.Name, StringComparison.OrdinalIgnoreCase));

                wsCl.Cell(rCl, 1).SetValue(idxCl++);
                wsCl.Cell(rCl, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsCl.Cell(rCl, 2).SetValue(c.Name);
                wsCl.Cell(rCl, 2).Style.Font.Bold = true;
                wsCl.Cell(rCl, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsCl.Cell(rCl, 3).SetValue(c.FieldName ?? "-");

                wsCl.Cell(rCl, 4).SetValue(c.InstitutionType ?? "Anadolu Meslek Programı");
                wsCl.Cell(rCl, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsCl.Cell(rCl, 5).SetValue(c.EducationType ?? "Örgün Eğitim");
                wsCl.Cell(rCl, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsCl.Cell(rCl, 6).SetValue(studentCount);
                wsCl.Cell(rCl, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsCl.Cell(rCl, 6).Style.Font.Bold = true;

                wsCl.Cell(rCl, 7).SetValue(c.HasInternship ? "Evet" : "Hayır");
                wsCl.Cell(rCl, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsCl.Cell(rCl, 8).SetValue(c.HasInternship ? (c.InternshipDays ?? "") : "-");

                wsCl.Cell(rCl, 9).SetValue(c.StartDate ?? "-");
                wsCl.Cell(rCl, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsCl.Cell(rCl, 10).SetValue(c.EndDate ?? "-");
                wsCl.Cell(rCl, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                wsCl.Cell(rCl, 11).SetValue(c.IgnoreHalfDayDeduction ? "Muaf (0 Sayılır)" : "Normal Kesilir");
                wsCl.Cell(rCl, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if ((rCl % 2) == 1) wsCl.Range(rCl, 1, rCl, clHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                wsCl.Row(rCl).Height = 20;
                rCl++;
            }

            FinalizeSheetTable(wsCl, clHeaders.Length, rCl - 1);

            wb.SaveAs(sfd.FileName);

            MessageBox.Show(
                $"Tüm veritabanı kayıtları 4 ayrı sekme halinde başarıyla Excel formatında aktarıldı:\n\n" +
                $"• Öğrenciler ({students.Count} Kayıt)\n" +
                $"• Öğretmenler ({teachers.Count} Kayıt)\n" +
                $"• İşletmeler ({enterprises.Count} Kayıt)\n" +
                $"• Sınıflar ({classes.Count} Kayıt)\n\n" +
                $"Dosya: {Path.GetFileName(sfd.FileName)}",
                "Aktarım Başarılı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        catch (Exception ex)
        {
            MessageBox.Show($"Aktarım hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }

    private static void ApplySheetHeaderStyle(IXLWorksheet ws, string title, string[] headers, XLColor headerColor)
    {
        ws.Cell(1, 1).Value = $"{title} — {DateTime.Now:dd.MM.yyyy}";
        ws.Range(1, 1, 1, headers.Length).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(15, 23, 42);
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 28;

        int headerRow = 3;
        ws.Row(headerRow).Height = 25;
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 9.5;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = headerColor;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
        }

    }

    private static void FinalizeSheetTable(IXLWorksheet ws, int columnCount, int lastRow)
    {
        if (lastRow >= 4)
        {
            var range = ws.Range(3, 1, lastRow, columnCount);
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorderColor = XLColor.FromArgb(203, 213, 225);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            range.Style.Border.OutsideBorderColor = XLColor.FromArgb(71, 85, 105);
        }


        ws.Columns(1, columnCount).AdjustToContents();
        if (ws.Column(1).Width < 6) ws.Column(1).Width = 6;
    }

    private void UpdateLicenseBadge()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateLicenseBadge));
            return;
        }

        var lic = _licenseService.CurrentLicense;
        if (lic.IsActive)
        {
            if (lic.RemainingDays > 30)
            {
                _btnLicenseBadge.Text = "🟢 Lisanslı";
                _btnLicenseBadge.BackColor = Color.FromArgb(5, 150, 105); // emerald-600
                _btnLicenseBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(4, 120, 87);
            }
            else
            {
                _btnLicenseBadge.Text = $"⚠️ Lisans Bitiyor ({lic.RemainingDays} Gün)";
                _btnLicenseBadge.BackColor = Color.FromArgb(217, 119, 6); // amber-600
                _btnLicenseBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 83, 9);
            }
        }
        else if (lic.Status == LicenseStatus.Expired)
        {
            _btnLicenseBadge.Text = "⚠️ Süresi Doldu (Demo)";
            _btnLicenseBadge.BackColor = Color.FromArgb(220, 38, 38); // red-600
            _btnLicenseBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(185, 28, 28);
        }
        else if (lic.Status == LicenseStatus.TamperedClock)
        {
            _btnLicenseBadge.Text = "⛔ Tarih Hilesi (Kilitli)";
            _btnLicenseBadge.BackColor = Color.FromArgb(185, 28, 28);
            _btnLicenseBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(153, 27, 27);
        }
        else
        {
            _btnLicenseBadge.Text = "⚠️ Demo Sürüm (Kısıtlı)";
            _btnLicenseBadge.BackColor = Color.FromArgb(217, 119, 6); // amber-600
            _btnLicenseBadge.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 83, 9);
        }
    }

    private void OpenLicenseActivationDialog()
    {
        var lic = _licenseService.CurrentLicense;
        if (lic.IsActive && lic.RemainingDays > 30)
        {
            MessageBox.Show(
                $"Yazılımınız tam sürüm olarak lisanslıdır ve {lic.EndDate:dd.MM.yyyy} tarihine kadar ({lic.RemainingDays} gün) geçerlidir.\n\nLisans yenileme ve yeni anahtar girişi son 1 ay (30 gün) kala otomatik olarak kullanıma açılacaktır.",
                "Lisans Aktif ve Geçerli — 3308 Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dlg = new LicenseActivationDialog(_licenseService, () =>
        {
            UpdateLicenseBadge();
            _ = InitializeHeartbeatAsync();
        });
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            UpdateLicenseBadge();
        }
    }

    private async Task InitializeLicenseAndHeartbeatAsync()
    {
        try
        {
            await _licenseService.RefreshLicenseStatusAsync();
            UpdateLicenseBadge();
            await InitializeHeartbeatAsync();
        }
        catch
        {
            // Offline and error safe
        }
    }

    private async Task InitializeHeartbeatAsync()
    {
        try
        {
            var lic = _licenseService.CurrentLicense;
            var settings = await _settingRepo.GetAppSettingsAsync();
            var students = await _studentRepo.GetAllAsync();
            var enterprises = await _enterpriseRepo.GetAllAsync();

            var result = await LicenseHeartbeatService.SendHeartbeatAsync(
                lic,
                settings,
                System.Linq.Enumerable.Count(students),
                System.Linq.Enumerable.Count(enterprises));

            if (result.IsBlocked && _licenseService.CurrentLicense.IsActive)
            {
                BeginInvoke(new Action(async () =>
                {
                    await _licenseService.RevokeLicenseToDemoAsync(result.Reason ?? "Yetkisiz lisans kullanımı");
                    UpdateLicenseBadge();

                    var msg = "⚠️ GÜVENLİK VE LİSANS DOĞRULAMA UYARISI\n\n" +
                              "Kurumunuzda kullanılan 3308 Pro lisansı, merkezi sunucu kayıtlarımızla eşleşmemektedir (Kayıtsız/Yetkisiz Lisans).\n\n" +
                              "3308 sayılı Mesleki Eğitim Kanunu kapsamında resmi evraklarınızda ve denetimlerde geçersizlik yaşamamak için lütfen orijinal lisans temin ediniz.\n\n" +
                              "ℹ️ Programınız Deneme (Demo) moduna geçirilmiştir. Mevcut verileriniz KORUNMAKTADIR.\n\n" +
                              "📧 Lisans ve İletişim: 3308pro@gmail.com\n" +
                              $"💻 Makine Kodunuz: {_licenseService.MachineCode}";

                    MessageBox.Show(this, msg, "3308 Pro - Lisans Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
            }
        }
        catch
        {
            // Non-blocking fire-and-forget
        }
    }

    // ─────────────────────────────────────────────────────────────
    // EĞİTİM-ÖĞRETİM YILI YÖNETİMİ & ARŞİV GEÇİŞLERİ
    // ─────────────────────────────────────────────────────────────
    private async void BtnAcademicYear_Click(object? sender, EventArgs e)
    {
        var menu = new ContextMenuStrip
        {
            Font = new Font("Segoe UI", 9.5f),
            ShowImageMargin = false,
            BackColor = Color.White
        };

        var headerItem = new ToolStripMenuItem("📅  EĞİTİM-ÖĞRETİM YILLARI")
        {
            Enabled = false,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new ToolStripSeparator());

        var yearNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var dbYears = await _academicYearRepo.GetAllAsync();
            foreach (var y in dbYears)
            {
                if (!string.IsNullOrWhiteSpace(y.Name))
                    yearNames.Add(y.Name.Trim());
            }
        }
        catch { }

        try
        {
            if (Directory.Exists(_dbConfig.DataDirectory))
            {
                var files = Directory.GetFiles(_dbConfig.DataDirectory, "meb_*.db");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.StartsWith("meb_") && fileName != "meb_koordinatorluk")
                    {
                        var yearPart = fileName[4..].Replace('_', '-');
                        yearNames.Add(yearPart);
                    }
                }
            }
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(_dbConfig.CurrentAcademicYear))
            yearNames.Add(_dbConfig.CurrentAcademicYear.Trim());

        var sortedYears = yearNames.OrderByDescending(y => y).ToList();

        foreach (var yearName in sortedYears)
        {
            var access = _licenseService.GetAcademicYearAccess(yearName);
            bool isCurrentSelected = string.Equals(_dbConfig.CurrentAcademicYear, yearName, StringComparison.OrdinalIgnoreCase);

            string icon = access switch
            {
                AcademicYearAccessStatus.FutureLocked => "🔒",
                AcademicYearAccessStatus.ArchiveReadOnly => "📁",
                AcademicYearAccessStatus.DemoAccess => "⏳",
                _ => "🎓"
            };

            string suffix = access switch
            {
                AcademicYearAccessStatus.FutureLocked => "(Lisans Gerekli)",
                AcademicYearAccessStatus.ArchiveReadOnly => "(Arşiv - Salt Okunur)",
                AcademicYearAccessStatus.DemoAccess => "(Demo Sürüm)",
                _ => isCurrentSelected ? "(Seçili)" : ""
            };

            var itemText = $"{(isCurrentSelected ? "✔  " : "    ")}{icon}  {yearName}  {suffix}".TrimEnd();
            var item = new ToolStripMenuItem(itemText)
            {
                Font = isCurrentSelected ? new Font("Segoe UI", 9.5f, FontStyle.Bold) : new Font("Segoe UI", 9.5f)
            };

            if (access == AcademicYearAccessStatus.FutureLocked)
            {
                item.ForeColor = Color.FromArgb(156, 163, 175);
            }
            else if (access == AcademicYearAccessStatus.ArchiveReadOnly)
            {
                item.ForeColor = Color.FromArgb(180, 83, 9);
            }

            item.Click += (s, ev) => HandleAcademicYearSelected(yearName, access);
            menu.Items.Add(item);
        }

        if (CurrentUserSession.IsAdmin)
        {
            menu.Items.Add(new ToolStripSeparator());
            var itemRollover = new ToolStripMenuItem("➕  Yeni Eğitim-Öğretim Yılı Başlat / Devret...")
            {
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(5, 150, 105)
            };
            itemRollover.Click += (s, ev) => OpenAcademicYearRolloverDialog();
            menu.Items.Add(itemRollover);
        }

        menu.Show(_btnAcademicYear, new Point(0, _btnAcademicYear.Height + 2));
    }

    private void HandleAcademicYearSelected(string yearName, AcademicYearAccessStatus access)
    {
        if (access == AcademicYearAccessStatus.FutureLocked)
        {
            var lic = _licenseService.CurrentLicense;
            MessageBox.Show(
                $"🔒 {yearName} Eğitim-Öğretim Yılı Kilitli\n\n" +
                $"Bu eğitim-öğretim yılı mevcut lisansınızın kapsamı dışındadır.\n" +
                $"• Mevcut Lisans Bitiş Tarihiniz: {lic.EndDate:dd.MM.yyyy}\n" +
                $"• Seçilen Yıl: {yearName}\n\n" +
                $"Yeni eğitim-öğretim yılını açmak ve veri devir işlemlerini başlatmak için lütfen lisansınızı yenileyiniz.\n\n" +
                $"🔑 Makine Kodu (HWID): {_licenseService.MachineCode}\n" +
                $"✉️ Lisans Yenileme & Destek: 3308pro@gmail.com",
                "Lisans Kapsamı Dışında — 3308 Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (string.Equals(_dbConfig.CurrentAcademicYear, yearName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool isReadOnly = access == AcademicYearAccessStatus.ArchiveReadOnly;
        var targetPath = _dbConfig.GetDbPathForYear(yearName);

        if (!File.Exists(targetPath) && !File.Exists(_dbConfig.DefaultDatabasePath))
        {
            var promptRollover = MessageBox.Show(
                $"'{yearName}' yılına ait veritabanı henüz oluşturulmamış.\n\n" +
                $"Mevcut yıl verilerinizi devrederek yeni yıla başlamak ister misiniz?",
                "Yeni Yıl — 3308 Pro",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (promptRollover == DialogResult.Yes)
            {
                OpenAcademicYearRolloverDialog();
            }
            return;
        }

        SwitchAcademicYear(yearName, isReadOnly);
    }

    public void SwitchAcademicYear(string yearName, bool isReadOnly)
    {
        _dbConfig.SwitchToYear(yearName, isReadOnly);
        CurrentUserSession.IsArchiveReadOnly = isReadOnly;

        if (isReadOnly)
        {
            _btnAcademicYear.BackColor = Color.FromArgb(217, 119, 6); // amber-600
            _btnAcademicYear.Text = $"📁  {yearName} (ARŞİV)  ▾";
            _pnlArchiveBanner.Visible = true;
            _lblArchiveBannerText.Text = $"📁 ARŞİV MODU: {yearName} Eğitim-Öğretim yılı kayıtlarını inceliyorsunuz. Geçmiş yıl verileri salt okunurdur, değişiklik yapılamaz.";
        }
        else
        {
            _btnAcademicYear.BackColor = Color.FromArgb(29, 78, 216); // blue-700
            _btnAcademicYear.Text = $"🎓  {yearName}  ▾";
            _pnlArchiveBanner.Visible = false;
        }

        ResetAndReloadAllViews();
    }

    private void SwitchToActiveAcademicYear()
    {
        string activeYear = "2026-2027";
        try
        {
            var active = _academicYearRepo.GetActiveYearAsync().GetAwaiter().GetResult();
            if (active != null && !string.IsNullOrWhiteSpace(active.Name))
                activeYear = active.Name;
        }
        catch { }

        SwitchAcademicYear(activeYear, false);
    }

    private void ResetAndReloadAllViews()
    {
        _dashboardView = null;
        _studentsView = null;
        _studentArchiveView = null;
        _teachersView = null;
        _enterprisesView = null;
        _classesView = null;
        _fieldsView = null;
        _addressGroupsView = null;
        _distributionView = null;
        _formsView = null;
        _attendanceView = null;
        _documentsView = null;
        _coordinatorDocsView = null;
        _feesView = null;
        _settingsView = null;
        _insuranceSettingsView = null;
        _usersView = null;
        _insuranceOpsViews.Clear();

        try
        {
            var appSettings = _settingRepo.GetAppSettingsAsync().GetAwaiter().GetResult();
            CurrentUserSession.TeacherCanEditData = appSettings.TeacherCanEditData;
        }
        catch { }

        SwitchTab(_currentTab);
    }

    private void OpenAcademicYearRolloverDialog()
    {
        using var dlg = new AcademicYearRolloverDialog(
            _dbConfig,
            _rolloverService,
            _licenseService,
            _settingRepo,
            targetYear =>
            {
                SwitchAcademicYear(targetYear, false);
            });

        dlg.ShowDialog(this);
    }
}
