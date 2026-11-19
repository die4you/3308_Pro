using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Common;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class StudentsView : UserControl
{
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly ITeacherRepository _teacherRepo;
    private readonly ISettingRepository? _settingRepo;
    private readonly ILicenseService? _licenseService;

    private List<Student> _allStudents = [];
    private List<SchoolClass> _classes = [];
    private List<Field> _fields = [];
    private List<Enterprise> _enterprises = [];
    private List<Teacher> _teachers = [];
    private readonly HashSet<int> _selectedStudentIds = [];
    private bool _headerCheckboxChecked = false;
    private bool _isLoading = false;
    private bool _isUpdatingFilters = false;

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbFilterClass = null!;
    private ComboBox _cmbFilterGrade = null!;
    private ComboBox _cmbFilterField = null!;
    private ComboBox _cmbFilterStatus = null!;
    private ComboBox _cmbFilterEduType = null!;
    private ComboBox _cmbFilterTc = null!;
    private Label _lblFilterCount = null!;
    private bool _filterMissingTcPending = false;

    // Bulk Assign Bar
    private Label _lblSelectedCountBadge = null!;
    private ComboBox _cmbBulkEnterprise = null!;
    private ComboBox _cmbBulkTeacher = null!;
    private readonly Dictionary<string, CheckBox> _bulkDayCheckBoxes = new();

    public StudentsView(
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        IEnterpriseRepository enterpriseRepo,
        ITeacherRepository teacherRepo,
        ISettingRepository? settingRepo = null,
        ILicenseService? licenseService = null)
    {
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _enterpriseRepo = enterpriseRepo;
        _teacherRepo = teacherRepo;
        _settingRepo = settingRepo;
        _licenseService = licenseService;

        InitializeComponent();
        LoadLookupsAndDataAsync();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Container Panel
        var pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24, 20, 24, 20)
        };

        // Header section
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = "🎓 Öğrenci Yönetimi ve Gün Belirleme",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "İşletmelerde beceri eğitimine gidecek 12. sınıf öğrencileri ve staj günleri",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(2, 32),
            AutoSize = true
        };

        // Action Buttons at top right
        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 460,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnAddStudent = new Button
        {
            Text = "➕ Yeni Öğrenci Ekle",
            Size = new Size(150, 36),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnAddStudent.FlatAppearance.BorderSize = 0;
        btnAddStudent.Click += (s, e) => OpenStudentDialog(null);

        var btnAuditStudents = new Button
        {
            Text = "🔍 Öğrenci Kontrolü",
            Size = new Size(140, 36),
            BackColor = Color.FromArgb(234, 88, 12), // orange-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnAuditStudents.FlatAppearance.BorderSize = 0;
        btnAuditStudents.Click += (s, e) =>
        {
            using var dlg = new StudentAuditDialog(_studentRepo, _classRepo, _fieldRepo);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ChangesCount > 0)
            {
                _ = RefreshStudentsAsync();
            }
        };

        // Context menu for all data import operations
        var menuImport = new ContextMenuStrip
        {
            Font = new Font("Segoe UI", 9f)
        };

        menuImport.Items.Add("📥 e-Okul'dan Sınıf Listesi Aktar (.xls/.xlsx)", null, BtnUploadExcel_Click);
        menuImport.Items.Add("📋 Metin Yapıştırarak Ekle", null, BtnBulkPaste_Click);
        menuImport.Items.Add("🆔 T.C. Kimlik No & Doğum Tarihi Aktar", null, (s, e) =>
        {
            if (!Common.LicenseUIHelper.CheckFeature("e-Okul'dan T.C. Kimlik No Eşleştirme", this)) return;
            using var dlg = new EOkulTcImportDialog(_studentRepo);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ImportedCount > 0)
            {
                _ = RefreshStudentsAsync();
            }
        });
        menuImport.Items.Add("💳 IBAN / Banka Bilgisi Aktar", null, (s, e) =>
        {
            if (!Common.LicenseUIHelper.CheckFeature("Excel'den IBAN Eşleştirme", this)) return;
            using var dlg = new StudentIbanImportDialog(_studentRepo);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ImportedCount > 0)
            {
                _ = RefreshStudentsAsync();
            }
        });
        menuImport.Items.Add(new ToolStripSeparator());
        menuImport.Items.Add("🖨️ Toplu Sınıf Listeleri Yazdır", null, (s, e) =>
        {
            if (_settingRepo == null) return;
            using var dlg = new ClassListPrintDialog(_classRepo, _studentRepo, _settingRepo);
            dlg.ShowDialog(this);
        });
        menuImport.Items.Add("📄 Örnek Excel Şablonu İndir", null, BtnDownloadTemplate_Click);

        var btnImportMenu = new Button
        {
            Text = "📥 Veri İçe Aktar ▾",
            Size = new Size(150, 36),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnImportMenu.FlatAppearance.BorderSize = 0;
        btnImportMenu.Click += (s, e) => menuImport.Show(btnImportMenu, new Point(0, btnImportMenu.Height));

        pnlTopButtons.Controls.AddRange([btnAddStudent, btnAuditStudents, btnImportMenu]);

        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
        {
            btnAddStudent.Visible = false;
            btnAuditStudents.Visible = false;
            btnImportMenu.Visible = false;
        }

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Filter Bar Panel — tek satır (ClassesView stili)
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(16, 10, 16, 10),
            Margin = new Padding(0, 0, 0, 12)
        };
        pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFilter.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        _txtSearch = new TextBox
        {
            PlaceholderText = "🔍 Öğrenci adı, no veya sınıf ara...",
            Location = new Point(16, 12),
            Width = 160,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _cmbFilterClass = new ComboBox
        {
            Location = new Point(184, 12),
            Width = 145,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterClass.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterGrade = new ComboBox
        {
            Location = new Point(337, 12),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterGrade.Items.AddRange(["Tüm Seviyeler", "9. Sınıf", "10. Sınıf", "11. Sınıf", "12. Sınıf"]);
        _cmbFilterGrade.SelectedIndex = 0;
        _cmbFilterGrade.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterField = new ComboBox
        {
            Location = new Point(445, 12),
            Width = 135,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterField.SelectedIndexChanged += (s, e) => ApplyFilter();

        // Öğretim Türü — ClassesView gibi aynı satırda (değerler açılışta sınıflardan otomatik yüklenir)
        _cmbFilterEduType = new ComboBox
        {
            Location = new Point(588, 12),
            Width = 130,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterEduType.Items.Add("Tüm Öğretim Türleri");
        _cmbFilterEduType.SelectedIndex = 0;
        _cmbFilterEduType.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterStatus = new ComboBox
        {
            Location = new Point(726, 12),
            Width = 145,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterStatus.Items.AddRange([
            "Tüm Atama Durumları",
            "Tam Atanmışlar (İşletme + Öğretmen)",
            "İşletmesi Olmayanlar",
            "Öğretmeni Olmayanlar"
        ]);
        _cmbFilterStatus.SelectedIndex = 0;
        _cmbFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterTc = new ComboBox
        {
            Location = new Point(879, 12),
            Width = 135,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterTc.Items.AddRange([
            "Tüm TCKN Durumları",
            "TCKN Tanımlı Olanlar",
            "TCKN Eksik Olanlar"
        ]);
        _cmbFilterTc.SelectedIndex = 0;
        _cmbFilterTc.SelectedIndexChanged += (s, e) => ApplyFilter();

        _lblFilterCount = new Label
        {
            Text = "Toplam 0 kayıt",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(1022, 16)
        };

        pnlFilter.Controls.AddRange([_txtSearch, _cmbFilterClass, _cmbFilterGrade, _cmbFilterField,
                                      _cmbFilterEduType, _cmbFilterStatus, _cmbFilterTc, _lblFilterCount]);

        // Bulk Assign Bar
        var pnlBulk = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.FromArgb(239, 246, 255), // blue-50
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 0, 14)
        };
        pnlBulk.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(191, 219, 254), 1); // blue-200
            e.Graphics.DrawRectangle(pen, 0, 0, pnlBulk.Width - 1, pnlBulk.Height - 1);
        };

        var lblBulkIcon = new Label
        {
            Text = "⚡",
            Font = new Font("Segoe UI", 16f),
            Location = new Point(14, 12),
            Size = new Size(32, 36)
        };

        var lblBulkTitle = new Label
        {
            Text = "Toplu Atama İşlemleri",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(50, 12),
            AutoSize = true
        };

        _lblSelectedCountBadge = new Label
        {
            Text = "0 öğrenci seçildi",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            Location = new Point(190, 12),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2)
        };

        var lblBulkSub = new Label
        {
            Text = "Tablodan öğrenci seçip tek tıkla işletme, koordinatör öğretmen ve staj günü atayın.",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(50, 36),
            AutoSize = true
        };

        // Right side of bulk bar: Enterprise, Teacher, Days, Apply button
        _cmbBulkEnterprise = new ComboBox
        {
            Location = new Point(480, 18),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8.5f)
        };

        _cmbBulkTeacher = new ComboBox
        {
            Location = new Point(648, 18),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 8.5f)
        };
        _cmbBulkTeacher.SelectedIndexChanged += (s, e) =>
        {
            // Öğretmen seçildiğinde öğretmenin görev günlerini otomatik işaretle
            if (_cmbBulkTeacher.SelectedIndex > 0 && _cmbBulkTeacher.SelectedIndex - 1 < _teachers.Count)
            {
                var selectedTeacher = _teachers[_cmbBulkTeacher.SelectedIndex - 1];
                if (!string.IsNullOrWhiteSpace(selectedTeacher.AvailableDays))
                {
                    var teacherDays = selectedTeacher.AvailableDays
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(d => TurkishTextHelper.NormalizeDayName(d.Trim()))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var kv in _bulkDayCheckBoxes)
                    {
                        kv.Value.Checked = teacherDays.Contains(kv.Key);
                    }
                }
            }
        };

        var pnlBulkDays = new FlowLayoutPanel
        {
            Location = new Point(816, 16),
            Size = new Size(240, 32),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(4, 2, 4, 2)
        };

        string[] days = ["Pzt", "Sal", "Çar", "Per", "Cum"];
        string[] fullDays = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma"];
        for (int i = 0; i < days.Length; i++)
        {
            var chk = new CheckBox
            {
                Text = days[i],
                Tag = fullDays[i],
                AutoSize = true,
                Margin = new Padding(0, 2, 4, 0),
                Font = new Font("Segoe UI", 8f)
            };
            _bulkDayCheckBoxes[fullDays[i]] = chk;
            pnlBulkDays.Controls.Add(chk);
        }

        var btnExecuteBulk = new Button
        {
            Text = "✓ Atama Yap",
            Location = new Point(1064, 16),
            Size = new Size(95, 32),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnExecuteBulk.FlatAppearance.BorderSize = 0;
        btnExecuteBulk.Click += BtnExecuteBulk_Click;

        var btnClearSelection = new Button
        {
            Text = "✕ Temizle",
            Location = new Point(1166, 16),
            Size = new Size(85, 32),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(185, 28, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnClearSelection.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnClearSelection.Click += BtnClearAssignments_Click;

        pnlBulk.Controls.AddRange([
            lblBulkIcon, lblBulkTitle, _lblSelectedCountBadge, lblBulkSub,
            _cmbBulkEnterprise, _cmbBulkTeacher, pnlBulkDays, btnExecuteBulk, btnClearSelection
        ]);

        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
        {
            pnlBulk.Visible = false;
        }

        // Grid Panel
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1)
        };
        pnlGridContainer.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlGridContainer.Width - 1, pnlGridContainer.Height - 1);
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 40 }
        };
        UITheme.ApplyModernGridStyle(_grid);

        // ModernGridStyle ReadOnly=true yaptığı için seçim ve buton işlemlerine izin veriyoruz:
        _grid.ReadOnly = false;
        _grid.EditMode = DataGridViewEditMode.EditProgrammatically;

        SetupGridColumns();
        _grid.CellClick += Grid_CellContentClick;
        _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
        _grid.CellPainting += Grid_CellPainting;

        _grid.SortCompare += (s, e) =>
        {
            if (e.Column.Name == "colNumber")
            {
                int n1 = MebKoordinatorluk.Core.Services.FeeCalculator.ParseStudentNumber(e.CellValue1?.ToString());
                int n2 = MebKoordinatorluk.Core.Services.FeeCalculator.ParseStudentNumber(e.CellValue2?.ToString());
                e.SortResult = n1.CompareTo(n2);
                e.Handled = true;
            }
            else if (e.Column.Name == "colIndex")
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
                if (_grid.Columns.Contains("colIndex"))
                    _grid.Rows[i].Cells["colIndex"].Value = i + 1;
            }
        };

        _grid.CellMouseMove += (s, e) =>
        {
            if (e.RowIndex == -1 && e.ColumnIndex >= 0 && _grid.Columns.Contains("colSelect") && e.ColumnIndex == _grid.Columns["colSelect"].Index)
                _grid.Cursor = Cursors.Hand;
            else
                _grid.Cursor = Cursors.Default;
        };

        pnlGridContainer.Controls.Add(_grid);

        // Assemble Top-to-Bottom Flow inside Main
        var pnlContent = new Panel
        {
            Dock = DockStyle.Fill
        };
        pnlContent.Controls.Add(pnlGridContainer);
        pnlContent.Controls.Add(pnlBulk);
        pnlContent.Controls.Add(pnlFilter);

        Controls.Add(pnlContent);
        Controls.Add(pnlHeader);
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();

        var colSelect = new DataGridViewCheckBoxColumn
        {
            Name = "colSelect",
            HeaderText = string.Empty,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 52,
            Resizable = DataGridViewTriState.False,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };
        colSelect.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var colIndex = new DataGridViewTextBoxColumn
        {
            Name = "colIndex",
            HeaderText = "Sıra No",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 58,
            Resizable = DataGridViewTriState.False,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };
        colIndex.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var colNumber = new DataGridViewTextBoxColumn
        {
            Name = "colNumber",
            HeaderText = "Okul No",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 70,
            Resizable = DataGridViewTriState.False,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };
        colNumber.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var colNameClass = new DataGridViewTextBoxColumn
        {
            Name = "colNameClass",
            HeaderText = "Öğrenci Adı Soyadı & Sınıf",
            FillWeight = 22,
            ReadOnly = true
        };

        var colAge = new DataGridViewTextBoxColumn
        {
            Name = "colAge",
            HeaderText = "Yaş",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 46,
            Resizable = DataGridViewTriState.False,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };
        colAge.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var colSgkEntry = new DataGridViewTextBoxColumn
        {
            Name = "colSgkEntry",
            HeaderText = "İşe Giriş T.",
            FillWeight = 11,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };
        colSgkEntry.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var colSgkExit = new DataGridViewTextBoxColumn
        {
            Name = "colSgkExit",
            HeaderText = "İşten Çıkış T.",
            FillWeight = 11,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };
        colSgkExit.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var colDays = new DataGridViewTextBoxColumn
        {
            Name = "colDays",
            HeaderText = "İşletme Günleri",
            FillWeight = 16,
            ReadOnly = true
        };

        var colEnterprise = new DataGridViewTextBoxColumn
        {
            Name = "colEnterprise",
            HeaderText = "Atanan İşletme & Mesafe",
            FillWeight = 20,
            ReadOnly = true
        };

        var colTeacher = new DataGridViewTextBoxColumn
        {
            Name = "colTeacher",
            HeaderText = "Koordinatör Öğretmen & Görev Günleri",
            FillWeight = 20,
            ReadOnly = true
        };

        var colStatus = new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "Durum",
            FillWeight = 12,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colAction = new DataGridViewButtonColumn
        {
            Name = "colAction",
            HeaderText = "İşlemler",
            Text = "✏️ Düzenle",
            UseColumnTextForButtonValue = true,
            FillWeight = 10
        };

        var colArchive = new DataGridViewButtonColumn
        {
            Name = "colArchive",
            HeaderText = "",
            Text = "🗄️ Arşiv",
            UseColumnTextForButtonValue = true,
            FillWeight = 8
        };

        var colDelete = new DataGridViewButtonColumn
        {
            Name = "colDelete",
            HeaderText = "",
            Text = "🗑️ Sil",
            UseColumnTextForButtonValue = true,
            FillWeight = 7
        };

        if (MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
        {
            _grid.Columns.AddRange([colSelect, colIndex, colNumber, colNameClass, colAge, colSgkEntry, colSgkExit, colDays, colEnterprise, colTeacher, colStatus, colAction, colArchive, colDelete]);
        }
        else
        {
            _grid.Columns.AddRange([colIndex, colNumber, colNameClass, colAge, colSgkEntry, colSgkExit, colDays, colEnterprise, colTeacher, colStatus]);
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && !Disposing)
        {
            _ = RefreshStudentsAsync();
        }
    }

    public async Task LoadLookupsAndDataAsync()
    {
        await RefreshStudentsAsync();
    }

    public async Task RefreshLookupsAsync()
    {
        _classes = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();
        _fields = (await _fieldRepo.GetAllAsync()).OrderBy(f => f.Name).ToList();
        _enterprises = (await _enterpriseRepo.GetAllAsync()).OrderBy(e => e.Name).ToList();
        _teachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();

        UpdateFilterDropdowns();
    }

    private void UpdateFilterDropdowns()
    {
        _isUpdatingFilters = true;
        try
        {
            // 1. Sınıf filtresi
            var currentClass = _cmbFilterClass.SelectedItem?.ToString();
            _cmbFilterClass.BeginUpdate();
            _cmbFilterClass.Items.Clear();
            _cmbFilterClass.Items.Add("-- Bir Sınıf Seçiniz --");
            _cmbFilterClass.Items.Add("Tüm Sınıflar");
            foreach (var c in _classes) _cmbFilterClass.Items.Add(c.Name);
            if (_filterMissingTcPending)
            {
                int allIdx = _cmbFilterClass.FindStringExact("Tüm Sınıflar");
                if (allIdx >= 0) _cmbFilterClass.SelectedIndex = allIdx;
                else if (_cmbFilterClass.Items.Count > 1) _cmbFilterClass.SelectedIndex = 1;
                _cmbFilterTc.SelectedIndex = 2; // TCKN Eksik
                _filterMissingTcPending = false;
            }
            else if (!string.IsNullOrWhiteSpace(currentClass) && _cmbFilterClass.Items.Contains(currentClass))
                _cmbFilterClass.SelectedItem = currentClass;
            else
                _cmbFilterClass.SelectedIndex = 0;
            _cmbFilterClass.EndUpdate();

            // 2. Alan filtresi
            var currentField = _cmbFilterField.SelectedItem?.ToString();
            _cmbFilterField.BeginUpdate();
            _cmbFilterField.Items.Clear();
            _cmbFilterField.Items.Add("Tüm Alanlar");
            foreach (var f in _fields) _cmbFilterField.Items.Add(f.Name);
            if (!string.IsNullOrWhiteSpace(currentField) && _cmbFilterField.Items.Contains(currentField))
                _cmbFilterField.SelectedItem = currentField;
            else
                _cmbFilterField.SelectedIndex = 0;
            _cmbFilterField.EndUpdate();

            // 3. Öğretim türü filtresi — sınıflardaki öğretim türlerine göre otomatik güncelle
            var eduTypes = _classes
                .Select(c =>
                {
                    if (!string.IsNullOrWhiteSpace(c.EducationType)) return c.EducationType.Trim();
                    if (!string.IsNullOrWhiteSpace(c.InstitutionType) && c.InstitutionType.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0) return "MESEM";
                    if (c.Name.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0) return "MESEM";
                    return "Örgün Eğitim";
                })
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e.Equals("Örgün Eğitim", StringComparison.OrdinalIgnoreCase) ? 0 :
                              e.Equals("MESEM", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(e => e)
                .ToList();

            if (eduTypes.Count == 0)
            {
                eduTypes = ["Örgün Eğitim", "MESEM"];
            }

            var currentEdu = _cmbFilterEduType.SelectedItem?.ToString();
            _cmbFilterEduType.BeginUpdate();
            _cmbFilterEduType.Items.Clear();
            _cmbFilterEduType.Items.Add("Tüm Öğretim Türleri");
            foreach (var edu in eduTypes) _cmbFilterEduType.Items.Add(edu);
            if (!string.IsNullOrWhiteSpace(currentEdu) && _cmbFilterEduType.Items.Contains(currentEdu))
                _cmbFilterEduType.SelectedItem = currentEdu;
            else
                _cmbFilterEduType.SelectedIndex = 0;
            _cmbFilterEduType.EndUpdate();

            // 4. Toplu Atama İşletme & Öğretmen
            var currentEnt = _cmbBulkEnterprise.SelectedItem?.ToString();
            _cmbBulkEnterprise.BeginUpdate();
            _cmbBulkEnterprise.Items.Clear();
            _cmbBulkEnterprise.Items.Add("-- Atanacak İşletme Seçin --");
            foreach (var ent in _enterprises) _cmbBulkEnterprise.Items.Add(ent.Name);
            if (!string.IsNullOrWhiteSpace(currentEnt) && _cmbBulkEnterprise.Items.Contains(currentEnt))
                _cmbBulkEnterprise.SelectedItem = currentEnt;
            else
                _cmbBulkEnterprise.SelectedIndex = 0;
            _cmbBulkEnterprise.EndUpdate();

            var currentTea = _cmbBulkTeacher.SelectedItem?.ToString();
            _cmbBulkTeacher.BeginUpdate();
            _cmbBulkTeacher.Items.Clear();
            _cmbBulkTeacher.Items.Add("-- Koordinatör Öğretmen Seçin --");
            foreach (var t in _teachers) _cmbBulkTeacher.Items.Add(t.FullName);
            if (!string.IsNullOrWhiteSpace(currentTea) && _cmbBulkTeacher.Items.Contains(currentTea))
                _cmbBulkTeacher.SelectedItem = currentTea;
            else
                _cmbBulkTeacher.SelectedIndex = 0;
            _cmbBulkTeacher.EndUpdate();
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    public async Task RefreshStudentsAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            await RefreshLookupsAsync();
            _allStudents = (await _studentRepo.GetAllAsync())
                .Where(s => s.IsActive && !s.StudentNumber.Contains("-P", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Classroom)
                .ThenBy(s => MebKoordinatorluk.Core.Services.FeeCalculator.ParseStudentNumber(s.StudentNumber))
                .ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshStudentsAsync error: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (_isUpdatingFilters) return;

        var search = _txtSearch.Text.Trim();
        bool hasSearch = !string.IsNullOrWhiteSpace(search);

        // Arama yapılmıyorken: "Bir Sınıf Seçiniz" (index <= 0) seçili iken öğrenciler listelenmesin
        if (!hasSearch && _cmbFilterClass.SelectedIndex <= 0)
        {
            _grid.Rows.Clear();
            _lblFilterCount.Text = "Toplam 0 kayıt";
            _lblSelectedCountBadge.Text = "0 öğrenci seçildi";
            UpdateHeaderCheckboxState();
            return;
        }

        bool Matches(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;
            return val.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   val.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        var selectedClassStr = _cmbFilterClass.SelectedItem?.ToString();
        bool isAllClasses = string.Equals(selectedClassStr, "Tüm Sınıflar", StringComparison.OrdinalIgnoreCase);
        var selectedClass = isAllClasses ? null : selectedClassStr;

        var selectedGradeText = _cmbFilterGrade.SelectedIndex > 0 ? _cmbFilterGrade.SelectedItem?.ToString() : null;
        string? selectedGrade = null;
        if (!string.IsNullOrWhiteSpace(selectedGradeText))
        {
            var m = System.Text.RegularExpressions.Regex.Match(selectedGradeText, @"\d+");
            if (m.Success) selectedGrade = m.Value;
        }

        var selectedField = _cmbFilterField.SelectedIndex > 0 ? _cmbFilterField.SelectedItem?.ToString() : null;
        var statusIdx = _cmbFilterStatus.SelectedIndex;
        var tcIdx = _cmbFilterTc.SelectedIndex;

        // Öğretim türü filtresi: index 0 = "Tüm Öğretim Türleri"
        var selectedEduType = _cmbFilterEduType.SelectedIndex > 0 ? _cmbFilterEduType.SelectedItem?.ToString() : null;

        var filtered = _allStudents.Where(s =>
        {
            // Arama kutusuna bir şey yazıldıysa: Sınıf ve filtre kısıtlamalarına takılmadan tüm okulda arar
            if (hasSearch)
            {
                return Matches(s.FullName) ||
                       Matches(s.StudentNumber) ||
                       Matches(s.TcKimlikNo) ||
                       Matches(s.Classroom) ||
                       Matches(s.FieldName) ||
                       Matches(s.EnterpriseName) ||
                       Matches(s.TeacherName) ||
                       Matches(s.Iban);
            }

            if (!isAllClasses && !string.IsNullOrWhiteSpace(selectedClass) && !s.Classroom.Equals(selectedClass, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(selectedGrade))
            {
                bool matchGrade = string.Equals(s.Grade, selectedGrade, StringComparison.OrdinalIgnoreCase) ||
                                  s.Classroom.StartsWith(selectedGrade + "-", StringComparison.OrdinalIgnoreCase) ||
                                  s.Classroom.StartsWith(selectedGrade + "/", StringComparison.OrdinalIgnoreCase) ||
                                  s.Classroom.Contains(selectedGrade + ". Sınıf", StringComparison.OrdinalIgnoreCase) ||
                                  System.Text.RegularExpressions.Regex.IsMatch(s.Classroom, $@"\b{selectedGrade}\b");
                if (!matchGrade) return false;
            }

            if (!string.IsNullOrWhiteSpace(selectedField) && !string.Equals(s.FieldName, selectedField, StringComparison.OrdinalIgnoreCase))
                return false;

            // Öğretim türü filtresi — öğrencinin kendi türü veya sınıfının türü ile eşleştir
            if (!string.IsNullOrWhiteSpace(selectedEduType))
            {
                var cls = _classes.FirstOrDefault(c => (c.Id > 0 && c.Id == s.ClassId) || string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
                var studentEduType = cls != null && !string.IsNullOrWhiteSpace(cls.EducationType)
                    ? cls.EducationType.Trim()
                    : (!string.IsNullOrWhiteSpace(s.EducationType) ? s.EducationType.Trim() : (MebKoordinatorluk.Core.Services.FeeCalculator.IsMesem(s, cls) ? "MESEM" : "Örgün Eğitim"));

                if (selectedEduType == "Diğer")
                {
                    var known = new[] { "Örgün Eğitim", "İkinci Öğretim", "MESEM" };
                    if (known.Any(k => string.Equals(studentEduType, k, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(s.EducationType, k, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }
                else
                {
                    bool match = string.Equals(studentEduType, selectedEduType, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(s.EducationType, selectedEduType, StringComparison.OrdinalIgnoreCase);
                    if (!match) return false;
                }
            }

            if (statusIdx == 1) // Tam Atanmışlar
                return s.EnterpriseId.HasValue && s.TeacherId.HasValue;
            if (statusIdx == 2) // İşletmesi Olmayanlar
                return !s.EnterpriseId.HasValue;
            if (statusIdx == 3) // Öğretmeni Olmayanlar
                return !s.TeacherId.HasValue;

            // TCKN Durumu filtresi
            if (tcIdx == 1) // TCKN Tanımlı Olanlar
            {
                if (string.IsNullOrWhiteSpace(s.TcKimlikNo) || s.TcKimlikNo.Trim().Length != 11)
                    return false;
            }
            else if (tcIdx == 2) // TCKN Eksik Olanlar
            {
                if (!string.IsNullOrWhiteSpace(s.TcKimlikNo) && s.TcKimlikNo.Trim().Length == 11)
                    return false;
            }

            return true;
        })
        .OrderBy(s => s.Classroom)
        .ThenBy(s => MebKoordinatorluk.Core.Services.FeeCalculator.ParseStudentNumber(s.StudentNumber))
        .ToList();

        _lblFilterCount.Text = hasSearch ? $"Arama sonucu: {filtered.Count} kayıt" : $"Toplam {filtered.Count} kayıt";

        _grid.Rows.Clear();
        int idx = 1;
        foreach (var s in filtered)
        {
            var isSelected = _selectedStudentIds.Contains(s.Id);
            var entText = s.EnterpriseName ?? "-";
            var teacherText = s.TeacherName ?? "-";
            if (!string.IsNullOrWhiteSpace(s.DutyDays))
            {
                teacherText += $" ({s.DutyDays})";
            }

            string statusText;
            Color statusColor;
            if (s.EnterpriseId.HasValue && s.TeacherId.HasValue)
            {
                statusText = "✓ Tamamlandı";
                statusColor = Color.FromArgb(22, 101, 52); // green
            }
            else if (!s.EnterpriseId.HasValue && !s.TeacherId.HasValue)
            {
                statusText = "❌ Atama Yok";
                statusColor = Color.FromArgb(185, 28, 28); // red
            }
            else if (!s.EnterpriseId.HasValue)
            {
                statusText = "⚠️ İşletme Yok";
                statusColor = Color.FromArgb(180, 83, 9); // amber
            }
            else
            {
                statusText = "⚠️ Öğretmen Yok";
                statusColor = Color.FromArgb(180, 83, 9); // amber
            }

            string ageText = s.Age?.ToString() ?? "-";
            string entryDateText = s.SgkEntryDate?.ToString("dd.MM.yyyy") ?? "-";
            string exitDateText = s.SgkExitDate?.ToString("dd.MM.yyyy") ?? "-";

            string displayNameClass = $"{s.FullName} ({s.Classroom} - {s.FieldName})";
            bool isTcMissing = string.IsNullOrWhiteSpace(s.TcKimlikNo) || s.TcKimlikNo.Trim().Length != 11;
            if (isTcMissing)
            {
                displayNameClass += " ⚠️ [TCKN Eksik]";
            }

            var rowIndex = _grid.Rows.Add(
                isSelected,
                idx++,
                s.StudentNumber,
                displayNameClass,
                ageText,
                entryDateText,
                exitDateText,
                s.InternshipDays,
                entText,
                teacherText,
                statusText
            );

            _grid.Rows[rowIndex].Tag = s;
            _grid.Rows[rowIndex].Cells["colStatus"].Style.ForeColor = statusColor;
            _grid.Rows[rowIndex].Cells["colStatus"].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            var tooltipParts = new List<string>();
            if (isTcMissing)
            {
                tooltipParts.Add("⚠️ T.C. Kimlik Numarası eksik veya 11 hane değil!");
            }
            else
            {
                tooltipParts.Add($"🆔 TCKN: {s.TcKimlikNo}");
            }
            if (!string.IsNullOrWhiteSpace(s.Iban))
            {
                tooltipParts.Add($"🏦 Öğrenci IBAN: {s.Iban}");
            }
            _grid.Rows[rowIndex].Cells["colNameClass"].ToolTipText = string.Join(Environment.NewLine, tooltipParts);
        }

        UpdateHeaderCheckboxState();
    }

    public void FilterByMissingTc()
    {
        _filterMissingTcPending = true;

        int allIdx = _cmbFilterClass.FindStringExact("Tüm Sınıflar");
        if (allIdx >= 0)
        {
            _cmbFilterClass.SelectedIndex = allIdx;
        }
        else if (_cmbFilterClass.Items.Count > 1)
        {
            _cmbFilterClass.SelectedIndex = 1;
        }

        if (_cmbFilterTc != null && _cmbFilterTc.Items.Count > 2)
        {
            _cmbFilterTc.SelectedIndex = 2; // TCKN Eksik Olanlar
        }
        if (_cmbFilterGrade != null) _cmbFilterGrade.SelectedIndex = 0;
        if (_cmbFilterField != null) _cmbFilterField.SelectedIndex = 0;
        if (_cmbFilterEduType != null) _cmbFilterEduType.SelectedIndex = 0;
        if (_cmbFilterStatus != null) _cmbFilterStatus.SelectedIndex = 0;
        if (_txtSearch != null) _txtSearch.Text = string.Empty;

        ApplyFilter();
    }

    private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex >= 0 && _grid.Columns.Contains("colSelect") && e.ColumnIndex == _grid.Columns["colSelect"].Index)
        {
            ToggleAllFilteredSelection();
        }
    }

    private void ToggleAllFilteredSelection()
    {
        if (_grid.Rows.Count == 0) return;

        _headerCheckboxChecked = !_headerCheckboxChecked;
        foreach (DataGridViewRow r in _grid.Rows)
        {
            r.Cells["colSelect"].Value = _headerCheckboxChecked;
            if (r.Tag is Student st)
            {
                if (_headerCheckboxChecked)
                    _selectedStudentIds.Add(st.Id);
                else
                    _selectedStudentIds.Remove(st.Id);
            }
        }
        _lblSelectedCountBadge.Text = $"{_selectedStudentIds.Count} öğrenci seçildi";
        _grid.Refresh();
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not Student s) return;

        // CheckBox cell: Tek tıklamada anında seçim ve başlık senkronizasyonu
        if (_grid.Columns.Contains("colSelect") && e.ColumnIndex == _grid.Columns["colSelect"].Index)
        {
            bool curVal = Convert.ToBoolean(row.Cells["colSelect"].Value);
            bool newVal = !curVal;
            row.Cells["colSelect"].Value = newVal;

            if (newVal)
                _selectedStudentIds.Add(s.Id);
            else
                _selectedStudentIds.Remove(s.Id);

            _lblSelectedCountBadge.Text = $"{_selectedStudentIds.Count} öğrenci seçildi";
            _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
            UpdateHeaderCheckboxState();
        }
        else if (_grid.Columns.Contains("colAction") && e.ColumnIndex == _grid.Columns["colAction"].Index)
        {
            if (MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
                OpenStudentDialog(s);
        }
        else if (_grid.Columns.Contains("colArchive") && e.ColumnIndex == _grid.Columns["colArchive"].Index)
        {
            if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin) return;
            var result = MessageBox.Show(
                $"{s.FullName} ({s.StudentNumber}) isimli öğrenciyi Öğrenci Arşivi'ne taşımak istiyor musunuz?\n\n" +
                $"• Öğrenci numarasının sonuna '-P-{DateTime.Now.Year}' eklenecektir.\n" +
                "• Orijinal numara yeni öğrenciler için boşa çıkacaktır.\n" +
                "• Öğrenci tüm aktif listelerden kaldırılacaktır.",
                "Öğrenciyi Arşive Taşı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _ = ArchiveStudentAsync(s);
            }
        }
        else if (_grid.Columns.Contains("colDelete") && e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin) return;
            var result = MessageBox.Show(
                $"{s.FullName} isimli öğrenci kaydını silmek istediğinize emin misiniz?",
                "Öğrenci Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _ = DeleteStudentAsync(s.Id);
            }
        }
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex == -1 && e.ColumnIndex >= 0 && _grid.Columns.Contains("colSelect") && e.ColumnIndex == _grid.Columns["colSelect"].Index)
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

    private void UpdateHeaderCheckboxState()
    {
        if (_grid.Rows.Count == 0)
        {
            _headerCheckboxChecked = false;
        }
        else
        {
            bool allChecked = true;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (r.Tag is Student st && !_selectedStudentIds.Contains(st.Id))
                {
                    allChecked = false;
                    break;
                }
            }
            _headerCheckboxChecked = allChecked;
        }

        if (_grid.Columns.Contains("colSelect"))
        {
            _grid.InvalidateCell(_grid.Columns["colSelect"].Index, -1);
        }
    }

    private async Task DeleteStudentAsync(int id)
    {
        await _studentRepo.DeleteAsync(id);
        _selectedStudentIds.Remove(id);
        await RefreshStudentsAsync();
    }

    private async Task ArchiveStudentAsync(Student s)
    {
        try
        {
            if (!s.StudentNumber.Contains("-P", StringComparison.OrdinalIgnoreCase))
            {
                s.StudentNumber = $"{s.StudentNumber.Trim()}-P-{DateTime.Now.Year}";
            }
            s.IsActive = false;
            await _studentRepo.UpdateAsync(s);
            _selectedStudentIds.Remove(s.Id);
            await RefreshStudentsAsync();
            MessageBox.Show(
                $"{s.FullName} başarıyla Öğrenci Arşivi'ne taşındı (Arşiv No: {s.StudentNumber}).\n\n" +
                "Numarası boşa çıkarıldı ve aktif listelerden kaldırıldı.\n" +
                "Sol menüdeki '🗄️ Öğrenci Arşivi' sekmesinden görüntüleyebilirsiniz.",
                "Arşive Taşındı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Arşive taşınırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenStudentDialog(Student? existing)
    {
        if (existing == null && _licenseService != null && !_licenseService.CanAddStudent(_allStudents.Count))
        {
            var res = MessageBox.Show(
                "3308 Pro Deneme Sürümü Kısıtlaması:\n\nDeneme sürümünde en fazla 5 öğrenci kaydedilebilir.\nSınırsız öğrenci kaydı ve tüm özelliklerin kilidini açmak için lütfen lisansınızı etkinleştiriniz.\n\nLisans aktivasyon penceresini açmak ister misiniz?",
                "Demo Sürüm Kısıtlaması (3308 Pro)",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                using var licDlg = new LicenseActivationDialog(_licenseService);
                licDlg.ShowDialog(this);
            }
            return;
        }

        using var dlg = new StudentEditDialog(_classRepo, _fieldRepo, _enterpriseRepo, _teacherRepo, _studentRepo, existing);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _ = SaveStudentAsync(dlg.StudentResult);
        }
    }

    private async Task SaveStudentAsync(Student student)
    {
        if (student.Id == 0)
            await _studentRepo.AddAsync(student);
        else
            await _studentRepo.UpdateAsync(student);

        await RefreshStudentsAsync();
    }

    private async void BtnExecuteBulk_Click(object? sender, EventArgs e)
    {
        if (_selectedStudentIds.Count == 0)
        {
            MessageBox.Show("Lütfen en az bir öğrenci seçiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int? entId = null;
        string? entName = null;
        if (_cmbBulkEnterprise.SelectedIndex > 0 && _cmbBulkEnterprise.SelectedIndex - 1 < _enterprises.Count)
        {
            var ent = _enterprises[_cmbBulkEnterprise.SelectedIndex - 1];
            entId = ent.Id;
            entName = ent.Name;
        }

        int? teachId = null;
        string? teachName = null;
        if (_cmbBulkTeacher.SelectedIndex > 0 && _cmbBulkTeacher.SelectedIndex - 1 < _teachers.Count)
        {
            var t = _teachers[_cmbBulkTeacher.SelectedIndex - 1];
            teachId = t.Id;
            teachName = t.FullName;
        }

        var selectedDays = _bulkDayCheckBoxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        var daysStr = selectedDays.Count > 0 ? string.Join(",", selectedDays) : null;

        if (entId == null && teachId == null && daysStr == null)
        {
            MessageBox.Show("Lütfen toplu atanacak bir işletme, öğretmen veya gün seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (var id in _selectedStudentIds)
        {
            var student = await _studentRepo.GetByIdAsync(id);
            if (student != null)
            {
                if (entId.HasValue)
                {
                    student.EnterpriseId = entId.Value;
                    student.EnterpriseName = entName;
                }
                if (teachId.HasValue)
                {
                    student.TeacherId = teachId.Value;
                    student.TeacherName = teachName;
                }
                if (!string.IsNullOrWhiteSpace(daysStr))
                {
                    student.DutyDays = daysStr;
                }
                await _studentRepo.UpdateAsync(student);
            }
        }

        MessageBox.Show($"{_selectedStudentIds.Count} öğrenci için toplu atama başarıyla tamamlandı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _selectedStudentIds.Clear();
        _lblSelectedCountBadge.Text = "0 öğrenci seçildi";
        await RefreshStudentsAsync();
    }

    private async void BtnClearAssignments_Click(object? sender, EventArgs e)
    {
        if (_selectedStudentIds.Count == 0)
        {
            MessageBox.Show("Lütfen koordinatör öğretmen ve işletme atamaları temizlenecek en az bir öğrenci seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Seçili {_selectedStudentIds.Count} öğrencideki koordinatör öğretmen ve işletme atamaları temizlenecektir.\n\nOnaylıyor musunuz?",
            "Atamaları Temizle",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            int clearedCount = 0;
            foreach (var id in _selectedStudentIds.ToList())
            {
                var student = await _studentRepo.GetByIdAsync(id);
                if (student != null)
                {
                    student.EnterpriseId = null;
                    student.EnterpriseName = null;
                    student.TeacherId = null;
                    student.TeacherName = null;
                    student.DutyDays = string.Empty;
                    await _studentRepo.UpdateAsync(student);
                    clearedCount++;
                }
            }

            _selectedStudentIds.Clear();
            _lblSelectedCountBadge.Text = "0 öğrenci seçildi";
            UpdateHeaderCheckboxState();
            await RefreshStudentsAsync();

            MessageBox.Show($"{clearedCount} öğrencinin koordinatör öğretmen ve işletme atamaları başarıyla temizlendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnBulkPaste_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("Toplu Öğrenci Listesi Yapıştırma (e-Okul / Excel)", this))
            return;

        using var dlg = new BulkStudentsDialog(_studentRepo, _classRepo, _fieldRepo);
        if (dlg.ShowDialog() == DialogResult.OK && dlg.ImportedCount > 0)
        {
            _ = RefreshStudentsAsync();
        }
    }

    private async Task SaveBulkStudentsAsync(List<Student> students)
    {
        int count = 0;
        foreach (var s in students)
        {
            await _studentRepo.AddAsync(s);
            count++;
        }
        MessageBox.Show($"{count} öğrenci başarıyla içe aktarıldı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshStudentsAsync();
    }
    private async void BtnUploadExcel_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("e-Okul Sınıf Listesinden Toplu Öğrenci Aktarma", this))
            return;

        using var dlg = new Dialogs.EOkulImportDialog(_studentRepo, _classRepo, _fieldRepo);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await LoadLookupsAndDataAsync();
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = "Ornek_Ogrenciler.csv"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var content = "TC Kimlik No,IBAN,Okul No,Adı,Soyadı,Sınıf,Sınıf Seviyesi,Alanı / Dalı,Öğretim Türü,Cinsiyet,Doğum Tarihi,Staj İşletmesi,Koordinatör Öğretmen,Staj Günleri,Öğretmen Görev Günü,Devlet Katkı Kategorisi,Eğitim Yeri,Aile Sağlık Güvencesi,SGK Prim Kodu,SGK İşe Giriş Tarihi,SGK İşten Çıkış Tarihi,Öğrenci Telefon,Veli Telefon,Notlar\n" +
                          "12345678901,TR120006200000012345678901,101,Ali,YILMAZ,12-A,12,Bilişim Teknolojileri,Örgün Eğitim,Erkek,15.05.2007,TeknoSoft Bilişim A.Ş.,Ahmet YILDIZ,Pazartesi;Salı;Çarşamba,Salı,%30,İşletmede (Staj),Var,49 Nolu Belge Türü,15.09.2025,,05551112233,05559998877,Staj sözleşmesi teslim edildi\n" +
                          "98765432109,TR340001500000098765432109,102,Ayşe,DEMİR,12-A,12,Bilişim Teknolojileri,Örgün Eğitim,Kız,20.08.2007,TeknoSoft Bilişim A.Ş.,Ahmet YILDIZ,Pazartesi;Salı;Çarşamba,Salı,%30,İşletmede (Staj),Var,49 Nolu Belge Türü,15.09.2025,,05552223344,05558887766,\n";
            File.WriteAllText(sfd.FileName, content, Encoding.UTF8);
            MessageBox.Show("Örnek şablon başarıyla kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
