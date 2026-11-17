using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Views;

public class AttendanceView : UserControl
{
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly ISettingRepository? _settingRepo;

    private List<Student> _allStudents = [];
    private List<SchoolClass> _allClasses = [];
    private AppSettings? _appSettings;
    private readonly Dictionary<(int StudentId, int Day), string> _attendanceCache = new();
    private DateTime _selectedMonth = DateTime.Today;
    private Button _btnPrevMonth = null!;
    private Button _btnNextMonth = null!;
    private bool _isInitializing = true;
    private int _hoveredRowIndex = -1;
    private int _hoveredColumnIndex = -1;

    private Panel _pnlGrid = null!;
    private DataGridView _grid = null!;
    private Label _lblMonthInfo = null!;
    private ComboBox _cmbFilterInternship = null!;
    private ComboBox _cmbFilterField = null!;
    private ComboBox _cmbFilterGrade = null!;
    private ComboBox _cmbFilterClass = null!;
    private ComboBox _cmbFilterEdu = null!;
    private TextBox _txtSearch = null!;
    private CheckBox _chkSelectAll = null!;
    private bool _isUpdatingSelectAll = false;
    private Label _lblCount = null!;

    private ComboBox _cmbBulkDay = null!;
    private ComboBox _cmbBulkCode = null!;
    private DateTimePicker _dtpClearStart = null!;
    private DateTimePicker _dtpClearEnd = null!;
    private Button _btnToggleViewMode = null!;
    private bool _isAutoFitMode = true;
    private CheckBox _chkShowWeekends = null!;
    private CheckBox _chkOnlyUpToToday = null!;

    public AttendanceView(
        IAttendanceRepository attendanceRepo,
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        ISettingRepository? settingRepo = null)
    {
        _attendanceRepo = attendanceRepo;
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _settingRepo = settingRepo;

        InitializeComponent();
        _ = LoadDataAsync();

        VisibleChanged += (s, e) =>
        {
            if (Visible)
            {
                _ = LoadDataAsync();
            }
        };
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
            BackColor = Color.Transparent,
            Padding = new Padding(24, 16, 24, 0)
        };

        var lblTitle = new Label
        {
            Text = "📋 PRATİK EĞİTİM AYLIK DEVAM DEVAMSIZLIK ÇİZELGESİ",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "3308 sayılı kanun kapsamında işletmelerde pratik eğitim gören öğrencilerin aylık devam-devamsızlık takip çizelgesi",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        // Header Buttons
        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 600,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnPrint = new Button
        {
            Text = "🖨️ Resmi Çizelgeyi Yazdır (A4)",
            Size = new Size(185, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnPrint.FlatAppearance.BorderSize = 0;
        btnPrint.Click += BtnPrint_Click;

        var btnExportExcel = new Button
        {
            Text = "📊 Excel İndir",
            Size = new Size(120, 36),
            BackColor = Color.FromArgb(5, 150, 105),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnExportExcel.FlatAppearance.BorderSize = 0;
        btnExportExcel.Click += BtnExportExcel_Click;

        var btnUploadExcel = new Button
        {
            Text = "📊 Excel'den Yükle",
            Size = new Size(135, 36),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0),
            Visible = MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData
        };
        btnUploadExcel.FlatAppearance.BorderSize = 0;
        btnUploadExcel.Click += BtnUploadExcel_Click;

        var btnDownloadTemplate = new Button
        {
            Text = "📥 Şablon İndir",
            Size = new Size(110, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnDownloadTemplate.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

        pnlTopButtons.Controls.AddRange([btnPrint, btnExportExcel, btnUploadExcel, btnDownloadTemplate]);
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Month Selector Bar
        var pnlMonth = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.White,
            Padding = new Padding(24, 8, 24, 8)
        };
        pnlMonth.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlMonth.Height - 1, pnlMonth.Width, pnlMonth.Height - 1);
        };

        var pnlMonthCenter = new Panel
        {
            Size = new Size(1220, 36),
            BackColor = Color.Transparent
        };

        var lblMonthTitle = new Label
        {
            Text = "📅 Çizelge Ayı:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            Location = new Point(0, 9)
        };

        _btnPrevMonth = new Button
        {
            Text = "◀",
            Size = new Size(32, 28),
            Location = new Point(96, 4),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnPrevMonth.Click += async (s, e) => await TryChangeMonthAsync(-1);

        _lblMonthInfo = new Label
        {
            Text = _selectedMonth.ToString("MMMM yyyy"),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            BackColor = Color.FromArgb(239, 246, 255),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(132, 4),
            Size = new Size(160, 28),
            BorderStyle = BorderStyle.FixedSingle
        };

        _btnNextMonth = new Button
        {
            Text = "▶",
            Size = new Size(32, 28),
            Location = new Point(296, 4),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnNextMonth.Click += async (s, e) => await TryChangeMonthAsync(1);

        var btnResetMonth = new Button
        {
            Text = "🔄 Ayı Sıfırla",
            Location = new Point(334, 4),
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnResetMonth.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnResetMonth.Click += BtnResetMonth_Click;

        _btnToggleViewMode = new Button
        {
            Size = new Size(230, 28),
            Location = new Point(470, 4),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        UpdateViewModeButtonState();
        _btnToggleViewMode.Click += (s, e) =>
        {
            _isAutoFitMode = !_isAutoFitMode;
            UpdateViewModeButtonState();
            AdjustColumnWidthsAndCenter();
        };

        _chkShowWeekends = new CheckBox
        {
            Text = "📅 Hafta Sonları",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Checked = false,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Location = new Point(710, 9)
        };
        _chkShowWeekends.CheckedChanged += (s, e) => UpdateDayColumnsVisibility();

        _chkOnlyUpToToday = new CheckBox
        {
            Text = $"🕒 Sadece Bugüne Kadar ({DateTime.Today:dd MMMM})",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235), // blue-600
            Checked = false,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Location = new Point(870, 9)
        };
        _chkOnlyUpToToday.CheckedChanged += (s, e) => UpdateDayColumnsVisibility();

        pnlMonthCenter.Controls.AddRange([lblMonthTitle, _btnPrevMonth, _lblMonthInfo, _btnNextMonth, btnResetMonth, _btnToggleViewMode, _chkShowWeekends, _chkOnlyUpToToday]);
        pnlMonth.Controls.Add(pnlMonthCenter);

        pnlMonth.Resize += (s, e) =>
        {
            pnlMonthCenter.Location = new Point(Math.Max(24, (pnlMonth.ClientSize.Width - pnlMonthCenter.Width) / 2), 7);
        };
        pnlMonthCenter.Location = new Point(Math.Max(24, (pnlMonth.ClientSize.Width - pnlMonthCenter.Width) / 2), 7);

        // Filter Bar Panel
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 8, 24, 8)
        };
        pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        var pnlFilterCenter = new Panel
        {
            Size = new Size(1230, 36),
            BackColor = Color.Transparent
        };

        _cmbFilterInternship = new ComboBox
        {
            Location = new Point(0, 4),
            Width = 165,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterInternship.Items.AddRange(["Sadece İşletmeye Gidenler", "Tüm Öğrenciler", "İşletmeye Gitmeyenler"]);
        _cmbFilterInternship.SelectedIndex = 0;
        _cmbFilterInternship.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterField = new ComboBox { Location = new Point(171, 4), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbFilterField.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterGrade = new ComboBox
        {
            Location = new Point(307, 4),
            Width = 105,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterGrade.Items.AddRange(["Tüm Seviyeler", "9. Sınıf", "10. Sınıf", "11. Sınıf", "12. Sınıf"]);
        _cmbFilterGrade.SelectedIndex = 0;
        _cmbFilterGrade.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterClass = new ComboBox { Location = new Point(418, 4), Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbFilterClass.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterEdu = new ComboBox { Location = new Point(529, 4), Width = 135, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbFilterEdu.Items.AddRange(["Tüm Öğretim Türleri", "Örgün Eğitim", "Özel Eğitim", "MESEM"]);
        _cmbFilterEdu.SelectedIndex = 0;
        _cmbFilterEdu.SelectedIndexChanged += (s, e) => ApplyFilter();

        _txtSearch = new TextBox { Location = new Point(670, 4), Width = 145, PlaceholderText = "Öğrenci adı veya no..." };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _chkSelectAll = new CheckBox
        {
            Text = "Tümünü Seç / Kaldır",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Checked = true,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Location = new Point(825, 7)
        };
        _chkSelectAll.CheckedChanged += (s, e) =>
        {
            if (_isUpdatingSelectAll) return;
            bool isChecked = _chkSelectAll.Checked;
            _isUpdatingSelectAll = true;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow && _grid.Columns.Contains("colSelect"))
                {
                    row.Cells["colSelect"].Value = isChecked;
                }
            }
            _isUpdatingSelectAll = false;
            UpdateSelectedCountLabel();
        };

        _lblCount = new Label
        {
            Text = "Toplam 0 Öğrenci",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(995, 7),
            AutoSize = true
        };

        pnlFilterCenter.Controls.AddRange([_cmbFilterInternship, _cmbFilterField, _cmbFilterGrade, _cmbFilterClass, _cmbFilterEdu, _txtSearch, _chkSelectAll, _lblCount]);
        pnlFilter.Controls.Add(pnlFilterCenter);

        pnlFilter.Resize += (s, e) =>
        {
            pnlFilterCenter.Location = new Point(Math.Max(24, (pnlFilter.ClientSize.Width - pnlFilterCenter.Width) / 2), 8);
        };
        pnlFilterCenter.Location = new Point(Math.Max(24, (pnlFilter.ClientSize.Width - pnlFilterCenter.Width) / 2), 8);

        // Bulk Day Bar & Clear Date Range Bar
        bool canEdit = MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData;
        var pnlBulk = new Panel
        {
            Dock = DockStyle.Top,
            Height = canEdit ? 90 : 36,
            BackColor = Color.FromArgb(254, 243, 199), // amber-100
            Padding = new Padding(24, 6, 24, 6)
        };
        pnlBulk.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(252, 211, 77), 1); // amber-300
            e.Graphics.DrawLine(pen, 0, pnlBulk.Height - 1, pnlBulk.Width, pnlBulk.Height - 1);
            if (MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData)
            {
                using var penSep = new Pen(Color.FromArgb(253, 230, 138), 1); // amber-200
                e.Graphics.DrawLine(penSep, 10, 39, pnlBulk.Width - 10, 39);
            }
        };

        var lblReadOnlyNotice = new Label
        {
            Text = "🔒 Okuma Modu: Veri değişikliği yetkiniz kapalıdır. Kayıtları inceleyebilir, filtreleyebilir, Excel ve Yazıcı çıktıları alabilirsiniz.",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14),
            AutoSize = true,
            Location = new Point(24, 8),
            Visible = !canEdit
        };
        pnlBulk.Controls.Add(lblReadOnlyNotice);

        var pnlBulkCenter = new Panel
        {
            Size = new Size(850, 76),
            BackColor = Color.Transparent,
            Visible = canEdit
        };

        // --- Satır 1: Toplu Gün Girişi ---
        var lblBulkIcon = new Label
        {
            Text = "⚡ Toplu Gün Girişi:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(120, 53, 15), // amber-900
            AutoSize = true,
            Location = new Point(0, 7)
        };

        _cmbBulkDay = new ComboBox
        {
            Location = new Point(135, 4),
            Width = 70,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        for (int d = 1; d <= 31; d++) _cmbBulkDay.Items.Add(d.ToString());
        _cmbBulkDay.SelectedIndex = 0;

        _cmbBulkCode = new ComboBox
        {
            Location = new Point(215, 4),
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 24,
            MaxDropDownItems = 12
        };
        _cmbBulkCode.DrawItem += CmbBulkCode_DrawItem;
        foreach (var item in MebKoordinatorluk.Core.Services.AttendanceCodeHelper.AllCodes)
        {
            _cmbBulkCode.Items.Add(item);
        }
        _cmbBulkCode.SelectedIndex = 0; // X

        _cmbBulkCode.SelectedIndexChanged += (s, e) =>
        {
            var selectedCode = GetSelectedBulkCode();
            if (_grid != null && _grid.SelectedCells.Count > 0)
            {
                foreach (DataGridViewCell cell in _grid.SelectedCells)
                {
                    if (_grid.Columns[cell.ColumnIndex].Name.StartsWith("colDay_"))
                    {
                        cell.Value = selectedCode;
                    }
                }
            }
        };

        var btnApplyBulk = new Button
        {
            Text = "✓ Seçili Güne Hepsine Ata",
            Location = new Point(545, 2),
            Size = new Size(185, 30),
            BackColor = Color.FromArgb(217, 119, 6), // amber-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnApplyBulk.FlatAppearance.BorderSize = 0;
        btnApplyBulk.Click += BtnApplyBulk_Click;

        // --- Satır 2: Tarih Aralığı Temizleme (Boş Bırakma) ---
        var lblClearIcon = new Label
        {
            Text = "🧹 Tarih Aralığı Temizle:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14), // amber-800
            AutoSize = true,
            Location = new Point(0, 48)
        };

        _dtpClearStart = new DateTimePicker
        {
            Location = new Point(145, 44),
            Width = 110,
            Format = DateTimePickerFormat.Short,
            Font = new Font("Segoe UI", 9f)
        };

        var lblClearTo = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14),
            Location = new Point(260, 47),
            AutoSize = true
        };

        _dtpClearEnd = new DateTimePicker
        {
            Location = new Point(278, 44),
            Width = 110,
            Format = DateTimePickerFormat.Short,
            Font = new Font("Segoe UI", 9f)
        };

        var btnApplyClearRange = new Button
        {
            Text = "🗑️ Tarih Aralığını Boşalt",
            Location = new Point(396, 42),
            Size = new Size(225, 30),
            BackColor = Color.FromArgb(220, 38, 38), // red-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnApplyClearRange.FlatAppearance.BorderSize = 0;
        btnApplyClearRange.Click += BtnApplyClearRange_Click;

        var btnAutoClearTerm = new Button
        {
            Text = "🎓 Okul Dışı Günleri Boşalt",
            Location = new Point(628, 42),
            Size = new Size(220, 30),
            BackColor = Color.FromArgb(51, 65, 85), // slate-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAutoClearTerm.FlatAppearance.BorderSize = 0;
        btnAutoClearTerm.Click += BtnAutoClearTerm_Click;

        pnlBulkCenter.Controls.AddRange([
            lblBulkIcon, _cmbBulkDay, _cmbBulkCode, btnApplyBulk,
            lblClearIcon, _dtpClearStart, lblClearTo, _dtpClearEnd, btnApplyClearRange, btnAutoClearTerm
        ]);
        pnlBulk.Controls.Add(pnlBulkCenter);

        pnlBulk.Resize += (s, e) =>
        {
            pnlBulkCenter.Location = new Point(Math.Max(24, (pnlBulk.ClientSize.Width - pnlBulkCenter.Width) / 2), 7);
        };
        pnlBulkCenter.Location = new Point(Math.Max(24, (pnlBulk.ClientSize.Width - pnlBulkCenter.Width) / 2), 7);

        // Grid Container
        _pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        _grid = new DataGridView
        {
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            ScrollBars = ScrollBars.Both,
            RowTemplate = { Height = 42 }
        };
        UITheme.ApplyModernGridStyle(_grid);

        // ModernGridStyle sonrası ayarları uyguluyoruz:
        _grid.RowTemplate.Height = 42;
        _grid.ColumnHeadersHeight = 60;
        _grid.ReadOnly = false;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.MultiSelect = true;
        _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        _grid.CellValueChanged += Grid_CellValueChanged;

        _grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        _grid.CellContentClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "colSelect")
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                UpdateSelectedCountLabel();
            }
        };

        // Otomatik büyük harf, ortalama ve seçim
        _grid.EditingControlShowing += (s, e) =>
        {
            if (e.Control is TextBox tb)
            {
                tb.CharacterCasing = CharacterCasing.Upper;
                tb.TextAlign = HorizontalAlignment.Center;
                tb.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                tb.SelectAll();
            }
        };

        // Hücre düzenlemesi bittiğinde büyük harfe sabitle (boş bırakılmasına izin verilir)
        _grid.CellEndEdit += (s, e) =>
        {
            if (!MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData) return;
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name.StartsWith("colDay_"))
            {
                var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                var val = cell.Value?.ToString()?.Trim().ToUpperInvariant() ?? "";
                cell.Value = val;
            }
        };

        // Çift tıklamayla hızlı geçiş: X -> D -> İ -> (boş) -> X
        _grid.CellDoubleClick += (s, e) =>
        {
            if (!MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData) return;
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name.StartsWith("colDay_"))
            {
                var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                var current = cell.Value?.ToString()?.Trim().ToUpperInvariant() ?? "";
                cell.Value = current switch
                {
                    "X" => "D",
                    "D" => "İ",
                    "İ" => "",
                    "" => "X",
                    _ => "X"
                };
            }
        };

        // Titremesiz çizim için çift arabelleğe alma (DoubleBuffering)
        typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null,
            _grid,
            new object[] { true });

        // Fare ile gelinen satır ve sütunu vurgulama (Crosshair / Reticle Highlight)
        _grid.CellMouseEnter += Grid_CellMouseEnter;
        _grid.MouseLeave += Grid_MouseLeave;
        _grid.CellPainting += Grid_CellPainting;
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.Paint += Grid_Paint;
        _grid.Scroll += (s, e) => _grid.Invalidate(new Rectangle(0, 0, _grid.Width, 60));

        // Okul No sayısal karşılaştırma
        _grid.SortCompare += (s, e) =>
        {
            if (e.Column.Name == "colNumber")
            {
                int n1 = FeeCalculator.ParseStudentNumber(e.CellValue1?.ToString());
                int n2 = FeeCalculator.ParseStudentNumber(e.CellValue2?.ToString());
                e.SortResult = n1.CompareTo(n2);
                e.Handled = true;
            }
            else if (e.Column.Name == "colIdx")
            {
                int i1 = int.TryParse(e.CellValue1?.ToString(), out int v1) ? v1 : 0;
                int i2 = int.TryParse(e.CellValue2?.ToString(), out int v2) ? v2 : 0;
                e.SortResult = i1.CompareTo(i2);
                e.Handled = true;
            }
        };

        _grid.Sorted += (s, e) =>
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Columns.Contains("colIdx"))
                    _grid.Rows[i].Cells["colIdx"].Value = i + 1;
            }
        };

        // Delete veya Backspace basıldığında hücreyi boş ('') yap
        _grid.KeyDown += (s, e) =>
        {
            if (_grid.CurrentCell != null && _grid.Columns[_grid.CurrentCell.ColumnIndex].Name.StartsWith("colDay_"))
            {
                if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
                {
                    _grid.CurrentCell.Value = "";
                    e.Handled = true;
                }
            }
        };

        _pnlGrid.Controls.Add(_grid);
        _pnlGrid.Resize += (s, e) => AdjustColumnWidthsAndCenter();

        Controls.Add(_pnlGrid);
        Controls.Add(pnlBulk);
        Controls.Add(pnlFilter);
        Controls.Add(pnlMonth);
        Controls.Add(pnlHeader);

        AdjustColumnWidthsAndCenter();
    }

    public async Task LoadDataAsync()
    {
        try
        {
            _isInitializing = true;
            _lblMonthInfo.Text = _selectedMonth.ToString("MMMM yyyy");
            UpdateClearDatePickers();
            SetupMonthlyGridColumns();

            if (_settingRepo != null)
            {
                _appSettings = await _settingRepo.GetAppSettingsAsync();
            }

            _allStudents = (await _studentRepo.GetAllAsync())
                .Where(s => s.IsActive && !s.IsSurekliDevamsiz && !s.StudentNumber.Contains("-P", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Classroom)
                .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
                .ToList();
            _allClasses = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();
            var fields = (await _fieldRepo.GetAllAsync()).OrderBy(f => f.Name).ToList();

            _cmbFilterField.Items.Clear();
            _cmbFilterField.Items.Add("Tüm Alanlar");
            foreach (var f in fields) _cmbFilterField.Items.Add(f.Name);
            _cmbFilterField.SelectedIndex = 0;

            _cmbFilterClass.Items.Clear();
            _cmbFilterClass.Items.Add("Tüm Sınıflar");
            foreach (var c in _allClasses) _cmbFilterClass.Items.Add(c.Name);
            _cmbFilterClass.SelectedIndex = 0;

            UpdateNavButtonsState();
            if (_settingRepo != null)
            {
                var currentWage = await _settingRepo.GetYearlyMinimumWageAsync(_selectedMonth.Year);
                if (currentWage == null || currentWage.NetMinimumWage <= 0)
                {
                    MessageBox.Show(
                        $"⚠️ {_selectedMonth.Year} yılına ait asgari ücret henüz sistemde tanımlanmamıştır!\n\nLütfen 'Kurum ve Genel Ayarlar' bölümünden {_selectedMonth.Year} yılı Net Asgari Ücretini kaydediniz.",
                        "Asgari Ücret Tanımı Eksik",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            _isInitializing = false;
            await LoadCurrentMonthEntriesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Devamsızlık yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async void UpdateMonthAndReload()
    {
        try
        {
            UpdateNavButtonsState();
            _isInitializing = true;
            _lblMonthInfo.Text = _selectedMonth.ToString("MMMM yyyy");
            UpdateClearDatePickers();
            SetupMonthlyGridColumns();
            _isInitializing = false;
            await LoadCurrentMonthEntriesAsync();
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            MessageBox.Show(
                $"Ay verileri yüklenirken bir hata oluştu:\n{ex.Message}\n\n(Veritabanı bozulması tespit edildiyse lütfen sağlam 'Data\\meb_koordinatorluk.db' dosyasını geri yükleyin.)",
                "Veri Yükleme Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdateNavButtonsState()
    {
        var todayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var currentSel = new DateTime(_selectedMonth.Year, _selectedMonth.Month, 1);
        if (_btnNextMonth != null)
        {
            _btnNextMonth.Enabled = currentSel < todayMonth;
        }

        bool isCurrentMonth = _selectedMonth.Year == DateTime.Today.Year && _selectedMonth.Month == DateTime.Today.Month;
        if (_chkOnlyUpToToday != null)
        {
            _chkOnlyUpToToday.Enabled = isCurrentMonth;
            if (isCurrentMonth)
            {
                _chkOnlyUpToToday.Text = $"🕒 Sadece Bugüne Kadar ({DateTime.Today:dd MMMM})";
                _chkOnlyUpToToday.ForeColor = Color.FromArgb(37, 99, 235);
            }
            else
            {
                _chkOnlyUpToToday.Text = "🕒 Sadece Bugüne Kadar (Mevcut Ay)";
                _chkOnlyUpToToday.ForeColor = Color.FromArgb(148, 163, 184);
                _chkOnlyUpToToday.Checked = false;
            }
        }
    }

    private async Task TryChangeMonthAsync(int monthOffset)
    {
        var targetDate = _selectedMonth.AddMonths(monthOffset);
        var todayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var targetMonth = new DateTime(targetDate.Year, targetDate.Month, 1);

        // 1. Mevcut aydan sonrasına geçiş engeli
        if (targetMonth > todayMonth)
        {
            MessageBox.Show("Mevcut aydan sonraki aylara geçiş yapılamaz!", "Geçersiz Dönem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateNavButtonsState();
            return;
        }

        // 2. Hedef yıl için asgari ücret tanımı kontrolü
        if (_settingRepo != null)
        {
            var wage = await _settingRepo.GetYearlyMinimumWageAsync(targetDate.Year);
            if (wage == null || wage.NetMinimumWage <= 0)
            {
                MessageBox.Show(
                    $"⚠️ {targetDate.Year} yılına ait asgari ücret sistemde tanımlanmamıştır!\n\nİşlem yapabilmek için lütfen 'Kurum ve Genel Ayarlar' bölümünden {targetDate.Year} yılı Net Asgari Ücretini kaydediniz.",
                    "Asgari Ücret Tanımı Eksik",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        _selectedMonth = targetDate;
        UpdateMonthAndReload();
    }

    private void UpdateClearDatePickers()
    {
        if (_dtpClearStart == null || _dtpClearEnd == null) return;
        int days = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);
        _dtpClearStart.Value = new DateTime(_selectedMonth.Year, _selectedMonth.Month, 1);
        _dtpClearEnd.Value = new DateTime(_selectedMonth.Year, _selectedMonth.Month, days);
    }

    private (DateTime StartDate, DateTime EndDate) GetSchoolTermDates(Student? student = null)
    {
        int schoolYearStart = _selectedMonth.Month >= 9 ? _selectedMonth.Year : _selectedMonth.Year - 1;
        int schoolYearEnd = schoolYearStart + 1;

        DateTime defaultStart = new DateTime(schoolYearStart, 9, 9);
        DateTime defaultEnd = new DateTime(schoolYearEnd, 6, 20);

        if (_appSettings != null)
        {
            if (!string.IsNullOrWhiteSpace(_appSettings.OrgunStartDate) && DateTime.TryParse(_appSettings.OrgunStartDate, out var sDate))
            {
                defaultStart = sDate;
            }
            if (!string.IsNullOrWhiteSpace(_appSettings.OrgunEndDate) && DateTime.TryParse(_appSettings.OrgunEndDate, out var eDate))
            {
                defaultEnd = eDate;
            }
        }

        if (student != null && !string.IsNullOrWhiteSpace(student.Classroom))
        {
            var cls = _allClasses.FirstOrDefault(c => string.Equals(c.Name, student.Classroom, StringComparison.OrdinalIgnoreCase));
            if (cls != null)
            {
                if (!string.IsNullOrWhiteSpace(cls.StartDate) && DateTime.TryParse(cls.StartDate, out var csDate))
                    defaultStart = csDate;
                if (!string.IsNullOrWhiteSpace(cls.EndDate) && DateTime.TryParse(cls.EndDate, out var ceDate))
                    defaultEnd = ceDate;
            }
        }

        return (defaultStart, defaultEnd);
    }

    private string GetDefaultDayCode(Student s, int day)
    {
        var date = new DateTime(_selectedMonth.Year, _selectedMonth.Month, day);
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return "-";

        // Öğrencinin bireysel işe giriş veya işten çıkış tarihi kontrolü
        if (s.SgkEntryDate.HasValue && date < s.SgkEntryDate.Value.Date)
            return "";

        if (s.SgkExitDate.HasValue && date > s.SgkExitDate.Value.Date)
            return "";

        var cls = _allClasses.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
        if (FeeCalculator.IsMesem(s, cls))
        {
            return "X";
        }

        var (termStart, termEnd) = GetSchoolTermDates(s);

        // Eylül ayında okulun başlangıç tarihinden önceki günler boş ("") olmalıdır
        if (_selectedMonth.Month == 9 && termStart.Month == 9 && date < termStart.Date)
            return "";

        // Haziran ayında okulun bitiş tarihinden sonraki günler boş ("") olmalıdır
        if (_selectedMonth.Month == 6 && termEnd.Month == 6 && date > termEnd.Date)
            return "";

        return "X";
    }

    private static bool IsDateOutsideSgkPeriod(Student s, DateTime date)
    {
        if (s.SgkEntryDate.HasValue && date.Date < s.SgkEntryDate.Value.Date) return true;
        if (s.SgkExitDate.HasValue && date.Date > s.SgkExitDate.Value.Date) return true;
        return false;
    }

    private void SetupMonthlyGridColumns()
    {
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _grid.ColumnHeadersHeight = 60;

        var colSelect = new DataGridViewCheckBoxColumn
        {
            Name = "colSelect",
            HeaderText = "Yazdır",
            Width = 46,
            ReadOnly = false,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = Padding.Empty }
        };
        colSelect.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colSelect.HeaderCell.Style.Padding = Padding.Empty;
        colSelect.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.Columns.Add(colSelect);

        var colIdx = new DataGridViewTextBoxColumn
        {
            Name = "colIdx",
            HeaderText = "#",
            Width = 36,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f), Padding = Padding.Empty }
        };
        colIdx.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colIdx.HeaderCell.Style.Padding = Padding.Empty;
        colIdx.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.Columns.Add(colIdx);

        var colNumber = new DataGridViewTextBoxColumn
        {
            Name = "colNumber",
            HeaderText = "Okul No",
            Width = 62,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(2, 0, 2, 0) }
        };
        colNumber.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colNumber.HeaderCell.Style.Padding = Padding.Empty;
        colNumber.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.Columns.Add(colNumber);

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "Adı Soyadı",
            Width = 145,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(4, 0, 2, 0) }
        };
        colName.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colName.HeaderCell.Style.Padding = Padding.Empty;
        colName.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.Columns.Add(colName);

        var colClass = new DataGridViewTextBoxColumn
        {
            Name = "colClass",
            HeaderText = "Sınıf",
            Width = 52,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f), Padding = Padding.Empty }
        };
        colClass.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colClass.HeaderCell.Style.Padding = Padding.Empty;
        colClass.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.Columns.Add(colClass);

        var colEnterprise = new DataGridViewTextBoxColumn
        {
            Name = "colEnterprise",
            HeaderText = "İşletme Adı",
            Width = 115,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f), Padding = new Padding(4, 0, 2, 0) }
        };
        colEnterprise.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colEnterprise.HeaderCell.Style.Padding = Padding.Empty;
        colEnterprise.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.Columns.Add(colEnterprise);

        // Day Columns 1..daysInMonth
        // Gün hücreleri padding sıfırlanarak metin kesilmesi (...) tamamen engellendi
        int daysInMonth = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);
        bool showWeekends = _chkShowWeekends != null && _chkShowWeekends.Checked;
        bool onlyUpToToday = _chkOnlyUpToToday != null && _chkOnlyUpToToday.Checked;
        bool isCurrentMonth = _selectedMonth.Year == DateTime.Today.Year && _selectedMonth.Month == DateTime.Today.Month;
        int todayDay = isCurrentMonth ? DateTime.Today.Day : -1;

        for (int d = 1; d <= daysInMonth; d++)
        {
            var date = new DateTime(_selectedMonth.Year, _selectedMonth.Month, d);
            var dayOfWeek = date.DayOfWeek;
            var isWeekend = dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var isToday = (d == todayDay);
            var isFuture = isCurrentMonth && (d > todayDay);

            bool isColVisible = (!isWeekend || showWeekends || isToday);
            if (isFuture && onlyUpToToday) isColVisible = false;

            var col = new DataGridViewTextBoxColumn
            {
                Name = $"colDay_{d}",
                HeaderText = $"{d}\n{GetDayInitial(dayOfWeek)}",
                Width = 32,
                Tag = isWeekend,
                Visible = isColVisible,
                ReadOnly = !MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData, // Yetkiye göre kilitli/serbest
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Padding = Padding.Empty,
                    BackColor = isToday ? Color.FromArgb(239, 246, 255)
                              : (isFuture ? Color.FromArgb(241, 245, 249)
                              : (isWeekend ? Color.FromArgb(226, 232, 240) : Color.White)),
                    ForeColor = isToday ? Color.FromArgb(29, 78, 216)
                              : (isFuture ? Color.FromArgb(148, 163, 184) : Color.FromArgb(30, 41, 59))
                }
            };
            if (isToday)
            {
                col.HeaderCell.Style.BackColor = Color.FromArgb(37, 99, 235); // Blue-600
                col.HeaderCell.Style.ForeColor = Color.White;
            }
            else if (isWeekend)
            {
                col.HeaderCell.Style.BackColor = Color.FromArgb(226, 232, 240);
                col.HeaderCell.Style.ForeColor = Color.FromArgb(71, 85, 105);
            }
            else if (isFuture)
            {
                col.HeaderCell.Style.BackColor = Color.FromArgb(241, 245, 249);
                col.HeaderCell.Style.ForeColor = Color.FromArgb(148, 163, 184); // slate-400
            }

            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            col.HeaderCell.Style.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            col.HeaderCell.Style.Padding = Padding.Empty;
            col.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
            _grid.Columns.Add(col);
        }

        // Summary columns
        var colTotalD = new DataGridViewTextBoxColumn
        {
            Name = "colTotalD",
            HeaderText = "D",
            Width = 30,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.Red, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = Padding.Empty }
        };
        colTotalD.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colTotalD.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        colTotalD.HeaderCell.Style.Padding = Padding.Empty;
        _grid.Columns.Add(colTotalD);

        var colTotalI = new DataGridViewTextBoxColumn
        {
            Name = "colTotalI",
            HeaderText = "İ",
            Width = 30,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.Blue, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = Padding.Empty }
        };
        colTotalI.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colTotalI.HeaderCell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        colTotalI.HeaderCell.Style.Padding = Padding.Empty;
        _grid.Columns.Add(colTotalI);

        var colTotalDays = new DataGridViewTextBoxColumn
        {
            Name = "colTotalDays",
            HeaderText = "Toplam\n(Gün)",
            Width = 52,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = Padding.Empty }
        };
        colTotalDays.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colTotalDays.HeaderCell.Style.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        colTotalDays.HeaderCell.Style.Padding = Padding.Empty;
        colTotalDays.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(colTotalDays);

        AdjustColumnWidthsAndCenter();
    }

    private static string GetDayInitial(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => "Pzt",
        DayOfWeek.Tuesday => "Sal",
        DayOfWeek.Wednesday => "Çar",
        DayOfWeek.Thursday => "Per",
        DayOfWeek.Friday => "Cum",
        DayOfWeek.Saturday => "Cmt",
        DayOfWeek.Sunday => "Paz",
        _ => ""
    };

    private async Task LoadCurrentMonthEntriesAsync()
    {
        try
        {
            _attendanceCache.Clear();
            var entries = await _attendanceRepo.GetForMonthAsync(_selectedMonth.Year, _selectedMonth.Month);
            foreach (var e in entries)
            {
                _attendanceCache[(e.StudentId, e.DayOfMonth)] = e.Code;
            }
            ApplyFilter();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 11 || ex.Message.Contains("malformed") || ex.Message.Contains("corrupt"))
        {
            MessageBox.Show(
                $"Veritabanı dosyasında disk bozulması (malformed database) tespit edildi!\n\n" +
                $"Hata Detayı: {ex.Message}\n\n" +
                "Bu durum genellikle program açıkken USB/Flash diskin güvenle kaldırılmadan çekilmesinden veya anlık elektrik kesintisinden kaynaklanır.\n\n" +
                "Çözüm: Lütfen sağlam 'Data\\meb_koordinatorluk.db' yedeğini geri yükleyin.",
                "Veritabanı Bozulması Tespit Edildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        if (_isInitializing || _grid == null || _grid.Columns.Count == 0) return;

        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        int internFilter = _cmbFilterInternship.SelectedIndex; // 0: Sadece İşletmeye Gidenler, 1: Tüm Öğrenciler, 2: İşletmeye Gitmeyenler
        var selectedField = _cmbFilterField.SelectedIndex > 0 ? _cmbFilterField.SelectedItem?.ToString() : null;
        var selectedGradeText = _cmbFilterGrade.SelectedIndex > 0 ? _cmbFilterGrade.SelectedItem?.ToString() : null;
        int? selectedGrade = null;
        if (!string.IsNullOrWhiteSpace(selectedGradeText))
        {
            var m = System.Text.RegularExpressions.Regex.Match(selectedGradeText, @"\d+");
            if (m.Success && int.TryParse(m.Value, out int g)) selectedGrade = g;
        }

        var selectedClass = _cmbFilterClass.SelectedIndex > 0 ? _cmbFilterClass.SelectedItem?.ToString() : null;
        var selectedEdu = _cmbFilterEdu.SelectedIndex > 0 ? _cmbFilterEdu.SelectedItem?.ToString() : null;

        var filtered = _allStudents.Where(s =>
        {
            var cls = _allClasses.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));

            // İşletme Durumu Filtresi (0: Sadece İşletmeye Gidenler, 1: Tüm Öğrenciler, 2: İşletmeye Gitmeyenler)
            if (internFilter == 0 || internFilter == 2)
            {
                bool isIntern = cls != null ? cls.HasInternship : FeeCalculator.IsEligibleForFeeCalculation(s, cls);
                if (internFilter == 0 && !isIntern) return false;
                if (internFilter == 2 && isIntern) return false;
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var match = s.FullName.ToLowerInvariant().Contains(q) ||
                            s.StudentNumber.ToLowerInvariant().Contains(q) ||
                            (s.EnterpriseName ?? "").ToLowerInvariant().Contains(q);
                if (!match) return false;
            }

            if (!string.IsNullOrWhiteSpace(selectedField) && !string.Equals(s.FieldName, selectedField, StringComparison.OrdinalIgnoreCase))
                return false;

            if (selectedGrade.HasValue)
            {
                int g = FeeCalculator.GetGradeLevel(s, cls);
                if (g != selectedGrade.Value)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(selectedClass) && !string.Equals(s.Classroom, selectedClass, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(selectedEdu))
            {
                var edu = FeeCalculator.GetEducationType(s, cls);
                if (!string.Equals(edu, selectedEdu, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(s.EducationType, selectedEdu, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        })
        .OrderBy(s => s.Classroom)
        .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
        .ToList();

        _lblCount.Text = $"Toplam {filtered.Count} Öğrenci";

        _grid.Rows.Clear();
        int daysInMonth = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);
        int idx = 1;

        foreach (var s in filtered)
        {
            var rowValues = new List<object>
            {
                _chkSelectAll?.Checked ?? true,
                idx++,
                s.StudentNumber,
                s.FullName,
                s.Classroom,
                s.EnterpriseName ?? "-"
            };

            double countD = 0;
            double countI = 0;
            double totalAbsentDays = 0;

            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(_selectedMonth.Year, _selectedMonth.Month, d);
                bool isOutside = IsDateOutsideSgkPeriod(s, date);

                var code = isOutside ? "" : (_attendanceCache.TryGetValue((s.Id, d), out var c) ? c : GetDefaultDayCode(s, d));
                rowValues.Add(code);

                if (!isOutside)
                {
                    if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsUnexcused(code))
                    {
                        double days = MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetAbsenceDays(code);
                        countD += days;
                        totalAbsentDays += days;
                    }
                    else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsExcused(code))
                    {
                        double days = MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetAbsenceDays(code);
                        countI += days;
                        totalAbsentDays += days;
                    }
                }
            }

            rowValues.Add(countD.ToString("0.#"));
            rowValues.Add(countI.ToString("0.#"));
            rowValues.Add(totalAbsentDays.ToString("0.#"));

            var rowIndex = _grid.Rows.Add(rowValues.ToArray());
            _grid.Rows[rowIndex].Height = 42;
            _grid.Rows[rowIndex].Tag = s;
        }
        CenterGrid();
        UpdateSelectedCountLabel();
    }

    private void UpdateSelectedCountLabel()
    {
        if (_lblCount == null) return;
        int total = _grid.Rows.Count;
        int selected = 0;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (!row.IsNewRow && _grid.Columns.Contains("colSelect") && row.Cells["colSelect"].Value is true)
            {
                selected++;
            }
        }
        _lblCount.Text = selected == total
            ? $"Toplam {total} Öğrenci"
            : $"Toplam {total} Öğrenci ({selected} Yazdırılacak)";
    }

    private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_isInitializing || e.RowIndex < 0 || e.ColumnIndex < 0) return;
        var col = _grid.Columns[e.ColumnIndex];
        if (col.Name == "colSelect")
        {
            if (!_isUpdatingSelectAll) UpdateSelectedCountLabel();
            return;
        }
        if (!col.Name.StartsWith("colDay_")) return;

        if (!int.TryParse(col.Name.Substring("colDay_".Length), out int dayIndex)) return;
        int daysInMonth = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);
        if (dayIndex < 1 || dayIndex > daysInMonth) return;

        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not Student student) return;

        var val = row.Cells[e.ColumnIndex].Value?.ToString()?.Trim().ToUpperInvariant() ?? "";
        _attendanceCache[(student.Id, dayIndex)] = val;

        // Canlı toplam hücrelerini güncelle
        double countD = 0, countI = 0;
        double totalAbsentDays = 0;
        for (int d = 1; d <= daysInMonth; d++)
        {
            var dayColName = $"colDay_{d}";
            if (!_grid.Columns.Contains(dayColName)) continue;
            var date = new DateTime(_selectedMonth.Year, _selectedMonth.Month, d);
            if (IsDateOutsideSgkPeriod(student, date)) continue;

            var c = row.Cells[dayColName].Value?.ToString()?.Trim().ToUpperInvariant() ?? "";
            if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsUnexcused(c))
            {
                double days = MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetAbsenceDays(c);
                countD += days;
                totalAbsentDays += days;
            }
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsExcused(c))
            {
                double days = MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetAbsenceDays(c);
                countI += days;
                totalAbsentDays += days;
            }
        }

        if (_grid.Columns.Contains("colTotalD"))
            row.Cells["colTotalD"].Value = countD.ToString("0.#");
        if (_grid.Columns.Contains("colTotalI"))
            row.Cells["colTotalI"].Value = countI.ToString("0.#");
        if (_grid.Columns.Contains("colTotalDays"))
            row.Cells["colTotalDays"].Value = totalAbsentDays.ToString("0.#");

        _ = _attendanceRepo.SaveEntryAsync(new AttendanceEntry
        {
            Year = _selectedMonth.Year,
            Month = _selectedMonth.Month,
            StudentId = student.Id,
            DayOfMonth = dayIndex,
            Code = val
        });
    }

    private void InvalidateCrosshair(int row, int col)
    {
        if (row >= 0 && row < _grid.Rows.Count)
        {
            _grid.InvalidateRow(row);
        }
        if (col >= 0 && col < _grid.Columns.Count)
        {
            _grid.InvalidateColumn(col);
            _grid.InvalidateCell(col, -1);
        }
    }

    private void Grid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex == _hoveredRowIndex && e.ColumnIndex == _hoveredColumnIndex)
            return;

        int oldRow = _hoveredRowIndex;
        int oldCol = _hoveredColumnIndex;

        _hoveredRowIndex = e.RowIndex;
        _hoveredColumnIndex = e.ColumnIndex;

        InvalidateCrosshair(oldRow, oldCol);
        InvalidateCrosshair(_hoveredRowIndex, _hoveredColumnIndex);
    }

    private void Grid_MouseLeave(object? sender, EventArgs e)
    {
        int oldRow = _hoveredRowIndex;
        int oldCol = _hoveredColumnIndex;

        _hoveredRowIndex = -1;
        _hoveredColumnIndex = -1;

        InvalidateCrosshair(oldRow, oldCol);
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex == -1 && e.ColumnIndex >= 0)
        {
            var col = _grid.Columns[e.ColumnIndex];
            bool isWeekend = col.Tag is true;

            bool isCurrentMonth = _selectedMonth.Year == DateTime.Today.Year && _selectedMonth.Month == DateTime.Today.Month;
            int dayNum = 0;
            bool isDayCol = col.Name.StartsWith("colDay_") && int.TryParse(col.Name.Substring("colDay_".Length), out dayNum);
            bool isTodayCol = isCurrentMonth && isDayCol && (dayNum == DateTime.Today.Day);
            bool isFutureCol = isCurrentMonth && isDayCol && (dayNum > DateTime.Today.Day);

            int topBandHeight = 24;
            var subHeaderBounds = new Rectangle(
                e.CellBounds.X,
                e.CellBounds.Y + topBandHeight,
                e.CellBounds.Width,
                e.CellBounds.Height - topBandHeight);

            Color backColor;
            Color borderColor;
            Color textColor;

            if (isTodayCol)
            {
                // Bugün sütunu: dikkat çeken mavi başlık (#2563EB) ve beyaz metin
                if (e.ColumnIndex == _hoveredColumnIndex && _hoveredColumnIndex >= 0)
                {
                    backColor = Color.FromArgb(29, 78, 216); // blue-700
                    borderColor = Color.FromArgb(30, 64, 175);
                }
                else
                {
                    backColor = Color.FromArgb(37, 99, 235); // blue-600
                    borderColor = Color.FromArgb(29, 78, 216);
                }
                textColor = Color.White;
            }
            else if (e.ColumnIndex == _hoveredColumnIndex && _hoveredColumnIndex >= 0)
            {
                backColor = Color.FromArgb(219, 234, 254);
                borderColor = Color.FromArgb(147, 197, 253);
                textColor = isWeekend ? Color.FromArgb(71, 85, 105) : (col.Name == "colTotalD" ? Color.Red : (col.Name == "colTotalI" ? Color.Blue : (isFutureCol ? Color.FromArgb(148, 163, 184) : Color.FromArgb(30, 41, 59))));
            }
            else if (isFutureCol)
            {
                // Gelecek günler: belirgin soluk/gri başlık
                backColor = Color.FromArgb(241, 245, 249); // slate-100 belirgin açık gri
                borderColor = Color.FromArgb(203, 213, 225);
                textColor = Color.FromArgb(148, 163, 184); // slate-400 soluk metin
            }
            else if (isWeekend)
            {
                backColor = Color.FromArgb(226, 232, 240);
                borderColor = Color.FromArgb(203, 213, 225);
                textColor = Color.FromArgb(71, 85, 105);
            }
            else
            {
                backColor = Color.FromArgb(248, 250, 252);
                borderColor = Color.FromArgb(226, 232, 240);
                textColor = col.Name == "colTotalD" ? Color.Red : (col.Name == "colTotalI" ? Color.Blue : Color.FromArgb(30, 41, 59));
            }

            using (var brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, subHeaderBounds);
            }

            using (var pen = new Pen(borderColor, 1))
            {
                e.Graphics.DrawRectangle(pen, subHeaderBounds.X, subHeaderBounds.Y, subHeaderBounds.Width - 1, subHeaderBounds.Height - 1);
            }

            // Metin çizimi
            var font = col.HeaderCell.Style.Font ?? _grid.ColumnHeadersDefaultCellStyle.Font ?? new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(textColor))
            {
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                e.Graphics.DrawString(col.HeaderText, font, textBrush, subHeaderBounds, sf);
            }

            e.Handled = true;
        }
    }

    private void Grid_Paint(object? sender, PaintEventArgs e)
    {
        if (_grid == null || _grid.Columns.Count == 0) return;

        int topBandHeight = 24;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var fontWeek = new Font("Segoe UI", 8.2f, FontStyle.Bold);
        using var fontSection = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var penBorder = new Pen(Color.FromArgb(203, 213, 225), 1);
        using var sfCenter = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };

        // 1. Sol Kısım: Öğrenci ve İşletme Bilgileri
        string leftStartCol = _grid.Columns.Contains("colSelect") ? "colSelect" : "colIdx";
        if (_grid.Columns.Contains(leftStartCol) && _grid.Columns.Contains("colEnterprise"))
        {
            var rStart = _grid.GetCellDisplayRectangle(_grid.Columns[leftStartCol].Index, -1, true);
            var rEnd = _grid.GetCellDisplayRectangle(_grid.Columns["colEnterprise"].Index, -1, true);

            int leftX = rStart.Width > 0 ? rStart.Left : 0;
            int rightX = rEnd.Width > 0 ? rEnd.Right : 0;

            if (rightX > leftX)
            {
                var rectInfo = new Rectangle(leftX, 0, rightX - leftX, topBandHeight);
                using var brushInfo = new SolidBrush(Color.FromArgb(241, 245, 249)); // slate-100
                using var brushText = new SolidBrush(Color.FromArgb(71, 85, 105)); // slate-600
                g.FillRectangle(brushInfo, rectInfo);
                g.DrawRectangle(penBorder, rectInfo.X, rectInfo.Y, rectInfo.Width - 1, rectInfo.Height - 1);
                g.DrawString("ÖĞRENCİ VE İŞLETME BİLGİLERİ", fontSection, brushText, rectInfo, sfCenter);
            }
        }

        // 2. Orta Kısım: Hafta Grupları
        var weekGroups = AttendanceWeekHelper.GetMonthWeekGroups(_selectedMonth.Year, _selectedMonth.Month);
        foreach (var wg in weekGroups)
        {
            string startColName = $"colDay_{wg.StartDay}";
            string endColName = $"colDay_{wg.EndDay}";

            if (!_grid.Columns.Contains(startColName) || !_grid.Columns.Contains(endColName))
                continue;

            int startColIdx = _grid.Columns[startColName].Index;
            int endColIdx = _grid.Columns[endColName].Index;

            var rStart = _grid.GetCellDisplayRectangle(startColIdx, -1, true);
            var rEnd = _grid.GetCellDisplayRectangle(endColIdx, -1, true);

            int leftX = -1;
            int rightX = -1;

            if (_grid.Columns[startColName].Visible && rStart.Width > 0)
            {
                leftX = rStart.Left;
            }
            else
            {
                for (int d = wg.StartDay; d <= wg.EndDay; d++)
                {
                    string colName = $"colDay_{d}";
                    if (_grid.Columns.Contains(colName) && _grid.Columns[colName].Visible)
                    {
                        var r = _grid.GetCellDisplayRectangle(_grid.Columns[colName].Index, -1, true);
                        if (r.Width > 0)
                        {
                            leftX = r.Left;
                            break;
                        }
                    }
                }
            }

            if (_grid.Columns[endColName].Visible && rEnd.Width > 0)
            {
                rightX = rEnd.Right;
            }
            else
            {
                for (int d = wg.EndDay; d >= wg.StartDay; d--)
                {
                    string colName = $"colDay_{d}";
                    if (_grid.Columns.Contains(colName) && _grid.Columns[colName].Visible)
                    {
                        var r = _grid.GetCellDisplayRectangle(_grid.Columns[colName].Index, -1, true);
                        if (r.Width > 0)
                        {
                            rightX = r.Right;
                            break;
                        }
                    }
                }
            }

            if (leftX != -1 && rightX != -1 && rightX > leftX)
            {
                var rectWeek = new Rectangle(leftX, 0, rightX - leftX, topBandHeight);

                Color bgColor = wg.IsTransition
                    ? Color.FromArgb(241, 245, 249) // slate-100
                    : Color.FromArgb(238, 242, 255); // indigo-50
                Color textColor = wg.IsTransition
                    ? Color.FromArgb(100, 116, 139) // slate-500
                    : Color.FromArgb(30, 58, 138); // indigo-900

                using var brushBg = new SolidBrush(bgColor);
                using var brushText = new SolidBrush(textColor);

                g.FillRectangle(brushBg, rectWeek);
                g.DrawRectangle(penBorder, rectWeek.X, rectWeek.Y, rectWeek.Width - 1, rectWeek.Height - 1);
                g.DrawString(wg.Title, fontWeek, brushText, rectWeek, sfCenter);
            }
        }

        // 3. Sağ Kısım: Toplam Devamsızlık
        if (_grid.Columns.Contains("colTotalD") && _grid.Columns.Contains("colTotalDays"))
        {
            var rStart = _grid.GetCellDisplayRectangle(_grid.Columns["colTotalD"].Index, -1, true);
            var rEnd = _grid.GetCellDisplayRectangle(_grid.Columns["colTotalDays"].Index, -1, true);

            int leftX = rStart.Width > 0 ? rStart.Left : 0;
            int rightX = rEnd.Width > 0 ? rEnd.Right : 0;

            if (rightX > leftX)
            {
                var rectTotals = new Rectangle(leftX, 0, rightX - leftX, topBandHeight);
                using var brushTotals = new SolidBrush(Color.FromArgb(254, 242, 242)); // rose-50
                using var brushText = new SolidBrush(Color.FromArgb(153, 27, 27)); // rose-800
                g.FillRectangle(brushTotals, rectTotals);
                g.DrawRectangle(penBorder, rectTotals.X, rectTotals.Y, rectTotals.Width - 1, rectTotals.Height - 1);
                g.DrawString("TOPLAM", fontSection, brushText, rectTotals, sfCenter);
            }
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var col = _grid.Columns[e.ColumnIndex];
        bool isWeekend = col.Tag is true;

        bool isCurrentMonth = _selectedMonth.Year == DateTime.Today.Year && _selectedMonth.Month == DateTime.Today.Month;
        int dIndex = 0;
        bool isDayCol = col.Name.StartsWith("colDay_") && int.TryParse(col.Name.Substring("colDay_".Length), out dIndex);
        bool isTodayCol = isCurrentMonth && isDayCol && (dIndex == DateTime.Today.Day);
        bool isFutureCol = isCurrentMonth && isDayCol && (dIndex > DateTime.Today.Day);

        bool isHoveredRow = (e.RowIndex == _hoveredRowIndex && _hoveredRowIndex >= 0);
        bool isHoveredCol = (e.ColumnIndex == _hoveredColumnIndex && _hoveredColumnIndex >= 0);

        if (isHoveredRow && isHoveredCol)
        {
            e.CellStyle.BackColor = Color.FromArgb(191, 219, 254); // Kesişim odak hücresi: belirgin mavi
        }
        else if (isHoveredRow)
        {
            e.CellStyle.BackColor = isTodayCol ? Color.FromArgb(219, 234, 254) : (isWeekend ? Color.FromArgb(224, 231, 255) : Color.FromArgb(239, 246, 255)); // İlgili öğrenci satırı
        }
        else if (isHoveredCol)
        {
            e.CellStyle.BackColor = isTodayCol ? Color.FromArgb(219, 234, 254) : Color.FromArgb(240, 249, 255); // İlgili gün sütunu: hafif gök mavisi vurgu
        }
        else if (isTodayCol)
        {
            // Bugün sütunu: yumuşak mavi zemin (#EFF6FF)
            e.CellStyle.BackColor = Color.FromArgb(239, 246, 255);
        }
        else if (isWeekend)
        {
            // Cumartesi ve Pazar günleri hücreleri belirgin gri
            e.CellStyle.BackColor = Color.FromArgb(226, 232, 240); // slate-200 nötr gri zemin
            if (e.Value?.ToString() == "-")
            {
                e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184); // slate-400 soluk çizgi
            }
        }
        else if (isFutureCol)
        {
            // Henüz gelmemiş gelecek günler: belirgin açık gri zemin
            e.CellStyle.BackColor = Color.FromArgb(241, 245, 249); // slate-100 belirgin açık gri
            e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184); // slate-400
        }

        // Değer Renklendirmeleri (D: Kırmızı, İ: Mavi, SY/ÖY: Sarı)
        if (col.Name.StartsWith("colDay_") && _grid.Rows[e.RowIndex].Tag is Student st)
        {
            if (isDayCol)
            {
                var date = new DateTime(_selectedMonth.Year, _selectedMonth.Month, dIndex);
                if (IsDateOutsideSgkPeriod(st, date))
                {
                    e.CellStyle.BackColor = Color.FromArgb(241, 245, 249);
                    e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
                    e.Value = "";
                    return;
                }
            }

            var val = e.Value?.ToString()?.Trim().ToUpperInvariant() ?? "";
            if (val == "X")
            {
                e.CellStyle.ForeColor = Color.FromArgb(22, 101, 52); // green-800
                e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }
            else if (val == "-")
            {
                e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184); // slate-400
            }
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsDutyOrActivity(val)) // N, F
            {
                e.CellStyle.ForeColor = Color.FromArgb(109, 40, 217); // purple-700
                e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsUnexcused(val)) // D, ÖY, SY, Y
            {
                e.CellStyle.ForeColor = Color.FromArgb(220, 38, 38); // red-600
                e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsHealthReport(val)) // S, SH, K
            {
                e.CellStyle.ForeColor = Color.FromArgb(217, 119, 6); // amber-600
                e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsExcused(val)) // İ, SV, V, DA, YA, T, C, GÖ, TU
            {
                e.CellStyle.ForeColor = Color.FromArgb(37, 99, 235); // blue-600
                e.CellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            }
        }
    }

    private void CmbBulkCode_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _cmbBulkCode.Items.Count) return;

        e.DrawBackground();

        var item = _cmbBulkCode.Items[e.Index] as MebKoordinatorluk.Core.Services.AttendanceCodeInfo;
        string code = item?.Code ?? "";
        string desc = item?.Description ?? "";

        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Color codeColor = Color.FromArgb(30, 41, 59); // slate-800
        Color descColor = isSelected ? SystemColors.HighlightText : Color.FromArgb(71, 85, 105); // slate-600

        if (!isSelected)
        {
            if (code == "X") codeColor = Color.FromArgb(22, 101, 52); // green-800
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsDutyOrActivity(code)) codeColor = Color.FromArgb(109, 40, 217); // purple-700
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsUnexcused(code)) codeColor = Color.FromArgb(220, 38, 38); // red-600
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsHealthReport(code)) codeColor = Color.FromArgb(217, 119, 6); // amber-600
            else if (MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsExcused(code)) codeColor = Color.FromArgb(37, 99, 235); // blue-600
        }
        else
        {
            codeColor = SystemColors.HighlightText;
        }

        using var fontCode = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var fontDesc = new Font("Segoe UI", 9f, FontStyle.Regular);
        using var brushCode = new SolidBrush(codeColor);
        using var brushDesc = new SolidBrush(descColor);
        using var brushSep = new SolidBrush(isSelected ? SystemColors.HighlightText : Color.FromArgb(203, 213, 225));

        // 1. Sütun: Kod (Genişlik: 34px, Sola hizalı)
        var codeRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, 34, e.Bounds.Height);
        var sfCode = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
        g.DrawString(code, fontCode, brushCode, codeRect, sfCode);

        // Sabit Ayraç: x=42'de
        var sepRect = new Rectangle(e.Bounds.X + 42, e.Bounds.Y, 14, e.Bounds.Height);
        var sfSep = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
        g.DrawString("—", fontCode, brushSep, sepRect, sfSep);

        // 2. Sütun: Açıklama (x=60'tan itibaren cetvel gibi dümdüz)
        var descRect = new Rectangle(e.Bounds.X + 60, e.Bounds.Y, Math.Max(10, e.Bounds.Width - 64), e.Bounds.Height);
        var sfDesc = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(desc, fontDesc, brushDesc, descRect, sfDesc);

        e.DrawFocusRectangle();
    }

    private string GetSelectedBulkCode()
    {
        if (_cmbBulkCode.SelectedItem is MebKoordinatorluk.Core.Services.AttendanceCodeInfo info)
            return info.Code;
        var item = _cmbBulkCode.SelectedItem?.ToString() ?? "X";
        var dashIdx = item.IndexOf('—');
        if (dashIdx > 0)
            return item[..dashIdx].Trim().ToUpperInvariant();
        var spaceIdx = item.IndexOf(' ');
        if (spaceIdx > 0)
            return item[..spaceIdx].Trim().ToUpperInvariant();
        return item.Trim().ToUpperInvariant();
    }

    private async void BtnApplyBulk_Click(object? sender, EventArgs e)
    {
        if (!int.TryParse(_cmbBulkDay.SelectedItem?.ToString(), out var day)) return;
        var code = GetSelectedBulkCode();
        if (string.IsNullOrWhiteSpace(code)) code = "X";

        var targetColName = $"colDay_{day}";
        if (!_grid.Columns.Contains(targetColName)) return;

        var targetDate = new DateTime(_selectedMonth.Year, _selectedMonth.Month, day);

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is Student s)
            {
                if (IsDateOutsideSgkPeriod(s, targetDate)) continue;
                row.Cells[targetColName].Value = code;
                _attendanceCache[(s.Id, day)] = code;
                await _attendanceRepo.SaveEntryAsync(new AttendanceEntry
                {
                    Year = _selectedMonth.Year,
                    Month = _selectedMonth.Month,
                    StudentId = s.Id,
                    DayOfMonth = day,
                    Code = code
                });
            }
        }

        ApplyFilter();
        MessageBox.Show($"{day}. gün için tüm öğrencilere '{code}' kodu atandı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void BtnResetMonth_Click(object? sender, EventArgs e)
    {
        var res = MessageBox.Show(
            $"{_selectedMonth:MMMM yyyy} ayı için tüm öğrencilerin devamsızlık kayıtlarını çalışma takvimine göre (haftaiçi 'X', okul dışı günler boş '') sıfırlamak istiyor musunuz?",
            "Ayı Sıfırla",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res == DialogResult.Yes)
        {
            int daysInMonth = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);
            var entries = new List<AttendanceEntry>();

            foreach (var s in _allStudents)
            {
                for (int d = 1; d <= daysInMonth; d++)
                {
                    var code = GetDefaultDayCode(s, d);
                    _attendanceCache[(s.Id, d)] = code;
                    entries.Add(new AttendanceEntry
                    {
                        Year = _selectedMonth.Year,
                        Month = _selectedMonth.Month,
                        StudentId = s.Id,
                        DayOfMonth = d,
                        Code = code
                    });
                }
            }

            await _attendanceRepo.SaveEntriesAsync(entries);
            ApplyFilter();
            MessageBox.Show("Ay çalışma takvimine göre başarıyla sıfırlandı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void BtnApplyClearRange_Click(object? sender, EventArgs e)
    {
        var start = _dtpClearStart.Value.Date;
        var end = _dtpClearEnd.Value.Date;

        if (start > end)
        {
            MessageBox.Show("Başlangıç tarihi bitiş tarihinden büyük olamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var res = MessageBox.Show(
            $"{start:dd.MM.yyyy} — {end:dd.MM.yyyy} tarihleri arasındaki tüm öğrenci devamsızlık girişleri tamamen temizlenecek (boş bırakılacak).\n\nOnaylıyor musunuz?",
            "Tarih Aralığını Temizle",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res != DialogResult.Yes) return;

        var entriesToSave = new List<AttendanceEntry>();
        int currentYear = _selectedMonth.Year;
        int currentMonth = _selectedMonth.Month;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.Year == currentYear && date.Month == currentMonth)
            {
                int day = date.Day;
                bool isWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);

                foreach (var s in _allStudents)
                {
                    var cls = _allClasses.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
                    if (FeeCalculator.IsMesem(s, cls)) continue;

                    string code = isWeekend ? "-" : "";
                    _attendanceCache[(s.Id, day)] = code;
                    entriesToSave.Add(new AttendanceEntry
                    {
                        Year = currentYear,
                        Month = currentMonth,
                        StudentId = s.Id,
                        DayOfMonth = day,
                        Code = code
                    });
                }
            }
        }

        if (entriesToSave.Count > 0)
        {
            await _attendanceRepo.SaveEntriesAsync(entriesToSave);
        }

        ApplyFilter();
        MessageBox.Show("Belirtilen tarih aralığındaki veriler başarıyla temizlendi (boş bırakıldı).", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void BtnAutoClearTerm_Click(object? sender, EventArgs e)
    {
        var (termStart, termEnd) = GetSchoolTermDates();
        int currentYear = _selectedMonth.Year;
        int currentMonth = _selectedMonth.Month;
        int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

        List<int> daysToClear = new();

        if (currentMonth == 9 && termStart.Month == 9)
        {
            for (int d = 1; d < termStart.Day && d <= daysInMonth; d++)
            {
                daysToClear.Add(d);
            }
        }
        else if (currentMonth == 6 && termEnd.Month == 6)
        {
            for (int d = termEnd.Day + 1; d <= daysInMonth; d++)
            {
                daysToClear.Add(d);
            }
        }

        if (daysToClear.Count == 0)
        {
            MessageBox.Show(
                $"Bu ay ({_selectedMonth:MMMM}) için okul öncesi veya sonrası temizlenecek gün aralığı bulunamadı.\n(Okul başlangıç: {termStart:dd.MM.yyyy}, Okul bitiş: {termEnd:dd.MM.yyyy})",
                "Bilgi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        string daysStr = string.Join(", ", daysToClear.Select(d => $"{d} {_selectedMonth:MMMM}"));
        var res = MessageBox.Show(
            $"Okul takvimine göre ders yapılmayan şu günler temizlenecektir:\n{daysStr}\n\nOnaylıyor musunuz?",
            "Okul Dışı Günleri Temizle",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res != DialogResult.Yes) return;

        var entriesToSave = new List<AttendanceEntry>();
        foreach (var d in daysToClear)
        {
            var date = new DateTime(currentYear, currentMonth, d);
            bool isWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);

            foreach (var s in _allStudents)
            {
                var cls = _allClasses.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
                // MESEM öğrencileri dönem kısıtına tabi değildir, yıl boyu stajdadır
                if (FeeCalculator.IsMesem(s, cls)) continue;

                string code = isWeekend ? "-" : "";
                _attendanceCache[(s.Id, d)] = code;
                entriesToSave.Add(new AttendanceEntry
                {
                    Year = currentYear,
                    Month = currentMonth,
                    StudentId = s.Id,
                    DayOfMonth = d,
                    Code = code
                });
            }
        }

        await _attendanceRepo.SaveEntriesAsync(entriesToSave);
        ApplyFilter();
        MessageBox.Show("Okul takvimi dışındaki günler başarıyla temizlendi (boş bırakıldı).", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("Devamsızlık Verilerini Excel Olarak Dışa Aktarma", this))
            return;

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"Devamsizlik_{_selectedMonth:yyyy_MM}.csv"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.Append("No,Ad Soyad,Sinif,Isletme");
            int days = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);
            for (int d = 1; d <= days; d++) sb.Append($",{d}");
            sb.AppendLine(",Toplam");

            var rowsToExport = _grid.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow && (!_grid.Columns.Contains("colSelect") || r.Cells["colSelect"].Value is true))
                .ToList();
            if (rowsToExport.Count == 0) rowsToExport = _grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();

            foreach (var row in rowsToExport)
            {
                var line = $"{row.Cells["colNumber"].Value},{row.Cells["colName"].Value},{row.Cells["colClass"].Value},{row.Cells["colEnterprise"].Value}";
                for (int d = 1; d <= days; d++)
                {
                    line += $",{row.Cells[$"colDay_{d}"].Value}";
                }
                line += $",{row.Cells["colTotalDays"].Value}";
                sb.AppendLine(line);
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Devamsızlık çizelgesi başarıyla kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void BtnUploadExcel_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("Excel'den Toplu Devamsızlık Yükleme", this))
            return;

        using var ofd = new OpenFileDialog
        {
            Filter = "Excel ve CSV Dosyaları (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Tüm Dosyalar (*.*)|*.*",
            Title = "Devamsızlık Verisi Dosyasını Seçin"
        };

        if (ofd.ShowDialog() != DialogResult.OK) return;

        // 2 Ay ve Öncesi Geriye Dönük Devamsızlık & SGK Prim Güvenlik Kalkanı
        int monthDiff = (DateTime.Today.Year - _selectedMonth.Year) * 12 + (DateTime.Today.Month - _selectedMonth.Month);
        if (monthDiff >= 2)
        {
            var trCulture = new System.Globalization.CultureInfo("tr-TR");
            string monthStr = _selectedMonth.ToString("MMMM yyyy", trCulture).ToUpper(trCulture);
            var warningMsg = $"⚠️ KRİTİK SGK VE MEVZUAT UYARISI\n\n" +
                             $"Devamsızlık yüklemek istediğiniz dönem: {monthStr}\n" +
                             $"Bu dönem cari aydan {monthDiff} ay öncesine aittir.\n\n" +
                             $"📌 SGK Bildirge & İdari Ceza Riski:\n" +
                             $"• Bu aya ait SGK e-Bildirge (Muhtasar ve Prim Hizmet Beyannamesi) ve MYS ödemeleri yasal olarak KESİNLEŞMİŞ olabilir.\n" +
                             $"• 2 ay ve öncesine geriye dönük devamsızlık işlenmesi, öğrencinin prim gün sayısını değiştireceğinden SGK'ya 'İptal / Ek Bildirge' verilmesini zorunlu kılar.\n" +
                             $"• Yasal süresi geçen bildirimler SGK tarafından İdari Para Cezası (İPC) ve gecikme faizi ile cezalandırılabilir.\n\n" +
                             $"Riskleri kabul ederek {monthStr} ayına devamsızlık verilerini yine de Excel'den yüklemek istiyor musunuz?";

            var confirmResult = MessageBox.Show(
                this,
                warningMsg,
                "⚠️ Geriye Dönük Devamsızlık ve SGK Prim Uyarısı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            var grid = EOkulClassListParser.ExtractGridFromFile(ofd.FileName);
            if (grid.Count == 0)
            {
                MessageBox.Show("Seçilen dosyada okunabilir veri satırı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Temizleme Yardımcısı: Excel'den gelen floating number (231.0), boşluk, non-breaking space ve baş sıfırları temizler
            static string CleanStudentNo(string val)
            {
                if (string.IsNullOrWhiteSpace(val)) return "";
                val = val.Replace("\u00A0", " ").Trim();
                int dotIdx = val.IndexOf('.');
                if (dotIdx >= 0) val = val.Substring(0, dotIdx);
                int commaIdx = val.IndexOf(',');
                if (commaIdx >= 0) val = val.Substring(0, commaIdx);
                val = val.Trim();
                return val.TrimStart('0');
            }

            // Öğrenci No Tespiti: colNumber sütunundan dene; eğer boş ise colName öncesindeki sayısal hücreyi otomatik bul (C/D sütunları)
            static string FindStudentNoInRow(List<string> row, int colNumber, int colName)
            {
                if (colNumber >= 0 && colNumber < row.Count)
                {
                    string val = row[colNumber].Trim();
                    string clean = CleanStudentNo(val);
                    if (!string.IsNullOrEmpty(clean) && clean.All(char.IsDigit)) return val;
                }

                int maxCol = colName > 0 ? colName - 1 : Math.Min(5, row.Count - 1);
                for (int c = maxCol; c >= 0; c--)
                {
                    if (c >= row.Count) continue;
                    string val = row[c].Trim();
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    if (val.Contains("-") && val.Length > 7) continue;
                    if (val.ToLowerInvariant().Contains("dönem") || val.ToLowerInvariant().Contains("donem")) continue;

                    string clean = CleanStudentNo(val);
                    if (!string.IsNullOrEmpty(clean) && clean.All(char.IsDigit)) return val;
                }

                for (int c = 0; c < row.Count; c++)
                {
                    string val = row[c].Trim();
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    string clean = CleanStudentNo(val);
                    if (!string.IsNullOrEmpty(clean) && clean.All(char.IsDigit) && clean.Length <= 6) return val;
                }

                return colNumber >= 0 && colNumber < row.Count ? row[colNumber].Trim() : "";
            }

            // Sütun Indekslerinin Tespiti (Header Taraması & D, E, J, L, N Kontrolü)
            int headerRowIndex = -1;
            int colNumber = -1;
            int colName = -1;
            int colDate = -1;
            int colType = -1;
            int colDayCount = -1;

            var trCulture = CultureInfo.GetCultureInfo("tr-TR");

            for (int r = 0; r < Math.Min(15, grid.Count); r++)
            {
                var row = grid[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var cell = row[c].Trim().ToLower(trCulture);
                    if (colNumber < 0 && (cell.Contains("numara") || cell == "no" || cell.Contains("okul no") || cell == "numarası" || cell == "numarasi"))
                    {
                        colNumber = c;
                        headerRowIndex = r;
                    }
                    else if (colDate < 0 && (cell.Contains("tarih") || cell == "tarihi"))
                    {
                        colDate = c;
                        headerRowIndex = r;
                    }
                    else if (colType < 0 && (cell.Contains("tür") || cell == "türü" || cell.Contains("durum") || cell.Contains("kod")))
                    {
                        colType = c;
                        headerRowIndex = r;
                    }
                    else if (colName < 0 && (cell.Contains("ad") || cell.Contains("soyad") || cell == "adı soyadı"))
                    {
                        colName = c;
                    }
                    else if (colDayCount < 0 && (cell.Contains("gün") || cell.Contains("gun")))
                    {
                        colDayCount = c;
                    }
                }
                if (headerRowIndex >= 0 && colNumber >= 0 && colDate >= 0) break;
            }

            // Resimdeki varsayılan sütun yapısı (D=3: NUMARASI, E=4: ADI SOYADI, J=9: TARİHİ, L=11: TÜRÜ, N=13: GÜN SAYISI)
            if (colNumber < 0) colNumber = 3;   // D Sütunu (4. Sütun, index 3)
            if (colName < 0) colName = 4;     // E Sütunu (5. Sütun, index 4)
            if (colDate < 0) colDate = 9;     // J Sütunu (10. Sütun, index 9)
            if (colType < 0) colType = 11;    // L Sütunu (12. Sütun, index 11)
            if (colDayCount < 0) colDayCount = 13; // N Sütunu (14. Sütun, index 13)

            static DateTime? ParseDate(string val)
            {
                if (string.IsNullOrWhiteSpace(val)) return null;
                val = val.Replace("\u00A0", " ").Trim();
                if (val.Contains(" ")) val = val.Split(' ')[0]; // Tarih kısmını al

                if (DateTime.TryParse(val, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var dtTr))
                    return dtTr;

                if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtInv))
                    return dtInv;

                var formats = new[] { "dd/MM/yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "d/M/yyyy", "d.M.yyyy", "dd-MM-yyyy", "yyyy/MM/dd" };
                if (DateTime.TryParseExact(val, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDt))
                    return exactDt;

                if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double oaDate) && oaDate > 30000 && oaDate < 60000)
                {
                    try { return DateTime.FromOADate(oaDate); } catch { }
                }

                return null;
            }

            int startRow = headerRowIndex >= 0 ? headerRowIndex + 1 : 1; // 0. satır başlık ise veri D2 satırından (index 1) başlar
            var entriesToSave = new List<AttendanceEntry>();
            var unmatchedReports = new List<(int rowNum, string studentNo, string name, string dateStr, string typeStr, string reason)>();
            int totalProcessed = 0;
            int successCount = 0;
            int monthMismatchCount = 0;

            for (int i = startRow; i < grid.Count; i++)
            {
                var row = grid[i];
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                string GetCell(int cIdx) => (cIdx >= 0 && cIdx < row.Count) ? row[cIdx].Trim() : "";

                string rawNumStr = FindStudentNoInRow(row, colNumber, colName);
                string nameStr = GetCell(colName);
                string dateStr = GetCell(colDate);
                string typeStr = GetCell(colType);

                if (string.IsNullOrWhiteSpace(rawNumStr) && string.IsNullOrWhiteSpace(dateStr))
                    continue;

                totalProcessed++;

                string cleanNum = CleanStudentNo(rawNumStr);

                if (string.IsNullOrWhiteSpace(cleanNum))
                {
                    unmatchedReports.Add((i + 1, rawNumStr, nameStr, dateStr, typeStr, "Öğrenci numarası boş veya okunamadı"));
                    continue;
                }

                // Öğrenci Karşılaştırma (Numara eşleştirme: 231, 231.0, 0231 hepsi tam eşleşir)
                var student = _allStudents.FirstOrDefault(s =>
                {
                    var sClean = CleanStudentNo(s.StudentNumber);
                    if (!string.IsNullOrEmpty(cleanNum) && !string.IsNullOrEmpty(sClean))
                    {
                        return cleanNum.Equals(sClean, StringComparison.OrdinalIgnoreCase);
                    }
                    return s.StudentNumber.Trim().Equals(rawNumStr, StringComparison.OrdinalIgnoreCase);
                });

                if (student == null)
                {
                    unmatchedReports.Add((i + 1, rawNumStr, nameStr, dateStr, typeStr, "Öğrenci numarası sistemde bulunamadı (Öğrenciler ekranında bu numara kayıtlı değil)"));
                    continue;
                }

                // Tarih ve Ay Karşılaştırma (Seçili Sistem Ayı ile J Sütunundaki Ay Karşılaştırılır)
                var dt = ParseDate(dateStr);
                if (!dt.HasValue)
                {
                    unmatchedReports.Add((i + 1, rawNumStr, nameStr, dateStr, typeStr, "Tarih okunamadı veya geçersiz format"));
                    continue;
                }

                if (dt.Value.Year != _selectedMonth.Year || dt.Value.Month != _selectedMonth.Month)
                {
                    monthMismatchCount++;
                    continue; // Seçili sistem ayına ait olmayan tarihleri atla
                }

                // Türüne Göre Devamsızlık Girişi (L sütunundaki koda göre)
                string code = typeStr.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(code)) code = "D";

                int day = dt.Value.Day;
                _attendanceCache[(student.Id, day)] = code;
                entriesToSave.Add(new AttendanceEntry
                {
                    StudentId = student.Id,
                    Year = dt.Value.Year,
                    Month = dt.Value.Month,
                    DayOfMonth = day,
                    Code = code
                });
                successCount++;
            }

            if (entriesToSave.Count > 0)
            {
                await _attendanceRepo.SaveEntriesAsync(entriesToSave);
                ApplyFilter();
            }

            // Not Defteri Raporu Oluşturma (Eşleşmeyen veya Hatalı Öğrenciler Var İse)
            if (unmatchedReports.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("==========================================================================================");
                sb.AppendLine("                 MEB KOORDİNATÖRLÜK SİSTEMİ - EXCEL DEVAMSIZLIK EŞLEŞTİRME RAPORU");
                sb.AppendLine("==========================================================================================");
                sb.AppendLine($"Tarih / Saat       : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                sb.AppendLine($"Seçili Sistem Ayı  : {_selectedMonth:MMMM yyyy}");
                sb.AppendLine($"İşlenen Dosya      : {Path.GetFileName(ofd.FileName)}");
                sb.AppendLine("------------------------------------------------------------------------------------------");
                sb.AppendLine($"Okunan Satır Sayısı      : {totalProcessed}");
                sb.AppendLine($"Başarıyla Yüklenen Kayıt : {successCount}");
                sb.AppendLine($"Eşleşmeyen / Hatalı Satır: {unmatchedReports.Count}");
                if (monthMismatchCount > 0)
                    sb.AppendLine($"Farklı Aya Ait Satır Sayı: {monthMismatchCount} (Seçili ay dışındaki tarihler işlenmedi)");
                if (monthDiff >= 2)
                    sb.AppendLine($"⚠️ DİKKAT: Seçili ay ({_selectedMonth:MMMM yyyy}) cari tarihten {monthDiff} ay öncesidir. SGK e-Bildirge iptal/ek beyannamesi gerekebilir!");
                sb.AppendLine("==========================================================================================");
                sb.AppendLine();
                sb.AppendLine("EŞLEŞMEYEN VEYA SİSTEMDE BULUNAMAYAN ÖĞRENCİ VE SATIR BİLGİLERİ:");
                sb.AppendLine("------------------------------------------------------------------------------------------");
                sb.AppendLine(string.Format("{0,-8} | {1,-10} | {2,-25} | {3,-12} | {4,-6} | {5}",
                    "Satır No", "Okul No", "Adı Soyadı", "Tarih", "Tür", "Açıklama / Durum"));
                sb.AppendLine("------------------------------------------------------------------------------------------");

                foreach (var r in unmatchedReports)
                {
                    sb.AppendLine(string.Format("{0,-8} | {1,-10} | {2,-25} | {3,-12} | {4,-6} | {5}",
                        $"Satır {r.rowNum}",
                        string.IsNullOrWhiteSpace(r.studentNo) ? "-" : r.studentNo,
                        string.IsNullOrWhiteSpace(r.name) ? "-" : r.name,
                        string.IsNullOrWhiteSpace(r.dateStr) ? "-" : r.dateStr,
                        string.IsNullOrWhiteSpace(r.typeStr) ? "-" : r.typeStr,
                        r.reason));
                }

                sb.AppendLine("------------------------------------------------------------------------------------------");
                sb.AppendLine();
                sb.AppendLine("ÇÖZÜM VE KONTROL ADIMLARI:");
                sb.AppendLine("1. Yukarıda listelenen öğrencilerin Okul Numaralarını kontrol ediniz.");
                sb.AppendLine("2. Öğrenci henüz sistemde tanımlı değilse 'Öğrenciler' ekranından yeni kayıt açınız.");
                sb.AppendLine("3. Gerekli düzeltmeleri yaptıktan sonra Excel dosyasını tekrar yükleyebilirsiniz.");
                sb.AppendLine("==========================================================================================");

                string reportPath = Path.Combine(Path.GetTempPath(), $"Devamsizlik_Yukleme_Raporu_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(true));

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{reportPath}\"",
                        UseShellExecute = true
                    });
                }
                catch { }

                MessageBox.Show(
                    $"{successCount} devamsızlık kaydı başarıyla yüklendi.\n\n⚠️ {unmatchedReports.Count} satır sistemle eşleşmedi. Detaylı rapor Not Defteri'nde açıldı.",
                    "Aktarım Tamamlandı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else if (successCount > 0)
            {
                MessageBox.Show($"{successCount} devamsızlık kaydı başarıyla yüklendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Seçilen dosyada {_selectedMonth:MMMM yyyy} ayına ait yüklenecek geçerli devamsızlık kaydı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dosya okunurken veya işlenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = "Devamsizlik_Sablon.csv"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var content = "Öğrenci No,Adı Soyadı,Tarih,Yıl,Ay,Gün,Devamsızlık Kodu,Açıklama\n" +
                          "101,Ali YILMAZ,15.09.2026,2026,9,15,X,Staja Geldi\n" +
                          "101,Ali YILMAZ,16.09.2026,2026,9,16,D,Özürsüz Devamsız\n" +
                          "102,Ayşe DEMİR,15.09.2026,2026,9,15,İ,İzinli\n" +
                          "102,Ayşe DEMİR,16.09.2026,2026,9,16,R,Raporlu\n";
            File.WriteAllText(sfd.FileName, content, Encoding.UTF8);
            MessageBox.Show("Örnek şablon başarıyla kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void UpdateViewModeButtonState()
    {
        if (_btnToggleViewMode == null) return;
        if (_isAutoFitMode)
        {
            _btnToggleViewMode.Text = "📐 Görünüm: Ekrana Sığdır";
            _btnToggleViewMode.BackColor = Color.FromArgb(238, 242, 255); // indigo-50
            _btnToggleViewMode.ForeColor = Color.FromArgb(67, 56, 202); // indigo-700
            _btnToggleViewMode.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254); // indigo-200
        }
        else
        {
            _btnToggleViewMode.Text = "↔️ Görünüm: Geniş (Kaydırmalı)";
            _btnToggleViewMode.BackColor = Color.FromArgb(248, 250, 252); // slate-50
            _btnToggleViewMode.ForeColor = Color.FromArgb(71, 85, 105); // slate-600
            _btnToggleViewMode.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225); // slate-300
        }
    }

    private void UpdateDayColumnsVisibility()
    {
        if (_grid == null || _grid.Columns.Count == 0) return;

        bool showWeekends = _chkShowWeekends?.Checked ?? false;
        bool onlyUpToToday = _chkOnlyUpToToday != null && _chkOnlyUpToToday.Checked;
        bool isCurrentMonth = _selectedMonth.Year == DateTime.Today.Year && _selectedMonth.Month == DateTime.Today.Month;
        int todayDay = isCurrentMonth ? DateTime.Today.Day : -1;
        int daysInMonth = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);

        _grid.SuspendLayout();
        for (int d = 1; d <= daysInMonth; d++)
        {
            string colName = $"colDay_{d}";
            if (!_grid.Columns.Contains(colName)) continue;

            var dt = new DateTime(_selectedMonth.Year, _selectedMonth.Month, d);
            bool isWeekend = (dt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
            bool isToday = (d == todayDay);
            bool isFuture = isCurrentMonth && (d > todayDay);

            bool isVisible = (!isWeekend || showWeekends || isToday);
            if (isFuture && onlyUpToToday)
            {
                isVisible = false;
            }

            _grid.Columns[colName].Visible = isVisible;
        }
        _grid.ResumeLayout();
        AdjustColumnWidthsAndCenter();
        _grid.Invalidate();
    }

    private void UpdateWeekendColumnsVisibility() => UpdateDayColumnsVisibility();

    private void AdjustColumnWidthsAndCenter()
    {
        if (_grid == null || _pnlGrid == null || _grid.Columns.Count == 0) return;

        int daysInMonth = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);

        // Görünür gün sütunu sayısı (hafta sonları gizlendiğinde orantılı dağılım için)
        int visibleDaysCount = 0;
        for (int d = 1; d <= daysInMonth; d++)
        {
            string dayColName = $"colDay_{d}";
            if (_grid.Columns.Contains(dayColName) && _grid.Columns[dayColName].Visible)
                visibleDaysCount++;
        }
        if (visibleDaysCount == 0) visibleDaysCount = daysInMonth;

        if (_isAutoFitMode)
        {
            int scrollbarWidth = SystemInformation.VerticalScrollBarWidth;
            // 48px yatay kenar payı (24 sol + 24 sağ) + dikey kaydırma çubuğu + 4px sınır
            int usableWidth = _pnlGrid.ClientSize.Width - 48 - scrollbarWidth - 4;

            if (usableWidth > 500)
            {
                // Sabit kompakt sütun genişlikleri
                int wSelect = 44;
                int wIdx = 32;
                int wNum = 56;
                int wClass = 46;
                int wTotalD = 28;
                int wTotalI = 28;
                int wTotalDays = 48;
                int fixedBase = wSelect + wIdx + wNum + wClass + wTotalD + wTotalI + wTotalDays;

                int remaining = usableWidth - fixedBase;

                // Esnek metin sütunları (Ad Soyad + İşletme) kalan alanın ~%22'sini hedefler
                int flexTextTarget = Math.Clamp((int)(remaining * 0.22), 200, 320);
                int remainingForDays = remaining - flexTextTarget;

                // Gün sütunları 28px - 44px arasında esner
                int dayWidth = Math.Clamp(remainingForDays / visibleDaysCount, 28, 44);

                // Kalan net genişliği Adı Soyadı ve İşletme sütunlarına paylaştır
                int dayTotal = dayWidth * visibleDaysCount;
                int leftoverForText = Math.Max(190, usableWidth - fixedBase - dayTotal);

                int wName = Math.Clamp((int)(leftoverForText * 0.58), 110, 220);
                int wEnterprise = Math.Clamp(leftoverForText - wName, 80, 180);

                if (_grid.Columns.Contains("colSelect")) _grid.Columns["colSelect"].Width = wSelect;
                if (_grid.Columns.Contains("colIdx")) _grid.Columns["colIdx"].Width = wIdx;
                if (_grid.Columns.Contains("colNumber")) _grid.Columns["colNumber"].Width = wNum;
                if (_grid.Columns.Contains("colName")) _grid.Columns["colName"].Width = wName;
                if (_grid.Columns.Contains("colClass")) _grid.Columns["colClass"].Width = wClass;
                if (_grid.Columns.Contains("colEnterprise")) _grid.Columns["colEnterprise"].Width = wEnterprise;

                for (int d = 1; d <= daysInMonth; d++)
                {
                    string dayColName = $"colDay_{d}";
                    if (_grid.Columns.Contains(dayColName))
                    {
                        _grid.Columns[dayColName].Width = dayWidth;
                    }
                }

                if (_grid.Columns.Contains("colTotalD")) _grid.Columns["colTotalD"].Width = wTotalD;
                if (_grid.Columns.Contains("colTotalI")) _grid.Columns["colTotalI"].Width = wTotalI;
                if (_grid.Columns.Contains("colTotalDays")) _grid.Columns["colTotalDays"].Width = wTotalDays;
            }
        }
        else
        {
            // Geniş (Kaydırmalı) Mod: Rahat ve sabit sütun genişlikleri
            if (_grid.Columns.Contains("colSelect")) _grid.Columns["colSelect"].Width = 46;
            if (_grid.Columns.Contains("colIdx")) _grid.Columns["colIdx"].Width = 36;
            if (_grid.Columns.Contains("colNumber")) _grid.Columns["colNumber"].Width = 65;
            if (_grid.Columns.Contains("colName")) _grid.Columns["colName"].Width = 165;
            if (_grid.Columns.Contains("colClass")) _grid.Columns["colClass"].Width = 55;
            if (_grid.Columns.Contains("colEnterprise")) _grid.Columns["colEnterprise"].Width = 145;

            for (int d = 1; d <= daysInMonth; d++)
            {
                string dayColName = $"colDay_{d}";
                if (_grid.Columns.Contains(dayColName))
                {
                    _grid.Columns[dayColName].Width = 42;
                }
            }

            if (_grid.Columns.Contains("colTotalD")) _grid.Columns["colTotalD"].Width = 32;
            if (_grid.Columns.Contains("colTotalI")) _grid.Columns["colTotalI"].Width = 32;
            if (_grid.Columns.Contains("colTotalDays")) _grid.Columns["colTotalDays"].Width = 55;
        }

        CenterGrid();
    }

    private void CenterGrid()
    {
        if (_grid == null || _pnlGrid == null || _pnlGrid.ClientSize.Width <= 0 || _pnlGrid.ClientSize.Height <= 0)
            return;

        int totalColumnsWidth = 0;
        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (col.Visible) totalColumnsWidth += col.Width;
        }

        if (totalColumnsWidth <= 0)
        {
            _grid.Location = new Point(24, 12);
            _grid.Size = new Size(Math.Max(100, _pnlGrid.ClientSize.Width - 48), Math.Max(100, _pnlGrid.ClientSize.Height - 24));
            return;
        }

        int scrollbarWidth = SystemInformation.VerticalScrollBarWidth;
        int desiredWidth = totalColumnsWidth + scrollbarWidth + 4;

        if (_pnlGrid.ClientSize.Width >= desiredWidth + 48)
        {
            // Panel takvimden daha geniş: Takvim ekranın tam ortasına yerleşir
            int targetX = (_pnlGrid.ClientSize.Width - desiredWidth) / 2;
            _grid.Location = new Point(targetX, 12);
            _grid.Size = new Size(desiredWidth, Math.Max(100, _pnlGrid.ClientSize.Height - 24));
        }
        else
        {
            // Panel daha dar: Eşit 24px kenar payıyla yayılır, yatay kaydırma çubuğu devreye girer
            _grid.Location = new Point(24, 12);
            _grid.Size = new Size(Math.Max(100, _pnlGrid.ClientSize.Width - 48), Math.Max(100, _pnlGrid.ClientSize.Height - 24));
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        AdjustColumnWidthsAndCenter();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            AdjustColumnWidthsAndCenter();
        }
    }

    // ── Yazdır / Önizleme ────────────────────────────────────────────────────

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (_grid.Rows.Count == 0)
        {
            MessageBox.Show("Yazdırılacak kayıt bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int selectedCount = _grid.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow && (!_grid.Columns.Contains("colSelect") || r.Cells["colSelect"].Value is true));
        if (selectedCount == 0)
        {
            MessageBox.Show("Yazdırılacak öğrenci seçilmedi! Lütfen listedeki 'Yazdır' kutucuklarından en az birini işaretleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var pd = AttendancePrintHelper.CreatePrintDocument(_grid, _selectedMonth, _appSettings);
        string subtitle = _cmbFilterClass.SelectedIndex > 0
            ? $"{_cmbFilterClass.SelectedItem} — {_selectedMonth:MMMM yyyy} ({selectedCount} Öğrenci)"
            : $"Tüm Sınıflar — {_selectedMonth:MMMM yyyy} ({selectedCount} Öğrenci)";
        PrintPreviewHelper.ShowPreview(pd, $"Resmi Devam-Devamsızlık Çizelgesi — {subtitle}", this);
    }
}
