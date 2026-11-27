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

public class DocumentsView : UserControl
{
    private readonly IDocumentTrackingRepository _docRepo;
    private readonly ITeacherRepository _teacherRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly ISettingRepository? _settingRepo;

    private AppSettings _settings = new();
    private List<Teacher> _allTeachers = [];
    private List<Enterprise> _allEnterprises = [];
    private List<Student> _allStudents = [];
    private List<DocumentTrackingRecord> _currentMonthRecords = [];
    private DateTime _selectedMonth = DateTime.Today;
    private Button _btnPrevMonth = null!;
    private Button _btnNextMonth = null!;

    // Stat Cards
    private Label _lblStatTeachers = null!;
    private Label _lblStatEnterprises = null!;
    private Label _lblStatRate = null!;
    private Label _lblStatPending = null!;

    private Label _lblMonthInfo = null!;
    private ComboBox _cmbFilterField = null!;
    private ComboBox _cmbFilterTeacher = null!;
    private ComboBox _cmbFilterStatus = null!;
    private TextBox _txtSearch = null!;
    private Label _lblCount = null!;

    private int _hoveredRowIndex = -1;
    private int _hoveredColumnIndex = -1;

    private DataGridView _grid = null!;

    public DocumentsView(
        IDocumentTrackingRepository docRepo,
        ITeacherRepository teacherRepo,
        IEnterpriseRepository enterpriseRepo,
        IStudentRepository studentRepo,
        IFieldRepository fieldRepo,
        ISettingRepository? settingRepo = null)
    {
        _docRepo = docRepo;
        _teacherRepo = teacherRepo;
        _enterpriseRepo = enterpriseRepo;
        _studentRepo = studentRepo;
        _fieldRepo = fieldRepo;
        _settingRepo = settingRepo;

        InitializeComponent();
        LoadDataAsync();
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
            Text = "📑 Koordinatörlük Evrak Teslim ve Takip Çizelgesi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Öğretmenlerin işletmelerden getirdikleri haftalık rehberlik formları (geçiş günleri dahil), aylık rapor ve devamsızlık çizelgelerinin kontrolü",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 380,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnPrint = new Button
        {
            Text = "🖨️ Resmi Çizelgeyi Yazdır",
            Size = new Size(180, 36),
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

        pnlTopButtons.Controls.AddRange([btnPrint, btnExportExcel]);
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Stat Cards Panel (4 Cards)
        var pnlStats = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 0, 24, 12)
        };

        var tblStats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        tblStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var cardTeachers = CreateStatCard("ÖĞRETMEN SAYISI", "0", "👨‍🏫", Color.FromArgb(37, 99, 235), out _lblStatTeachers);
        var cardEnterprises = CreateStatCard("SORUMLU İŞLETME", "0", "🏢", Color.FromArgb(79, 70, 229), out _lblStatEnterprises);
        var cardRate = CreateStatCard("TESLİM ORANI", "%0", "📈", Color.FromArgb(22, 101, 52), out _lblStatRate);
        var cardPending = CreateStatCard("EKSİK EVRAKI OLAN", "0", "⏳", Color.FromArgb(217, 119, 6), out _lblStatPending);

        tblStats.Controls.Add(cardTeachers, 0, 0);
        tblStats.Controls.Add(cardEnterprises, 1, 0);
        tblStats.Controls.Add(cardRate, 2, 0);
        tblStats.Controls.Add(cardPending, 3, 0);
        pnlStats.Controls.Add(tblStats);

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

        var pnlMonthInner = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var lblMonthTitle = new Label
        {
            Text = "📅 Çizelge Ayı:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        };

        _btnPrevMonth = new Button { Text = "◀", Size = new Size(32, 28), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 0, 4, 0), Cursor = Cursors.Hand };
        _btnPrevMonth.Click += async (s, e) => await TryChangeMonthAsync(-1);

        _lblMonthInfo = new Label
        {
            Text = _selectedMonth.ToString("MMMM yyyy"),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            BackColor = Color.FromArgb(239, 246, 255),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(150, 28),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 4, 0)
        };

        _btnNextMonth = new Button { Text = "▶", Size = new Size(32, 28), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 0, 16, 0), Cursor = Cursors.Hand };
        _btnNextMonth.Click += async (s, e) => await TryChangeMonthAsync(1);

        bool canEditDocs = MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData;

        var lblReadOnlyDocs = new Label
        {
            Text = "🔒 Okuma Modu (Evrak teslim onayları kilitlidir)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14),
            AutoSize = true,
            Margin = new Padding(8, 6, 0, 0),
            Visible = !canEditDocs
        };

        var btnBulkCompleted = new Button
        {
            Text = "✓ Tümünü Teslim Edildi Yap",
            Size = new Size(190, 28),
            BackColor = Color.FromArgb(236, 253, 245), // emerald-50
            ForeColor = Color.FromArgb(4, 120, 87),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            Visible = canEditDocs
        };
        btnBulkCompleted.FlatAppearance.BorderColor = Color.FromArgb(167, 243, 208);
        btnBulkCompleted.Click += (s, e) => SetAllDocsState(true);

        var btnBulkReset = new Button
        {
            Text = "🔄 Tümünü Sıfırla",
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 0),
            Visible = canEditDocs
        };
        btnBulkReset.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnBulkReset.Click += (s, e) => SetAllDocsState(false);

        pnlMonthInner.Controls.AddRange([lblMonthTitle, _btnPrevMonth, _lblMonthInfo, _btnNextMonth, btnBulkCompleted, btnBulkReset, lblReadOnlyDocs]);
        pnlMonth.Controls.Add(pnlMonthInner);

        void CenterMonthBar()
        {
            pnlMonthInner.Location = new Point(
                Math.Max(0, (pnlMonth.ClientSize.Width - pnlMonthInner.Width) / 2),
                Math.Max(0, (pnlMonth.ClientSize.Height - pnlMonthInner.Height) / 2)
            );
        }
        pnlMonth.Resize += (s, e) => CenterMonthBar();
        pnlMonthInner.SizeChanged += (s, e) => CenterMonthBar();

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

        var pnlFilterInner = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _cmbFilterField = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cmbFilterField.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterTeacher = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cmbFilterTeacher.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterStatus = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cmbFilterStatus.Items.AddRange(["Tüm Kayıtlar", "⚠️ Eksik Evrakı Olanlar", "✓ Tamamı Teslim Edilenler"]);
        _cmbFilterStatus.SelectedIndex = 0;
        _cmbFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilter();

        _txtSearch = new TextBox { Width = 180, PlaceholderText = "Öğretmen veya işletme ara...", Margin = new Padding(0, 2, 12, 0) };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _lblCount = new Label
        {
            Text = "Toplam 0 Kayıt",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 0)
        };

        pnlFilterInner.Controls.AddRange([_cmbFilterField, _cmbFilterTeacher, _cmbFilterStatus, _txtSearch, _lblCount]);
        pnlFilter.Controls.Add(pnlFilterInner);

        void CenterFilterBar()
        {
            pnlFilterInner.Location = new Point(
                Math.Max(0, (pnlFilter.ClientSize.Width - pnlFilterInner.Width) / 2),
                Math.Max(0, (pnlFilter.ClientSize.Height - pnlFilterInner.Height) / 2)
            );
        }
        pnlFilter.Resize += (s, e) => CenterFilterBar();
        pnlFilterInner.SizeChanged += (s, e) => CenterFilterBar();

        // Grid Container
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 12, 24, 12)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 38 }
        };
        UITheme.ApplyModernGridStyle(_grid);

        // ModernGridStyle ReadOnly=true yaptığı için düzenlemeye ve checkbox tıklamaya izin veriyoruz:
        _grid.ReadOnly = false;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.MultiSelect = true;
        _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 60;

        // Çift arabelleğe alma (titremesiz çizim)
        typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null,
            _grid,
            new object[] { true });

        SetupGridColumns();
        _grid.CellClick += Grid_CellClick;
        _grid.CellMouseEnter += Grid_CellMouseEnter;
        _grid.MouseLeave += Grid_MouseLeave;
        _grid.CellPainting += Grid_CellPainting;
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.Paint += Grid_Paint;
        _grid.Scroll += (s, e) => _grid.Invalidate(new Rectangle(0, 0, _grid.Width, 60));

        pnlGrid.Controls.Add(_grid);

        Controls.Add(pnlGrid);
        Controls.Add(pnlFilter);
        Controls.Add(pnlMonth);
        Controls.Add(pnlStats);
        Controls.Add(pnlHeader);
    }

    private static Panel CreateStatCard(string title, string value, string icon, Color accent, out Label valueLabel)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(6),
            Padding = new Padding(12)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var lblT = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(12, 10),
            AutoSize = true
        };

        valueLabel = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(10, 30),
            AutoSize = true
        };

        var lblIcon = new Label
        {
            Text = icon,
            Font = new Font("Segoe UI", 16f),
            ForeColor = accent,
            Location = new Point(card.Width - 45, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true
        };

        card.Controls.AddRange([lblT, valueLabel, lblIcon]);
        return card;
    }

    private (string? transStart, string w1, string w2, string w3, string w4, string? transEnd) GetMonthWeekStructure()
    {
        var groups = AttendanceWeekHelper.GetMonthWeekGroups(_selectedMonth.Year, _selectedMonth.Month);

        string? transStart = null;
        var firstGroup = groups.FirstOrDefault();
        if (firstGroup != null && firstGroup.IsTransition)
        {
            transStart = firstGroup.Title;
        }

        var regularWeeks = groups.Where(g => !g.IsTransition).ToList();

        string w1 = regularWeeks.Count > 0 ? regularWeeks[0].Title : "1. Hafta";
        string w2 = regularWeeks.Count > 1 ? regularWeeks[1].Title : "2. Hafta";
        string w3 = regularWeeks.Count > 2 ? regularWeeks[2].Title : "3. Hafta";
        string w4 = regularWeeks.Count > 3 ? regularWeeks[3].Title : "4. Hafta";

        string? transEnd = null;
        if (regularWeeks.Count > 4)
        {
            var endGrp = regularWeeks[4];
            transEnd = endGrp.StartWorkDay == endGrp.EndWorkDay
                ? $"Geçiş ({endGrp.StartWorkDay})"
                : $"Geçiş ({endGrp.StartWorkDay}-{endGrp.EndWorkDay})";
        }

        return (transStart, w1, w2, w3, w4, transEnd);
    }

    private (AttendanceWeekGroup? startGrp, AttendanceWeekGroup? endGrp) GetTransitionGroups()
    {
        var groups = AttendanceWeekHelper.GetMonthWeekGroups(_selectedMonth.Year, _selectedMonth.Month);
        AttendanceWeekGroup? startGrp = (groups.Count > 0 && groups[0].IsTransition) ? groups[0] : null;
        var regularWeeks = groups.Where(g => !g.IsTransition).ToList();
        AttendanceWeekGroup? endGrp = regularWeeks.Count > 4 ? regularWeeks[4] : null;
        return (startGrp, endGrp);
    }

    private bool IsWeekInMidtermBreak(AttendanceWeekGroup? grp)
    {
        if (grp == null || _settings == null) return false;
        int startDay = grp.StartWorkDay > 0 ? grp.StartWorkDay : grp.StartDay;
        int endDay = grp.EndWorkDay > 0 ? grp.EndWorkDay : grp.EndDay;
        if (startDay <= 0 || endDay <= 0) return false;

        var weekStart = new DateTime(_selectedMonth.Year, _selectedMonth.Month, startDay);
        var weekEnd = new DateTime(_selectedMonth.Year, _selectedMonth.Month, endDay);
        return _settings.OverlapsWithMidtermBreak(weekStart, weekEnd);
    }

    private static DataGridViewTextBoxCell CreateExemptCell(string tooltip)
    {
        return new DataGridViewTextBoxCell
        {
            Value = "—",
            ToolTipText = tooltip,
            Style = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.FromArgb(241, 245, 249),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            }
        };
    }

    private static bool GetCellBoolValue(DataGridViewCell cell)
    {
        if (cell is DataGridViewCheckBoxCell && cell.Value != null)
        {
            if (cell.Value is bool b) return b;
            if (bool.TryParse(cell.Value.ToString(), out var parsed)) return parsed;
            if (int.TryParse(cell.Value.ToString(), out var iVal)) return iVal == 1;
        }
        return false;
    }

    private static string FormatHeaderWithLineBreak(string title)
    {
        int parenIdx = title.IndexOf('(');
        if (parenIdx > 0)
        {
            return title[..parenIdx].Trim() + "\n" + title[parenIdx..].Trim();
        }
        return title;
    }

    private void UpdateGridColumnHeaders()
    {
        if (_grid == null || _grid.Columns.Count == 0) return;

        var (transStart, w1, w2, w3, w4, transEnd) = GetMonthWeekStructure();

        bool hasTransStart = !string.IsNullOrEmpty(transStart);
        bool hasTransEnd = !string.IsNullOrEmpty(transEnd);

        if (_grid.Columns.Contains("colTransitionStart"))
        {
            _grid.Columns["colTransitionStart"].Visible = hasTransStart;
            if (hasTransStart)
                _grid.Columns["colTransitionStart"].HeaderText = FormatHeaderWithLineBreak(transStart!);
        }

        if (_grid.Columns.Contains("colW1"))
            _grid.Columns["colW1"].HeaderText = FormatHeaderWithLineBreak(w1);
        if (_grid.Columns.Contains("colW2"))
            _grid.Columns["colW2"].HeaderText = FormatHeaderWithLineBreak(w2);
        if (_grid.Columns.Contains("colW3"))
            _grid.Columns["colW3"].HeaderText = FormatHeaderWithLineBreak(w3);
        if (_grid.Columns.Contains("colW4"))
            _grid.Columns["colW4"].HeaderText = FormatHeaderWithLineBreak(w4);

        if (_grid.Columns.Contains("colTransitionEnd"))
        {
            _grid.Columns["colTransitionEnd"].Visible = hasTransEnd;
            if (hasTransEnd)
                _grid.Columns["colTransitionEnd"].HeaderText = FormatHeaderWithLineBreak(transEnd!);
        }

        _grid.Invalidate();
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();

        var colIdx = new DataGridViewTextBoxColumn { Name = "colIdx", HeaderText = "#", Width = 32, FillWeight = 3, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
        colIdx.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colIdx.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(colIdx);

        var colTeacher = new DataGridViewTextBoxColumn { Name = "colTeacher", HeaderText = "Koordinatör Öğretmen", FillWeight = 22, ReadOnly = true, DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleLeft } };
        colTeacher.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colTeacher.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(colTeacher);

        var colEnterprise = new DataGridViewTextBoxColumn { Name = "colEnterprise", HeaderText = "Sorumlu İşletme", FillWeight = 23, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft } };
        colEnterprise.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colEnterprise.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(colEnterprise);

        var colStudents = new DataGridViewTextBoxColumn { Name = "colStudents", HeaderText = "Öğr.", FillWeight = 6, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
        colStudents.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colStudents.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(colStudents);

        var (transStart, w1, w2, w3, w4, transEnd) = GetMonthWeekStructure();

        // 8 Document CheckBoxes (colTransitionStart, colW1..colW4, colTransitionEnd, colReport, colAttendance)
        string[] docNames = ["colTransitionStart", "colW1", "colW2", "colW3", "colW4", "colTransitionEnd", "colReport", "colAttendance"];
        string[] docHeaders = [
            FormatHeaderWithLineBreak(transStart ?? "Geçiş"),
            FormatHeaderWithLineBreak(w1),
            FormatHeaderWithLineBreak(w2),
            FormatHeaderWithLineBreak(w3),
            FormatHeaderWithLineBreak(w4),
            FormatHeaderWithLineBreak(transEnd ?? "Geçiş"),
            "Aylık\nRapor",
            "Devam\nÇizelgesi"
        ];
        int[] weights = [7, 7, 7, 7, 7, 7, 8, 8];

        for (int i = 0; i < docNames.Length; i++)
        {
            var colDoc = new DataGridViewCheckBoxColumn
            {
                Name = docNames[i],
                HeaderText = docHeaders[i],
                FillWeight = weights[i],
                ReadOnly = false,
                ThreeState = false,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            };
            colDoc.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colDoc.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
            colDoc.HeaderCell.Style.Font = new Font("Segoe UI", 8f, FontStyle.Bold);

            if (docNames[i] == "colTransitionStart")
                colDoc.Visible = !string.IsNullOrEmpty(transStart);
            else if (docNames[i] == "colTransitionEnd")
                colDoc.Visible = !string.IsNullOrEmpty(transEnd);

            _grid.Columns.Add(colDoc);
        }

        var colStatus = new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "Durum", FillWeight = 10, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
        colStatus.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colStatus.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(colStatus);
    }

    public async Task LoadDataAsync()
    {
        if (_settingRepo != null)
        {
            _settings = await _settingRepo.GetAppSettingsAsync();
        }
        _allTeachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();
        _allEnterprises = (await _enterpriseRepo.GetAllAsync()).OrderBy(e => e.Name).ToList();
        _allStudents = (await _studentRepo.GetAllAsync()).ToList();
        var fields = (await _fieldRepo.GetAllAsync()).OrderBy(f => f.Name).ToList();

        _cmbFilterField.Items.Clear();
        _cmbFilterField.Items.Add("Tüm Alanlar");
        foreach (var f in fields) _cmbFilterField.Items.Add(f.Name);
        _cmbFilterField.SelectedIndex = 0;

        _cmbFilterTeacher.Items.Clear();
        _cmbFilterTeacher.Items.Add("Tüm Öğretmenler");
        foreach (var t in _allTeachers) _cmbFilterTeacher.Items.Add(t.FullName);

        int defaultTeacherIdx = 0;
        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin && MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser != null)
        {
            var curUser = MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser;
            var matchIdx = _allTeachers.FindIndex(t =>
                (curUser.TeacherId.HasValue && t.Id == curUser.TeacherId.Value) ||
                string.Equals(t.FullName, curUser.FullName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.FullName, curUser.TeacherName, StringComparison.OrdinalIgnoreCase));
            if (matchIdx >= 0)
            {
                defaultTeacherIdx = matchIdx + 1;
            }
        }
        _cmbFilterTeacher.SelectedIndex = defaultTeacherIdx;

        UpdateMonthAndReload();

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
    }

    private void UpdateMonthAndReload()
    {
        _lblMonthInfo.Text = _selectedMonth.ToString("MMMM yyyy");
        UpdateNavButtonsState();
        UpdateGridColumnHeaders();
        _ = LoadRecordsAsync();
    }

    private void UpdateNavButtonsState()
    {
        var todayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var currentSel = new DateTime(_selectedMonth.Year, _selectedMonth.Month, 1);
        if (_btnNextMonth != null)
        {
            _btnNextMonth.Enabled = currentSel < todayMonth;
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

    private async Task LoadRecordsAsync()
    {
        _currentMonthRecords = (await _docRepo.GetForMonthAsync(_selectedMonth.Year, _selectedMonth.Month)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        var selectedField = _cmbFilterField.SelectedIndex > 0 ? _cmbFilterField.SelectedItem?.ToString() : null;
        var selectedTeacher = _cmbFilterTeacher.SelectedIndex > 0 ? _cmbFilterTeacher.SelectedItem?.ToString() : null;
        var statusIdx = _cmbFilterStatus.SelectedIndex;

        // Build teacher-enterprise pairings from student assignments
        var pairs = _allStudents
            .Where(s => s.TeacherId.HasValue && s.EnterpriseId.HasValue)
            .GroupBy(s => (TeacherId: s.TeacherId!.Value, EnterpriseId: s.EnterpriseId!.Value))
            .Select(g =>
            {
                var t = _allTeachers.FirstOrDefault(x => x.Id == g.Key.TeacherId);
                var e = _allEnterprises.FirstOrDefault(x => x.Id == g.Key.EnterpriseId);
                return new
                {
                    Teacher = t,
                    Enterprise = e,
                    Students = g.ToList(),
                    StudentCount = g.Count()
                };
            })
            .Where(p => p.Teacher != null && p.Enterprise != null)
            .ToList();

        _lblStatTeachers.Text = _allTeachers.Count.ToString();
        _lblStatEnterprises.Text = pairs.Select(p => p.Enterprise!.Id).Distinct().Count().ToString();

        var (transStart, _, _, _, _, transEnd) = GetMonthWeekStructure();
        var (startGrp, endGrp) = GetTransitionGroups();
        bool hasTransStart = !string.IsNullOrEmpty(transStart);
        bool hasTransEnd = !string.IsNullOrEmpty(transEnd);

        var groups = AttendanceWeekHelper.GetMonthWeekGroups(_selectedMonth.Year, _selectedMonth.Month);
        var regularWeeks = groups.Where(g => !g.IsTransition).ToList();
        AttendanceWeekGroup? w1Grp = regularWeeks.Count > 0 ? regularWeeks[0] : null;
        AttendanceWeekGroup? w2Grp = regularWeeks.Count > 1 ? regularWeeks[1] : null;
        AttendanceWeekGroup? w3Grp = regularWeeks.Count > 2 ? regularWeeks[2] : null;
        AttendanceWeekGroup? w4Grp = regularWeeks.Count > 3 ? regularWeeks[3] : null;

        int completedCount = 0;
        int totalExpectedDocs = 0;
        int deliveredDocs = 0;

        _grid.Rows.Clear();
        int idx = 1;

        foreach (var p in pairs)
        {
            var t = p.Teacher!;
            var ent = p.Enterprise!;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var match = t.FullName.ToLowerInvariant().Contains(q) ||
                            ent.Name.ToLowerInvariant().Contains(q);
                if (!match) continue;
            }

            if (!string.IsNullOrWhiteSpace(selectedField) && !(t.Branch ?? "").Contains(selectedField, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(selectedTeacher) && !t.FullName.Equals(selectedTeacher, StringComparison.OrdinalIgnoreCase))
                continue;

            // MESEM ve ara tatil tespiti: MESEM öğrencileri ara tatilde de işletmeye devam eder
            bool isMesemPair = p.Students.Any(s => FeeCalculator.IsMesem(s, null));
            bool isTransStartBreak = !isMesemPair && hasTransStart && IsWeekInMidtermBreak(startGrp);
            bool isW1Break = !isMesemPair && IsWeekInMidtermBreak(w1Grp);
            bool isW2Break = !isMesemPair && IsWeekInMidtermBreak(w2Grp);
            bool isW3Break = !isMesemPair && IsWeekInMidtermBreak(w3Grp);
            bool isW4Break = !isMesemPair && IsWeekInMidtermBreak(w4Grp);
            bool isTransEndBreak = !isMesemPair && hasTransEnd && IsWeekInMidtermBreak(endGrp);

            // Görev formundaki ziyaret tarihlerine göre geçiş haftasında görev kontrolü
            var visitDates = CoordinatorDocsView.GetMonthlyVisitDates(ent, t, p.Students, _selectedMonth);

            bool hasDutyTransStart = hasTransStart && startGrp != null && !isTransStartBreak &&
                visitDates.Any(d => d.Day >= startGrp.StartDay && d.Day <= startGrp.EndDay);

            bool hasDutyTransEnd = hasTransEnd && endGrp != null && !isTransEndBreak &&
                visitDates.Any(d => d.Day >= endGrp.StartDay && d.Day <= endGrp.EndDay);

            var rec = _currentMonthRecords.FirstOrDefault(r => r.TeacherId == t.Id && r.EnterpriseId == ent.Id);
            bool transS = hasDutyTransStart && (rec?.TransitionStartDelivered ?? false);
            bool w1 = !isW1Break && (rec?.Week1Delivered ?? false);
            bool w2 = !isW2Break && (rec?.Week2Delivered ?? false);
            bool w3 = !isW3Break && (rec?.Week3Delivered ?? false);
            bool w4 = !isW4Break && (rec?.Week4Delivered ?? false);
            bool transE = hasDutyTransEnd && (rec?.TransitionEndDelivered ?? false);
            bool monthly = rec?.MonthlyReportDelivered ?? false;
            bool att = rec?.AttendanceSheetDelivered ?? false;

            int pairExpected = (hasDutyTransStart ? 1 : 0)
                             + (!isW1Break ? 1 : 0)
                             + (!isW2Break ? 1 : 0)
                             + (!isW3Break ? 1 : 0)
                             + (!isW4Break ? 1 : 0)
                             + (hasDutyTransEnd ? 1 : 0)
                             + 2;
            totalExpectedDocs += pairExpected;

            int docSum = (hasDutyTransStart && transS ? 1 : 0) +
                         (w1 ? 1 : 0) + (w2 ? 1 : 0) + (w3 ? 1 : 0) + (w4 ? 1 : 0) +
                         (hasDutyTransEnd && transE ? 1 : 0) +
                         (monthly ? 1 : 0) + (att ? 1 : 0);

            deliveredDocs += docSum;
            bool isComplete = (pairExpected > 0 && docSum == pairExpected);
            if (isComplete) completedCount++;

            if (statusIdx == 1 && isComplete) continue; // Only pending
            if (statusIdx == 2 && !isComplete) continue; // Only completed

            string statusText = isComplete ? "✓ Tamamlandı" : $"⚠️ {pairExpected - docSum} Eksik";
            Color statusColor = isComplete ? Color.FromArgb(22, 101, 52) : Color.FromArgb(217, 119, 6);

            var rowIndex = _grid.Rows.Add(
                idx++,
                t.FullName,
                ent.Name,
                $"{p.StudentCount} Öğrenci",
                transS,
                w1, w2, w3, w4,
                transE,
                monthly, att,
                statusText
            );

            var addedRow = _grid.Rows[rowIndex];
            addedRow.Tag = (t.Id, ent.Id, rec, hasDutyTransStart, hasDutyTransEnd);
            addedRow.Cells["colStatus"].Style.ForeColor = statusColor;
            addedRow.Cells["colStatus"].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            // Geçiş günlerinde görev yoksa veya ara tatil ise hücreyi pasifleştir (— ve kilitli)
            if (hasTransStart && _grid.Columns.Contains("colTransitionStart"))
            {
                if (isTransStartBreak)
                {
                    addedRow.Cells["colTransitionStart"] = CreateExemptCell("Ara tatil nedeniyle koordinatörlük görevi bulunmamaktadır (Muaf)");
                    addedRow.Cells["colTransitionStart"].ReadOnly = true;
                }
                else if (!hasDutyTransStart)
                {
                    addedRow.Cells["colTransitionStart"] = CreateExemptCell("Bu geçiş günlerinde planlanmış koordinatörlük görevi bulunmamaktadır (Muaf)");
                    addedRow.Cells["colTransitionStart"].ReadOnly = true;
                }
            }

            // Ara tatildeki haftaları pasifleştir (— ve kilitli)
            if (isW1Break && _grid.Columns.Contains("colW1"))
            {
                addedRow.Cells["colW1"] = CreateExemptCell("Ara tatil nedeniyle koordinatörlük görevi bulunmamaktadır (Muaf)");
                addedRow.Cells["colW1"].ReadOnly = true;
            }
            if (isW2Break && _grid.Columns.Contains("colW2"))
            {
                addedRow.Cells["colW2"] = CreateExemptCell("Ara tatil nedeniyle koordinatörlük görevi bulunmamaktadır (Muaf)");
                addedRow.Cells["colW2"].ReadOnly = true;
            }
            if (isW3Break && _grid.Columns.Contains("colW3"))
            {
                addedRow.Cells["colW3"] = CreateExemptCell("Ara tatil nedeniyle koordinatörlük görevi bulunmamaktadır (Muaf)");
                addedRow.Cells["colW3"].ReadOnly = true;
            }
            if (isW4Break && _grid.Columns.Contains("colW4"))
            {
                addedRow.Cells["colW4"] = CreateExemptCell("Ara tatil nedeniyle koordinatörlük görevi bulunmamaktadır (Muaf)");
                addedRow.Cells["colW4"].ReadOnly = true;
            }

            if (hasTransEnd && _grid.Columns.Contains("colTransitionEnd"))
            {
                if (isTransEndBreak)
                {
                    addedRow.Cells["colTransitionEnd"] = CreateExemptCell("Ara tatil nedeniyle koordinatörlük görevi bulunmamaktadır (Muaf)");
                    addedRow.Cells["colTransitionEnd"].ReadOnly = true;
                }
                else if (!hasDutyTransEnd)
                {
                    addedRow.Cells["colTransitionEnd"] = CreateExemptCell("Bu geçiş günlerinde planlanmış koordinatörlük görevi bulunmamaktadır (Muaf)");
                    addedRow.Cells["colTransitionEnd"].ReadOnly = true;
                }
            }
        }

        _lblCount.Text = $"Toplam {_grid.Rows.Count} Kayıt";

        int rate = totalExpectedDocs > 0 ? (int)Math.Round((double)deliveredDocs / totalExpectedDocs * 100) : 0;
        _lblStatRate.Text = $"%{rate}";
        _lblStatPending.Text = (pairs.Count - completedCount).ToString();
    }

    private async void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData) return;

        var colName = _grid.Columns[e.ColumnIndex].Name;
        if (colName is not ("colTransitionStart" or "colW1" or "colW2" or "colW3" or "colW4" or "colTransitionEnd" or "colReport" or "colAttendance"))
            return;

        var row = _grid.Rows[e.RowIndex];
        var cell = row.Cells[e.ColumnIndex];
        if (cell.ReadOnly || cell is not DataGridViewCheckBoxCell) return;

        if (row.Tag is not ValueTuple<int, int, DocumentTrackingRecord?, bool, bool> tag) return;

        int teacherId = tag.Item1;
        int enterpriseId = tag.Item2;
        var rec = tag.Item3;
        bool hasDutyTransStart = tag.Item4;
        bool hasDutyTransEnd = tag.Item5;

        // Checkbox değerini anında tersine çevir
        bool curVal = Convert.ToBoolean(cell.Value);
        bool newVal = !curVal;
        cell.Value = newVal;
        _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

        if (rec == null)
        {
            rec = new DocumentTrackingRecord
            {
                TeacherId = teacherId,
                EnterpriseId = enterpriseId,
                Year = _selectedMonth.Year,
                Month = _selectedMonth.Month
            };
            _currentMonthRecords.Add(rec);
            row.Tag = (teacherId, enterpriseId, rec, hasDutyTransStart, hasDutyTransEnd);
        }

        var (transStart, _, _, _, _, transEnd) = GetMonthWeekStructure();
        bool hasTransStart = !string.IsNullOrEmpty(transStart);
        bool hasTransEnd = !string.IsNullOrEmpty(transEnd);

        rec.TransitionStartDelivered = hasTransStart && hasDutyTransStart && GetCellBoolValue(row.Cells["colTransitionStart"]);
        rec.Week1Delivered = GetCellBoolValue(row.Cells["colW1"]);
        rec.Week2Delivered = GetCellBoolValue(row.Cells["colW2"]);
        rec.Week3Delivered = GetCellBoolValue(row.Cells["colW3"]);
        rec.Week4Delivered = GetCellBoolValue(row.Cells["colW4"]);
        rec.TransitionEndDelivered = hasTransEnd && hasDutyTransEnd && GetCellBoolValue(row.Cells["colTransitionEnd"]);
        rec.MonthlyReportDelivered = GetCellBoolValue(row.Cells["colReport"]);
        rec.AttendanceSheetDelivered = GetCellBoolValue(row.Cells["colAttendance"]);

        // Canlı durum hücresini aktif checkbox hücrelerine göre dinamik güncelle
        string[] docCols = ["colTransitionStart", "colW1", "colW2", "colW3", "colW4", "colTransitionEnd", "colReport", "colAttendance"];
        int pairExpected = 0;
        int docSum = 0;
        foreach (var cName in docCols)
        {
            if (_grid.Columns.Contains(cName) && _grid.Columns[cName].Visible)
            {
                var c = row.Cells[cName];
                if (c is DataGridViewCheckBoxCell)
                {
                    pairExpected++;
                    if (GetCellBoolValue(c)) docSum++;
                }
            }
        }

        bool isComplete = (pairExpected > 0 && docSum == pairExpected);
        row.Cells["colStatus"].Value = isComplete ? "✓ Tamamlandı" : $"⚠️ {pairExpected - docSum} Eksik";
        row.Cells["colStatus"].Style.ForeColor = isComplete ? Color.FromArgb(22, 101, 52) : Color.FromArgb(217, 119, 6);

        UpdateStatSummary();

        await _docRepo.SaveRecordAsync(rec);
    }

    private void UpdateStatSummary()
    {
        int completedCount = 0;
        int deliveredDocs = 0;
        int totalExpectedDocs = 0;
        string[] docCols = ["colTransitionStart", "colW1", "colW2", "colW3", "colW4", "colTransitionEnd", "colReport", "colAttendance"];

        foreach (DataGridViewRow r in _grid.Rows)
        {
            int pairExpected = 0;
            int dCount = 0;

            foreach (var cName in docCols)
            {
                if (_grid.Columns.Contains(cName) && _grid.Columns[cName].Visible)
                {
                    var c = r.Cells[cName];
                    if (c is DataGridViewCheckBoxCell)
                    {
                        pairExpected++;
                        if (GetCellBoolValue(c)) dCount++;
                    }
                }
            }

            totalExpectedDocs += pairExpected;
            deliveredDocs += dCount;
            if (pairExpected > 0 && dCount == pairExpected) completedCount++;
        }

        int rate = totalExpectedDocs > 0 ? (int)Math.Round((double)deliveredDocs / totalExpectedDocs * 100) : 0;
        _lblStatRate.Text = $"%{rate}";
        _lblStatPending.Text = (_grid.Rows.Count - completedCount).ToString();
    }

    private async void SetAllDocsState(bool state)
    {
        string[] docCols = ["colTransitionStart", "colW1", "colW2", "colW3", "colW4", "colTransitionEnd", "colReport", "colAttendance"];

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not ValueTuple<int, int, DocumentTrackingRecord?, bool, bool> tag) continue;
            int teacherId = tag.Item1;
            int enterpriseId = tag.Item2;
            var rec = tag.Item3;
            bool hasDutyTransStart = tag.Item4;
            bool hasDutyTransEnd = tag.Item5;

            if (rec == null)
            {
                rec = new DocumentTrackingRecord
                {
                    TeacherId = teacherId,
                    EnterpriseId = enterpriseId,
                    Year = _selectedMonth.Year,
                    Month = _selectedMonth.Month
                };
                _currentMonthRecords.Add(rec);
                row.Tag = (teacherId, enterpriseId, rec, hasDutyTransStart, hasDutyTransEnd);
            }

            if (hasDutyTransStart && _grid.Columns.Contains("colTransitionStart") && row.Cells["colTransitionStart"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colTransitionStart"].Value = state;
                rec.TransitionStartDelivered = state;
            }
            if (row.Cells["colW1"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colW1"].Value = state;
                rec.Week1Delivered = state;
            }
            if (row.Cells["colW2"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colW2"].Value = state;
                rec.Week2Delivered = state;
            }
            if (row.Cells["colW3"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colW3"].Value = state;
                rec.Week3Delivered = state;
            }
            if (row.Cells["colW4"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colW4"].Value = state;
                rec.Week4Delivered = state;
            }
            if (hasDutyTransEnd && _grid.Columns.Contains("colTransitionEnd") && row.Cells["colTransitionEnd"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colTransitionEnd"].Value = state;
                rec.TransitionEndDelivered = state;
            }
            if (row.Cells["colReport"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colReport"].Value = state;
                rec.MonthlyReportDelivered = state;
            }
            if (row.Cells["colAttendance"] is DataGridViewCheckBoxCell)
            {
                row.Cells["colAttendance"].Value = state;
                rec.AttendanceSheetDelivered = state;
            }

            int pairExpected = 0;
            int docSum = 0;
            foreach (var cName in docCols)
            {
                if (_grid.Columns.Contains(cName) && _grid.Columns[cName].Visible)
                {
                    var c = row.Cells[cName];
                    if (c is DataGridViewCheckBoxCell)
                    {
                        pairExpected++;
                        if (GetCellBoolValue(c)) docSum++;
                    }
                }
            }

            bool isComplete = (pairExpected > 0 && docSum == pairExpected);
            row.Cells["colStatus"].Value = isComplete ? "✓ Tamamlandı" : $"⚠️ {pairExpected - docSum} Eksik";
            row.Cells["colStatus"].Style.ForeColor = isComplete ? Color.FromArgb(22, 101, 52) : Color.FromArgb(217, 119, 6);

            await _docRepo.SaveRecordAsync(rec);
        }

        UpdateStatSummary();
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
            int topBandHeight = 24;
            var subHeaderBounds = new Rectangle(
                e.CellBounds.X,
                e.CellBounds.Y + topBandHeight,
                e.CellBounds.Width,
                e.CellBounds.Height - topBandHeight);

            Color backColor;
            Color borderColor;
            Color textColor;

            bool isHovered = (e.ColumnIndex == _hoveredColumnIndex && _hoveredColumnIndex >= 0);

            if (col.Name is "colTransitionStart" or "colW1" or "colW2" or "colW3" or "colW4" or "colTransitionEnd")
            {
                backColor = isHovered ? Color.FromArgb(224, 231, 255) : Color.FromArgb(238, 242, 255); // indigo-50
                borderColor = Color.FromArgb(199, 210, 254); // indigo-200
                textColor = Color.FromArgb(49, 46, 129); // indigo-900
            }
            else if (col.Name is "colReport" or "colAttendance")
            {
                backColor = isHovered ? Color.FromArgb(209, 250, 229) : Color.FromArgb(236, 253, 245); // emerald-50
                borderColor = Color.FromArgb(167, 243, 208); // emerald-200
                textColor = Color.FromArgb(6, 95, 70); // emerald-800
            }
            else if (col.Name == "colStatus")
            {
                backColor = isHovered ? Color.FromArgb(254, 235, 170) : Color.FromArgb(254, 243, 199); // amber-100
                borderColor = Color.FromArgb(253, 230, 138); // amber-200
                textColor = Color.FromArgb(146, 64, 14); // amber-800
            }
            else
            {
                backColor = isHovered ? Color.FromArgb(219, 234, 254) : Color.FromArgb(248, 250, 252); // slate-50
                borderColor = Color.FromArgb(226, 232, 240); // slate-200
                textColor = Color.FromArgb(51, 65, 85); // slate-700
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
            var font = col.HeaderCell.Style.Font ?? _grid.ColumnHeadersDefaultCellStyle.Font ?? new Font("Segoe UI", 8f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(textColor))
            {
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
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

        using var fontSection = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var penBorder = new Pen(Color.FromArgb(203, 213, 225), 1);
        using var sfCenter = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };

        // 1. Sol Grup: Öğretmen ve İşletme Bilgileri (colIdx..colStudents)
        if (_grid.Columns.Contains("colIdx") && _grid.Columns.Contains("colStudents"))
        {
            var rStart = _grid.GetCellDisplayRectangle(_grid.Columns["colIdx"].Index, -1, true);
            var rEnd = _grid.GetCellDisplayRectangle(_grid.Columns["colStudents"].Index, -1, true);

            int leftX = rStart.Width > 0 ? rStart.Left : 0;
            int rightX = rEnd.Width > 0 ? rEnd.Right : 0;

            if (rightX > leftX)
            {
                var rect = new Rectangle(leftX, 0, rightX - leftX, topBandHeight);
                using var brushBg = new SolidBrush(Color.FromArgb(241, 245, 249)); // slate-100
                using var brushText = new SolidBrush(Color.FromArgb(71, 85, 105)); // slate-600
                g.FillRectangle(brushBg, rect);
                g.DrawRectangle(penBorder, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                g.DrawString("ÖĞRETMEN VE İŞLETME BİLGİLERİ", fontSection, brushText, rect, sfCenter);
            }
        }

        // 2. Orta Grup: Haftalık Rehberlik Formları (colTransitionStart..colTransitionEnd)
        var firstWeeklyCol = (_grid.Columns.Contains("colTransitionStart") && _grid.Columns["colTransitionStart"].Visible)
            ? _grid.Columns["colTransitionStart"]
            : _grid.Columns["colW1"];
        var lastWeeklyCol = (_grid.Columns.Contains("colTransitionEnd") && _grid.Columns["colTransitionEnd"].Visible)
            ? _grid.Columns["colTransitionEnd"]
            : _grid.Columns["colW4"];

        if (firstWeeklyCol != null && lastWeeklyCol != null)
        {
            var rStart = _grid.GetCellDisplayRectangle(firstWeeklyCol.Index, -1, true);
            var rEnd = _grid.GetCellDisplayRectangle(lastWeeklyCol.Index, -1, true);

            int leftX = rStart.Width > 0 ? rStart.Left : 0;
            int rightX = rEnd.Width > 0 ? rEnd.Right : 0;

            if (rightX > leftX)
            {
                var rect = new Rectangle(leftX, 0, rightX - leftX, topBandHeight);
                using var brushBg = new SolidBrush(Color.FromArgb(238, 242, 255)); // indigo-50
                using var brushText = new SolidBrush(Color.FromArgb(67, 56, 202)); // indigo-700
                g.FillRectangle(brushBg, rect);
                g.DrawRectangle(penBorder, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                g.DrawString("HAFTALIK REHBERLİK FORMLARI", fontSection, brushText, rect, sfCenter);
            }
        }

        // 3. Sağ Grup: Aylık Evraklar (colReport..colAttendance)
        if (_grid.Columns.Contains("colReport") && _grid.Columns.Contains("colAttendance"))
        {
            var rStart = _grid.GetCellDisplayRectangle(_grid.Columns["colReport"].Index, -1, true);
            var rEnd = _grid.GetCellDisplayRectangle(_grid.Columns["colAttendance"].Index, -1, true);

            int leftX = rStart.Width > 0 ? rStart.Left : 0;
            int rightX = rEnd.Width > 0 ? rEnd.Right : 0;

            if (rightX > leftX)
            {
                var rect = new Rectangle(leftX, 0, rightX - leftX, topBandHeight);
                using var brushBg = new SolidBrush(Color.FromArgb(236, 253, 245)); // emerald-50
                using var brushText = new SolidBrush(Color.FromArgb(4, 120, 87)); // emerald-700
                g.FillRectangle(brushBg, rect);
                g.DrawRectangle(penBorder, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                g.DrawString("AYLIK EVRAKLAR", fontSection, brushText, rect, sfCenter);
            }
        }

        // 4. En Sağ: Durum (colStatus)
        if (_grid.Columns.Contains("colStatus"))
        {
            var rStatus = _grid.GetCellDisplayRectangle(_grid.Columns["colStatus"].Index, -1, true);
            if (rStatus.Width > 0)
            {
                var rect = new Rectangle(rStatus.Left, 0, rStatus.Width, topBandHeight);
                using var brushBg = new SolidBrush(Color.FromArgb(254, 243, 199)); // amber-100
                using var brushText = new SolidBrush(Color.FromArgb(146, 64, 14)); // amber-800
                g.FillRectangle(brushBg, rect);
                g.DrawRectangle(penBorder, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                g.DrawString("DURUM", fontSection, brushText, rect, sfCenter);
            }
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        if (cell.ReadOnly && cell is DataGridViewTextBoxCell && cell.Value?.ToString() == "—")
        {
            e.CellStyle.BackColor = Color.FromArgb(241, 245, 249);
            e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            return;
        }

        bool isHoveredRow = (e.RowIndex == _hoveredRowIndex && _hoveredRowIndex >= 0);
        bool isHoveredCol = (e.ColumnIndex == _hoveredColumnIndex && _hoveredColumnIndex >= 0);

        if (isHoveredRow && isHoveredCol)
        {
            e.CellStyle.BackColor = Color.FromArgb(191, 219, 254); // Kesişim odak hücresi: belirgin mavi
        }
        else if (isHoveredRow)
        {
            e.CellStyle.BackColor = Color.FromArgb(239, 246, 255); // İlgili satır: hafif mavi vurgu
        }
        else if (isHoveredCol)
        {
            e.CellStyle.BackColor = Color.FromArgb(240, 249, 255); // İlgili sütun: hafif gök mavisi vurgu
        }
        else
        {
            // Sütun kategorilerine göre ferah, soft pastel zemin tonları
            var col = _grid.Columns[e.ColumnIndex];
            if (col.Name is "colTransitionStart" or "colW1" or "colW2" or "colW3" or "colW4" or "colTransitionEnd")
            {
                e.CellStyle.BackColor = Color.FromArgb(248, 250, 255); // hafif indigo esintisi
            }
            else if (col.Name is "colReport" or "colAttendance")
            {
                e.CellStyle.BackColor = Color.FromArgb(246, 253, 249); // hafif zümrüt/yeşil esintisi
            }
            else if (col.Name == "colStatus")
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 251, 235); // hafif kehribar esintisi
            }
            else
            {
                e.CellStyle.BackColor = (e.RowIndex % 2 == 1) ? Color.FromArgb(248, 250, 252) : Color.White;
            }
        }
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("Evrak Takip Çizelgesini Excel Olarak Dışa Aktarma", this))
            return;

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"Evrak_Takip_{_selectedMonth:yyyy_MM}.csv"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var (transStart, w1, w2, w3, w4, transEnd) = GetMonthWeekStructure();
            bool hasTransStart = !string.IsNullOrEmpty(transStart);
            bool hasTransEnd = !string.IsNullOrEmpty(transEnd);

            var headerList = new List<string> { "No", "Ogretmen", "Isletme", "Ogrenci" };
            if (hasTransStart) headerList.Add(transStart!.Replace("\n", " "));
            headerList.AddRange([w1.Replace("\n", " "), w2.Replace("\n", " "), w3.Replace("\n", " "), w4.Replace("\n", " ")]);
            if (hasTransEnd) headerList.Add(transEnd!.Replace("\n", " "));
            headerList.AddRange(["AylikRapor", "Devamsizlik", "Durum"]);

            var sb = new StringBuilder();
            sb.AppendLine("T.C.");
            if (!string.IsNullOrWhiteSpace(_settings.Valilik))
                sb.AppendLine(_settings.Valilik);
            if (!string.IsNullOrWhiteSpace(_settings.DistrictMem))
                sb.AppendLine(_settings.DistrictMem);
            if (!string.IsNullOrWhiteSpace(_settings.SchoolName))
                sb.AppendLine(_settings.SchoolName);
            sb.AppendLine($"{_selectedMonth:MMMM yyyy} DÖNEMİ KOORDİNATÖRLÜK EVRAK TESLİM VE TAKİP ÇİZELGESİ");
            sb.AppendLine();
            sb.AppendLine(string.Join(",", headerList));

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var rowValues = new List<string>
                {
                    row.Cells["colIdx"].Value?.ToString() ?? "",
                    $"\"{row.Cells["colTeacher"].Value}\"",
                    $"\"{row.Cells["colEnterprise"].Value}\"",
                    $"\"{row.Cells["colStudents"].Value}\""
                };
                if (hasTransStart)
                {
                    var val = row.Cells["colTransitionStart"].Value?.ToString();
                    rowValues.Add(val == "—" ? "\"Muaf (—)\"" : (val ?? "False"));
                }
                var valW1 = row.Cells["colW1"].Value?.ToString();
                rowValues.Add(valW1 == "—" ? "\"Muaf (—)\"" : (valW1 ?? "False"));
                var valW2 = row.Cells["colW2"].Value?.ToString();
                rowValues.Add(valW2 == "—" ? "\"Muaf (—)\"" : (valW2 ?? "False"));
                var valW3 = row.Cells["colW3"].Value?.ToString();
                rowValues.Add(valW3 == "—" ? "\"Muaf (—)\"" : (valW3 ?? "False"));
                var valW4 = row.Cells["colW4"].Value?.ToString();
                rowValues.Add(valW4 == "—" ? "\"Muaf (—)\"" : (valW4 ?? "False"));
                if (hasTransEnd)
                {
                    var val = row.Cells["colTransitionEnd"].Value?.ToString();
                    rowValues.Add(val == "—" ? "\"Muaf (—)\"" : (val ?? "False"));
                }
                rowValues.Add(row.Cells["colReport"].Value?.ToString() ?? "False");
                rowValues.Add(row.Cells["colAttendance"].Value?.ToString() ?? "False");
                rowValues.Add($"\"{row.Cells["colStatus"].Value}\"");

                sb.AppendLine(string.Join(",", rowValues));
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Evrak takip çizelgesi kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        var pd = BuildGridPrintDocument();
        PrintPreviewHelper.ShowPreview(pd, $"Evrak Takip Çizelgesi Önizleme — {_selectedMonth:MMMM yyyy}", this);
    }

    private int _printPageIndex = 0;
    private List<int[]> _printPages = [];  // her sayfa için [startRow, endRow]

    private PrintDocument BuildGridPrintDocument()
    {
        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins   = new Margins(40, 40, 50, 50);

        // Baskı başlangıcında sayfalamayı hazırla
        pd.BeginPrint += (s, e) =>
        {
            _printPageIndex = 0;
            _printPages.Clear();

            // Her sayfaya kaç satır sığacağını hesapla (yaklaşık)
            int rowH    = 24;
            int headerH = 135; // başlık alanı
            int pageH   = pd.DefaultPageSettings.Bounds.Height
                          - pd.DefaultPageSettings.Margins.Top
                          - pd.DefaultPageSettings.Margins.Bottom;
            int rowsPerPage = (pageH - headerH) / rowH;

            int total = _grid.Rows.Count;
            for (int start = 0; start < total; start += rowsPerPage)
            {
                _printPages.Add([start, Math.Min(start + rowsPerPage - 1, total - 1)]);
            }
        };

        pd.PrintPage += (s, ev) =>
        {
            if (ev.Graphics == null) return;

            var g      = ev.Graphics;
            var bounds = ev.MarginBounds;

            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // ── Başlık ──────────────────────────────────────────────────────
            using var fontTc    = new Font("Segoe UI", 9f, FontStyle.Regular);
            using var fontSchool= new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fontTitle = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var fontSub   = new Font("Segoe UI", 8.5f);
            using var fontHead  = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fontCell  = new Font("Segoe UI", 7.5f);
            using var fontCellB = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var brushTitle= new SolidBrush(Color.Black);
            using var brushDark = new SolidBrush(Color.Black);
            using var brushGray = new SolidBrush(Color.FromArgb(71, 85, 105));
            using var brushWhite= new SolidBrush(Color.White);
            using var brushRow  = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var brushGreen= new SolidBrush(Color.FromArgb(22, 163, 74));  // Getirdi - Canlı Yeşil
            using var brushRed  = new SolidBrush(Color.FromArgb(220, 38, 38));  // Getirmedi - Canlı Kırmızı
            using var brushWarn = new SolidBrush(Color.FromArgb(217, 119, 6));   // Eksik - Kehribar/Turuncu
            using var penGrid   = new Pen(Color.FromArgb(148, 163, 184), 0.75f);
            using var penOuter  = new Pen(Color.FromArgb(71, 85, 105), 1.0f);

            // 1. Antet ve Resmi Başlık
            string valilik = string.IsNullOrWhiteSpace(_settings.Valilik) ? "İSTANBUL VALİLİĞİ" : _settings.Valilik.ToUpper(new CultureInfo("tr-TR"));
            string mem = !string.IsNullOrWhiteSpace(_settings.DistrictMem) ? _settings.DistrictMem.ToUpper(new CultureInfo("tr-TR")) : "";
            string okulLine = string.IsNullOrWhiteSpace(_settings.SchoolName) ? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ" : _settings.SchoolName;
            string monthName = _selectedMonth.ToString("MMMM yyyy", new CultureInfo("tr-TR")).ToUpper(new CultureInfo("tr-TR"));
            string title    = $"{monthName} DÖNEMİ KOORDİNATÖRLÜK EVRAK TESLİM VE TAKİP ÇİZELGESİ";
            string subtitle = $"Dönem: {_selectedMonth:MMMM yyyy}  |  Sayfa {_printPageIndex + 1}/{_printPages.Count}";

            // Başlık ve Alt Başlık (Ortalı)
            using var sfTitleCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            int curY = bounds.Y;
            g.DrawString("T.C.", fontTc, brushDark, new RectangleF(bounds.X, curY, bounds.Width, 14), sfTitleCenter);
            curY += 13;
            g.DrawString(valilik, fontTc, brushDark, new RectangleF(bounds.X, curY, bounds.Width, 14), sfTitleCenter);
            curY += 13;
            if (!string.IsNullOrWhiteSpace(mem))
            {
                g.DrawString(mem, fontTc, brushDark, new RectangleF(bounds.X, curY, bounds.Width, 14), sfTitleCenter);
                curY += 13;
            }
            g.DrawString(okulLine, fontSchool, brushDark, new RectangleF(bounds.X, curY, bounds.Width, 16), sfTitleCenter);
            curY += 16;
            g.DrawString(title,    fontTitle, brushTitle, new RectangleF(bounds.X, curY, bounds.Width, 20), sfTitleCenter);
            curY += 18;
            g.DrawString(subtitle, fontSub,   brushGray,  new RectangleF(bounds.X, curY, bounds.Width, 16), sfTitleCenter);
            curY += 20;

            int tableStartY = curY;
            int y   = tableStartY;
            int x   = bounds.X;
            int w   = bounds.Width;

            // ── Sütun tanımları ─────────────────────────────────────────────
            var (transStart, w1, w2, w3, w4, transEnd) = GetMonthWeekStructure();
            bool hasTransStart = !string.IsNullOrEmpty(transStart);
            bool hasTransEnd = !string.IsNullOrEmpty(transEnd);

            var colWList = new List<int> { 26, 160, 160, 40 };
            var headersList = new List<string> { "#", "Öğretmen", "İşletme", "Öğr." };
            var colKeysList = new List<string> { "colIdx", "colTeacher", "colEnterprise", "colStudents" };

            int weeklyColCount = 4;
            if (hasTransStart)
            {
                colWList.Add(46);
                headersList.Add(transStart!);
                colKeysList.Add("colTransitionStart");
                weeklyColCount++;
            }

            colWList.AddRange([46, 46, 46, 46]);
            headersList.AddRange([w1, w2, w3, w4]);
            colKeysList.AddRange(["colW1", "colW2", "colW3", "colW4"]);

            if (hasTransEnd)
            {
                colWList.Add(46);
                headersList.Add(transEnd!);
                colKeysList.Add("colTransitionEnd");
                weeklyColCount++;
            }

            int docStartCol = 4 + weeklyColCount;
            int statusCol = docStartCol + 2;

            colWList.AddRange([44, 44, 56]);
            headersList.AddRange(["Aylık Rap.", "Dev.Çiz.", "Durum"]);
            colKeysList.AddRange(["colReport", "colAttendance", "colStatus"]);

            var colW = colWList.ToArray();
            var headers = headersList.ToArray();
            var colKeys = colKeysList.ToArray();

            // Sütunları sığdırmak için toplam genişliği ölçekle
            int totalW = colW.Sum();
            float scale = (float)w / totalW;
            var scaledW = colW.Select(cw => (int)(cw * scale)).ToArray();
            scaledW[scaledW.Length - 1] += (w - scaledW.Sum()); // Kalan piksel farkı son sütuna

            var sfNear   = new StringFormat { Alignment = StringAlignment.Near,   LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            // ── Tablo başlığı satırları (Çift Kademeli Hafta Gruplu) ─────────────
            int topBandH = 14;
            int subBandH = 16;
            using var fontTopHdr = new Font("Segoe UI", 6.5f, FontStyle.Bold);
            using var brushTopBg = new SolidBrush(Color.FromArgb(241, 245, 249)); // slate-100
            using var brushTopWeekBg = new SolidBrush(Color.FromArgb(238, 242, 255)); // indigo-50
            using var brushTopDocBg = new SolidBrush(Color.FromArgb(236, 253, 245)); // emerald-50
            using var brushTopStatusBg = new SolidBrush(Color.FromArgb(254, 243, 199)); // amber-100
            using var brushTopDark = new SolidBrush(Color.FromArgb(51, 65, 85));
            using var brushTopBlue = new SolidBrush(Color.FromArgb(30, 58, 138));
            using var brushTopGreen = new SolidBrush(Color.FromArgb(4, 120, 87));
            using var brushTopAmber = new SolidBrush(Color.FromArgb(146, 64, 14));

            // Üst Kısım 1: Öğretmen ve İşletme Bilgileri (col 0..3)
            int wInfo = scaledW[0] + scaledW[1] + scaledW[2] + scaledW[3];
            var rectInfo = new Rectangle(x, y, wInfo, topBandH);
            g.FillRectangle(brushTopBg, rectInfo);
            g.DrawRectangle(penGrid, rectInfo);
            g.DrawString("ÖĞRETMEN VE İŞLETME BİLGİLERİ", fontTopHdr, brushTopDark, rectInfo, sfCenter);

            // Üst Kısım 2: Haftalık Rehberlik Formları (col 4 .. 4 + weeklyColCount - 1)
            int wWeeks = 0;
            for (int k = 4; k < 4 + weeklyColCount; k++) wWeeks += scaledW[k];
            var rectWeeks = new Rectangle(x + wInfo, y, wWeeks, topBandH);
            g.FillRectangle(brushTopWeekBg, rectWeeks);
            g.DrawRectangle(penGrid, rectWeeks);
            g.DrawString("HAFTALIK REHBERLİK FORMLARI", fontTopHdr, brushTopBlue, rectWeeks, sfCenter);

            // Üst Kısım 3: Aylık Evraklar (col docStartCol .. docStartCol + 1)
            int wDocs = scaledW[docStartCol] + scaledW[docStartCol + 1];
            var rectDocs = new Rectangle(x + wInfo + wWeeks, y, wDocs, topBandH);
            g.FillRectangle(brushTopDocBg, rectDocs);
            g.DrawRectangle(penGrid, rectDocs);
            g.DrawString("AYLIK EVRAKLAR", fontTopHdr, brushTopGreen, rectDocs, sfCenter);

            // Üst Kısım 4: Durum (col statusCol)
            int wStatus = scaledW[statusCol];
            var rectStatus = new Rectangle(x + wInfo + wWeeks + wDocs, y, wStatus, topBandH);
            g.FillRectangle(brushTopStatusBg, rectStatus);
            g.DrawRectangle(penGrid, rectStatus);
            g.DrawString("DURUM", fontTopHdr, brushTopAmber, rectStatus, sfCenter);

            // Alt Kısım: Renk Kodlu Sütun Başlıkları
            int cx = x;
            for (int col = 0; col < headers.Length; col++)
            {
                Color colHdrBg = (col >= 4 && col < 4 + weeklyColCount)
                    ? Color.FromArgb(49, 46, 129)  // indigo-900 (Haftalık Rehberlik)
                    : (col >= docStartCol && col < docStartCol + 2)
                        ? Color.FromArgb(6, 95, 70)    // emerald-800 (Aylık Evraklar)
                        : (col == statusCol)
                            ? Color.FromArgb(146, 64, 14)   // amber-800 (Durum)
                            : Color.FromArgb(51, 65, 85);    // slate-700 (Öğretmen ve İşletme)

                using var brushColBg = new SolidBrush(colHdrBg);
                var cellRect = new Rectangle(cx, y + topBandH, scaledW[col], subBandH);
                g.FillRectangle(brushColBg, cellRect);
                g.DrawRectangle(penGrid, cellRect);

                var sfH = sfCenter;
                g.DrawString(headers[col], fontHead, brushWhite,
                    new RectangleF(cx + 2, y + topBandH + 1, scaledW[col] - 4, subBandH - 2), sfH);

                cx += scaledW[col];
            }
            y += (topBandH + subBandH);

            // ── Satırlar ────────────────────────────────────────────────────
            int rowH = 22;
            using var fontSymbol = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            using var brushWeekCell = new SolidBrush(Color.FromArgb(248, 250, 255));
            using var brushDocCell = new SolidBrush(Color.FromArgb(246, 253, 249));
            using var brushStatusCell = new SolidBrush(Color.FromArgb(255, 251, 235));
            int startRow = _printPages[_printPageIndex][0];
            int endRow   = _printPages[_printPageIndex][1];

            for (int r = startRow; r <= endRow; r++)
            {
                var row = _grid.Rows[r];
                bool alt = (r - startRow) % 2 == 1;

                cx = x;
                for (int col = 0; col < colKeys.Length; col++)
                {
                    // Hücre arka plan rengi
                    var cellRect = new Rectangle(cx, y, scaledW[col], rowH);
                    Brush cellBgBrush = (col >= 4 && col < 4 + weeklyColCount)
                        ? brushWeekCell
                        : (col >= docStartCol && col < docStartCol + 2)
                            ? brushDocCell
                            : (col == statusCol)
                                ? brushStatusCell
                                : (alt ? brushRow : brushWhite);

                    g.FillRectangle(cellBgBrush, cellRect);
                    string colKey = colKeys[col];
                    var cellVal = row.Cells[colKey].Value;

                    string textToDraw;
                    Brush textBrush = brushDark;
                    Font textFont = fontCell;
                    StringFormat sf = sfCenter;

                    // Checkbox sütunları (Geçiş, H1..H4, Geçiş, Aylık Rapor, Devam Çizelgesi)
                    if (col >= 4 && col <= docStartCol + 1)
                    {
                        if (cellVal?.ToString() == "—")
                        {
                            textToDraw = "—";
                            textBrush = brushGray;
                            textFont = fontCell;
                        }
                        else
                        {
                            bool isChecked = false;
                            if (cellVal is bool b) isChecked = b;
                            else if (cellVal != null && bool.TryParse(cellVal.ToString(), out var bVal)) isChecked = bVal;
                            else if (cellVal != null && int.TryParse(cellVal.ToString(), out var iVal)) isChecked = (iVal == 1);

                            if (isChecked)
                            {
                                textToDraw = "✓";
                                textBrush = brushGreen;
                                textFont = fontSymbol;
                            }
                            else
                            {
                                textToDraw = "✗";
                                textBrush = brushRed;
                                textFont = fontSymbol;
                            }
                        }
                    }
                    else if (col == statusCol) // Durum
                    {
                        textToDraw = cellVal?.ToString() ?? "";
                        if (textToDraw.Contains("Tamamlandı"))
                        {
                            textBrush = brushGreen;
                            textFont = fontCellB;
                        }
                        else if (textToDraw.Contains("Eksik"))
                        {
                            textBrush = brushWarn;
                            textFont = fontCellB;
                        }
                    }
                    else if (col == 1) // Öğretmen adı bold
                    {
                        textToDraw = cellVal?.ToString() ?? "";
                        textFont = fontCellB;
                    }
                    else
                    {
                        textToDraw = cellVal?.ToString() ?? "";
                    }

                    g.DrawString(textToDraw, textFont, textBrush,
                        new RectangleF(cx + 3, y + 1, scaledW[col] - 6, rowH - 2), sf);

                    if (col < colKeys.Length - 1)
                        g.DrawLine(penGrid, cx + scaledW[col], y, cx + scaledW[col], y + rowH);

                    cx += scaledW[col];
                }

                // Yatay çizgi
                g.DrawLine(penGrid, x, y + rowH, x + w, y + rowH);
                y += rowH;
            }

            // Dış çerçeve
            int totalTableHeight = (endRow - startRow + 1) * rowH + (topBandH + subBandH);
            g.DrawRectangle(penOuter, bounds.X, tableStartY, w, totalTableHeight);

            _printPageIndex++;
            ev.HasMorePages = _printPageIndex < _printPages.Count;
        };

        return pd;
    }
}
