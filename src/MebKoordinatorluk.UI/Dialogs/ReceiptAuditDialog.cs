using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class ReceiptMatchItem
{
    public Student Student { get; set; } = null!;
    public string SourceFilePath { get; set; } = string.Empty;
    public string OriginalFileName => Path.GetFileName(SourceFilePath);
    public string ProposedFileName { get; set; } = string.Empty;
    public string MatchReason { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
}

public class UnmatchedFileItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
}

public class StudentComboItem
{
    public Student Student { get; }
    public StudentComboItem(Student student) => Student = student;
    public override string ToString() => $"{Student.FullName} (No: {Student.StudentNumber}, Sınıf: {Student.Classroom})";
}

public class ReceiptAuditDialog : Form
{
    private readonly List<Student> _allStudents;
    private readonly List<SchoolClass> _classes;
    private readonly DateTime _selectedMonth;
    private readonly IFeeReceiptRepository? _receiptRepo;

    private ComboBox _cmbClass = null!;
    private TextBox _txtSourceFolder = null!;
    private ComboBox _cmbNamingPattern = null!;

    private Label _lblKpiMatched = null!;
    private Label _lblKpiMissing = null!;
    private Label _lblKpiUnmatched = null!;

    private TabControl _tabControl = null!;
    private DataGridView _gridMatched = null!;
    private DataGridView _gridMissing = null!;
    private DataGridView _gridUnmatched = null!;
    private ComboBox _cmbManualStudentSearch = null!;
    private CheckBox _chkDeleteSource = null!;

    private List<ReceiptMatchItem> _matchedItems = new();
    private List<Student> _missingStudents = new();
    private List<UnmatchedFileItem> _unmatchedFiles = new();
    private bool _hasExecutedCopy = false;
    private bool _headerCheckboxChecked = true;

    public ReceiptAuditDialog(List<Student> students, List<SchoolClass> classes, DateTime selectedMonth, IFeeReceiptRepository? receiptRepo = null)
    {
        _allStudents = students ?? new List<Student>();
        _classes = classes ?? new List<SchoolClass>();
        _selectedMonth = selectedMonth;
        _receiptRepo = receiptRepo;

        InitializeComponent();
        PopulateClassDropdown();
        PopulateManualStudentCombo();
    }

    private void InitializeComponent()
    {
        Text = "🧾 Banka Dekontu Eşleştirme ve Otomatik Arşivleme";
        Size = new Size(1100, 750);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(950, 650);
        BackColor = UITheme.Surface;
        Font = new Font("Segoe UI", 9f);

        // Header Panel (Purple gradient accent)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(88, 28, 135), // purple-900
            Padding = new Padding(24, 0, 24, 0)
        };

        var lblHeaderTitle = new Label
        {
            Text = "🧾 Banka Dekontu Otomatik Eşleştirme, İsimlendirme ve Arşivleme",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(24, 10),
            AutoSize = true
        };

        var lblHeaderSub = new Label
        {
            Text = "Öğrencilerin banka dekontlarını otomatik tarayın, eşleştirin, standart formatta isimlendirip hedef klasöre kopyalayın.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(233, 213, 255), // purple-200
            Location = new Point(25, 33),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblHeaderTitle, lblHeaderSub]);

        // Input Controls Panel
        var pnlInputs = new Panel
        {
            Dock = DockStyle.Top,
            Height = 125,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 12, 24, 12)
        };
        pnlInputs.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlInputs.Height - 1, pnlInputs.Width, pnlInputs.Height - 1);
        };

        // Row 1: Sınıf Seçimi, Kaynak Klasör, Hedef Klasör
        var lblClass = new Label { Text = "🏫 Sınıf / Bölüm:", Location = new Point(24, 12), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbClass = new ComboBox
        {
            Location = new Point(24, 32),
            Width = 140,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };

        var lblSource = new Label { Text = "📥 Gelen Dekont Klasörü:", Location = new Point(176, 12), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtSourceFolder = new TextBox
        {
            Location = new Point(176, 32),
            Width = 370,
            Font = new Font("Segoe UI", 9f),
            PlaceholderText = "İçinde PDF/JPEG dekontların olduğu klasörü seçin..."
        };
        var btnBrowseSource = new Button
        {
            Text = "📁 Seç...",
            Location = new Point(552, 31),
            Size = new Size(70, 26),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            Cursor = Cursors.Hand
        };
        btnBrowseSource.Click += (s, e) => BrowseFolder(_txtSourceFolder, "Gelen Dekont Klasörünü Seçin");

        var lblTarget = new Label 
        { 
            Text = "📂 Hedef Arşiv (Otomatik):", 
            Location = new Point(640, 12), 
            AutoSize = true, 
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        string targetFolderName = $"Banka_Dekontlari_{_selectedMonth.ToString("MMMM_yyyy", new CultureInfo("tr-TR"))}";
        var lblTargetInfo = new Label
        {
            Text = $"🖥️ Masaüstü \\ {targetFolderName}",
            Location = new Point(640, 35),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(5, 150, 105), // emerald-600
            Cursor = Cursors.Hand
        };
        lblTargetInfo.Click += (s, e) => OpenDefaultTargetFolder();

        // Row 2: İsimlendirme Formatı & Tara Butonu & Masaüstü Klasörünü Aç
        var lblNaming = new Label { Text = "🏷️ Yeni Dosya İsimlendirme Formatı:", Location = new Point(24, 68), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbNamingPattern = new ComboBox
        {
            Location = new Point(24, 86),
            Width = 492,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbNamingPattern.Items.AddRange(new object[]
        {
            "[Sınıf]_[OkulNo]_[AdSoyad]_[İşletme]_Dekont  (Örn: 12-A_105_Ahmet_Kaya_ABC_Ltd_Dekont.pdf)",
            "[Sınıf]_[OkulNo]_[AdSoyad]_Dekont  (Örn: 12-A_105_Ahmet_Kaya_Dekont.pdf)",
            "[İşletme]_[Sınıf]_[OkulNo]_[AdSoyad]_Dekont  (Örn: ABC_Ltd_12-A_105_Ahmet_Kaya_Dekont.pdf)",
            "[AdSoyad]_[OkulNo]_[Sınıf]_Dekont  (Örn: Ahmet_Kaya_105_12-A_Dekont.pdf)",
            "[OkulNo]_[AdSoyad]_[Sınıf]_Dekont  (Örn: 105_Ahmet_Kaya_12-A_Dekont.pdf)",
            "[Donem]_[Sınıf]_[OkulNo]_[AdSoyad] (Örn: 2026-10_12-A_105_Ahmet_Kaya.pdf)"
        });
        _cmbNamingPattern.SelectedIndex = 0;

        var btnScan = new Button
        {
            Text = "⚡ Dekontları Tara ve Eşleştir",
            Location = new Point(530, 84),
            Size = new Size(230, 28),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(147, 51, 234), // purple-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnScan.FlatAppearance.BorderSize = 0;
        btnScan.Click += BtnScan_Click;

        var btnOpenTarget = new Button
        {
            Text = "📂 Masaüstü Klasörünü Aç",
            Location = new Point(775, 84),
            Size = new Size(185, 28),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnOpenTarget.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnOpenTarget.Click += (s, e) => OpenDefaultTargetFolder();

        pnlInputs.Controls.AddRange([
            lblClass, _cmbClass,
            lblSource, _txtSourceFolder, btnBrowseSource,
            lblTarget, lblTargetInfo,
            lblNaming, _cmbNamingPattern,
            btnScan, btnOpenTarget
        ]);

        // KPI Summary Bar
        var pnlKpi = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(238, 242, 255), // indigo-50
            Padding = new Padding(24, 8, 24, 8)
        };

        var flpKpi = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true
        };

        _lblKpiMatched = new Label
        {
            Text = "🟢 Eşleşen Dekont: 0",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(5, 150, 105), // emerald-600
            AutoSize = true,
            Margin = new Padding(0, 4, 32, 0)
        };

        _lblKpiMissing = new Label
        {
            Text = "🔴 Dekontu Eksik Öğrenci: 0",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38), // red-600
            AutoSize = true,
            Margin = new Padding(0, 4, 32, 0)
        };

        _lblKpiUnmatched = new Label
        {
            Text = "❓ Tanımsız / Eşleşmeyen Dosya: 0",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(217, 119, 6), // amber-600
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };

        flpKpi.Controls.AddRange([_lblKpiMatched, _lblKpiMissing, _lblKpiUnmatched]);
        pnlKpi.Controls.Add(flpKpi);

        // TabControl for Results
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding = new Point(12, 6)
        };

        var tabMatched = new TabPage("🟢 Eşleşen Dekontlar (0)");
        var tabMissing = new TabPage("🔴 Dekontu Eksik Öğrenciler (0)");
        var tabUnmatched = new TabPage("❓ Tanımsız / Eşleşmeyen Dosyalar (0)");

        _gridMatched = CreateGrid();
        _gridMatched.ReadOnly = false;
        _gridMatched.EditMode = DataGridViewEditMode.EditProgrammatically;
        _gridMatched.CellClick += GridMatched_CellClick;
        _gridMatched.CellContentClick += GridMatched_CellContentClick;
        _gridMatched.ColumnHeaderMouseClick += GridMatched_ColumnHeaderMouseClick;
        _gridMatched.CellPainting += GridMatched_CellPainting;
        _gridMatched.CellMouseMove += GridMatched_CellMouseMove;
        _gridMatched.KeyDown += GridMatched_KeyDown;
        SetupMatchedGridColumns();
        tabMatched.Controls.Add(_gridMatched);

        _gridMissing = CreateGrid();
        SetupMissingGridColumns();
        tabMissing.Controls.Add(_gridMissing);

        _gridUnmatched = CreateGrid();
        SetupUnmatchedGridColumns();

        // Manual Match Bar for Tab 3
        var pnlManualMatch = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Color.FromArgb(254, 243, 199), // amber-100
            Padding = new Padding(12, 8, 12, 8)
        };

        var lblManualHint = new Label
        {
            Text = "💡 Seçili Dosyayı Öğrenciyle Manuel Eşleştir:",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14), // amber-800
            AutoSize = true,
            Location = new Point(12, 12)
        };

        _cmbManualStudentSearch = new ComboBox
        {
            Width = 320,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(275, 8)
        };

        var btnManualMatch = new Button
        {
            Text = "🔗 Öğrenciye Bağla & Aktar",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(124, 58, 237), // purple-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(185, 28),
            Location = new Point(605, 7),
            Cursor = Cursors.Hand
        };
        btnManualMatch.FlatAppearance.BorderSize = 0;
        btnManualMatch.Click += BtnManualMatch_Click;

        pnlManualMatch.Controls.AddRange([lblManualHint, _cmbManualStudentSearch, btnManualMatch]);

        tabUnmatched.Controls.Add(_gridUnmatched);
        tabUnmatched.Controls.Add(pnlManualMatch);

        _tabControl.TabPages.AddRange([tabMatched, tabMissing, tabUnmatched]);

        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 12, 24, 12)
        };
        pnlGridContainer.Controls.Add(_tabControl);

        // Bottom Actions Panel
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 12, 24, 12)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var btnExecuteCopy = new Button
        {
            Text = "📁 Seçilen Dekontları Düzenle ve Kopyala",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(5, 150, 105), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(330, 36),
            Location = new Point(24, 12),
            Cursor = Cursors.Hand
        };
        btnExecuteCopy.FlatAppearance.BorderSize = 0;
        btnExecuteCopy.Click += BtnExecuteCopy_Click;

        _chkDeleteSource = new CheckBox
        {
            Text = "✂️ Kaynaktan Sil (Taşı)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 28, 28), // red-700
            AutoSize = true,
            Location = new Point(366, 20),
            Cursor = Cursors.Hand
        };

        var btnExportMissingExcel = new Button
        {
            Text = "📊 Eksik Listesini İndir (CSV)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(200, 36),
            Location = new Point(545, 12),
            Cursor = Cursors.Hand
        };
        btnExportMissingExcel.FlatAppearance.BorderSize = 0;
        btnExportMissingExcel.Click += BtnExportMissingExcel_Click;

        var btnClose = new Button
        {
            Text = "Kapat",
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(90, 36),
            Location = new Point(pnlBottom.Width - 114, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();

        pnlBottom.Controls.AddRange([btnExecuteCopy, _chkDeleteSource, btnExportMissingExcel, btnClose]);

        Controls.Add(pnlGridContainer);
        Controls.Add(pnlKpi);
        Controls.Add(pnlInputs);
        Controls.Add(pnlHeader);
        Controls.Add(pnlBottom);
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 36 },
            Font = new Font("Segoe UI", 9f)
        };
        UITheme.ApplyModernGridStyle(grid);
        return grid;
    }

    private void SetupMatchedGridColumns()
    {
        _gridMatched.Columns.Clear();

        var colSelect = new DataGridViewCheckBoxColumn
        {
            Name = "colSelect",
            HeaderText = string.Empty,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 45,
            Resizable = DataGridViewTriState.False,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };
        colSelect.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _gridMatched.Columns.Add(colSelect);

        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIdx", HeaderText = "#", Width = 35, FillWeight = 3, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "Öğr. No", FillWeight = 7, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) } });
        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Öğrenci Adı Soyadı", FillWeight = 14, ReadOnly = true, DefaultCellStyle = { Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) } });
        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClass", HeaderText = "Sınıf", FillWeight = 6, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEnterprise", HeaderText = "Staj İşletmesi", FillWeight = 14, ReadOnly = true });
        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOriginal", HeaderText = "Gelen Dosya Adı", FillWeight = 16, ReadOnly = true });
        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProposed", HeaderText = "Yeni Hedef Dosya Adı", FillWeight = 20, ReadOnly = true, DefaultCellStyle = { ForeColor = Color.FromArgb(5, 150, 105), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) } });
        _gridMatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colReason", HeaderText = "Eşleşme Kriteri", FillWeight = 12, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        var btnCol = new DataGridViewButtonColumn
        {
            Name = "colPreview",
            HeaderText = "İşlem",
            Text = "👁️ Aç",
            UseColumnTextForButtonValue = true,
            FillWeight = 6,
            ReadOnly = true
        };
        _gridMatched.Columns.Add(btnCol);
    }

    private void SetupMissingGridColumns()
    {
        _gridMissing.Columns.Clear();
        _gridMissing.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIdx", HeaderText = "#", Width = 40, FillWeight = 4, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _gridMissing.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "Okul No", FillWeight = 10, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) } });
        _gridMissing.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Öğrenci Adı Soyadı", FillWeight = 22, ReadOnly = true, DefaultCellStyle = { Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) } });
        _gridMissing.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClass", HeaderText = "Sınıf", FillWeight = 10, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _gridMissing.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEdu", HeaderText = "Öğretim Türü", FillWeight = 14, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _gridMissing.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEnterprise", HeaderText = "Staj İşletmesi", FillWeight = 26, ReadOnly = true });
        _gridMissing.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "Durum", FillWeight = 14, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.Red, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) } });
    }

    private void SetupUnmatchedGridColumns()
    {
        _gridUnmatched.Columns.Clear();
        _gridUnmatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIdx", HeaderText = "#", Width = 40, FillWeight = 4, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _gridUnmatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFile", HeaderText = "Dosya Adı", FillWeight = 35, ReadOnly = true, DefaultCellStyle = { Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) } });
        _gridUnmatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSize", HeaderText = "Boyut", FillWeight = 12, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _gridUnmatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate", HeaderText = "Son Değiştirme Tarihi", FillWeight = 18, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _gridUnmatched.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPath", HeaderText = "Tam Dosya Yolu", FillWeight = 31, ReadOnly = true });

        var btnCol = new DataGridViewButtonColumn
        {
            Name = "colPreviewUnmatched",
            HeaderText = "İşlem",
            Text = "👁️ Aç",
            UseColumnTextForButtonValue = true,
            FillWeight = 8
        };
        _gridUnmatched.Columns.Add(btnCol);
        _gridUnmatched.CellContentClick += GridUnmatched_CellContentClick;
    }

    private void PopulateClassDropdown()
    {
        _cmbClass.Items.Clear();
        _cmbClass.Items.Add("Tüm Sınıflar");
        foreach (var c in _classes.OrderBy(c => c.Name))
        {
            _cmbClass.Items.Add(c.Name);
        }
        _cmbClass.SelectedIndex = 0;
    }

    private void PopulateManualStudentCombo()
    {
        _cmbManualStudentSearch.Items.Clear();
        var sorted = _allStudents.OrderBy(s => s.FullName).ToList();
        foreach (var s in sorted)
        {
            _cmbManualStudentSearch.Items.Add(new StudentComboItem(s));
        }
        _cmbManualStudentSearch.SelectedIndex = -1;
        _cmbManualStudentSearch.Text = string.Empty;
    }

    private void BtnManualMatch_Click(object? sender, EventArgs e)
    {
        string searchText = _cmbManualStudentSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            MessageBox.Show("Öğrenci arama kutusu boş! Lütfen eşleştirilecek bir öğrenci seçiniz.", "Öğrenci Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbManualStudentSearch.Focus();
            return;
        }

        if (_gridUnmatched.SelectedRows.Count == 0)
        {
            MessageBox.Show("Lütfen eşleştirilecek dosyayı listeden seçiniz.", "Dosya Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        StudentComboItem? comboItem = _cmbManualStudentSearch.SelectedItem as StudentComboItem;
        if (comboItem == null)
        {
            var match = _cmbManualStudentSearch.Items
                .OfType<StudentComboItem>()
                .FirstOrDefault(item =>
                    item.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Student.FullName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrEmpty(item.Student.StudentNumber) && item.Student.StudentNumber.Equals(searchText, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                comboItem = match;
            }
        }

        if (comboItem == null)
        {
            MessageBox.Show("Yazılan kriterlere uygun bir öğrenci bulunamadı. Lütfen listeden geçerli bir öğrenci seçiniz.", "Öğrenci Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbManualStudentSearch.Focus();
            return;
        }

        var selectedStudent = comboItem.Student;
        var row = _gridUnmatched.SelectedRows[0];
        if (row.Tag is not UnmatchedFileItem unmatchedItem) return;

        // Remove from unmatched list
        _unmatchedFiles.Remove(unmatchedItem);

        // Remove from missing students list if present
        var missingStudent = _missingStudents.FirstOrDefault(s => s.Id == selectedStudent.Id || (s.StudentNumber == selectedStudent.StudentNumber && !string.IsNullOrEmpty(s.StudentNumber)));
        if (missingStudent != null)
        {
            _missingStudents.Remove(missingStudent);
        }

        // Create new matched item
        string ext = Path.GetExtension(unmatchedItem.FilePath);
        string proposedName = BuildProposedFileName(selectedStudent, _cmbNamingPattern.SelectedIndex, ext);

        _matchedItems.Add(new ReceiptMatchItem
        {
            Student = selectedStudent,
            SourceFilePath = unmatchedItem.FilePath,
            ProposedFileName = proposedName,
            MatchReason = "Manuel Eşleştirildi (%100)",
            IsSelected = true
        });

        _hasExecutedCopy = false;

        // Re-render UI grids & KPIs
        RenderResults();

        // Arama kutusunu temizle
        _cmbManualStudentSearch.SelectedIndex = -1;
        _cmbManualStudentSearch.Text = string.Empty;

        MessageBox.Show(
            $"✅ '{unmatchedItem.FileName}' dosyası başarıyla {selectedStudent.FullName} öğrencisine bağlandı ve 'Eşleşen Dekontlar' listesine aktarıldı!",
            "Manuel Eşleştirme Başarılı",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void BrowseFolder(TextBox targetTxt, string title)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (!string.IsNullOrWhiteSpace(targetTxt.Text) && Directory.Exists(targetTxt.Text))
        {
            dialog.SelectedPath = targetTxt.Text;
        }
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            targetTxt.Text = dialog.SelectedPath;
        }
    }

    private void BtnScan_Click(object? sender, EventArgs e)
    {
        string sourceDir = _txtSourceFolder.Text.Trim();
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            MessageBox.Show("Lütfen geçerli bir 'Gelen Dekont Klasörü' seçiniz.", "Klasör Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string selectedClass = _cmbClass.SelectedIndex > 0 ? _cmbClass.SelectedItem?.ToString() ?? "" : "";

        // Target all students
        var targetStudents = _allStudents.ToList();
        if (!string.IsNullOrWhiteSpace(selectedClass))
        {
            targetStudents = targetStudents.Where(s => string.Equals(s.Classroom, selectedClass, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (targetStudents.Count == 0)
        {
            MessageBox.Show("Seçilen sınıfta öğrenci kaydı bulunamadı.", "Öğrenci Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Get supported files (.pdf, .jpg, .jpeg, .png) recursively from folder & subfolders
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" };
        var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
            .Where(f => allowedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (files.Count == 0)
        {
            MessageBox.Show("Seçilen klasörde PDF veya Görsel (.jpg/.png) dekont dosyası bulunamadı.", "Dosya Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Perform Matching
        PerformMatching(targetStudents, files);
    }

    private void PerformMatching(List<Student> students, List<string> files)
    {
        _matchedItems.Clear();
        _missingStudents.Clear();
        _unmatchedFiles.Clear();
        _hasExecutedCopy = false;

        var matchedFilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int patternIndex = _cmbNamingPattern.SelectedIndex;

        // Pre-extract & cache normalized text and token lists for all files (10x speed boost & Zlib/FlateDecode PDF decompression)
        var fileDataCache = new Dictionary<string, (string NormCombinedText, List<string> Tokens)>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in files)
        {
            string fn = Path.GetFileName(filePath);
            string pdfContent = "";
            if (Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    byte[] b = File.ReadAllBytes(filePath);
                    pdfContent = ExtractPdfTextWithDecompression(b);
                }
                catch { }
            }
            string combined = fn + " " + pdfContent;
            string normText = NormalizeTextPreserveSpaces(combined);
            var tokens = GetTokens(combined);
            fileDataCache[filePath] = (normText, tokens);
        }

        foreach (var student in students)
        {
            string tc = (student.TcKimlikNo ?? "").Trim();
            string fullName = (student.FullName ?? "").Trim();
            string firstName = (student.FirstName ?? "").Trim();
            string lastName = (student.LastName ?? "").Trim();
            string iban = NormalizeText(student.Iban ?? "");

            var studentTokens = GetTokens(fullName);
            var firstNameTokens = GetTokens(firstName);
            var lastNameTokens = GetTokens(lastName);
            string lastNameToken = lastNameTokens.LastOrDefault() ?? "";
            string normConcatFullName = NormalizeText(fullName);

            string? bestMatchedFile = null;
            string bestMatchReason = "";
            int bestScore = 0;

            foreach (var filePath in files)
            {
                if (matchedFilesSet.Contains(filePath)) continue;

                var (fileNormText, fileTokens) = fileDataCache[filePath];
                string fileName = Path.GetFileName(filePath);
                string normFileNameConcat = NormalizeText(fileName);

                int score = 0;
                string reason = "";

                // 1. TC Kimlik No eşleşmesi (%100)
                if (!string.IsNullOrEmpty(tc) && tc.Length == 11 && (fileName.Contains(tc) || fileNormText.Contains(tc)))
                {
                    score = 100;
                    reason = "TC Kimlik No Eşleşti (%100)";
                }
                // 2. IBAN No eşleşmesi (%100)
                else if (!string.IsNullOrEmpty(iban) && iban.Length >= 15 && (normFileNameConcat.Contains(iban) || NormalizeText(fileNormText).Contains(iban)))
                {
                    score = 100;
                    reason = "IBAN Numarası Eşleşti (%100)";
                }
                // 3. Tüm İsim Kelimeleri Dosya Adı / İçeriğinde Var (%90)
                else if (studentTokens.Count >= 2 && studentTokens.All(st => fileTokens.Contains(st) || fileNormText.Contains(st)))
                {
                    score = 90;
                    reason = "Tüm İsim Kelimeleri Eşleşti (%90)";
                }
                // 4. Soyad + En Az Bir Ad Kelimesi Eşleşti (%85)
                else if (!string.IsNullOrEmpty(lastNameToken) && (fileTokens.Contains(lastNameToken) || fileNormText.Contains(lastNameToken)) &&
                         firstNameTokens.Any(fn => fileTokens.Contains(fn) || fileNormText.Contains(fn)))
                {
                    score = 85;
                    reason = "Ad ve Soyad Kelimeleri Eşleşti (%85)";
                }
                // 5. Birleşik Ad-Soyad İçerme (%80)
                else if (!string.IsNullOrEmpty(normConcatFullName) && normConcatFullName.Length > 3 &&
                         (normFileNameConcat.Contains(normConcatFullName) || NormalizeText(fileNormText).Contains(normConcatFullName)))
                {
                    score = 80;
                    reason = "Birleşik Ad Soyad Eşleşti (%80)";
                }
                // 6. En az 2 İsim Kelimesi Veya Soyad Kelimesi (%75)
                else if (studentTokens.Count(st => fileTokens.Contains(st) || fileNormText.Contains(st)) >= 2 ||
                         (!string.IsNullOrEmpty(lastNameToken) && lastNameToken.Length >= 4 && (fileTokens.Contains(lastNameToken) || fileNormText.Contains(lastNameToken))))
                {
                    score = 75;
                    reason = "İsim/Soyisim Kısmi Eşleşti (%75)";
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatchedFile = filePath;
                    bestMatchReason = reason;
                }
            }

            if (bestMatchedFile != null && bestScore >= 75)
            {
                matchedFilesSet.Add(bestMatchedFile);

                string ext = Path.GetExtension(bestMatchedFile);
                string proposedName = BuildProposedFileName(student, patternIndex, ext);

                _matchedItems.Add(new ReceiptMatchItem
                {
                    Student = student,
                    SourceFilePath = bestMatchedFile,
                    ProposedFileName = proposedName,
                    MatchReason = bestMatchReason,
                    IsSelected = true
                });
            }
            else
            {
                _missingStudents.Add(student);
            }
        }

        // Collect unmatched files
        foreach (var filePath in files)
        {
            if (!matchedFilesSet.Contains(filePath))
            {
                var fi = new FileInfo(filePath);
                _unmatchedFiles.Add(new UnmatchedFileItem
                {
                    FilePath = filePath,
                    FileSize = fi.Length,
                    LastModified = fi.LastWriteTime
                });
            }
        }

        // Update Grids & KPIs
        RenderResults();
    }

    private string BuildProposedFileName(Student s, int patternIndex, string ext)
    {
        string cls = CleanFileName(s.Classroom ?? "Sınıf");
        string no = CleanFileName(s.StudentNumber ?? "0");
        string name = CleanFileName(s.FullName ?? "Ogrenci");
        string enterprise = CleanFileName(s.EnterpriseName ?? "Isletme");
        string period = _selectedMonth.ToString("yyyy-MM");

        return patternIndex switch
        {
            1 => $"{cls}_{no}_{name}_Dekont{ext}",
            2 => $"{enterprise}_{cls}_{no}_{name}_Dekont{ext}",
            3 => $"{name}_{no}_{cls}_Dekont{ext}",
            4 => $"{no}_{name}_{cls}_Dekont{ext}",
            5 => $"{period}_{cls}_{no}_{name}{ext}",
            _ => $"{cls}_{no}_{name}_{enterprise}_Dekont{ext}"
        };
    }

    private static string CleanFileName(string str)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            str = str.Replace(c, '_');
        }
        return str.Replace(" ", "_").Trim('_');
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        text = text.ToLower(new CultureInfo("tr-TR"));
        text = text.Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        text = Regex.Replace(text, @"[^a-z0-9]", "");
        return text;
    }

    private static string NormalizeTextPreserveSpaces(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        text = text.ToLower(new CultureInfo("tr-TR"));
        text = text.Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        text = Regex.Replace(text, @"[^a-z0-9\s]", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static List<string> GetTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        return NormalizeTextPreserveSpaces(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2)
            .Distinct()
            .ToList();
    }

    private void RenderResults()
    {
        // KPIs
        _lblKpiMatched.Text = $"🟢 Eşleşen Dekont: {_matchedItems.Count}";
        _lblKpiMissing.Text = $"🔴 Dekontu Eksik Öğrenci: {_missingStudents.Count}";
        _lblKpiUnmatched.Text = $"❓ Tanımsız / Eşleşmeyen Dosya: {_unmatchedFiles.Count}";

        // Tab Titles
        _tabControl.TabPages[0].Text = $"🟢 Eşleşen Dekontlar ({_matchedItems.Count})";
        _tabControl.TabPages[1].Text = $"🔴 Dekontu Eksik Öğrenciler ({_missingStudents.Count})";
        _tabControl.TabPages[2].Text = $"❓ Tanımsız / Eşleşmeyen Dosyalar ({_unmatchedFiles.Count})";

        // Tab 1 Grid
        _gridMatched.Rows.Clear();
        int idx1 = 1;
        foreach (var item in _matchedItems)
        {
            int rIdx = _gridMatched.Rows.Add(
                item.IsSelected,
                idx1++,
                item.Student.StudentNumber,
                item.Student.FullName,
                item.Student.Classroom,
                item.Student.EnterpriseName ?? "Atanmadı",
                item.OriginalFileName,
                item.ProposedFileName,
                item.MatchReason
            );
            _gridMatched.Rows[rIdx].Tag = item;
        }
        UpdateMatchedHeaderCheckboxState();

        // Tab 2 Grid
        _gridMissing.Rows.Clear();
        int idx2 = 1;
        foreach (var s in _missingStudents)
        {
            int rIdx = _gridMissing.Rows.Add(
                idx2++,
                s.StudentNumber,
                s.FullName,
                s.Classroom,
                s.EducationType ?? "Örgün Eğitim",
                s.EnterpriseName ?? "Atanmadı",
                "Dekont Eksik / Bulunamadı"
            );
            _gridMissing.Rows[rIdx].Tag = s;
        }

        // Tab 3 Grid
        _gridUnmatched.Rows.Clear();
        int idx3 = 1;
        foreach (var u in _unmatchedFiles)
        {
            string sizeStr = $"{u.FileSize / 1024.0:N0} KB";
            int rIdx = _gridUnmatched.Rows.Add(
                idx3++,
                u.FileName,
                sizeStr,
                u.LastModified.ToString("dd.MM.yyyy HH:mm"),
                u.FilePath
            );
            _gridUnmatched.Rows[rIdx].Tag = u;
        }
    }

    private void GridMatched_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        if (_gridMatched.Columns[e.ColumnIndex].Name == "colSelect")
        {
            var row = _gridMatched.Rows[e.RowIndex];
            bool curVal = Convert.ToBoolean(row.Cells["colSelect"].Value);
            bool newVal = !curVal;
            row.Cells["colSelect"].Value = newVal;

            if (row.Tag is ReceiptMatchItem item)
            {
                item.IsSelected = newVal;
            }

            _gridMatched.InvalidateCell(e.ColumnIndex, e.RowIndex);
            UpdateMatchedHeaderCheckboxState();
        }
    }

    private void GridMatched_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _gridMatched.Columns[e.ColumnIndex].Name == "colPreview")
        {
            if (_gridMatched.Rows[e.RowIndex].Tag is ReceiptMatchItem item && File.Exists(item.SourceFilePath))
            {
                OpenFile(item.SourceFilePath);
            }
        }
    }

    private void GridMatched_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex >= 0 && _gridMatched.Columns.Contains("colSelect") && e.ColumnIndex == _gridMatched.Columns["colSelect"].Index)
        {
            ToggleAllMatchedSelection();
        }
    }

    private void ToggleAllMatchedSelection()
    {
        if (_gridMatched.Rows.Count == 0) return;

        _headerCheckboxChecked = !_headerCheckboxChecked;
        foreach (DataGridViewRow r in _gridMatched.Rows)
        {
            r.Cells["colSelect"].Value = _headerCheckboxChecked;
            if (r.Tag is ReceiptMatchItem item)
            {
                item.IsSelected = _headerCheckboxChecked;
            }
        }
        _gridMatched.Refresh();
    }

    private void UpdateMatchedHeaderCheckboxState()
    {
        if (_gridMatched.Rows.Count == 0)
        {
            _headerCheckboxChecked = false;
        }
        else
        {
            bool allChecked = true;
            foreach (DataGridViewRow r in _gridMatched.Rows)
            {
                if (r.Cells["colSelect"] != null && !Convert.ToBoolean(r.Cells["colSelect"].Value))
                {
                    allChecked = false;
                    break;
                }
            }
            _headerCheckboxChecked = allChecked;
        }

        if (_gridMatched.Columns.Contains("colSelect"))
        {
            _gridMatched.InvalidateCell(_gridMatched.Columns["colSelect"].Index, -1);
        }
    }

    private void GridMatched_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex == -1 && e.ColumnIndex >= 0 && _gridMatched.Columns.Contains("colSelect") && e.ColumnIndex == _gridMatched.Columns["colSelect"].Index)
        {
            e.PaintBackground(e.CellBounds, true);

            var size = 15;
            var pt = new Point(
                e.CellBounds.X + (e.CellBounds.Width - size) / 2,
                e.CellBounds.Y + (e.CellBounds.Height - size) / 2);

            if (e.Graphics != null)
            {
                if (Application.RenderWithVisualStyles)
                {
                    var state = _headerCheckboxChecked
                        ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                        : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                    CheckBoxRenderer.DrawCheckBox(e.Graphics, pt, state);
                }
                else
                {
                    ControlPaint.DrawCheckBox(e.Graphics, new Rectangle(pt, new Size(size, size)),
                        _headerCheckboxChecked ? ButtonState.Checked : ButtonState.Normal);
                }
            }

            e.Handled = true;
        }
    }

    private void GridMatched_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex == -1 && e.ColumnIndex >= 0 && _gridMatched.Columns.Contains("colSelect") && e.ColumnIndex == _gridMatched.Columns["colSelect"].Index)
            _gridMatched.Cursor = Cursors.Hand;
        else if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && (_gridMatched.Columns[e.ColumnIndex].Name == "colSelect" || _gridMatched.Columns[e.ColumnIndex].Name == "colPreview"))
            _gridMatched.Cursor = Cursors.Hand;
        else
            _gridMatched.Cursor = Cursors.Default;
    }

    private void GridMatched_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && _gridMatched.CurrentRow != null)
        {
            var row = _gridMatched.CurrentRow;
            if (row.Index >= 0 && _gridMatched.Columns.Contains("colSelect"))
            {
                bool curVal = Convert.ToBoolean(row.Cells["colSelect"].Value);
                bool newVal = !curVal;
                row.Cells["colSelect"].Value = newVal;
                if (row.Tag is ReceiptMatchItem item)
                {
                    item.IsSelected = newVal;
                }
                _gridMatched.InvalidateCell(_gridMatched.Columns["colSelect"].Index, row.Index);
                UpdateMatchedHeaderCheckboxState();
                e.Handled = true;
            }
        }
    }

    private static void GridUnmatched_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is DataGridView grid && e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "colPreviewUnmatched")
        {
            if (grid.Rows[e.RowIndex].Tag is UnmatchedFileItem item && File.Exists(item.FilePath))
            {
                OpenFile(item.FilePath);
            }
        }
    }

    private static void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dosya açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExecuteCopy_Click(object? sender, EventArgs e)
    {
        _gridMatched.EndEdit();

        // Collect checked items
        var selectedToCopy = new List<ReceiptMatchItem>();
        foreach (DataGridViewRow row in _gridMatched.Rows)
        {
            if (row.Cells["colSelect"] != null && Convert.ToBoolean(row.Cells["colSelect"].Value) && row.Tag is ReceiptMatchItem item)
            {
                selectedToCopy.Add(item);
            }
        }

        if (selectedToCopy.Count == 0)
        {
            MessageBox.Show("Lütfen kopyalanıp yeniden isimlendirilecek en az bir dekont seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string targetDir = GetDefaultTargetFolder();

        if (!Directory.Exists(targetDir))
        {
            try
            {
                Directory.CreateDirectory(targetDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hedef klasör oluşturulamadı:\n{ex.Message}", "Klasör Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        bool moveSource = _chkDeleteSource != null && _chkDeleteSource.Checked;
        string actionText = moveSource ? "hedef klasöre taşınacak (kaynaktan silinecektir)" : "hedef klasöre kopyalanacaktır";

        var confirm = MessageBox.Show(
            $"Seçilen {selectedToCopy.Count} adet dekont dosyası yeni isimleri ile {actionText}:\n\n" +
            $"📁 Hedef Klasör: {targetDir}\n" +
            (moveSource ? "\n⚠️ DİKKAT: İşlem sonrası kopyalanan dosyalar gelen (kaynak) klasörden silinecektir!\n\n" : "\n\n") +
            "Devam etmek istiyor musunuz?",
            "Dekont Arşivleme Onayı",
            MessageBoxButtons.YesNo,
            moveSource ? MessageBoxIcon.Warning : MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        int successCount = 0;
        int deleteCount = 0;
        int errorCount = 0;

        foreach (var item in selectedToCopy)
        {
            try
            {
                string destPath = Path.Combine(targetDir, item.ProposedFileName);

                // Check collision
                if (File.Exists(destPath))
                {
                    string nameNoExt = Path.GetFileNameWithoutExtension(item.ProposedFileName);
                    string ext = Path.GetExtension(item.ProposedFileName);
                    destPath = Path.Combine(targetDir, $"{nameNoExt}_{Guid.NewGuid():N}"[..4] + ext);
                }

                File.Copy(item.SourceFilePath, destPath, overwrite: true);
                successCount++;

                if (_receiptRepo != null)
                {
                    try
                    {
                        _ = _receiptRepo.AddOrUpdateAsync(new FeeReceipt
                        {
                            StudentId = item.Student.Id,
                            Period = _selectedMonth.ToString("yyyy-MM"),
                            FileName = Path.GetFileName(destPath),
                            ArchivePath = destPath,
                            AuditDate = DateTime.Now,
                            MatchReason = item.MatchReason
                        });
                    }
                    catch { }
                }

                if (moveSource)
                {
                    try
                    {
                        File.Delete(item.SourceFilePath);
                        deleteCount++;
                    }
                    catch { }
                }
            }
            catch
            {
                errorCount++;
            }
        }

        if (successCount > 0)
        {
            _hasExecutedCopy = true;
        }

        MessageBox.Show(
            $"İşlem Tamamlandı!\n\n" +
            $"✅ Başarıyla Arşivlenen: {successCount} dekont\n" +
            (moveSource ? $"✂️ Kaynaktan Silinen: {deleteCount} dosya\n" : "") +
            (errorCount > 0 ? $"❌ Hata Alınan: {errorCount} dosya\n" : "") +
            $"\nDosyalar şu adreste arşivlendi:\n{targetDir}",
            "Arşivleme Tamamlandı",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        // Open target folder in Explorer
        try
        {
            Process.Start(new ProcessStartInfo(targetDir) { UseShellExecute = true });
        }
        catch { }
    }

    private void BtnExportMissingExcel_Click(object? sender, EventArgs e)
    {
        if (_missingStudents.Count == 0)
        {
            MessageBox.Show("Dekontu eksik öğrenci kaydı bulunmamaktadır.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"Eksik_Dekont_Listesi_{_selectedMonth:yyyy_MM}.csv",
            Title = "Eksik Dekont Listesini Kaydet"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sira,Okul No,Ogrenci Adi Soyadi,Sinif,Ogretim Turu,Staj Isletmesi,Durum");

            int idx = 1;
            foreach (var s in _missingStudents)
            {
                sb.AppendLine($"{idx++},{s.StudentNumber},{s.FullName},{s.Classroom},{s.EducationType ?? "Örgün Eğitim"},{s.EnterpriseName ?? "Atanmadı"},Eksik Dekont");
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Eksik dekont listesi başarıyla indirildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static string ExtractPdfTextWithDecompression(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        try
        {
            // 1. Raw Latin1 & UTF8 fallback
            string rawLatin1 = CleanPdfString(Encoding.Latin1.GetString(pdfBytes));
            string rawUtf8 = CleanPdfString(Encoding.UTF8.GetString(pdfBytes));
            sb.AppendLine(rawLatin1);
            sb.AppendLine(rawUtf8);
            sb.AppendLine(DecodePdfHexStrings(rawLatin1));

            // 2. Find and decompress PDF stream blocks (FlateDecode / Zlib / Deflate)
            byte[] streamMarker = Encoding.ASCII.GetBytes("stream");
            byte[] endMarker = Encoding.ASCII.GetBytes("endstream");

            int pos = 0;
            while (pos < pdfBytes.Length)
            {
                int streamStart = FindBytes(pdfBytes, streamMarker, pos);
                if (streamStart == -1) break;

                int dataStart = streamStart + 6;
                // Skip past any leading newline/space bytes to find start of compressed binary data
                while (dataStart < pdfBytes.Length && (pdfBytes[dataStart] == '\r' || pdfBytes[dataStart] == '\n' || pdfBytes[dataStart] == ' ' || pdfBytes[dataStart] == '\t'))
                {
                    dataStart++;
                }

                int streamEnd = FindBytes(pdfBytes, endMarker, dataStart);
                if (streamEnd == -1) break;

                int length = streamEnd - dataStart;
                while (length > 0 && (pdfBytes[dataStart + length - 1] == '\r' || pdfBytes[dataStart + length - 1] == '\n'))
                {
                    length--;
                }

                if (length > 0 && dataStart >= 0 && dataStart + length <= pdfBytes.Length)
                {
                    byte[] compressed = new byte[length];
                    System.Buffer.BlockCopy(pdfBytes, dataStart, compressed, 0, length);

                    byte[]? decompressed = TryDecompressZlib(compressed);
                    if (decompressed != null && decompressed.Length > 0)
                    {
                        string utf8Text = CleanPdfString(Encoding.UTF8.GetString(decompressed));
                        string latin1Text = CleanPdfString(Encoding.Latin1.GetString(decompressed));
                        string utf16BeText = CleanPdfString(DecodeUtf16Be(decompressed));

                        sb.AppendLine(utf8Text);
                        sb.AppendLine(latin1Text);
                        sb.AppendLine(utf16BeText);
                        sb.AppendLine(DecodePdfHexStrings(utf8Text));
                        sb.AppendLine(DecodePdfHexStrings(latin1Text));
                    }
                }

                pos = streamEnd + 8;
            }
        }
        catch { }

        return sb.ToString();
    }

    private static byte[]? TryDecompressZlib(byte[] compressed)
    {
        if (compressed == null || compressed.Length < 4) return null;

        // Try ZLibStream at offsets 0..4
        for (int offset = 0; offset <= 4 && offset < compressed.Length - 4; offset++)
        {
            try
            {
                using var ms = new MemoryStream(compressed, offset, compressed.Length - offset);
                using var zlib = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                zlib.CopyTo(outMs);
                if (outMs.Length > 0) return outMs.ToArray();
            }
            catch { }
        }

        // Try DeflateStream at offsets 0..4 (skipping zlib header)
        for (int offset = 0; offset <= 4 && offset < compressed.Length - 4; offset++)
        {
            try
            {
                using var ms = new MemoryStream(compressed, offset, compressed.Length - offset);
                using var deflate = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                deflate.CopyTo(outMs);
                if (outMs.Length > 0) return outMs.ToArray();
            }
            catch { }
        }

        return null;
    }

    private static string CleanPdfString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        // Replace PDF octal escapes like \304\261
        return Regex.Replace(input, @"\\([0-7]{3})", m =>
        {
            try
            {
                byte b = Convert.ToByte(m.Groups[1].Value, 8);
                return ((char)b).ToString();
            }
            catch { return m.Value; }
        });
    }

    private static string DecodeUtf16Be(byte[] bytes)
    {
        try
        {
            char[] chars = new char[bytes.Length];
            int count = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != 0)
                {
                    chars[count++] = (char)bytes[i];
                }
            }
            return new string(chars, 0, count);
        }
        catch { return ""; }
    }

    private static string DecodePdfHexStrings(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder();
        var matches = Regex.Matches(text, @"<([0-9A-Fa-f]{4,})>");
        foreach (Match m in matches)
        {
            string hex = m.Groups[1].Value;
            if (hex.Length % 2 != 0) continue;
            try
            {
                byte[] bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }
                sb.AppendLine(Encoding.UTF8.GetString(bytes));
                sb.AppendLine(DecodeUtf16Be(bytes));
            }
            catch { }
        }
        return sb.ToString();
    }

    private static int FindBytes(byte[] src, byte[] pattern, int startIndex)
    {
        int maxFirst = src.Length - pattern.Length;
        for (int i = startIndex; i <= maxFirst; i++)
        {
            if (src[i] != pattern[0]) continue;
            int j = 1;
            while (j < pattern.Length && src[i + j] == pattern[j]) j++;
            if (j == pattern.Length) return i;
        }
        return -1;
    }

    public string GetDefaultTargetFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string monthYear = _selectedMonth.ToString("MMMM_yyyy", new CultureInfo("tr-TR"));
        return Path.Combine(desktop, $"Banka_Dekontlari_{monthYear}");
    }

    private void OpenDefaultTargetFolder()
    {
        string targetPath = GetDefaultTargetFolder();
        if (!Directory.Exists(targetPath))
        {
            try
            {
                Directory.CreateDirectory(targetPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Masaüstünde hedef klasör oluşturulamadı:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Klasör açılamadı:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (e.CloseReason == CloseReason.UserClosing)
        {
            if (_matchedItems.Count > 0 && !_hasExecutedCopy)
            {
                var result = MessageBox.Show(
                    $"Eşleştirilmiş {_matchedItems.Count} adet dekont henüz düzenlenip hedef klasöre kopyalanmadı!\n\n" +
                    "Düzenleme ve kopyalama işlemini yapmadan çıkmak istediğinize emin misiniz?",
                    "Dekontlar Kopyalanmadı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
