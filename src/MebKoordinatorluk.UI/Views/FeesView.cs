using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using ClosedXML.Excel;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Views;

public class FeesView : UserControl
{
    private readonly IStudentRepository _studentRepo;
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly IFeeReceiptRepository _receiptRepo;

    private List<Student> _allStudents = [];
    private List<Enterprise> _allEnterprises = [];
    private List<SchoolClass> _allClasses = [];
    private List<AttendanceEntry> _attendanceEntries = [];
    private Dictionary<int, FeeReceipt> _periodReceipts = new();
    private AppSettings _settings = new();
    private DateTime _selectedMonth = DateTime.Today;
    private decimal _effectiveMinimumWage = 0m;
    private Button _btnPrevMonth = null!;
    private Button _btnNextMonth = null!;

    // KPI Labels
    private Label _lblKpiCount = null!;
    private Label _lblKpiCategories = null!;
    private Label _lblKpiGross = null!;
    private Label _lblKpiDeduction = null!;
    private Label _lblKpiNet = null!;

    private Label _lblMonthInfo = null!;
    private ComboBox _cmbFilterEdu = null!;
    private ComboBox _cmbFilterClass = null!;
    private ComboBox _cmbFilterCategory = null!;
    private TextBox _txtSearch = null!;
    private CheckBox _chkSelectAll = null!;
    private bool _isUpdatingSelectAll = false;

    private DataGridView _grid = null!;
    private Label _lblSummaryText = null!;

    public FeesView(
        IStudentRepository studentRepo,
        IAttendanceRepository attendanceRepo,
        ISchoolClassRepository classRepo,
        IEnterpriseRepository enterpriseRepo,
        ISettingRepository settingRepo,
        IFeeReceiptRepository receiptRepo)
    {
        _studentRepo = studentRepo;
        _attendanceRepo = attendanceRepo;
        _classRepo = classRepo;
        _enterpriseRepo = enterpriseRepo;
        _settingRepo = settingRepo;
        _receiptRepo = receiptRepo;

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
            Text = "💵 İşletme Katkı Payı Detay Bordrosu",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Öğrenci staj devamsızlıklarına göre aylık ücret, devlet katkısı tahakkuku ve kesinti bordrosu",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 560,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnPrint = new Button
        {
            Text = "🖨️ Bordroyu Yazdır (A4)",
            Size = new Size(170, 36),
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

        var btnAuditReceipts = new Button
        {
            Text = "🧾 Dekont Eşleştir & Arşivle",
            Size = new Size(195, 36),
            BackColor = Color.FromArgb(124, 58, 237),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0),
            Visible = MebKoordinatorluk.Core.Security.CurrentUserSession.CanEditData
        };
        btnAuditReceipts.FlatAppearance.BorderSize = 0;
        btnAuditReceipts.Click += BtnAuditReceipts_Click;

        pnlTopButtons.Controls.AddRange([btnPrint, btnExportExcel, btnAuditReceipts]);
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // KPI Summary Cards Panel (4 cards)
        var pnlKpi = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 0, 24, 12)
        };

        var tblKpi = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        tblKpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblKpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblKpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblKpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var card1 = CreateKpiCard("TOPLAM STAJYER ÖĞRENCİ", "0", out _lblKpiCount, out _lblKpiCategories, "%30: 0 | 1/2: 0", Color.FromArgb(30, 41, 59));
        var card2 = CreateKpiCard("TAHAKKUK EDEN KATKI (BRÜT)", "₺0,00", out _lblKpiGross, out _, "Devlet katkısı toplamı", Color.FromArgb(37, 99, 235));
        var card3 = CreateKpiCard("DEVAMSIZLIK KESİNTİSİ", "₺0,00", out _lblKpiDeduction, out _, "Gelmediği gün kesintileri", Color.FromArgb(220, 38, 38));
        var card4 = CreateKpiCard("ÖDENECEK NET TUTAR", "₺0,00", out _lblKpiNet, out _, "Banka aktarımı net tutarı", Color.FromArgb(5, 150, 105));

        tblKpi.Controls.Add(card1, 0, 0);
        tblKpi.Controls.Add(card2, 1, 0);
        tblKpi.Controls.Add(card3, 2, 0);
        tblKpi.Controls.Add(card4, 3, 0);
        pnlKpi.Controls.Add(tblKpi);

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
            Text = "📅 Bordro Dönemi:",
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

        var btnRefreshAtt = new Button
        {
            Text = "🔄 Devamsızlıktan Yenile",
            Size = new Size(180, 28),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 0)
        };
        btnRefreshAtt.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnRefreshAtt.Click += (s, e) => UpdateMonthAndReload();

        pnlMonthInner.Controls.AddRange([lblMonthTitle, _btnPrevMonth, _lblMonthInfo, _btnNextMonth, btnRefreshAtt]);
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
        CenterMonthBar();

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

        _cmbFilterEdu = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cmbFilterEdu.Items.AddRange(["Tüm Öğretim Türleri", "Örgün Eğitim", "Özel Eğitim", "MESEM"]);
        _cmbFilterEdu.SelectedIndex = 0;
        _cmbFilterEdu.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterClass = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cmbFilterClass.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterCategory = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cmbFilterCategory.Items.AddRange(["Tüm Kategoriler", "%30", "1/2"]);
        _cmbFilterCategory.SelectedIndex = 0;
        _cmbFilterCategory.SelectedIndexChanged += (s, e) => ApplyFilter();

        _txtSearch = new TextBox { Width = 200, PlaceholderText = "Öğrenci veya işletme ara...", Margin = new Padding(0, 2, 8, 0) };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _chkSelectAll = new CheckBox
        {
            Text = "Tümünü Seç / Kaldır",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Checked = true,
            AutoSize = true,
            Margin = new Padding(8, 4, 0, 0),
            Cursor = Cursors.Hand
        };
        _chkSelectAll.CheckedChanged += (s, e) =>
        {
            bool isChecked = _chkSelectAll.Checked;
            _isUpdatingSelectAll = true;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.Cells["colSelect"].Value = isChecked;
            }
            _isUpdatingSelectAll = false;
            RecalculateSelectedSummary();
        };

        pnlFilterInner.Controls.AddRange([_cmbFilterEdu, _cmbFilterClass, _cmbFilterCategory, _txtSearch, _chkSelectAll]);
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
        CenterFilterBar();

        // Bottom Approval / Summary Text
        var pnlBottomText = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 8, 24, 8)
        };
        pnlBottomText.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottomText.Width, 0);
        };

        _lblSummaryText = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(71, 85, 105),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlBottomText.Controls.Add(_lblSummaryText);

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
            ReadOnly = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 34 }
        };
        UITheme.ApplyModernGridStyle(_grid);
        _grid.ReadOnly = false;

        SetupGridColumns();

        _grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        _grid.CellValueChanged += (s, e) =>
        {
            if (e.ColumnIndex == 0 && !_isUpdatingSelectAll)
            {
                RecalculateSelectedSummary();
            }
        };

        _grid.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex != 0)
            {
                var cell = _grid.Rows[e.RowIndex].Cells["colSelect"];
                bool currentVal = cell.Value is true;
                cell.Value = !currentVal;
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                RecalculateSelectedSummary();
            }
        };

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

        pnlGrid.Controls.Add(_grid);

        Controls.Add(pnlGrid);
        Controls.Add(pnlBottomText);
        Controls.Add(pnlFilter);
        Controls.Add(pnlMonth);
        Controls.Add(pnlKpi);
        Controls.Add(pnlHeader);
    }

    private static Panel CreateKpiCard(string title, string value, out Label valLabel, out Label subLabel, string subText, Color valColor)
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
            Location = new Point(12, 8),
            AutoSize = true
        };

        valLabel = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = valColor,
            Location = new Point(10, 26),
            AutoSize = true
        };

        subLabel = new Label
        {
            Text = subText,
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(12, 50),
            AutoSize = true
        };

        card.Controls.AddRange([lblT, valLabel, subLabel]);
        return card;
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();

        var colCheck = new DataGridViewCheckBoxColumn
        {
            Name = "colSelect",
            HeaderText = "Yazdır",
            Width = 50,
            FillWeight = 4,
            ReadOnly = false
        };
        _grid.Columns.Add(colCheck);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIdx", HeaderText = "#", Width = 32, FillWeight = 3, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "Öğrenci No", FillWeight = 7, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Adı Soyadı", FillWeight = 13, DefaultCellStyle = { Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClass", HeaderText = "Sınıf", FillWeight = 5, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEduType", HeaderText = "Öğretim Türü", FillWeight = 8, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEnterprise", HeaderText = "İşletme Adı", FillWeight = 18, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCategory", HeaderText = "Kategori", FillWeight = 6, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colReceiptStatus", HeaderText = "🧾 Dekont", FillWeight = 8, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colGross", HeaderText = "Brüt Katkı (₺)", FillWeight = 9, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDevletKatkisi", HeaderText = "Devlet Katkısı (₺)", FillWeight = 9, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(37, 99, 235) }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIsletmeMaliyeti", HeaderText = "İşletme Maliyeti (₺)", FillWeight = 9, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(180, 83, 9) }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAbsent", HeaderText = "Gelmediği Gün", FillWeight = 7, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDeduction", HeaderText = "Kesinti (₺)", FillWeight = 8, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.Red }, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNet", HeaderText = "Ödenecek Net (₺)", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(5, 150, 105) }, ReadOnly = true });
    }

    public async Task LoadDataAsync()
    {
        _settings = await _settingRepo.GetAppSettingsAsync();
        _allStudents = (await _studentRepo.GetAllAsync())
            .Where(s => s.IsActive && !s.IsSurekliDevamsiz && !s.StudentNumber.Contains("-P", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Classroom)
            .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
            .ToList();
        _allEnterprises = (await _enterpriseRepo.GetAllAsync()).ToList();
        _allClasses = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();

        _cmbFilterClass.Items.Clear();
        _cmbFilterClass.Items.Add("Tüm Sınıflar");
        foreach (var c in _allClasses)
        {
            var dummyStudent = new Student { Classroom = c.Name, EducationType = c.EducationType };
            if (FeeCalculator.IsEligibleForFeeCalculation(dummyStudent, c))
            {
                _cmbFilterClass.Items.Add(c.Name);
            }
        }
        _cmbFilterClass.SelectedIndex = 0;

        UpdateMonthAndReload();

        // Aktif yıl için asgari ücret tanımı kontrolü
        var currentWage = await _settingRepo.GetYearlyMinimumWageAsync(_selectedMonth.Year);
        if (currentWage == null || currentWage.NetMinimumWage <= 0)
        {
            MessageBox.Show(
                $"⚠️ {_selectedMonth.Year} yılına ait asgari ücret henüz sistemde tanımlanmamıştır!\n\nBordro ve ücret hesaplamalarının doğru yapılabilmesi için lütfen 'Kurum ve Genel Ayarlar' bölümünden {_selectedMonth.Year} yılı Net Asgari Ücretini kaydediniz.",
                "Asgari Ücret Tanımı Eksik",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateMonthAndReload()
    {
        _lblMonthInfo.Text = _selectedMonth.ToString("MMMM yyyy");
        UpdateNavButtonsState();
        _ = LoadAttendanceAndApplyAsync();
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
        var wage = await _settingRepo.GetYearlyMinimumWageAsync(targetDate.Year);
        if (wage == null || wage.NetMinimumWage <= 0)
        {
            MessageBox.Show(
                $"⚠️ {targetDate.Year} yılına ait asgari ücret sistemde tanımlanmamıştır!\n\nİşlem yapabilmek ve bordro hesaplayabilmek için lütfen 'Kurum ve Genel Ayarlar' bölümünden {targetDate.Year} yılı Net Asgari Ücretini kaydediniz.",
                "Asgari Ücret Tanımı Eksik",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _selectedMonth = targetDate;
        UpdateMonthAndReload();
    }

    private async Task LoadAttendanceAndApplyAsync()
    {
        var yearlyWage = await _settingRepo.GetEffectiveWageForYearAsync(_selectedMonth.Year);
        _effectiveMinimumWage = yearlyWage.NetMinimumWage;
        _attendanceEntries = (await _attendanceRepo.GetForMonthAsync(_selectedMonth.Year, _selectedMonth.Month)).ToList();
        var receipts = await _receiptRepo.GetForPeriodAsync(_selectedMonth.ToString("yyyy-MM"));
        _periodReceipts = receipts.ToDictionary(r => r.StudentId, r => r);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        var selectedClass = _cmbFilterClass.SelectedIndex > 0 ? _cmbFilterClass.SelectedItem?.ToString() : null;
        var catIdx = _cmbFilterCategory.SelectedIndex;
        var selectedEdu = _cmbFilterEdu.SelectedIndex > 0 ? _cmbFilterEdu.SelectedItem?.ToString() : null;

        var filtered = _allStudents.Where(s =>
        {
            var cls = _allClasses.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
            if (!FeeCalculator.IsEligibleForFeeCalculation(s, cls))
                return false;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var match = s.FullName.ToLowerInvariant().Contains(q) ||
                            s.StudentNumber.ToLowerInvariant().Contains(q) ||
                            (s.EnterpriseName ?? "").ToLowerInvariant().Contains(q);
                if (!match) return false;
            }

            if (!string.IsNullOrWhiteSpace(selectedClass) && !string.Equals(s.Classroom, selectedClass, StringComparison.OrdinalIgnoreCase))
                return false;

            var eduType = FeeCalculator.GetEducationType(s, cls);
            if (!string.IsNullOrWhiteSpace(selectedEdu) && !string.Equals(eduType, selectedEdu, StringComparison.OrdinalIgnoreCase))
                return false;

            if (catIdx == 1 || catIdx == 2)
            {
                bool isHalf = FeeCalculator.IsHalfWage(s, cls);
                if (catIdx == 1 && isHalf) return false;  // %30 seçiliyse 1/2 olanlar elenir
                if (catIdx == 2 && !isHalf) return false; // 1/2 seçiliyse %30 olanlar elenir
            }

            return true;
        })
        .OrderBy(s => s.Classroom)
        .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
        .ToList();

        decimal wageToUse = _effectiveMinimumWage > 0 ? _effectiveMinimumWage : _settings.StatutoryMinimumWage;
        var summary = FeeCalculator.CalculateMonthlyFees(
            _selectedMonth.Year,
            _selectedMonth.Month,
            wageToUse,
            filtered,
            _allEnterprises,
            _attendanceEntries,
            _settings,
            _allClasses);

        _grid.Rows.Clear();
        int idx = 1;

        _isUpdatingSelectAll = true;
        foreach (var r in summary.Rows)
        {
            string receiptStatus = "🔴 Teslim Edilmedi";
            string? receiptToolTip = null;
            Color receiptColor = Color.FromArgb(220, 38, 38);

            if (_periodReceipts.TryGetValue(r.Student.Id, out var receipt))
            {
                receiptStatus = "🟢 Teslim Edildi";
                receiptToolTip = $"Dosya: {receipt.FileName}\nEşleşme: {receipt.MatchReason}\nTarih: {receipt.AuditDate:dd.MM.yyyy HH:mm}";
                receiptColor = Color.FromArgb(5, 150, 105);
            }

            var rowIndex = _grid.Rows.Add(
                true,
                idx++,
                r.Student.StudentNumber,
                r.Student.FullName,
                r.Student.Classroom,
                r.EducationType,
                r.EnterpriseName,
                r.RateText,
                receiptStatus,
                r.FullWage.ToString("N2"),
                r.DevletKatkisi.ToString("N2"),
                r.IsletmeMaliyeti.ToString("N2"),
                r.ToplamKesintiGunSayisi.ToString("0.#"),
                r.KesintiTutari.ToString("N2"),
                r.OdenecekTutar.ToString("N2")
            );

            _grid.Rows[rowIndex].Cells["colReceiptStatus"].Style.ForeColor = receiptColor;
            if (!string.IsNullOrEmpty(receiptToolTip))
            {
                _grid.Rows[rowIndex].Cells["colReceiptStatus"].ToolTipText = receiptToolTip;
            }

            _grid.Rows[rowIndex].Tag = r;
            if (!string.IsNullOrEmpty(r.Note))
            {
                _grid.Rows[rowIndex].Cells["colAbsent"].ToolTipText = r.Note;
                _grid.Rows[rowIndex].Cells["colDeduction"].ToolTipText = $"Günlük {r.DailyWage:N2} ₺ üzerinden {r.ToplamKesintiGunSayisi:0.#} gün kesinti";
            }
        }
        _isUpdatingSelectAll = false;
        _chkSelectAll.Checked = true;

        RecalculateSelectedSummary();
    }

    private void RecalculateSelectedSummary()
    {
        int count = 0;
        int kat1 = 0;
        int kat2 = 0;
        decimal gross = 0;
        decimal devletKatkisi = 0;
        decimal isletmeMaliyeti = 0;
        decimal deduction = 0;
        decimal net = 0;

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"].Value is true && row.Tag is FeeCalculationRow r)
            {
                count++;
                if (r.RateText.Contains("1/2")) kat2++;
                else kat1++;
                gross += r.FullWage;
                devletKatkisi += r.DevletKatkisi;
                isletmeMaliyeti += r.IsletmeMaliyeti;
                deduction += r.KesintiTutari;
                net += r.OdenecekTutar;
            }
        }

        _lblKpiCount.Text = count.ToString();
        _lblKpiCategories.Text = $"%30: {kat1} | 1/2: {kat2}";
        _lblKpiGross.Text = gross.ToString("C2");
        _lblKpiDeduction.Text = deduction.ToString("C2");
        _lblKpiNet.Text = net.ToString("C2");

        _lblSummaryText.Text = $"Yukarıda kimliği ve işletmesi yazılı {count} öğrenci için {_selectedMonth:MMMM yyyy} döneminde toplam {gross:C2} brüt tahakkuktan (Devlet Katkısı: {devletKatkisi:C2}, İşletme Maliyeti: {isletmeMaliyeti:C2}), devamsızlık ve boş günler sonucu {deduction:C2} kesinti yapılmış olup, net {net:C2} ödeme tahakkuk ettirilmiştir.";
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("Katkı Payı Bordrosunu Excel Olarak Dışa Aktarma", this))
            return;

        var selectedRows = _grid.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Cells["colSelect"].Value is true)
            .ToList();

        if (selectedRows.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak seçili kayıt bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx|CSV Dosyası (*.csv)|*.csv",
            FileName = $"Katki_Payi_Bordrosu_{_selectedMonth:yyyy_MM}.xlsx"
        };

        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            if (sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                sb.AppendLine("T.C.");
                if (!string.IsNullOrWhiteSpace(_settings.Valilik))
                    sb.AppendLine(_settings.Valilik);
                if (!string.IsNullOrWhiteSpace(_settings.DistrictMem))
                    sb.AppendLine(_settings.DistrictMem);
                if (!string.IsNullOrWhiteSpace(_settings.SchoolName))
                    sb.AppendLine(_settings.SchoolName);
                sb.AppendLine($"{_selectedMonth:MMMM yyyy} DÖNEMİ İŞLETME KATKI PAYI VE ÜCRET BORDROSU");
                sb.AppendLine();
                sb.AppendLine("No,Ogrenci No,Adi Soyadi,Sinif,Ogretim Turu,Isletme,Kategori,Brut Katki,Devlet Katkisi,Isletme Maliyeti,Devamsiz Gun,Bos Gun,Toplam Kesilen Gun,Kesinti,Net Odenecek,Not");

                int exportIdx = 1;
                foreach (DataGridViewRow row in selectedRows)
                {
                    if (row.Tag is FeeCalculationRow r)
                    {
                        sb.AppendLine($"{exportIdx++},{r.Student.StudentNumber},\"{r.Student.FullName}\",{r.Student.Classroom},{r.EducationType},\"{r.EnterpriseName}\",{r.RateText},{r.FullWage:N2},{r.DevletKatkisi:N2},{r.IsletmeMaliyeti:N2},{r.DevamsizGunSayisi},{r.BosGunSayisi},{r.ToplamKesintiGunSayisi},{r.KesintiTutari:N2},{r.OdenecekTutar:N2},\"{r.Note}\"");
                    }
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            }
            else
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Ücret Bordrosu");

                // Antetli Resmi Başlık
                int rIdx = 1;
                void AddCenterHeader(string text, double fontSize = 11, bool bold = true)
                {
                    ws.Cell(rIdx, 1).Value = text;
                    ws.Range(rIdx, 1, rIdx, 16).Merge();
                    ws.Cell(rIdx, 1).Style.Font.Bold = bold;
                    ws.Cell(rIdx, 1).Style.Font.FontSize = fontSize;
                    ws.Cell(rIdx, 1).Style.Font.FontColor = XLColor.FromArgb(15, 23, 42);
                    ws.Cell(rIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(rIdx, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Row(rIdx).Height = (fontSize > 11) ? 24 : 18;
                    rIdx++;
                }

                AddCenterHeader("T.C.", 10, true);
                if (!string.IsNullOrWhiteSpace(_settings.Valilik))
                    AddCenterHeader(_settings.Valilik, 10, true);
                if (!string.IsNullOrWhiteSpace(_settings.DistrictMem))
                    AddCenterHeader(_settings.DistrictMem, 10, true);
                if (!string.IsNullOrWhiteSpace(_settings.SchoolName))
                    AddCenterHeader(_settings.SchoolName, 11, true);

                string title = $"DEVLET KATKISI VE ÜCRET BORDROSU — {_selectedMonth:MMMM yyyy}";
                AddCenterHeader(title, 12, true);
                rIdx++; // Boşluk

                // Sütun Başlıkları
                string[] headers = [
                    "No", "Öğrenci No", "Adı Soyadı", "Sınıf", "Öğretim Türü", "İşletme Adı",
                    "Kategori", "Brüt Katkı (₺)", "Devlet Katkısı (₺)", "İşletme Maliyeti (₺)",
                    "Devamsız Gün", "Boş Gün", "Toplam Kesinti Gün", "Kesinti Tutarı (₺)",
                    "Ödenecek Net (₺)", "Açıklama / Not"
                ];

                int headerRow = rIdx;
                ws.Row(headerRow).Height = 25;

                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cell(headerRow, c + 1);
                    cell.Value = headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 9.5;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(5, 150, 105); // emerald-600
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Alignment.WrapText = true;
                }

                int firstDataRow = headerRow + 1;
                int currentRow = firstDataRow;
                int no = 1;

                foreach (DataGridViewRow row in selectedRows)
                {
                    if (row.Tag is not FeeCalculationRow r) continue;

                    ws.Cell(currentRow, 1).SetValue(no++);
                    ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(currentRow, 2).SetValue(r.Student.StudentNumber);
                    ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(currentRow, 3).SetValue(r.Student.FullName);
                    ws.Cell(currentRow, 3).Style.Font.Bold = true;

                    ws.Cell(currentRow, 4).SetValue(r.Student.Classroom);
                    ws.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(currentRow, 5).SetValue(r.EducationType);
                    ws.Cell(currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(currentRow, 6).SetValue(r.EnterpriseName);

                    ws.Cell(currentRow, 7).SetValue(r.RateText);
                    ws.Cell(currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Brüt Katkı
                    ws.Cell(currentRow, 8).SetValue(r.FullWage);
                    ws.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // Devlet Katkısı
                    ws.Cell(currentRow, 9).SetValue(r.DevletKatkisi);
                    ws.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 9).Style.Font.Bold = true;
                    ws.Cell(currentRow, 9).Style.Font.FontColor = XLColor.FromArgb(37, 99, 235); // blue-600
                    ws.Cell(currentRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // İşletme Maliyeti
                    ws.Cell(currentRow, 10).SetValue(r.IsletmeMaliyeti);
                    ws.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // Günler
                    ws.Cell(currentRow, 11).SetValue(r.DevamsizGunSayisi);
                    ws.Cell(currentRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(currentRow, 12).SetValue(r.BosGunSayisi);
                    ws.Cell(currentRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(currentRow, 13).SetValue(r.ToplamKesintiGunSayisi);
                    ws.Cell(currentRow, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Kesinti Tutarı
                    ws.Cell(currentRow, 14).SetValue(r.KesintiTutari);
                    ws.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 14).Style.Font.FontColor = r.KesintiTutari > 0 ? XLColor.FromArgb(220, 38, 38) : XLColor.FromArgb(100, 116, 139);
                    ws.Cell(currentRow, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // Net Ödenecek
                    ws.Cell(currentRow, 15).SetValue(r.OdenecekTutar);
                    ws.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 15).Style.Font.Bold = true;
                    ws.Cell(currentRow, 15).Style.Font.FontColor = XLColor.FromArgb(5, 150, 105); // emerald-600
                    ws.Cell(currentRow, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // Not
                    ws.Cell(currentRow, 16).SetValue(r.Note);

                    // Zebra arka plan
                    if ((currentRow % 2) == 1)
                    {
                        ws.Range(currentRow, 1, currentRow, 16).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                    }

                    ws.Row(currentRow).Height = 20;
                    currentRow++;
                }

                int lastDataRow = currentRow - 1;

                // Genel Toplam Satırı
                if (lastDataRow >= firstDataRow)
                {
                    ws.Cell(currentRow, 1).SetValue("GENEL TOPLAM");
                    ws.Range(currentRow, 1, currentRow, 7).Merge();
                    ws.Cell(currentRow, 1).Style.Font.Bold = true;
                    ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Toplam Brüt
                    ws.Cell(currentRow, 8).FormulaA1 = $"SUM(H{firstDataRow}:H{lastDataRow})";
                    ws.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 8).Style.Font.Bold = true;

                    // Toplam Devlet Katkısı
                    ws.Cell(currentRow, 9).FormulaA1 = $"SUM(I{firstDataRow}:I{lastDataRow})";
                    ws.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 9).Style.Font.Bold = true;
                    ws.Cell(currentRow, 9).Style.Font.FontColor = XLColor.FromArgb(37, 99, 235);

                    // Toplam İşletme Maliyeti
                    ws.Cell(currentRow, 10).FormulaA1 = $"SUM(J{firstDataRow}:J{lastDataRow})";
                    ws.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 10).Style.Font.Bold = true;

                    // Toplam Kesinti Tutarı
                    ws.Cell(currentRow, 14).FormulaA1 = $"SUM(N{firstDataRow}:N{lastDataRow})";
                    ws.Cell(currentRow, 14).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 14).Style.Font.Bold = true;
                    ws.Cell(currentRow, 14).Style.Font.FontColor = XLColor.FromArgb(220, 38, 38);

                    // Toplam Net Ödenecek
                    ws.Cell(currentRow, 15).FormulaA1 = $"SUM(O{firstDataRow}:O{lastDataRow})";
                    ws.Cell(currentRow, 15).Style.NumberFormat.Format = "#,##0.00 ₺";
                    ws.Cell(currentRow, 15).Style.Font.Bold = true;
                    ws.Cell(currentRow, 15).Style.Font.FontColor = XLColor.FromArgb(5, 150, 105);

                    ws.Range(currentRow, 1, currentRow, 16).Style.Fill.BackgroundColor = XLColor.FromArgb(236, 253, 245); // emerald-50
                    ws.Range(currentRow, 1, currentRow, 16).Style.Font.Bold = true;
                    ws.Row(currentRow).Height = 24;

                    // Çerçeveler
                    var tableRange = ws.Range(headerRow, 1, currentRow, 16);
                    tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    tableRange.Style.Border.InsideBorderColor = XLColor.FromArgb(203, 213, 225);
                    tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                    tableRange.Style.Border.OutsideBorderColor = XLColor.FromArgb(71, 85, 105);
                }

                // Sütun Genişlikleri Otomatik Hizalama
                ws.Columns(1, 16).AdjustToContents();

                // Çok dar kalmaması için min genişlikler
                if (ws.Column(1).Width < 6) ws.Column(1).Width = 6;
                if (ws.Column(2).Width < 12) ws.Column(2).Width = 12;
                if (ws.Column(3).Width < 22) ws.Column(3).Width = 22;
                if (ws.Column(6).Width < 25) ws.Column(6).Width = 25;

                wb.SaveAs(sfd.FileName);
            }

            MessageBox.Show($"Bordro başarıyla Excel olarak kaydedildi:\n{Path.GetFileName(sfd.FileName)}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel dosyası kaydedilirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Yazdır / Önizleme ────────────────────────────────────────────────────

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        var printRows = _grid.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Cells["colSelect"].Value is true)
            .ToList();

        if (printRows.Count == 0)
        {
            MessageBox.Show("Yazdırılacak seçili öğrenci kaydı bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var pd = BuildFeesPrintDocument(printRows);
        PrintPreviewHelper.ShowPreview(pd, $"İşletme Katkı Payı Bordrosu — {_selectedMonth:MMMM yyyy}", this);
    }

    private int _printPageIndex = 0;
    private int _printRowStart  = 0;

    private PrintDocument BuildFeesPrintDocument(List<DataGridViewRow> printRows)
    {
        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins   = new Margins(40, 40, 40, 40);

        pd.BeginPrint += (s, e) =>
        {
            _printPageIndex = 0;
            _printRowStart  = 0;
        };

        pd.PrintPage += (s, ev) =>
        {
            if (ev.Graphics == null) return;
            var g      = ev.Graphics;
            var bounds = ev.MarginBounds;

            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            using var fontTc    = new Font("Segoe UI", 9f, FontStyle.Regular);
            using var fontSchool= new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fontTitle = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var fontSub   = new Font("Segoe UI", 8.5f);
            using var fontHead  = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fontCell  = new Font("Segoe UI", 8f);
            using var brushTitle= new SolidBrush(Color.Black);
            using var brushText = new SolidBrush(Color.Black);
            using var brushGray = new SolidBrush(Color.FromArgb(71, 85, 105));
            using var brushWhite= new SolidBrush(Color.White);
            using var brushAlt  = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var penGrid   = new Pen(Color.FromArgb(148, 163, 184), 0.75f);
            using var penOuter  = new Pen(Color.FromArgb(71, 85, 105), 1.0f);

            // 1. Antet ve Resmi Başlık
            string valilik = string.IsNullOrWhiteSpace(_settings.Valilik) ? "İSTANBUL VALİLİĞİ" : _settings.Valilik.ToUpper(new CultureInfo("tr-TR"));
            string mem = !string.IsNullOrWhiteSpace(_settings.DistrictMem) ? _settings.DistrictMem.ToUpper(new CultureInfo("tr-TR")) : "";
            string okulLine = string.IsNullOrWhiteSpace(_settings.SchoolName) ? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ" : _settings.SchoolName;
            string monthName = _selectedMonth.ToString("MMMM yyyy", new CultureInfo("tr-TR")).ToUpper(new CultureInfo("tr-TR"));
            string titleLine = $"{monthName} DÖNEMİ İŞLETME KATKI PAYI VE ÜCRET BORDROSU";

            // Başlık ve Alt Başlık (Ortalı)
            using var sfTitleCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            int curY = bounds.Y;
            g.DrawString("T.C.", fontTc, brushText, new RectangleF(bounds.X, curY, bounds.Width, 14), sfTitleCenter);
            curY += 13;
            g.DrawString(valilik, fontTc, brushText, new RectangleF(bounds.X, curY, bounds.Width, 14), sfTitleCenter);
            curY += 13;
            if (!string.IsNullOrWhiteSpace(mem))
            {
                g.DrawString(mem, fontTc, brushText, new RectangleF(bounds.X, curY, bounds.Width, 14), sfTitleCenter);
                curY += 13;
            }
            g.DrawString(okulLine, fontSchool, brushText, new RectangleF(bounds.X, curY, bounds.Width, 16), sfTitleCenter);
            curY += 16;
            g.DrawString(titleLine, fontTitle, brushTitle, new RectangleF(bounds.X, curY, bounds.Width, 20), sfTitleCenter);
            curY += 18;
            g.DrawString($"Dönem: {_selectedMonth:MMMM yyyy}  |  Sayfa {_printPageIndex + 1}", fontSub, brushGray, new RectangleF(bounds.X, curY, bounds.Width, 16), sfTitleCenter);
            curY += 20;

            int tableStartY = curY;
            int y  = tableStartY;
            int x  = bounds.X;
            int w  = bounds.Width;

            // Sütunlar: #, Öğr.No, Adı Soyadı, Sınıf, Öğr. Türü, İşletme, Kat., Brüt, Dev.Kat., İşl.Mal., Gün, Kesinti, Net
            int[] colW   = [28, 55, 145, 45, 70, 150, 45, 75, 75, 75, 35, 75, 80];
            string[] hdr = ["#", "Öğr.No", "Adı Soyadı", "Sınıf", "Öğr. Türü", "İşletme", "Kat.", "Brüt (₺)", "Devlet (₺)", "İşletme (₺)", "Gün", "Kesinti (₺)", "Net (₺)"];
            string[] colNames = ["colIdx","colNumber","colName","colClass","colEduType","colEnterprise","colCategory","colGross","colDevletKatkisi","colIsletmeMaliyeti","colAbsent","colDeduction","colNet"];

            StringAlignment[] colAlign = [
                StringAlignment.Center, // #
                StringAlignment.Center, // Öğr.No
                StringAlignment.Near,   // Adı Soyadı
                StringAlignment.Center, // Sınıf
                StringAlignment.Center, // Öğr. Türü
                StringAlignment.Near,   // İşletme
                StringAlignment.Center, // Kat.
                StringAlignment.Far,    // Brüt (₺)
                StringAlignment.Far,    // Devlet (₺)
                StringAlignment.Far,    // İşletme (₺)
                StringAlignment.Center, // Gün
                StringAlignment.Far,    // Kesinti (₺)
                StringAlignment.Far     // Net (₺)
            ];

            int totalW = colW.Sum();
            float sc   = (float)w / totalW;
            var sw     = colW.Select(c => (int)(c * sc)).ToArray();
            sw[sw.Length - 1] += (w - sw.Sum());

            int rowH = 22;

            // Tablo başlığı
            using var hdrBrush = new SolidBrush(Color.FromArgb(15, 118, 110)); // Canlı derin Teal
            g.FillRectangle(hdrBrush, x, y, w, rowH);
            int cx = x;
            for (int i = 0; i < hdr.Length; i++)
            {
                var sfH = new StringFormat
                {
                    Alignment = colAlign[i],
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(hdr[i], fontHead, brushWhite,
                    new RectangleF(cx + 2, y, sw[i] - 4, rowH), sfH);

                if (i < hdr.Length - 1)
                    g.DrawLine(new Pen(Color.FromArgb(13, 148, 136), 1f), cx + sw[i], y, cx + sw[i], y + rowH);

                cx += sw[i];
            }
            y += rowH;

            int rowCount = printRows.Count;
            int pageH    = bounds.Bottom - y - 30;
            int drawn    = 0;

            for (int r = _printRowStart; r < rowCount; r++)
            {
                if (drawn * rowH >= pageH) { _printRowStart = r; break; }
                if (r == rowCount - 1) _printRowStart = rowCount;

                var row = printRows[r];
                if (drawn % 2 == 1) g.FillRectangle(brushAlt, x, y, w, rowH);

                cx = x;
                for (int i = 0; i < colNames.Length; i++)
                {
                    var sfCell = new StringFormat
                    {
                        Alignment = colAlign[i],
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    };

                    string cellVal = i == 0 ? (r + 1).ToString() : (row.Cells[colNames[i]].Value?.ToString() ?? "");

                    g.DrawString(cellVal, fontCell, brushText,
                        new RectangleF(cx + 3, y, sw[i] - 6, rowH), sfCell);

                    if (i < colNames.Length - 1)
                        g.DrawLine(penGrid, cx + sw[i], y, cx + sw[i], y + rowH);

                    cx += sw[i];
                }
                g.DrawLine(penGrid, x, y + rowH, x + w, y + rowH);
                y += rowH;
                drawn++;
            }

            // Dış çerçeve
            g.DrawRectangle(penOuter, x, tableStartY, w, drawn * rowH + rowH);

            _printPageIndex++;
            ev.HasMorePages = _printRowStart < rowCount;
        };

        return pd;
    }

    private void BtnAuditReceipts_Click(object? sender, EventArgs e)
    {
        using var dlg = new Dialogs.ReceiptAuditDialog(_allStudents, _allClasses, _selectedMonth, _receiptRepo);
        dlg.ShowDialog(this);
        _ = LoadAttendanceAndApplyAsync();
    }
}
