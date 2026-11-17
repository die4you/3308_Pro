using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class DistributionView : UserControl
{
    private readonly IStudentRepository _studentRepo;
    private readonly ITeacherRepository _teacherRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly IAddressGroupRepository _groupRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly ISchoolClassRepository _classRepo;

    private List<Student> _allStudents = [];
    private List<Teacher> _allTeachers = [];
    private List<Enterprise> _allEnterprises = [];
    private List<AddressGroup> _allGroups = [];
    private List<SchoolClass> _allClasses = [];

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbFilterIssue = null!;

    // Örgün Eğitim Grubu Checkbox'ları
    private CheckBox _chkOrg9 = null!;
    private CheckBox _chkOrg10 = null!;
    private CheckBox _chkOrg11 = null!;
    private CheckBox _chkOrg12 = null!;

    // MESEM Grubu Checkbox'ları
    private CheckBox _chkMesemAll = null!;
    private CheckBox _chkMesem9 = null!;
    private CheckBox _chkMesem10 = null!;
    private CheckBox _chkMesem11 = null!;
    private CheckBox _chkMesem12 = null!;

    // Rozet & Seçili Öğrenciler
    private Label _lblScopeBadge = null!;
    private List<Student> _targetStudents = [];
    private bool _isSyncingMesem = false;

    public DistributionView(
        IStudentRepository studentRepo,
        ITeacherRepository teacherRepo,
        IEnterpriseRepository enterpriseRepo,
        IAddressGroupRepository groupRepo,
        ISettingRepository settingRepo,
        ISchoolClassRepository classRepo)
    {
        _studentRepo = studentRepo;
        _teacherRepo = teacherRepo;
        _enterpriseRepo = enterpriseRepo;
        _groupRepo = groupRepo;
        _settingRepo = settingRepo;
        _classRepo = classRepo;

        InitializeComponent();
        using var _ = LoadDataAsync();
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
            Text = "🔄 Akıllı Koordinatörlük Dağıtımı & Eşleştirme",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Öğretmen görev günleri ile öğrenci staj günlerini eşleştirin, çakışmaları anında tespit edin",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        // Header Actions
        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 460,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnAutoDistribute = new Button
        {
            Text = "⚡ Akıllı Otomatik Dağıtımı Çalıştır",
            Size = new Size(240, 36),
            BackColor = Color.FromArgb(5, 150, 105), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnAutoDistribute.FlatAppearance.BorderSize = 0;
        btnAutoDistribute.Click += BtnAutoDistribute_Click;

        var btnClearAll = new Button
        {
            Text = "Atamaları Sıfırla",
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnClearAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnClearAll.Click += BtnClearAll_Click;

        pnlTopButtons.Controls.AddRange([btnAutoDistribute, btnClearAll]);
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Grup & Kademe Filtre Paneli (Örgün Eğitim ve MESEM Checkbox Grupları)
        var pnlGroupFilter = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 8, 24, 6),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };
        pnlGroupFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlGroupFilter.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlGroupFilter.Height - 1, pnlGroupFilter.Width, pnlGroupFilter.Height - 1);
        };

        var lblOrgGroup = new Label
        {
            Text = "🏫 Örgün Eğitim:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Margin = new Padding(0, 7, 4, 0)
        };

        _chkOrg9 = new CheckBox { Text = "9. Sınıf", AutoSize = true, Margin = new Padding(2, 6, 4, 0), Font = new Font("Segoe UI", 8.5f) };
        _chkOrg10 = new CheckBox { Text = "10. Sınıf", AutoSize = true, Margin = new Padding(2, 6, 4, 0), Font = new Font("Segoe UI", 8.5f) };
        _chkOrg11 = new CheckBox { Text = "11. Sınıf", AutoSize = true, Margin = new Padding(2, 6, 4, 0), Font = new Font("Segoe UI", 8.5f) };
        _chkOrg12 = new CheckBox { Text = "12. Sınıf", AutoSize = true, Checked = true, Margin = new Padding(2, 6, 12, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(29, 78, 216) };

        var lblSep1 = new Label
        {
            Text = "|",
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            Font = new Font("Segoe UI", 11f),
            Margin = new Padding(4, 5, 8, 0)
        };

        var lblMesemGroup = new Label
        {
            Text = "🏭 MESEM:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Margin = new Padding(4, 7, 4, 0)
        };

        _chkMesemAll = new CheckBox { Text = "Tüm MESEM", AutoSize = true, Checked = true, Margin = new Padding(2, 6, 4, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(5, 150, 105) };
        _chkMesem9 = new CheckBox { Text = "9. Sınıf", AutoSize = true, Checked = true, Margin = new Padding(2, 6, 4, 0), Font = new Font("Segoe UI", 8.5f) };
        _chkMesem10 = new CheckBox { Text = "10. Sınıf", AutoSize = true, Checked = true, Margin = new Padding(2, 6, 4, 0), Font = new Font("Segoe UI", 8.5f) };
        _chkMesem11 = new CheckBox { Text = "11. Sınıf", AutoSize = true, Checked = true, Margin = new Padding(2, 6, 4, 0), Font = new Font("Segoe UI", 8.5f) };
        _chkMesem12 = new CheckBox { Text = "12. Sınıf", AutoSize = true, Checked = true, Margin = new Padding(2, 6, 12, 0), Font = new Font("Segoe UI", 8.5f) };

        var lblSep2 = new Label
        {
            Text = "|",
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            Font = new Font("Segoe UI", 11f),
            Margin = new Padding(4, 5, 8, 0)
        };

        _lblScopeBadge = new Label
        {
            Text = "👥 0 Öğrenci (Ortak Havuz)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            AutoSize = true,
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(4, 4, 8, 0)
        };

        var btnQuickDefault = new Button
        {
            Text = "Varsayılan (12+MESEM)",
            Size = new Size(140, 26),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f),
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 4, 2, 0)
        };
        btnQuickDefault.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        var btnQuickAll = new Button
        {
            Text = "Tümünü Seç",
            Size = new Size(80, 26),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f),
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 4, 2, 0)
        };
        btnQuickAll.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        var btnQuickClear = new Button
        {
            Text = "✕ Temizle",
            Size = new Size(68, 26),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(100, 116, 139),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f),
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 4, 2, 0)
        };
        btnQuickClear.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        _chkMesemAll.CheckedChanged += (s, e) =>
        {
            if (_isSyncingMesem) return;
            _isSyncingMesem = true;
            bool isChecked = _chkMesemAll.Checked;
            _chkMesem9.Checked = isChecked;
            _chkMesem10.Checked = isChecked;
            _chkMesem11.Checked = isChecked;
            _chkMesem12.Checked = isChecked;
            _isSyncingMesem = false;
            ApplyFilter();
        };

        void OnMesemGradeChanged()
        {
            if (_isSyncingMesem) return;
            _isSyncingMesem = true;
            _chkMesemAll.Checked = _chkMesem9.Checked && _chkMesem10.Checked && _chkMesem11.Checked && _chkMesem12.Checked;
            _isSyncingMesem = false;
            ApplyFilter();
        }

        _chkMesem9.CheckedChanged += (s, e) => OnMesemGradeChanged();
        _chkMesem10.CheckedChanged += (s, e) => OnMesemGradeChanged();
        _chkMesem11.CheckedChanged += (s, e) => OnMesemGradeChanged();
        _chkMesem12.CheckedChanged += (s, e) => OnMesemGradeChanged();

        _chkOrg9.CheckedChanged += (s, e) => ApplyFilter();
        _chkOrg10.CheckedChanged += (s, e) => ApplyFilter();
        _chkOrg11.CheckedChanged += (s, e) => ApplyFilter();
        _chkOrg12.CheckedChanged += (s, e) => ApplyFilter();

        btnQuickDefault.Click += (s, e) =>
        {
            _isSyncingMesem = true;
            _chkOrg9.Checked = false;
            _chkOrg10.Checked = false;
            _chkOrg11.Checked = false;
            _chkOrg12.Checked = true;
            _chkMesemAll.Checked = true;
            _chkMesem9.Checked = true;
            _chkMesem10.Checked = true;
            _chkMesem11.Checked = true;
            _chkMesem12.Checked = true;
            _isSyncingMesem = false;
            ApplyFilter();
        };

        btnQuickAll.Click += (s, e) =>
        {
            _isSyncingMesem = true;
            _chkOrg9.Checked = true;
            _chkOrg10.Checked = true;
            _chkOrg11.Checked = true;
            _chkOrg12.Checked = true;
            _chkMesemAll.Checked = true;
            _chkMesem9.Checked = true;
            _chkMesem10.Checked = true;
            _chkMesem11.Checked = true;
            _chkMesem12.Checked = true;
            _isSyncingMesem = false;
            ApplyFilter();
        };

        btnQuickClear.Click += (s, e) =>
        {
            _isSyncingMesem = true;
            _chkOrg9.Checked = false;
            _chkOrg10.Checked = false;
            _chkOrg11.Checked = false;
            _chkOrg12.Checked = false;
            _chkMesemAll.Checked = false;
            _chkMesem9.Checked = false;
            _chkMesem10.Checked = false;
            _chkMesem11.Checked = false;
            _chkMesem12.Checked = false;
            _isSyncingMesem = false;
            ApplyFilter();
        };

        pnlGroupFilter.Controls.AddRange([
            lblOrgGroup, _chkOrg9, _chkOrg10, _chkOrg11, _chkOrg12,
            lblSep1,
            lblMesemGroup, _chkMesemAll, _chkMesem9, _chkMesem10, _chkMesem11, _chkMesem12,
            lblSep2,
            _lblScopeBadge, btnQuickDefault, btnQuickAll, btnQuickClear
        ]);

        // Filter Bar Panel
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFilter.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        _txtSearch = new TextBox
        {
            PlaceholderText = "🔍 Öğrenci, işletme veya öğretmen ara...",
            Location = new Point(24, 12),
            Width = 260,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _cmbFilterIssue = new ComboBox
        {
            Location = new Point(294, 12),
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterIssue.Items.AddRange([
            "Tüm Kayıtlar",
            "❌ Sadece Gün Çakışması Olanlar",
            "⚠️ Sadece Uyarı / Eksik Olanlar",
            "✓ Sadece Tam Uyumlu Olanlar",
            "Atama Yapılmamış Öğrenciler"
        ]);
        _cmbFilterIssue.SelectedIndex = 0;
        _cmbFilterIssue.SelectedIndexChanged += (s, e) => ApplyFilter();

        var lblLegendOk = new Label
        {
            Text = "✓ Uyumlu: Gün & Branş Örtüşüyor",
            ForeColor = Color.FromArgb(22, 101, 52),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(580, 16)
        };

        var lblLegendError = new Label
        {
            Text = "❌ Çakışma: Gün Uyuşmazlığı",
            ForeColor = Color.FromArgb(185, 28, 28),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(800, 16)
        };

        pnlFilter.Controls.AddRange([_txtSearch, _cmbFilterIssue, lblLegendOk, lblLegendError]);

        // Grid Container
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 16, 24, 16)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 42 }
        };
        UITheme.ApplyModernGridStyle(_grid);

        SetupGridColumns();
        _grid.CellContentClick += Grid_CellContentClick;

        pnlGrid.Controls.Add(_grid);

        Controls.Add(pnlGrid);
        Controls.Add(pnlFilter);
        Controls.Add(pnlGroupFilter);
        Controls.Add(pnlHeader);
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();

        var colIdx = new DataGridViewTextBoxColumn
        {
            Name = "colIdx",
            HeaderText = "#",
            Width = 40,
            FillWeight = 4,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colStudent = new DataGridViewTextBoxColumn
        {
            Name = "colStudent",
            HeaderText = "Öğrenci Bilgisi & Günleri",
            FillWeight = 28,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colEnterprise = new DataGridViewTextBoxColumn
        {
            Name = "colEnterprise",
            HeaderText = "Atanan İşletme",
            FillWeight = 26
        };

        var colTeacher = new DataGridViewTextBoxColumn
        {
            Name = "colTeacher",
            HeaderText = "Koordinatör Öğretmen & Günleri",
            FillWeight = 26
        };

        var colCompatibility = new DataGridViewTextBoxColumn
        {
            Name = "colCompatibility",
            HeaderText = "Uyumluluk",
            FillWeight = 16,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colClear = new DataGridViewButtonColumn
        {
            Name = "colClear",
            HeaderText = "Temizle",
            Text = "✕ Kaldır",
            UseColumnTextForButtonValue = true,
            FillWeight = 10
        };

        _grid.Columns.AddRange([colIdx, colStudent, colEnterprise, colTeacher, colCompatibility, colClear]);
    }

    public async Task LoadDataAsync()
    {
        _allClasses = (await _classRepo.GetAllAsync()).ToList();
        _allStudents = (await _studentRepo.GetAllAsync())
            .Where(s => s.IsActive && !s.StudentNumber.Contains("-P", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.StudentNumber)
            .ToList();
        _allTeachers = (await _teacherRepo.GetAllAsync()).ToList();
        _allEnterprises = (await _enterpriseRepo.GetAllAsync()).ToList();
        _allGroups = (await _groupRepo.GetAllAsync()).ToList();
        ApplyFilter();
    }

    private bool MatchesGroupAndGrade(Student s)
    {
        var cls = _allClasses.FirstOrDefault(c => (c.Id > 0 && c.Id == s.ClassId) || string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
        var eduType = FeeCalculator.GetEducationType(s, cls);

        // Özel Eğitim öğrencileri (kendisi veya sınıfı Özel Eğitim olanlar) Örgün Eğitim veya MESEM havuzuna dahil edilmez
        bool isOzelEgitim = string.Equals(eduType, "Özel Eğitim", StringComparison.OrdinalIgnoreCase) ||
                            (s.EducationType != null && s.EducationType.IndexOf("Özel Eğitim", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (cls != null && cls.EducationType != null && cls.EducationType.IndexOf("Özel Eğitim", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (s.FieldName != null && s.FieldName.IndexOf("Zihinsel Engelliler", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (cls?.FieldName != null && cls.FieldName.IndexOf("Zihinsel Engelliler", StringComparison.OrdinalIgnoreCase) >= 0);

        if (isOzelEgitim)
        {
            return false;
        }

        bool isMesem = string.Equals(eduType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                       FeeCalculator.IsMesem(s, cls) ||
                       s.Classroom.Contains("MESEM", StringComparison.OrdinalIgnoreCase);

        string grade = !string.IsNullOrWhiteSpace(s.Grade) ? s.Grade.Trim() : "";
        if (string.IsNullOrWhiteSpace(grade))
        {
            var g = FeeCalculator.GetGradeLevel(s, cls);
            if (g > 0) grade = g.ToString();
        }
        if (string.IsNullOrWhiteSpace(grade))
        {
            var m = System.Text.RegularExpressions.Regex.Match(s.Classroom, @"\b(9|10|11|12)\b");
            if (m.Success) grade = m.Value;
        }

        if (isMesem)
        {
            if (_chkMesemAll.Checked) return true;
            if (grade == "9" && _chkMesem9.Checked) return true;
            if (grade == "10" && _chkMesem10.Checked) return true;
            if (grade == "11" && _chkMesem11.Checked) return true;
            if (grade == "12" && _chkMesem12.Checked) return true;
            if (string.IsNullOrWhiteSpace(grade) && (_chkMesem9.Checked || _chkMesem10.Checked || _chkMesem11.Checked || _chkMesem12.Checked))
                return true;
            return false;
        }
        else
        {
            // Örgün Eğitim
            if (grade == "9" && _chkOrg9.Checked) return true;
            if (grade == "10" && _chkOrg10.Checked) return true;
            if (grade == "11" && _chkOrg11.Checked) return true;
            if (grade == "12" && _chkOrg12.Checked) return true;
            return false;
        }
    }

    private string GetScopeDescription()
    {
        var parts = new List<string>();
        var orgGrades = new List<string>();
        if (_chkOrg9.Checked) orgGrades.Add("9");
        if (_chkOrg10.Checked) orgGrades.Add("10");
        if (_chkOrg11.Checked) orgGrades.Add("11");
        if (_chkOrg12.Checked) orgGrades.Add("12");
        if (orgGrades.Count > 0)
        {
            parts.Add($"Örgün {string.Join("-", orgGrades)}. Sınıflar");
        }

        if (_chkMesemAll.Checked)
        {
            parts.Add("Tüm MESEM");
        }
        else
        {
            var mesemGrades = new List<string>();
            if (_chkMesem9.Checked) mesemGrades.Add("9");
            if (_chkMesem10.Checked) mesemGrades.Add("10");
            if (_chkMesem11.Checked) mesemGrades.Add("11");
            if (_chkMesem12.Checked) mesemGrades.Add("12");
            if (mesemGrades.Count > 0)
            {
                parts.Add($"MESEM {string.Join("-", mesemGrades)}. Sınıflar");
            }
        }

        return parts.Count > 0 ? string.Join(" + ", parts) : "Seçim Yapılmadı";
    }

    private void ApplyFilter()
    {
        // 1. Grup ve Kademe Checkbox filtrelerine göre ortak havuzu oluştur
        _targetStudents = _allStudents.Where(MatchesGroupAndGrade).ToList();
        _lblScopeBadge.Text = $"👥 {_targetStudents.Count} Öğrenci (Ortak Havuz)";

        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        var issueIdx = _cmbFilterIssue.SelectedIndex;

        var items = new List<(Student student, string status, Color color, string reason)>();

        foreach (var s in _targetStudents)
        {
            Teacher? assignedTeacher = s.TeacherId.HasValue ? _allTeachers.FirstOrDefault(t => t.Id == s.TeacherId.Value) : null;
            Enterprise? assignedEnt = s.EnterpriseId.HasValue ? _allEnterprises.FirstOrDefault(e => e.Id == s.EnterpriseId.Value) : null;

            string statusText;
            Color statusColor;
            string reason = "";

            if (!s.EnterpriseId.HasValue && !s.TeacherId.HasValue)
            {
                statusText = "Atama Yok";
                statusColor = Color.FromArgb(100, 116, 139); // slate
            }
            else if (!s.EnterpriseId.HasValue)
            {
                statusText = "⚠️ İşletme Eksik";
                statusColor = Color.FromArgb(180, 83, 9);
            }
            else if (!s.TeacherId.HasValue || assignedTeacher == null)
            {
                statusText = "⚠️ Öğretmen Eksik";
                statusColor = Color.FromArgb(180, 83, 9);
            }
            else
            {
                // Check conflicts:
                var studentDays = s.GetDaysList();
                var teacherDays = assignedTeacher.GetDaysList();
                var commonDays = studentDays.Intersect(teacherDays, StringComparer.OrdinalIgnoreCase).ToList();

                if (commonDays.Count == 0)
                {
                    statusText = "❌ Gün Çakışması";
                    statusColor = Color.FromArgb(185, 28, 28);
                    reason = $"Öğrenci ({s.InternshipDays}) ile Öğretmen ({assignedTeacher.AvailableDays}) ortak günü yok!";
                }
                else
                {
                    statusText = $"✓ Uyumlu ({string.Join(",", commonDays)})";
                    statusColor = Color.FromArgb(22, 101, 52);
                }
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var match = s.FullName.ToLowerInvariant().Contains(q) ||
                            s.StudentNumber.ToLowerInvariant().Contains(q) ||
                            (s.EnterpriseName ?? "").ToLowerInvariant().Contains(q) ||
                            (s.TeacherName ?? "").ToLowerInvariant().Contains(q);
                if (!match) continue;
            }

            if (issueIdx == 1 && !statusText.StartsWith("❌")) continue;
            if (issueIdx == 2 && !statusText.StartsWith("⚠️")) continue;
            if (issueIdx == 3 && !statusText.StartsWith("✓")) continue;
            if (issueIdx == 4 && statusText != "Atama Yok") continue;

            items.Add((s, statusText, statusColor, reason));
        }

        _grid.Rows.Clear();
        int idx = 1;
        foreach (var item in items)
        {
            var s = item.student;
            var entText = !string.IsNullOrWhiteSpace(s.EnterpriseName) ? s.EnterpriseName : "(İşletme Atanmadı)";
            var teachText = !string.IsNullOrWhiteSpace(s.TeacherName) ? s.TeacherName : "(Öğretmen Atanmadı)";
            if (s.TeacherId.HasValue)
            {
                var t = _allTeachers.FirstOrDefault(x => x.Id == s.TeacherId.Value);
                if (t != null && !string.IsNullOrWhiteSpace(t.AvailableDays))
                {
                    teachText += $" [{t.AvailableDays}]";
                }
            }

            var rowIndex = _grid.Rows.Add(
                idx++,
                $"{s.FullName} ({s.Classroom}) - Günler: {s.InternshipDays}",
                entText,
                teachText,
                item.status
            );

            _grid.Rows[rowIndex].Tag = s;
            _grid.Rows[rowIndex].Cells["colCompatibility"].Style.ForeColor = item.color;
            _grid.Rows[rowIndex].Cells["colCompatibility"].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            if (!string.IsNullOrWhiteSpace(item.reason))
            {
                _grid.Rows[rowIndex].Cells["colCompatibility"].ToolTipText = item.reason;
            }
        }
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not Student s) return;

        if (e.ColumnIndex == _grid.Columns["colClear"].Index)
        {
            var res = MessageBox.Show(
                $"{s.FullName} isimli öğrencinin işletme ve koordinatör atamasını kaldırmak istediğinize emin misiniz?",
                "Atamayı Kaldır",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                s.EnterpriseId = null;
                s.EnterpriseName = null;
                s.TeacherId = null;
                s.TeacherName = null;
                s.DutyDays = "";
                _ = _studentRepo.UpdateAsync(s).ContinueWith(_ => Invoke(new Action(() => _ = LoadDataAsync())));
            }
        }
    }

    private void BtnClearAll_Click(object? sender, EventArgs e)
    {
        var res = MessageBox.Show(
            "Tüm öğrencilerin işletme ve öğretmen atamalarını sıfırlamak istediğinize emin misiniz?",
            "Tüm Atamaları Sıfırla",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (res == DialogResult.Yes)
        {
            foreach (var s in _allStudents)
            {
                s.EnterpriseId = null;
                s.EnterpriseName = null;
                s.TeacherId = null;
                s.TeacherName = null;
                s.DutyDays = "";
                _studentRepo.UpdateAsync(s).GetAwaiter().GetResult();
            }
            MessageBox.Show("Tüm atamalar sıfırlandı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = LoadDataAsync();
        }
    }

    private void BtnAutoDistribute_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("⚡ Akıllı Otomatik Koordinatörlük Dağıtım Motoru", this))
            return;

        if (_targetStudents.Count == 0)
        {
            MessageBox.Show("Seçilen filtre kriterlerine uygun öğrenci bulunamadı. Lütfen en az bir sınıf seviyesi veya eğitim türü seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var scopeDesc = GetScopeDescription();
        using var dlg = new AutoDistributeDialog(_teacherRepo, _enterpriseRepo, _studentRepo, _targetStudents, scopeDesc);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _ = LoadDataAsync();
        }
    }
}
