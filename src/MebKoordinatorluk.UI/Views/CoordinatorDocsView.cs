using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Common;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Views;

public class CoordinatorDocsView : UserControl
{
    private readonly ITeacherRepository _teacherRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly IAttendanceRepository _attendanceRepo;

    private List<Teacher> _teachers = [];
    private List<Student> _students = [];
    private List<Enterprise> _enterprises = [];
    private List<SchoolClass> _classes = [];
    private AppSettings _appSettings = new();
    private readonly Dictionary<int, double> _studentMonthlyAbsenceMap = new();
    private readonly Dictionary<(int studentId, int day), string> _studentDayAttendanceMap = new();
    private readonly Dictionary<int, (double excused, double unexcused)> _studentAbsenceTotalsMap = new();

    // Filter controls
    private ComboBox _cmbDocType = null!;
    private ComboBox _cmbTeacher = null!;
    private ComboBox _cmbEnterprise = null!;
    private ComboBox _cmbMonth = null!;
    private Label _lblDutyDate = null!;
    private ComboBox _cmbVisitDate = null!;
    private Label _lblOrientation = null!;
    private ComboBox _cmbOrientation = null!;
    private Label _lblLayout = null!;
    private ComboBox _cmbPageLayout = null!;
    private DateTime _selectedMonth = DateTime.Today;

    // Content & Preview
    private Panel _pnlHeader = null!;
    private Panel _pnlFilter = null!;
    private Panel _pnlPreviewContainer = null!;
    private Panel _pnlA4 = null!;
    private Button _btnPrintCurrent = null!;
    private Button _btnPrintAll = null!;
    private Button _btnExportExcel = null!;
    private bool _isInitializing = true;

    // Multi-page print state
    private List<Enterprise> _printEnterprisesQueue = [];
    private int _printPageIndex = 0;

    public CoordinatorDocsView(
        ITeacherRepository teacherRepo,
        IStudentRepository studentRepo,
        IEnterpriseRepository enterpriseRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        ISettingRepository settingRepo,
        IAttendanceRepository attendanceRepo)
    {
        _teacherRepo = teacherRepo;
        _studentRepo = studentRepo;
        _enterpriseRepo = enterpriseRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _settingRepo = settingRepo;
        _attendanceRepo = attendanceRepo;

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

        // 1. Header
        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 16, 24, 0)
        };

        var lblTitle = new Label
        {
            Text = "📁 RESMİ KOORDİNATÖR EVRAKLARI",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "MEB 3308 Sayılı Kanun kapsamındaki standart resmi evrak, çalışma planı ve tutanak oluşturma merkezi",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 520,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnPrintCurrent = new Button
        {
            Text = "🖨️ Formu Yazdır (A4)",
            Size = new Size(165, 36),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0)
        };
        _btnPrintCurrent.FlatAppearance.BorderSize = 0;
        _btnPrintCurrent.Click += BtnPrintCurrent_Click;

        _btnPrintAll = new Button
        {
            Text = "📑 Tümünü Yazdır",
            Size = new Size(150, 36),
            BackColor = Color.FromArgb(79, 70, 229), // indigo-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0)
        };
        _btnPrintAll.FlatAppearance.BorderSize = 0;
        _btnPrintAll.Click += BtnPrintAll_Click;

        _btnExportExcel = new Button
        {
            Text = "📊 Excel İndir",
            Size = new Size(120, 36),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0)
        };
        _btnExportExcel.FlatAppearance.BorderSize = 0;
        _btnExportExcel.Click += BtnExportExcel_Click;

        pnlTopButtons.Controls.AddRange([_btnPrintCurrent, _btnPrintAll, _btnExportExcel]);
        _pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // 2. Filter Bar
        _pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 8, 24, 8)
        };
        _pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, _pnlFilter.Height - 1, _pnlFilter.Width, _pnlFilter.Height - 1);
        };

        var pnlFilterCenter = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var lblDocType = new Label
        {
            Text = "Evrak:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 4, 0),
            AutoSize = true
        };

        _cmbDocType = new ComboBox
        {
            Width = 270,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 4, 12, 0)
        };
        _cmbDocType.Items.AddRange([
            "1. Aylık İşletme Ziyaret ve Rehberlik Formu (Ek-1)",
            "2. Günlük Rehberlik Görev Formu",
            "3. Aylık Devam-Devamsızlık Bildirim Çizelgesi",
            "4. Aylık Koordinatörlük Çalışma Planı ve Programı",
            "5. İşletmelerde Mesleki Eğitim Sözleşmesi",
            "6. İşletme Belirleme ve Uygunluk Formu",
            "7. Öğrenci Gelişim ve Değerlendirme Çizelgesi",
            "8. Beceri Eğitimi Sınav Değerlendirme Tutanağı"
        ]);
        _cmbDocType.SelectedIndex = 0;
        _cmbDocType.SelectedIndexChanged += (s, e) => ApplyDocTypeSettings(_cmbDocType.SelectedIndex);

        var lblTeacher = new Label
        {
            Text = "Öğretmen:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 4, 0),
            AutoSize = true
        };

        _cmbTeacher = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 4, 12, 0)
        };
        _cmbTeacher.SelectedIndexChanged += (s, e) => OnTeacherFilterChanged();

        var lblEnterprise = new Label
        {
            Text = "İşletme:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 4, 0),
            AutoSize = true
        };

        _cmbEnterprise = new ComboBox
        {
            Width = 190,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 4, 12, 0)
        };
        _cmbEnterprise.SelectedIndexChanged += (s, e) =>
        {
            UpdateButtonsState();
            if (_cmbDocType.SelectedIndex == 1) UpdateVisitDatesList();
            UpdatePreview();
        };

        var lblMonth = new Label
        {
            Text = "Ay:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 4, 0),
            AutoSize = true
        };

        _cmbMonth = new ComboBox
        {
            Width = 115,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 4, 12, 0)
        };
        for (int i = 0; i < 12; i++)
        {
            var m = DateTime.Today.AddMonths(-i);
            _cmbMonth.Items.Add(m.ToString("MMMM yyyy"));
        }
        _cmbMonth.SelectedIndex = 0;
        _cmbMonth.SelectedIndexChanged += async (s, e) =>
        {
            _selectedMonth = DateTime.Today.AddMonths(-_cmbMonth.SelectedIndex);
            await ReloadMonthlyAttendanceAsync();
            if (_cmbDocType.SelectedIndex == 1) UpdateVisitDatesList();
            UpdatePreview();
        };

        _lblDutyDate = new Label
        {
            Text = "Görev Günü:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 4, 0),
            AutoSize = true,
            Visible = false
        };

        _cmbVisitDate = new ComboBox
        {
            Width = 195,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 4, 12, 0),
            Visible = false
        };
        _cmbVisitDate.SelectedIndexChanged += (s, e) => UpdatePreview();

        _lblOrientation = new Label
        {
            Text = "Yönlendirme:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 4, 0),
            AutoSize = true,
            Visible = false
        };

        _cmbOrientation = new ComboBox
        {
            Width = 95,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 4, 12, 0),
            Visible = false
        };
        _cmbOrientation.Items.AddRange(["Yatay", "Dikey"]);
        _cmbOrientation.SelectedIndex = 1; // Standart Dikey
        _cmbOrientation.SelectedIndexChanged += (s, e) =>
        {
            UpdateA4PanelSize();
            UpdatePreview();
        };

        _lblLayout = new Label
        {
            Text = "Yerleşim:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 8, 4, 0),
            AutoSize = true
        };

        _cmbPageLayout = new ComboBox
        {
            Width = 195,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 4, 0, 0)
        };
        _cmbPageLayout.Items.AddRange([
            "1 İşletme / Sayfa (Tam)",
            "2 İşletme / Sayfa (Tasarruflu)"
        ]);
        _cmbPageLayout.SelectedIndex = 1; // Açılışta standart 2 sayfa (2 İşletme / Sayfa) gelsin
        _cmbPageLayout.SelectedIndexChanged += (s, e) => UpdatePreview();

        pnlFilterCenter.Controls.AddRange([
            lblDocType, _cmbDocType,
            lblTeacher, _cmbTeacher,
            lblEnterprise, _cmbEnterprise,
            lblMonth, _cmbMonth,
            _lblDutyDate, _cmbVisitDate,
            _lblOrientation, _cmbOrientation,
            _lblLayout, _cmbPageLayout
        ]);
        _pnlFilter.Controls.Add(pnlFilterCenter);

        void CenterFilter()
        {
            pnlFilterCenter.Location = new Point(Math.Max(16, (_pnlFilter.ClientSize.Width - pnlFilterCenter.Width) / 2), 8);
        }
        _pnlFilter.Resize += (s, e) => CenterFilter();
        pnlFilterCenter.SizeChanged += (s, e) => CenterFilter();
        CenterFilter();

        // 3. Preview Area (Scrollable with A4 Sheet)
        _pnlPreviewContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(24)
        };

        // A4 Paper Simulation (794 x 1123 px at 96 DPI)
        _pnlA4 = new Panel
        {
            Size = new Size(794, 1123),
            BackColor = Color.White,
            Padding = Padding.Empty
        };
        _pnlA4.Paint += PnlA4_Paint;

        // Double-buffering for flicker-free rendering
        typeof(Panel).InvokeMember(
            "DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null,
            _pnlA4,
            [true]);

        _pnlPreviewContainer.Controls.Add(_pnlA4);
        _pnlPreviewContainer.Resize += (s, e) => CenterA4Panel();
        CenterA4Panel();

        Controls.Add(_pnlPreviewContainer);
        Controls.Add(_pnlFilter);
        Controls.Add(_pnlHeader);
    }

    private void CenterA4Panel()
    {
        if (_pnlPreviewContainer == null || _pnlA4 == null) return;
        int x = Math.Max(24, (_pnlPreviewContainer.ClientSize.Width - _pnlA4.Width) / 2);
        int y = 24;
        _pnlA4.Location = new Point(x, y);
    }

    private void UpdateA4PanelSize()
    {
        if (_pnlA4 == null) return;
        int idx = _cmbDocType != null ? _cmbDocType.SelectedIndex : 0;
        bool isAttendance = idx == 2;
        bool isLandscape = isAttendance && (_cmbOrientation == null || _cmbOrientation.SelectedIndex == 0);

        _pnlA4.Size = isLandscape ? new Size(1123, 794) : new Size(794, 1123);
        CenterA4Panel();
    }

    private void ApplyDocTypeSettings(int index)
    {
        bool isDaily = index == 1;
        bool isAttendance = index == 2;

        if (_lblDutyDate != null) _lblDutyDate.Visible = isDaily;
        if (_cmbVisitDate != null) _cmbVisitDate.Visible = isDaily;

        if (_lblOrientation != null) _lblOrientation.Visible = isAttendance;
        if (_cmbOrientation != null)
        {
            _cmbOrientation.Visible = isAttendance;
            if (isAttendance)
            {
                _cmbOrientation.SelectedIndex = 1; // Standart Dikey gelsin
            }
        }

        if (_lblLayout != null) _lblLayout.Visible = true;
        if (_cmbPageLayout != null)
        {
            _cmbPageLayout.Visible = true;
            if (index == 0 || isDaily || isAttendance)
            {
                _cmbPageLayout.SelectedIndex = 1; // Ek-1, Günlük ve Aylık Devamsızlıkta standart 2 İşletme / Sayfa gelsin
            }
        }

        UpdateA4PanelSize();
        UpdateButtonsState();
        if (isDaily) UpdateVisitDatesList();
        UpdatePreview();
    }

    public async Task LoadDataAsync()
    {
        try
        {
            _isInitializing = true;
            _appSettings = await _settingRepo.GetAppSettingsAsync() ?? new AppSettings();
            _teachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();
            _enterprises = (await _enterpriseRepo.GetAllAsync()).OrderBy(e => e.Name).ToList();
            _students = (await _studentRepo.GetAllAsync()).Where(s => s.IsActive).ToList();
            _classes = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();

            // Populate Teacher ComboBox
            _cmbTeacher.Items.Clear();
            _cmbTeacher.Items.Add("Tüm Öğretmenler");
            foreach (var t in _teachers)
            {
                _cmbTeacher.Items.Add(t.FullName);
            }
            int defaultTeacherIdx = 0;
            if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin && MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser != null)
            {
                var curUser = MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser;
                var matchIdx = _teachers.FindIndex(t =>
                    (curUser.TeacherId.HasValue && t.Id == curUser.TeacherId.Value) ||
                    string.Equals(t.FullName, curUser.FullName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.FullName, curUser.TeacherName, StringComparison.OrdinalIgnoreCase));
                if (matchIdx >= 0)
                {
                    defaultTeacherIdx = matchIdx + 1;
                }
            }
            _cmbTeacher.SelectedIndex = defaultTeacherIdx;

            OnTeacherFilterChanged();
            await ReloadMonthlyAttendanceAsync();

            _isInitializing = false;
            UpdatePreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Evrak modülü verileri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task ReloadMonthlyAttendanceAsync()
    {
        try
        {
            _studentMonthlyAbsenceMap.Clear();
            _studentDayAttendanceMap.Clear();
            _studentAbsenceTotalsMap.Clear();

            var entries = await _attendanceRepo.GetForMonthAsync(_selectedMonth.Year, _selectedMonth.Month);
            foreach (var e in entries)
            {
                _studentDayAttendanceMap[(e.StudentId, e.DayOfMonth)] = e.Code;

                bool unexcused = AttendanceCodeHelper.IsUnexcused(e.Code);
                bool excused = AttendanceCodeHelper.IsExcused(e.Code);

                if (unexcused || excused)
                {
                    double days = AttendanceCodeHelper.GetAbsenceDays(e.Code);
                    if (!_studentMonthlyAbsenceMap.ContainsKey(e.StudentId))
                        _studentMonthlyAbsenceMap[e.StudentId] = 0;
                    _studentMonthlyAbsenceMap[e.StudentId] += days;

                    if (!_studentAbsenceTotalsMap.ContainsKey(e.StudentId))
                        _studentAbsenceTotalsMap[e.StudentId] = (0, 0);

                    var cur = _studentAbsenceTotalsMap[e.StudentId];
                    if (excused)
                        _studentAbsenceTotalsMap[e.StudentId] = (cur.excused + days, cur.unexcused);
                    else if (unexcused)
                        _studentAbsenceTotalsMap[e.StudentId] = (cur.excused, cur.unexcused + days);
                }
            }
        }
        catch
        {
            // Fallback gracefully
        }
    }

    private List<Enterprise> GetAvailableEnterprises()
    {
        int teacherIdx = _cmbTeacher.SelectedIndex;
        Teacher? selectedTeacher = teacherIdx > 0 && teacherIdx <= _teachers.Count ? _teachers[teacherIdx - 1] : null;

        if (selectedTeacher != null)
        {
            var teacherStudentEnterpriseIds = _students
                .Where(s => s.TeacherId == selectedTeacher.Id && s.EnterpriseId.HasValue)
                .Select(s => s.EnterpriseId!.Value)
                .ToHashSet();

            return _enterprises
                .Where(e => e.TeacherId == selectedTeacher.Id || teacherStudentEnterpriseIds.Contains(e.Id))
                .OrderBy(e => e.Name)
                .ToList();
        }

        return _enterprises;
    }

    private void OnTeacherFilterChanged()
    {
        var availableEnterprises = GetAvailableEnterprises();

        _cmbEnterprise.Items.Clear();
        if (availableEnterprises.Count == 0)
        {
            _cmbEnterprise.Items.Add("İşletme Bulunamadı");
        }
        else
        {
            _cmbEnterprise.Items.Add($"Tüm İşletmeler ({availableEnterprises.Count})");
            foreach (var e in availableEnterprises)
            {
                _cmbEnterprise.Items.Add(e.Name);
            }
        }

        _cmbEnterprise.SelectedIndex = 0;
        if (_cmbDocType.SelectedIndex == 1) UpdateVisitDatesList();
        UpdateButtonsState();
        UpdatePreview();
    }

    private void UpdateButtonsState()
    {
        if (_btnPrintCurrent == null || _cmbDocType == null || _cmbEnterprise == null) return;

        int docTypeIdx = _cmbDocType.SelectedIndex;
        bool isDaily = docTypeIdx == 1;
        bool isAttendance = docTypeIdx == 2;
        bool isAll = _cmbEnterprise.SelectedIndex <= 0;

        if (isAll)
        {
            _btnPrintCurrent.Text = isDaily ? "🖨️ Tüm İşletmeleri Yazdır" : (isAttendance ? "🖨️ Tüm Çizelgeleri Yazdır" : "🖨️ Tüm Formları Yazdır");
        }
        else
        {
            _btnPrintCurrent.Text = isDaily ? "🖨️ Ayın Formlarını Yazdır" : (isAttendance ? "🖨️ Çizelgeyi Yazdır" : "🖨️ Formu Yazdır (A4)");
        }
    }

    private void UpdateVisitDatesList()
    {
        if (_cmbVisitDate == null) return;
        var (ent, _, teacher, students, _) = GetSelectionForPreview();
        var dates = ent != null ? GetMonthlyVisitDates(ent, teacher, students, _selectedMonth) : [];

        _cmbVisitDate.Items.Clear();
        if (dates.Count == 0)
        {
            _cmbVisitDate.Items.Add($"{_selectedMonth:MMMM yyyy}");
        }
        else
        {
            for (int i = 0; i < dates.Count; i++)
            {
                _cmbVisitDate.Items.Add($"{i + 1}. Görev ({dates[i]:dd.MM.yyyy dddd})");
            }
        }
        if (_cmbVisitDate.Items.Count > 0) _cmbVisitDate.SelectedIndex = 0;
    }

    private (Enterprise? ent1, Enterprise? ent2, Teacher? teacher, List<Student> students1, List<Student> students2) GetSelectionForPreview()
    {
        var available = GetAvailableEnterprises();
        int selIdx = _cmbEnterprise != null ? _cmbEnterprise.SelectedIndex : 0;

        Enterprise? ent1 = null;
        Enterprise? ent2 = null;

        if (available.Count > 0)
        {
            if (selIdx <= 0) // "Tüm İşletmeler" seçili
            {
                ent1 = available[0];
                ent2 = available.Count > 1 ? available[1] : null;
            }
            else
            {
                int entIdx = selIdx - 1; // "Tüm İşletmeler" 0 olduğu için -1
                if (entIdx >= 0 && entIdx < available.Count)
                {
                    ent1 = available[entIdx];
                    ent2 = (entIdx + 1 < available.Count) ? available[entIdx + 1] : null;
                }
            }
        }

        int teacherIdx = _cmbTeacher != null ? _cmbTeacher.SelectedIndex : 0;
        Teacher? teacher = teacherIdx > 0 && teacherIdx <= _teachers.Count ? _teachers[teacherIdx - 1] : null;

        List<Student> st1 = ent1 != null ? GetStudentsForEnterprise(ent1, teacher) : [];
        List<Student> st2 = ent2 != null ? GetStudentsForEnterprise(ent2, teacher) : [];

        Teacher? resolvedTeacher = ent1 != null ? ResolveTeacherForEnterprise(ent1, teacher, st1) : teacher;

        return (ent1, ent2, resolvedTeacher, st1, st2);
    }

    /// <summary>
    /// Bir işletme için koordinatör öğretmeni hiyerarşik olarak çözümler:
    /// 1. Filtreden seçilmiş öğretmen varsa onu kullanır.
    /// 2. İşletmenin kendi TeacherId'si tanımlıysa _teachers listesinden bulur.
    /// 3. İşletmedeki öğrencilerin TeacherId'sine bakar.
    /// 4. Öğrencilerdeki TeacherName metnine bakar.
    /// 5. İşletmedeki TeacherName metnine bakar.
    /// </summary>
    private Teacher? ResolveTeacherForEnterprise(Enterprise ent, Teacher? selectedTeacher, IEnumerable<Student>? students = null)
    {
        if (selectedTeacher != null) return selectedTeacher;

        if (ent.TeacherId.HasValue)
        {
            var t = _teachers.FirstOrDefault(x => x.Id == ent.TeacherId.Value);
            if (t != null) return t;
        }

        var stList = students ?? _students.Where(s => s.EnterpriseId == ent.Id || string.Equals(s.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase));

        var stWithTeacherId = stList.FirstOrDefault(s => s.TeacherId.HasValue);
        if (stWithTeacherId?.TeacherId != null)
        {
            var t = _teachers.FirstOrDefault(x => x.Id == stWithTeacherId.TeacherId.Value);
            if (t != null) return t;
        }

        var stWithTeacherName = stList.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.TeacherName));
        if (stWithTeacherName != null && !string.IsNullOrWhiteSpace(stWithTeacherName.TeacherName))
        {
            var t = _teachers.FirstOrDefault(x => string.Equals(x.FullName, stWithTeacherName.TeacherName, StringComparison.OrdinalIgnoreCase));
            if (t != null) return t;
            return new Teacher { FullName = stWithTeacherName.TeacherName, Branch = "Koordinatör Öğretmen" };
        }

        if (!string.IsNullOrWhiteSpace(ent.TeacherName))
        {
            var t = _teachers.FirstOrDefault(x => string.Equals(x.FullName, ent.TeacherName, StringComparison.OrdinalIgnoreCase));
            if (t != null) return t;
            return new Teacher { FullName = ent.TeacherName, Branch = "Koordinatör Öğretmen" };
        }

        return null;
    }

    private List<Student> GetStudentsForEnterprise(Enterprise ent, Teacher? teacher)
    {
        var stList = _students
            .Where(s => s.EnterpriseId == ent.Id || string.Equals(s.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Classroom)
            .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
            .ToList();

        if (teacher != null)
        {
            var ts = stList.Where(s => s.TeacherId == teacher.Id).ToList();
            if (ts.Count > 0) stList = ts;
        }

        return stList;
    }

    private void UpdatePreview()
    {
        if (_isInitializing || _pnlA4 == null) return;
        _pnlA4.Invalidate();
    }

    private void PnlA4_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Paper Border / Shadow look
        using var penBorder = new Pen(Color.FromArgb(203, 213, 225), 1);
        g.DrawRectangle(penBorder, 0, 0, _pnlA4.Width - 1, _pnlA4.Height - 1);

        int docTypeIdx = _cmbDocType.SelectedIndex;
        if (docTypeIdx == 0) // 1. Aylık İşletme Ziyaret ve Rehberlik Formu (Ek-1)
        {
            var (ent1, ent2, teacher, st1, st2) = GetSelectionForPreview();
            if (ent1 != null)
            {
                Teacher? t1 = teacher;
                Teacher? t2 = ent2 != null ? ResolveTeacherForEnterprise(ent2, _cmbTeacher?.SelectedIndex > 0 ? teacher : null, st2) : null;

                if (_cmbPageLayout.SelectedIndex == 1) // 2 İşletme / Sayfa (Tasarruflu)
                {
                    DrawTwoPerA4Page(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height), ent1, ent2, t1, t2, st1, st2, _appSettings, _selectedMonth, _studentMonthlyAbsenceMap);
                }
                else // 1 İşletme / Sayfa (Tam)
                {
                    DrawEk1Page(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height), ent1, t1, st1, _appSettings, _selectedMonth, _studentMonthlyAbsenceMap);
                }
            }
            else
            {
                DrawPlaceholderPage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height), "Kayıtlı işletme bulunamadı.");
            }
        }
        else if (docTypeIdx == 1) // 2. Günlük Rehberlik Görev Formu
        {
            var (ent1, ent2, teacher, st1, st2) = GetSelectionForPreview();
            if (ent1 != null)
            {
                var visitDates = GetMonthlyVisitDates(ent1, teacher, st1, _selectedMonth);
                if (visitDates.Count == 0) visitDates.Add(_selectedMonth);

                int visitIdx = _cmbVisitDate != null ? _cmbVisitDate.SelectedIndex : 0;
                if (visitIdx < 0 || visitIdx >= visitDates.Count) visitIdx = 0;
                DateTime currentDutyDate = visitDates[visitIdx];

                if (_cmbPageLayout.SelectedIndex == 1) // 2 Form / Sayfa (Tasarruflu)
                {
                    DateTime? nextDutyDate = (visitIdx + 1 < visitDates.Count)
                        ? visitDates[visitIdx + 1]
                        : (visitIdx > 0 ? visitDates[visitIdx - 1] : null);

                    DrawTwoPerA4DailyGuidancePage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height),
                        ent1, nextDutyDate.HasValue ? ent1 : null,
                        teacher, teacher,
                        st1, st1,
                        _appSettings,
                        currentDutyDate, nextDutyDate);
                }
                else // 1 Form / Sayfa (Tam)
                {
                    DrawDailyGuidancePage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height),
                        ent1, teacher, st1, _appSettings, currentDutyDate);
                }
            }
            else
            {
                DrawPlaceholderPage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height), "Kayıtlı işletme bulunamadı.");
            }
        }
        else if (docTypeIdx == 2) // 3. Aylık Devam-Devamsızlık Bildirim Çizelgesi
        {
            var (ent1, ent2, teacher, st1, st2) = GetSelectionForPreview();
            if (ent1 != null)
            {
                bool isLandscape = _cmbOrientation == null || _cmbOrientation.SelectedIndex == 0;
                bool isTwoPerPage = _cmbPageLayout != null && _cmbPageLayout.SelectedIndex == 1;

                Teacher? t1 = teacher;
                Teacher? t2 = ent2 != null ? ResolveTeacherForEnterprise(ent2, _cmbTeacher?.SelectedIndex > 0 ? teacher : null, st2) : null;

                if (isTwoPerPage)
                {
                    DrawTwoPerA4AttendancePage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height),
                        ent1, ent2, t1, t2, st1, st2, _appSettings, _selectedMonth,
                        _studentDayAttendanceMap, _studentAbsenceTotalsMap, isLandscape);
                }
                else
                {
                    DrawMonthlyAttendancePage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height),
                        ent1, t1, st1, _appSettings, _selectedMonth,
                        _studentDayAttendanceMap, _studentAbsenceTotalsMap, isLandscape);
                }
            }
            else
            {
                DrawPlaceholderPage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height), "Kayıtlı işletme bulunamadı.");
            }
        }
        else
        {
            string docTitle = _cmbDocType.SelectedItem?.ToString() ?? "KOORDİNATÖRLÜK EVRAKI";
            DrawPlaceholderPage(g, new Rectangle(0, 0, _pnlA4.Width, _pnlA4.Height),
                $"📋 SEÇİLEN EVRAK: {docTitle}\n\n" +
                $"📅 Dönem / Ay: {_selectedMonth:MMMM yyyy}\n\n" +
                "📌 Bu evrak bir sonraki aşamada resmi MEB formatında hazırlanacaktır.\n" +
                "Şu an '1. Aylık İşletme Ziyaret ve Rehberlik Formu (Ek-1)', '2. Günlük Rehberlik Görev Formu' ve '3. Aylık Devam-Devamsızlık Bildirim Çizelgesi' tam aktiftir.");
        }
    }

    /// <summary>
    /// Computes and returns the exact list of visit dates for the given enterprise in the selected month,
    /// based on the duty days assigned in Görev Formu (Student.DutyDays), teacher available days, or enterprise active days.
    /// </summary>
    public static List<DateTime> GetMonthlyVisitDates(Enterprise enterprise, Teacher? teacher, List<Student> students, DateTime month)
    {
        var targetDays = new HashSet<DayOfWeek>();

        // 1. ÖNCELİK: Görev Formundaki Görev Günü (Student.DutyDays)
        var stWithDuty = students.Where(s => !string.IsNullOrWhiteSpace(s.DutyDays)).ToList();
        if (stWithDuty.Count > 0)
        {
            foreach (var st in stWithDuty)
            {
                var parts = st.DutyDays.Split([',', ';', '/', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    string norm = TurkishTextHelper.NormalizeDayName(p.Trim());
                    var dow = DayNameToDayOfWeek(norm);
                    if (dow.HasValue) targetDays.Add(dow.Value);
                }
            }
        }

        // 2. ÖNCELİK: Öğrencide DutyDays tanımlı değilse koordinatör öğretmenin okul koordinatörlük günü (Teacher.AvailableDays)
        if (targetDays.Count == 0 && teacher != null)
        {
            List<string> teacherDays = teacher.GetDaysList();
            if (teacherDays.Count == 0 && !string.IsNullOrWhiteSpace(teacher.AvailableDays))
            {
                teacherDays = teacher.AvailableDays.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            foreach (var td in teacherDays)
            {
                string norm = TurkishTextHelper.NormalizeDayName(td.Trim());
                var dow = DayNameToDayOfWeek(norm);
                if (dow.HasValue) targetDays.Add(dow.Value);
            }
        }

        // 3. ÖNCELİK: Öğretmenin günü de belirtilmemişse işletme veya öğrenci staj gününe bakılır
        if (targetDays.Count == 0)
        {
            string daysStr = enterprise.ActiveDays;
            if (string.IsNullOrWhiteSpace(daysStr) && students.Count > 0)
            {
                daysStr = students[0].InternshipDays;
            }

            if (!string.IsNullOrWhiteSpace(daysStr))
            {
                var parts = daysStr.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    string norm = TurkishTextHelper.NormalizeDayName(p.Trim());
                    var dow = DayNameToDayOfWeek(norm);
                    if (dow.HasValue) targetDays.Add(dow.Value);
                }
            }
        }

        if (targetDays.Count == 0)
        {
            targetDays.Add(DayOfWeek.Tuesday); // Varsayılan koordinatörlük günü
        }

        // Seçilen ayın (month) takvimindeki tüm ilgili günleri topla:
        int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        List<DateTime> visitDates = [];
        for (int day = 1; day <= daysInMonth; day++)
        {
            var dt = new DateTime(month.Year, month.Month, day);
            if (targetDays.Contains(dt.DayOfWeek))
            {
                visitDates.Add(dt);
            }
        }

        return visitDates;
    }

    private static DayOfWeek? DayNameToDayOfWeek(string normDay) => normDay switch
    {
        "Pazartesi" => DayOfWeek.Monday,
        "Salı" => DayOfWeek.Tuesday,
        "Çarşamba" => DayOfWeek.Wednesday,
        "Perşembe" => DayOfWeek.Thursday,
        "Cuma" => DayOfWeek.Friday,
        "Cumartesi" => DayOfWeek.Saturday,
        "Pazar" => DayOfWeek.Sunday,
        _ => null
    };

    /// <summary>
    /// Computes and formats monthly visit dates formatted like "06-13-20-27.10.2026".
    /// </summary>
    public static string FormatVisitDates(Enterprise enterprise, Teacher? teacher, List<Student> students, DateTime month)
    {
        var dates = GetMonthlyVisitDates(enterprise, teacher, students, month);
        if (dates.Count == 0) return $"{month:MMMM yyyy}";
        string daysPart = string.Join("-", dates.Select(d => d.Day.ToString("00")));
        return $"{daysPart}.{month:MM.yyyy}";
    }

    /// <summary>
    /// Renders 2 Enterprises on a single A4 page (Top & Bottom halves with middle cut line).
    /// </summary>
    public static void DrawTwoPerA4Page(
        Graphics g,
        Rectangle bounds,
        Enterprise ent1,
        Enterprise? ent2,
        Teacher? teacher1,
        Teacher? teacher2,
        List<Student> students1,
        List<Student> students2,
        AppSettings settings,
        DateTime month,
        Dictionary<int, double> absentDaysMap)
    {
        int halfH = bounds.Height / 2;
        Rectangle topBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, halfH - 8);
        Rectangle btmBounds = new Rectangle(bounds.X, bounds.Y + halfH + 8, bounds.Width, halfH - 8);

        // 1. Üst İşletme
        DrawCompactEk1Page(g, topBounds, ent1, teacher1, students1, settings, month, absentDaysMap);

        // 2. Ayırma Çizgisi (Kesim Çizgisi)
        int yCut = bounds.Y + halfH;
        using var pDotted = new Pen(Color.FromArgb(148, 163, 184), 1f) { DashStyle = DashStyle.Dash };
        g.DrawLine(pDotted, bounds.X + 24, yCut, bounds.Right - 24, yCut);

        string cutText = "✂   BURADAN KESİNİZ (A5 FORMU)   ✂";
        using var fCut = new Font("Segoe UI", 6.8f, FontStyle.Bold);
        using var bCutBg = new SolidBrush(Color.White);
        using var bCutText = new SolidBrush(Color.FromArgb(100, 116, 139));
        var cutSize = g.MeasureString(cutText, fCut);
        int badgeW = (int)cutSize.Width + 18;
        int badgeH = 14;
        int badgeX = bounds.X + (bounds.Width - badgeW) / 2;
        g.FillRectangle(bCutBg, badgeX, yCut - 7, badgeW, badgeH);
        g.DrawRectangle(pDotted, badgeX, yCut - 7, badgeW, badgeH);
        using var sfCut = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(cutText, fCut, bCutText, new RectangleF(badgeX, yCut - 7, badgeW, badgeH), sfCut);

        // 3. Alt İşletme (Varsa)
        if (ent2 != null)
        {
            DrawCompactEk1Page(g, btmBounds, ent2, teacher2 ?? teacher1, students2, settings, month, absentDaysMap);
        }
        else
        {
            using var fEmpty = new Font("Segoe UI", 9f, FontStyle.Italic);
            using var bEmpty = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString("(Bu sayfada eşleşen 2. işletme bulunmamaktadır)", fEmpty, bEmpty, btmBounds, sfCut);
        }
    }

    public static void DrawTwoPerA4Page(
        Graphics g,
        Rectangle bounds,
        Enterprise ent1,
        Enterprise? ent2,
        Teacher? teacher,
        List<Student> students1,
        List<Student> students2,
        AppSettings settings,
        DateTime month,
        Dictionary<int, double> absentDaysMap)
    {
        DrawTwoPerA4Page(g, bounds, ent1, ent2, teacher, teacher, students1, students2, settings, month, absentDaysMap);
    }

    /// <summary>
    /// Renders a compact A5-compatible Ek-1 form for 2-per-page saving mode.
    /// </summary>
    public static void DrawCompactEk1Page(
        Graphics g,
        Rectangle bounds,
        Enterprise enterprise,
        Teacher? teacher,
        List<Student> students,
        AppSettings settings,
        DateTime month,
        Dictionary<int, double> absentDaysMap)
    {
        int x = bounds.X + 32;
        int w = bounds.Width - 64;
        int y = bounds.Y + 8;

        using var fSchool = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var fHeaderSub = new Font("Segoe UI", 7f, FontStyle.Regular);
        using var fDocTitle = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var fSecTitle = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var fLabel = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var fValue = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var fValueBold = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var fSmall = new Font("Segoe UI", 6.8f, FontStyle.Regular);
        using var fSmallItalic = new Font("Segoe UI", 6.8f, FontStyle.Italic);

        using var bDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bBlue = new SolidBrush(Color.FromArgb(30, 58, 138));
        using var bMuted = new SolidBrush(Color.FromArgb(71, 85, 105));
        using var bGrayBg = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var bSectionBg = new SolidBrush(Color.FromArgb(239, 246, 255));
        using var bAltBg = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var bNoteBg = new SolidBrush(Color.FromArgb(250, 250, 250));
        using var bGreen = new SolidBrush(Color.FromArgb(5, 150, 105));

        using var pThin = new Pen(Color.FromArgb(203, 213, 225), 1);
        using var pDark = new Pen(Color.FromArgb(15, 23, 42), 1f);
        using var pNavy = new Pen(Color.FromArgb(30, 58, 138), 1.5f);

        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

        // 1. Antetli Başlık
        string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MESLEKİ VE TEKNİK ANADOLU LİSESİ MÜDÜRLÜĞÜ" : settings.SchoolName;
        string gov = !string.IsNullOrWhiteSpace(settings.Valilik) ? settings.Valilik.ToUpper(new CultureInfo("tr-TR")) : "MİLLÎ EĞİTİM BAKANLIĞI";
        string memShort = !string.IsNullOrWhiteSpace(settings.DistrictMem) ? $" - {settings.DistrictMem.ToUpper(new CultureInfo("tr-TR"))}" : "";
        g.DrawString($"T.C.  {gov}{memShort}  -  {schoolName}", fSchool, bDark, new RectangleF(x, y, w, 13), sfCenter);
        y += 13;
        g.DrawString("İŞLETMELERDE MESLEKİ EĞİTİM / STAJ AYLIK REHBERLİK VE DENETİM FORMU (EK-1)", fDocTitle, bBlue, new RectangleF(x, y, w, 15), sfCenter);
        y += 15;
        g.DrawString($"Dönem: {month:MMMM yyyy} | 3308 Sayılı Kanun Kapsamında Aylık Ziyaret Fişi", fSmallItalic, bMuted, new RectangleF(x, y, w, 12), sfCenter);
        y += 13;
        g.DrawLine(pNavy, x, y, x + w, y);
        y += 3;

        // 2. İşletme ve Görev Bilgileri
        int infoH = 48; // 3 rows x 16px
        g.FillRectangle(bGrayBg, x, y, w, infoH);
        g.DrawRectangle(pDark, x, y, w, infoH);

        int rowH = 16;
        int col1W = 115, col2W = 250, col3W = 120;
        int col4W = w - (col1W + col2W + col3W);

        for (int i = 1; i < 3; i++) g.DrawLine(pThin, x, y + (i * rowH), x + w, y + (i * rowH));
        g.DrawLine(pThin, x + col1W, y, x + col1W, y + infoH);
        g.DrawLine(pThin, x + col1W + col2W, y, x + col1W + col2W, y + infoH);
        g.DrawLine(pThin, x + col1W + col2W + col3W, y, x + col1W + col2W + col3W, y + infoH);

        // Satır 1
        g.DrawString(" İşletme Adı:", fLabel, bDark, new RectangleF(x + 2, y + 1, col1W - 4, rowH - 2), sfLeft);
        g.DrawString(enterprise.Name, fValueBold, bDark, new RectangleF(x + col1W + 4, y + 1, col2W - 8, rowH - 2), sfLeft);
        g.DrawString(" Vergi No:", fLabel, bDark, new RectangleF(x + col1W + col2W + 2, y + 1, col3W - 4, rowH - 2), sfLeft);
        string tax = string.IsNullOrWhiteSpace(enterprise.TaxNumber) ? "-" : enterprise.TaxNumber;
        g.DrawString(tax, fValue, bDark, new RectangleF(x + col1W + col2W + col3W + 4, y + 1, col4W - 8, rowH - 2), sfLeft);

        // Satır 2
        g.DrawString(" Adres / Telefon:", fLabel, bDark, new RectangleF(x + 2, y + rowH + 1, col1W - 4, rowH - 2), sfLeft);
        string addrPhone = $"{enterprise.Address ?? "-"} / Tel: {enterprise.Phone ?? "-"}";
        g.DrawString(addrPhone, fValue, bDark, new RectangleF(x + col1W + 4, y + rowH + 1, col2W - 8, rowH - 2), sfLeft);
        g.DrawString(" Ziyaret Tarihleri:", fLabel, bDark, new RectangleF(x + col1W + col2W + 2, y + rowH + 1, col3W - 4, rowH - 2), sfLeft);
        string visitDatesCompact = FormatVisitDates(enterprise, teacher, students, month);
        g.DrawString(visitDatesCompact, fValueBold, bBlue, new RectangleF(x + col1W + col2W + col3W + 4, y + rowH + 1, col4W - 8, rowH - 2), sfLeft);

        // Satır 3
        g.DrawString(" Koordinatör Öğr:", fLabel, bDark, new RectangleF(x + 2, y + (rowH * 2) + 1, col1W - 4, rowH - 2), sfLeft);
        string teacherText = teacher != null ? $"{teacher.FullName} ({teacher.Branch})" : (enterprise.TeacherName ?? "-");
        g.DrawString(teacherText, fValueBold, bDark, new RectangleF(x + col1W + 4, y + (rowH * 2) + 1, col2W - 8, rowH - 2), sfLeft);
        g.DrawString(" Usta Öğretici:", fLabel, bDark, new RectangleF(x + col1W + col2W + 2, y + (rowH * 2) + 1, col3W - 4, rowH - 2), sfLeft);
        string masterTeacher = string.IsNullOrWhiteSpace(enterprise.ContactPerson) ? "İşletme Yetkilisi" : enterprise.ContactPerson;
        g.DrawString(masterTeacher, fValue, bDark, new RectangleF(x + col1W + col2W + col3W + 4, y + (rowH * 2) + 1, col4W - 8, rowH - 2), sfLeft);

        y += infoH + 8;

        // 3. Kriterler Tablosu (BÖLÜM 1)
        g.FillRectangle(bSectionBg, x, y, w, 15);
        g.DrawRectangle(pDark, x, y, w, 15);
        g.DrawString("BÖLÜM 1: AYLIK REHBERLİK VE DENETİM KRİTERLERİ", fSecTitle, bBlue, new RectangleF(x + 4, y + 1, w - 8, 13), sfLeft);
        y += 15;

        // Sütun genişlikleri toplamı tam w olmalı (24 + (w - 238) + 100 + 114 = w)
        int[] crCols = [24, w - 238, 100, 114];
        string[] crHeaders = ["No", "Denetim ve Rehberlik Konusu (MEB 3308)", "Değerlendirme", "Açıklama"];
        g.FillRectangle(bGrayBg, x, y, w, 16);
        g.DrawRectangle(pDark, x, y, w, 16);
        int curX = x;
        for (int i = 0; i < crCols.Length; i++)
        {
            g.DrawString(crHeaders[i], fLabel, bDark, new RectangleF(curX, y, crCols[i], 16), sfCenter);
            if (i < crCols.Length - 1) g.DrawLine(pThin, curX + crCols[i], y, curX + crCols[i], y + 16);
            curX += crCols[i];
        }
        y += 16;

        var criteria = new (string text, string result, string desc)[]
        {
            ("Usta öğretici / eğitici personelin yıllık eğitim planı (Gelişim Tablosu) var mı? Uyguluyor ve rotasyona uyuyor mu?", "[ X ] Uygun", "Gelişim tablosu var"),
            ("Öğrencilerin günlük çalışmaları yıllık çalışma takvimine ve eğitim planına uygun olarak planlanmış mı?", "[ X ] Uygun", "Planlandı / Evet"),
            ("Öğrenci devam/devamsızlık durumu günlük olarak takip edilip zamanında bildiriliyor mu?", "[ X ] Uygun", "Takip ediliyor / Evet"),
            ("Mesleki eğitim çalışmaları ve pratik temrin uygulamaları düzenli puanla değerlendiriliyor mu?", "[ X ] Uygun", "Değerlendiriliyor"),
            ("Yapılan işlerle ilgili olarak her öğrenciye iş/staj dosyası tutturuluyor ve paraflanıyor mu?", "[ X ] Uygun", "İş dosyası tutuluyor"),
            ("Öğrencilere 3308 sayılı Kanunun 25. maddesine göre aylık ücretleri banka kanalıyla ödeniyor mu?", "[ X ] Uygun", "Ücret ödeniyor / Evet"),
            ("Mesleki eğitim çalışma saatlerinde ve mevzuata uygun dinlenme sürelerine uyularak yapılıyor mu?", "[ X ] Uygun", "Mevzuata uygun"),
            ("İş sağlığı ve güvenliği konusunda yeterli bilgi verilmiş, gerekli koruyucu tedbirler alınmış mı?", "[ X ] Uygun", "İSG tedbirleri tam"),
            ("Öğrenciler işyeri disiplin kurallarına, çalışma düzenine ve kılık-kıyafet esaslarına uyuyor mu?", "[ X ] Uygun", "Kurallara uyuluyor"),
            ("İşletmenin meslek eğitimi ile görevli personelinin usta öğreticilik belgesi var mı?", "[ X ] Uygun", "Belgesi var / Evet")
        };

        int crRowH = 17;
        for (int i = 0; i < criteria.Length; i++)
        {
            if (i % 2 == 1) g.FillRectangle(bAltBg, x, y, w, crRowH);
            curX = x;

            g.DrawString((i + 1).ToString(), fValue, bDark, new RectangleF(curX, y + 1, crCols[0], crRowH - 2), sfCenter);
            curX += crCols[0];
            g.DrawLine(pThin, curX, y, curX, y + crRowH);

            g.DrawString(criteria[i].text, fSmall, bDark, new RectangleF(curX + 3, y + 1, crCols[1] - 6, crRowH - 2), sfLeft);
            curX += crCols[1];
            g.DrawLine(pThin, curX, y, curX, y + crRowH);

            g.DrawString(criteria[i].result, fValueBold, bGreen, new RectangleF(curX, y + 1, crCols[2], crRowH - 2), sfCenter);
            curX += crCols[2];
            g.DrawLine(pThin, curX, y, curX, y + crRowH);

            g.DrawString(criteria[i].desc, fSmall, bDark, new RectangleF(curX, y + 1, crCols[3], crRowH - 2), sfCenter);

            g.DrawLine(pThin, x, y + crRowH, x + w, y + crRowH);
            y += crRowH;
        }
        g.DrawRectangle(pDark, x, y - (criteria.Length * crRowH) - 16, w, (criteria.Length * crRowH) + 16);
        y += 8;

        // ── Alt Sınırlara Göre Dinamik Konumlandırma (A5 Yarısının En Altına Yanaştırma) ──
        int targetBottom = bounds.Bottom - 8;
        int footerH = 12;
        int footerY = targetBottom - footerH;
        int signH = 72;
        int signY = footerY - 6 - signH;

        // 4. Görüşler Kutusu (BÖLÜM 2)
        int noteH = 44;
        int noteY = signY - 8 - noteH;
        int sec2TitleY = noteY - 14;

        g.FillRectangle(bSectionBg, x, sec2TitleY, w, 14);
        g.DrawRectangle(pDark, x, sec2TitleY, w, 14);
        g.DrawString("BÖLÜM 2: KOORDİNATÖR ÖĞRETMENİN AYLIK TESPİT, REHBERLİK VE GÖRÜŞLERİ", fSecTitle, bBlue, new RectangleF(x + 4, sec2TitleY + 1, w - 8, 12), sfLeft);

        g.FillRectangle(bNoteBg, x, noteY, w, noteH);
        g.DrawRectangle(pDark, x, noteY, w, noteH);
        string noteText = "İşletmede yapılan aylık koordinatörlük, rehberlik ve denetim ziyaretinde; öğrencilerin işyeri kurallarına ve İSG talimatlarına tam uyum sağladığı, gelişim tablosuna ve staj takvimine uygun şekilde eğitimlerini aksatmadan sürdürdükleri tespit edilmiştir. Öğretim programının uygulanmasında herhangi bir aksaklık bulunmamaktadır.";
        g.DrawString(noteText, fSmall, bDark, new RectangleF(x + 6, noteY + 3, w - 12, noteH - 6), sfLeft);

        // 5. İmza Bloğu (A5 Yarısının Altına Sabitlenmiş)
        y = signY;
        int signSpacing = 20;
        int signColW = (w - signSpacing) / 2;

        int signX1 = x;
        g.DrawRectangle(pDark, signX1, y, signColW, signH);
        g.DrawString("İŞLETME YETKİLİSİ / USTA ÖĞRETİCİ", fLabel, bDark, new RectangleF(signX1, y + 4, signColW, 13), sfCenter);
        g.DrawString(masterTeacher, fValueBold, bDark, new RectangleF(signX1, y + 18, signColW, 13), sfCenter);
        g.DrawString("Kaşe / Islak İmza", fSmallItalic, bMuted, new RectangleF(signX1, y + 34, signColW, 12), sfCenter);
        g.DrawString($"Tarih: ...../...../{month.Year}", fSmall, bDark, new RectangleF(signX1, y + signH - 16, signColW, 14), sfCenter);

        int signX2 = x + w - signColW;
        g.DrawRectangle(pDark, signX2, y, signColW, signH);
        g.DrawString("KOORDİNATÖR ÖĞRETMEN", fLabel, bDark, new RectangleF(signX2, y + 4, signColW, 13), sfCenter);
        g.DrawString(teacher?.FullName ?? enterprise.TeacherName ?? "-", fValueBold, bDark, new RectangleF(signX2, y + 18, signColW, 13), sfCenter);
        g.DrawString("İmza", fSmallItalic, bMuted, new RectangleF(signX2, y + 34, signColW, 12), sfCenter);
        g.DrawString($"Tarih: ...../...../{month.Year}", fSmall, bDark, new RectangleF(signX2, y + signH - 16, signColW, 14), sfCenter);

        // 6. Sayfa Alt Bilgisi (En Altta)
        y = footerY;
        g.DrawString("3308 Sayılı Mesleki Eğitim Kanunu uyarınca 2 nüsha tanzim edilir.", fSmallItalic, bMuted, new RectangleF(x, y, w, 12), sfCenter);
    }

    /// <summary>
    /// Renders the official MEB Ek-1 Monthly Enterprise Visit and Guidance Report form onto the graphics context (Full Page).
    /// </summary>
    public static void DrawEk1Page(
        Graphics g,
        Rectangle bounds,
        Enterprise enterprise,
        Teacher? teacher,
        List<Student> students,
        AppSettings settings,
        DateTime month,
        Dictionary<int, double> absentDaysMap)
    {
        int x = bounds.X + 36;
        int w = bounds.Width - 72; // ~722px
        int y = bounds.Y + 28;

        using var fSchool = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var fHeaderSub = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var fDocTitle = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        using var fSecTitle = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var fLabel = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var fValue = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var fValueBold = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var fSmall = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var fSmallItalic = new Font("Segoe UI", 7.5f, FontStyle.Italic);

        using var bDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bBlue = new SolidBrush(Color.FromArgb(30, 58, 138));
        using var bMuted = new SolidBrush(Color.FromArgb(71, 85, 105));
        using var bGrayBg = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var bSectionBg = new SolidBrush(Color.FromArgb(239, 246, 255)); // light blue-50
        using var bAltBg = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var bNoteBg = new SolidBrush(Color.FromArgb(250, 250, 250));
        using var bGreen = new SolidBrush(Color.FromArgb(5, 150, 105));

        using var pThin = new Pen(Color.FromArgb(203, 213, 225), 1);
        using var pDark = new Pen(Color.FromArgb(15, 23, 42), 1.2f);
        using var pNavy = new Pen(Color.FromArgb(30, 58, 138), 2f);

        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        using var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        // ── 1. ANTETLİ BAŞLIK ──────────────────────────────────────────────────────────
        g.DrawString("T.C.", fHeaderSub, bDark, new RectangleF(x, y, w, 15), sfCenter);
        y += 15;
        string gov = !string.IsNullOrWhiteSpace(settings.Valilik) ? settings.Valilik.ToUpper(new CultureInfo("tr-TR")) : "MİLLÎ EĞİTİM BAKANLIĞI";
        g.DrawString(gov, fHeaderSub, bDark, new RectangleF(x, y, w, 15), sfCenter);
        y += 15;
        if (!string.IsNullOrWhiteSpace(settings.DistrictMem))
        {
            g.DrawString(settings.DistrictMem.ToUpper(new CultureInfo("tr-TR")), fHeaderSub, bDark, new RectangleF(x, y, w, 15), sfCenter);
            y += 15;
        }
        string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MESLEKİ VE TEKNİK ANADOLU LİSESİ MÜDÜRLÜĞÜ" : settings.SchoolName;
        g.DrawString(schoolName, fSchool, bDark, new RectangleF(x, y, w, 18), sfCenter);
        y += 18;
        g.DrawString("Mesleki ve Teknik Eğitim Koordinatörlüğü", fHeaderSub, bMuted, new RectangleF(x, y, w, 15), sfCenter);
        y += 16;

        g.DrawLine(pNavy, x, y, x + w, y);
        y += 4;

        g.DrawString("İŞLETMELERDE MESLEKİ EĞİTİM / STAJ AYLIK REHBERLİK VE DENETİM FORMU (EK-1)", fDocTitle, bBlue, new RectangleF(x, y, w, 22), sfCenter);
        y += 22;
        g.DrawString($"Denetim / Ziyaret Dönemi: {month:MMMM yyyy} | 3308 Sayılı Mesleki Eğitim Kanunu Kapsamında", fSmallItalic, bMuted, new RectangleF(x, y, w, 14), sfCenter);
        y += 18;

        // ── 2. İŞLETME VE GÖREV BİLGİLERİ TABLOSU ────────────────────────────────────
        int infoH = 80;
        g.FillRectangle(bGrayBg, x, y, w, infoH);
        g.DrawRectangle(pDark, x, y, w, infoH);

        int rowH = 20;
        int col1W = 125, col2W = 240, col3W = 130;
        int col4W = w - (col1W + col2W + col3W);

        // Satır çizgileri
        for (int i = 1; i < 4; i++)
        {
            g.DrawLine(pThin, x, y + (i * rowH), x + w, y + (i * rowH));
        }
        // Sütun dikey çizgileri
        g.DrawLine(pThin, x + col1W, y, x + col1W, y + infoH);
        g.DrawLine(pThin, x + col1W + col2W, y, x + col1W + col2W, y + infoH);
        g.DrawLine(pThin, x + col1W + col2W + col3W, y, x + col1W + col2W + col3W, y + infoH);

        // Satır 1
        g.DrawString(" İşletme Adı / Unvanı:", fLabel, bDark, new RectangleF(x + 2, y + 2, col1W - 4, rowH - 4), sfLeft);
        g.DrawString(enterprise.Name, fValueBold, bDark, new RectangleF(x + col1W + 4, y + 2, col2W - 8, rowH - 4), sfLeft);
        g.DrawString(" Vergi No / Dairesi:", fLabel, bDark, new RectangleF(x + col1W + col2W + 2, y + 2, col3W - 4, rowH - 4), sfLeft);
        string tax = string.IsNullOrWhiteSpace(enterprise.TaxNumber) ? "-" : enterprise.TaxNumber;
        g.DrawString(tax, fValue, bDark, new RectangleF(x + col1W + col2W + col3W + 4, y + 2, col4W - 8, rowH - 4), sfLeft);

        // Satır 2
        g.DrawString(" İşletme Adresi:", fLabel, bDark, new RectangleF(x + 2, y + rowH + 2, col1W - 4, rowH - 4), sfLeft);
        g.DrawString(enterprise.Address ?? "-", fValue, bDark, new RectangleF(x + col1W + 4, y + rowH + 2, col2W - 8, rowH - 4), sfLeft);
        g.DrawString(" Telefon / İletişim:", fLabel, bDark, new RectangleF(x + col1W + col2W + 2, y + rowH + 2, col3W - 4, rowH - 4), sfLeft);
        g.DrawString(enterprise.Phone ?? "-", fValue, bDark, new RectangleF(x + col1W + col2W + col3W + 4, y + rowH + 2, col4W - 8, rowH - 4), sfLeft);

        // Satır 3
        g.DrawString(" Koordinatör Öğretmen:", fLabel, bDark, new RectangleF(x + 2, y + (rowH * 2) + 2, col1W - 4, rowH - 4), sfLeft);
        string teacherText = teacher != null ? $"{teacher.FullName} ({teacher.Branch})" : (enterprise.TeacherName ?? "-");
        g.DrawString(teacherText, fValueBold, bDark, new RectangleF(x + col1W + 4, y + (rowH * 2) + 2, col2W - 8, rowH - 4), sfLeft);
        g.DrawString(" Eğitici Personel / Usta Öğr:", fLabel, bDark, new RectangleF(x + col1W + col2W + 2, y + (rowH * 2) + 2, col3W - 4, rowH - 4), sfLeft);
        string masterTeacher = string.IsNullOrWhiteSpace(enterprise.ContactPerson) ? "İşletme Yetkilisi" : enterprise.ContactPerson;
        g.DrawString(masterTeacher, fValue, bDark, new RectangleF(x + col1W + col2W + col3W + 4, y + (rowH * 2) + 2, col4W - 8, rowH - 4), sfLeft);

        // Satır 4
        g.DrawString(" Denetim Ayı / Yıl:", fLabel, bDark, new RectangleF(x + 2, y + (rowH * 3) + 2, col1W - 4, rowH - 4), sfLeft);
        g.DrawString(month.ToString("MMMM yyyy"), fValue, bDark, new RectangleF(x + col1W + 4, y + (rowH * 3) + 2, col2W - 8, rowH - 4), sfLeft);
        g.DrawString(" Ziyaret Tarihleri:", fLabel, bDark, new RectangleF(x + col1W + col2W + 2, y + (rowH * 3) + 2, col3W - 4, rowH - 4), sfLeft);
        string visitDates = FormatVisitDates(enterprise, teacher, students, month);
        g.DrawString(visitDates, fValueBold, bBlue, new RectangleF(x + col1W + col2W + col3W + 4, y + (rowH * 3) + 2, col4W - 8, rowH - 4), sfLeft);

        y += infoH + 10;

        // ── 3. REHBERLİK VE DENETİM KONTROL LİSTESİ (BÖLÜM 1) ──────────────────────
        g.FillRectangle(bSectionBg, x, y, w, 20);
        g.DrawRectangle(pDark, x, y, w, 20);
        g.DrawString("BÖLÜM 1: AYLIK REHBERLİK VE DENETİM KRİTERLERİ", fSecTitle, bBlue, new RectangleF(x + 6, y + 2, w - 12, 16), sfLeft);
        y += 20;

        int[] crCols = [28, w - 248, 110, 110]; // Toplam w = 722px
        string[] crHeaders = ["No", "Denetim ve Rehberlik Konusu (MEB 3308 Standart Maddeleri)", "Değerlendirme", "Açıklama"];

        int crHeaderH = 22;
        g.FillRectangle(bGrayBg, x, y, w, crHeaderH);
        g.DrawRectangle(pDark, x, y, w, crHeaderH);
        int curX = x;
        for (int i = 0; i < crCols.Length; i++)
        {
            var rH = new RectangleF(curX, y, crCols[i], crHeaderH);
            g.DrawString(crHeaders[i], fLabel, bDark, rH, sfCenter);
            if (i < crCols.Length - 1) g.DrawLine(pThin, curX + crCols[i], y, curX + crCols[i], y + crHeaderH);
            curX += crCols[i];
        }
        y += crHeaderH;

        var criteria = new (string text, string result, string desc)[]
        {
            ("Usta öğretici / eğitici personelin yıllık eğitim planı (Gelişim Tablosu) var mı? Uyguluyor ve rotasyona uyuyor mu?", "[ X ] Uygun", "Gelişim tablosu var"),
            ("Öğrencilerin günlük çalışmaları yıllık çalışma takvimine ve eğitim planına uygun olarak planlanmış mı?", "[ X ] Uygun", "Planlandı / Evet"),
            ("Öğrenci devam/devamsızlık durumu günlük olarak takip edilip okul müdürlüğüne zamanında bildiriliyor mu?", "[ X ] Uygun", "Takip ediliyor / Evet"),
            ("Mesleki eğitim çalışmaları ve pratik temrin uygulamaları düzenli olarak puanla değerlendiriliyor mu?", "[ X ] Uygun", "Değerlendiriliyor"),
            ("Yapılan işlerle ilgili olarak her öğrenciye iş/staj dosyası tutturuluyor ve düzenli paraflanıyor mu?", "[ X ] Uygun", "İş dosyası tutuluyor"),
            ("Öğrencilere 3308 sayılı Kanunun 25. maddesine göre aylık net asgari ücret payı banka kanalıyla ödeniyor mu?", "[ X ] Uygun", "Ücret ödeniyor / Evet"),
            ("Mesleki eğitim çalışma saatlerinde ve mevzuata uygun dinlenme sürelerine riayet edilerek yapılıyor mu?", "[ X ] Uygun", "Mevzuata uygun"),
            ("İş sağlığı ve güvenliği konusunda yeterli bilgi verilmiş, gerekli koruyucu donanım ve tedbirler alınmış mı?", "[ X ] Uygun", "İSG tedbirleri tam"),
            ("Öğrenciler işyeri disiplin kurallarına, çalışma düzenine ve kılık-kıyafet esaslarına uyuyor mu?", "[ X ] Uygun", "Kurallara uyuluyor"),
            ("İşletmenin meslek eğitimi ile görevli personelinin usta öğreticilik / eğitici personel belgesi var mı?", "[ X ] Uygun", "Usta öğretici belgesi var"),
            ("Eğitici personelin sorumlu olduğu öğrenci grubu sayısı mevzuata (Yönetmelik Madde 192) uygun mu?", "[ X ] Uygun", "Mevzuata uygun"),
            ("Okul, öğretim programlarını (Gelişim Tablosu) işletmeye verdi ve işletmece uygulanıyor mu?", "[ X ] Uygun", "Teslim edildi / Evet")
        };

        // ── Alt Sınırlara Göre Dinamik Konumlandırma (İmzaları En Alta Yanaştırma) ──
        int targetBottom = bounds.Bottom - 30;
        int footerH = 15;
        int footerY = targetBottom - footerH;
        int signH = 110;
        int signY = footerY - 14 - signH;

        int crRowH = 26;
        for (int i = 0; i < criteria.Length; i++)
        {
            if (i % 2 == 1) g.FillRectangle(bAltBg, x, y, w, crRowH);

            curX = x;
            // No (Tek sıra numarası, çiftleme kaldırıldı)
            g.DrawString((i + 1).ToString(), fValue, bDark, new RectangleF(curX, y + 3, crCols[0], crRowH - 6), sfCenter);
            curX += crCols[0];
            g.DrawLine(pThin, curX, y, curX, y + crRowH);

            // Madde
            g.DrawString(criteria[i].text, fValue, bDark, new RectangleF(curX + 6, y + 3, crCols[1] - 12, crRowH - 6), sfLeft);
            curX += crCols[1];
            g.DrawLine(pThin, curX, y, curX, y + crRowH);

            // Değerlendirme
            g.DrawString(criteria[i].result, fValueBold, bGreen, new RectangleF(curX, y + 3, crCols[2], crRowH - 6), sfCenter);
            curX += crCols[2];
            g.DrawLine(pThin, curX, y, curX, y + crRowH);

            // Açıklama
            g.DrawString(criteria[i].desc, fSmall, bDark, new RectangleF(curX, y + 3, crCols[3], crRowH - 6), sfCenter);

            g.DrawLine(pThin, x, y + crRowH, x + w, y + crRowH);
            y += crRowH;
        }

        g.DrawRectangle(pDark, x, y - (criteria.Length * crRowH) - crHeaderH, w, (criteria.Length * crRowH) + crHeaderH);
        y += 12;

        // ── 4. KOORDİNATÖR ÖĞRETMENİN AYLIK TESPİT VE GÖRÜŞLERİ (BÖLÜM 2) ───────────
        g.FillRectangle(bSectionBg, x, y, w, 20);
        g.DrawRectangle(pDark, x, y, w, 20);
        g.DrawString("BÖLÜM 2: KOORDİNATÖR ÖĞRETMENİN AYLIK TESPİT, REHBERLİK VE GÖRÜŞLERİ", fSecTitle, bBlue, new RectangleF(x + 6, y + 2, w - 12, 16), sfLeft);
        y += 20;

        int noteH = Math.Max(75, (signY - 16) - y);
        g.FillRectangle(bNoteBg, x, y, w, noteH);
        g.DrawRectangle(pDark, x, y, w, noteH);

        string noteText =
            "İşletmede yapılan aylık koordinatörlük, denetim ve rehberlik ziyaretinde; öğrencilerin işyeri çalışma kurallarına ve İSG talimatlarına tam uyum sağladığı, " +
            "alan/dal mesleki eğitim programına uygun olarak usta öğretici gözetiminde pratik eğitimlerini aksatmadan sürdürdükleri tespit edilmiştir. " +
            "Öğretim programının ve gelişim tablolarının uygulanması ile ilgili herhangi bir aksaklık bulunmamaktadır.";

        using var fNote = new Font("Segoe UI", 9f, FontStyle.Regular);
        g.DrawString(noteText, fNote, bDark, new RectangleF(x + 10, y + 8, w - 20, noteH - 16), sfLeft);

        // ── 5. İMZA VE ONAY BLOĞU (Sayfanın Altına Sabitlenmiş) ───
        y = signY;
        int signSpacing = 40;
        int signColW = (w - signSpacing) / 2; // (722 - 40) / 2 = 341px

        // 1. İşletme Yetkilisi
        int signX1 = x;
        g.DrawRectangle(pDark, signX1, y, signColW, signH);
        g.DrawString("İŞLETME YETKİLİSİ / EĞİTİCİ PERSONEL", fLabel, bDark, new RectangleF(signX1, y + 8, signColW, 16), sfCenter);
        g.DrawString(masterTeacher, fValueBold, bDark, new RectangleF(signX1, y + 28, signColW, 18), sfCenter);
        g.DrawString("Kaşe / Islak İmza", fSmallItalic, bMuted, new RectangleF(signX1, y + 48, signColW, 14), sfCenter);
        g.DrawString($"Tarih: ..... / ..... / {month.Year}", fSmall, bDark, new RectangleF(signX1, y + signH - 24, signColW, 16), sfCenter);

        // 2. Koordinatör Öğretmen
        int signX2 = x + w - signColW;
        g.DrawRectangle(pDark, signX2, y, signColW, signH);
        g.DrawString("KOORDİNATÖR ÖĞRETMEN", fLabel, bDark, new RectangleF(signX2, y + 8, signColW, 16), sfCenter);
        g.DrawString(teacher?.FullName ?? enterprise.TeacherName ?? "-", fValueBold, bDark, new RectangleF(signX2, y + 28, signColW, 18), sfCenter);
        g.DrawString(teacher?.Branch ?? (!string.IsNullOrWhiteSpace(enterprise.TeacherName) ? "Koordinatör Öğretmen" : "-"), fSmall, bDark, new RectangleF(signX2, y + 48, signColW, 14), sfCenter);
        g.DrawString("İmza", fSmallItalic, bMuted, new RectangleF(signX2, y + 64, signColW, 14), sfCenter);
        g.DrawString($"Tarih: ..... / ..... / {month.Year}", fSmall, bDark, new RectangleF(signX2, y + signH - 24, signColW, 16), sfCenter);

        // ── 6. SAYFA ALT BİLGİSİ (En Altta) ─────────────────────────────────────────────────────
        y = footerY;
        g.DrawString("3308 Sayılı Mesleki Eğitim Kanunu ve Millî Eğitim Bakanlığı Ortaöğretim Kurumları Yönetmeliği uyarınca 2 nüsha tanzim edilir.", fSmallItalic, bMuted, new RectangleF(x, y, w, 15), sfCenter);
    }

    public void SelectDocument(int index)
    {
        if (_cmbDocType != null && index >= 0 && index < _cmbDocType.Items.Count)
        {
            _cmbDocType.SelectedIndex = index;
            ApplyDocTypeSettings(index);
        }
    }

    public static string NumberToTurkishWords(int number)
    {
        if (number <= 0) return "SIFIR";
        if (number > 99) return number.ToString();

        string[] ones = ["", "BİR", "İKİ", "ÜÇ", "DÖRT", "BEŞ", "ALTI", "YEDİ", "SEKİZ", "DOKUZ"];
        string[] tens = ["", "ON", "YİRMİ", "OTUZ", "KIRK", "ELLİ", "ALTMIŞ", "YETMİŞ", "SEKSEN", "DOKSAN"];

        int t = number / 10;
        int o = number % 10;
        string result = (tens[t] + " " + ones[o]).Trim();
        return string.IsNullOrWhiteSpace(result) ? number.ToString() : result;
    }

    /// <summary>
    /// Renders the official MEB Daily Guidance Task Form (İşletmelerde Meslek Eğitimi Günlük Rehberlik Görev Formu) onto graphics context.
    /// Matches the official MEB format with enterprise info, 3 guidance evaluation points with ruled lines, and 3-signatory block.
    /// </summary>
    public static void DrawDailyGuidancePage(
        Graphics g,
        Rectangle bounds,
        Enterprise enterprise,
        Teacher? teacher,
        List<Student> students,
        AppSettings settings,
        DateTime dutyDate)
    {
        int x = bounds.X + 36;
        int w = bounds.Width - 72; // ~722px
        int y = bounds.Y + 28;

        using var fHeaderSub = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var fSchool = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var fMainTitle = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        using var fDocTitle = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        using var fSecTitle = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var fLabel = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var fValue = new Font("Segoe UI", 9f, FontStyle.Regular);
        using var fValueBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var fSmall = new Font("Segoe UI", 8f, FontStyle.Regular);
        using var fSmallBold = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var fSmallItalic = new Font("Segoe UI", 7.5f, FontStyle.Italic);

        using var bDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bBlue = new SolidBrush(Color.FromArgb(30, 58, 138));
        using var bMuted = new SolidBrush(Color.FromArgb(71, 85, 105));
        using var bGrayBg = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var bSectionBg = new SolidBrush(Color.FromArgb(239, 246, 255));
        using var bAltBg = new SolidBrush(Color.FromArgb(248, 250, 252));

        using var pThin = new Pen(Color.FromArgb(203, 213, 225), 1f);
        using var pDark = new Pen(Color.FromArgb(15, 23, 42), 1.2f);
        using var pNavy = new Pen(Color.FromArgb(30, 58, 138), 2f);

        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        using var sfLeftTop = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };

        // 1. Antetli Başlık
        g.DrawString("T.C.", fHeaderSub, bDark, new RectangleF(x, y, w, 14), sfCenter);
        y += 14;
        string gov = !string.IsNullOrWhiteSpace(settings.Valilik) ? settings.Valilik.ToUpper(new CultureInfo("tr-TR")) : "MİLLÎ EĞİTİM BAKANLIĞI";
        g.DrawString(gov, fHeaderSub, bDark, new RectangleF(x, y, w, 14), sfCenter);
        y += 14;
        if (!string.IsNullOrWhiteSpace(settings.DistrictMem))
        {
            g.DrawString(settings.DistrictMem.ToUpper(new CultureInfo("tr-TR")), fHeaderSub, bDark, new RectangleF(x, y, w, 14), sfCenter);
            y += 14;
        }
        string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MESLEKİ VE TEKNİK ANADOLU LİSESİ MÜDÜRLÜĞÜ" : settings.SchoolName;
        g.DrawString(schoolName, fSchool, bDark, new RectangleF(x, y, w, 18), sfCenter);
        y += 18;

        g.DrawLine(pNavy, x, y, x + w, y);
        y += 6;

        g.DrawString("İŞLETMELERDE MESLEK EĞİTİMİ", fMainTitle, bBlue, new RectangleF(x, y, w, 18), sfCenter);
        y += 18;
        g.DrawString("GÜNLÜK REHBERLİK GÖREV FORMU", fDocTitle, bDark, new RectangleF(x, y, w, 22), sfCenter);
        y += 24;

        // 2. İşletme ve Görev Bilgileri Tablosu (5 Satır)
        int infoH = 135; // 5 satır x 27px
        int rowH = 27;
        int leftColW = 280;
        int rightColW = w - leftColW;

        g.FillRectangle(bGrayBg, x, y, leftColW, infoH);
        g.FillRectangle(bAltBg, x + leftColW, y, rightColW, infoH);
        g.DrawRectangle(pDark, x, y, w, infoH);

        for (int i = 1; i < 5; i++)
        {
            g.DrawLine(pThin, x, y + (i * rowH), x + w, y + (i * rowH));
        }
        g.DrawLine(pDark, x + leftColW, y, x + leftColW, y + infoH);

        var firstStudent = students.FirstOrDefault();
        string fieldName = firstStudent != null && !string.IsNullOrWhiteSpace(firstStudent.FieldName)
            ? (!string.IsNullOrWhiteSpace(firstStudent.Classroom) ? $"{firstStudent.Classroom} {firstStudent.FieldName}" : firstStudent.FieldName)
            : (!string.IsNullOrWhiteSpace(enterprise.Sector) ? enterprise.Sector : "Mesleki Eğitim");

        string branchName = !string.IsNullOrWhiteSpace(enterprise.Sector)
            ? enterprise.Sector
            : (firstStudent?.FieldName ?? "Genel");

        int stCount = students.Count;
        string stCountStr = $"{stCount} ({NumberToTurkishWords(stCount)})";

        (string lbl, string val)[] infoRows = [
            ("İşletmenin Adı", enterprise.Name),
            ("İzlemede Sorumlu Olduğu Öğrenci Sayısı", stCountStr),
            ("Meslek Alanı", fieldName),
            ("Meslek Dalı", branchName),
            ("Görev Tarihi", dutyDate.ToString("dd.MM.yyyy"))
        ];

        for (int i = 0; i < infoRows.Length; i++)
        {
            int rowY = y + (i * rowH);
            g.DrawString(infoRows[i].lbl, fLabel, bDark, new RectangleF(x + 10, rowY, leftColW - 14, rowH), sfLeft);
            g.DrawString(infoRows[i].val, (i == 0 || i == 4) ? fValueBold : fValue, (i == 4 ? bBlue : bDark), new RectangleF(x + leftColW + 10, rowY, rightColW - 14, rowH), sfLeft);
        }

        y += infoH + 12;

        // 3. Alt Sınırlara Göre Dinamik Hesaplama (İmza ve Açıklamaları En Alta Yanaştırma)
        int targetBottom = bounds.Bottom - 32;
        int noteH = 65;
        int noteY = targetBottom - noteH;
        int signH = 100;
        int signY = noteY - 12 - signH;

        // 3. Aylık Rehberlik Formuna Göre : (3 Madde + Yazma Çizgileri - Kalan Boşluğa Göre Ferahlatılmış)
        int sec3StartY = y;
        int availableSec3H = (signY - 14) - sec3StartY;
        int secHeaderH = 26;
        int itemH = Math.Max(92, (availableSec3H - secHeaderH) / 3);
        int table2H = secHeaderH + (itemH * 3);

        g.FillRectangle(bSectionBg, x, y, w, secHeaderH);
        g.DrawRectangle(pDark, x, y, w, table2H);
        g.DrawString("Aylık Rehberlik Formuna Göre :", fSecTitle, bBlue, new RectangleF(x + 10, y + 2, w - 20, secHeaderH - 4), sfLeft);
        y += secHeaderH;

        string[] questions = [
            "İşletmede öğrenim gören öğrencilerin eğitimini olumsuz yönde etkileyen hususlar varsa yazınız :",
            "Belirtilen aksaklıklarla ilgili yapılan rehberlik ve alınan önlemler :",
            "Aylık Rehberlik Formunda belirtilmesinde yarar görülen hususlar :"
        ];

        for (int i = 0; i < questions.Length; i++)
        {
            int qY = y + (i * itemH);

            // Soru başlığı
            g.DrawString(questions[i], fSmallBold, bDark, new RectangleF(x + 10, qY + 4, w - 20, 20), sfLeft);

            // Yazma / not satırları (Dinamik ve ferah satır aralıkları)
            int lineStart = qY + 28;
            int numLines = Math.Max(3, (itemH - 34) / 25);
            int lineSpacing = (itemH - 36) / numLines;
            for (int l = 0; l < numLines; l++)
            {
                int lineY = lineStart + (l * lineSpacing);
                g.DrawLine(pThin, x + 10, lineY, x + w - 10, lineY);
            }

            // Soru bloğu alt çizgisi (sonuncu hariç)
            if (i < questions.Length - 1)
            {
                g.DrawLine(pDark, x, qY + itemH, x + w, qY + itemH);
            }
        }

        // 4. İmza Bölümü (3 Sütun - Sayfanın Altına Sabitlenmiş)
        y = signY;
        int signColW = w / 3;
        int signColW3 = w - (signColW * 2);

        g.FillRectangle(bAltBg, x, y, w, signH);
        g.DrawRectangle(pDark, x, y, w, signH);
        g.DrawLine(pDark, x + signColW, y, x + signColW, y + signH);
        g.DrawLine(pDark, x + (signColW * 2), y, x + (signColW * 2), y + signH);

        // Sütun 1: İşletme Eğitim Yetkilisi
        int c1X = x;
        string contactPerson = string.IsNullOrWhiteSpace(enterprise.ContactPerson) ? "" : enterprise.ContactPerson;
        if (!string.IsNullOrWhiteSpace(contactPerson))
        {
            g.DrawString(contactPerson, fSmallBold, bDark, new RectangleF(c1X, y + 16, signColW, 16), sfCenter);
        }
        g.DrawString("Kaşe / Islak İmza", fSmallItalic, bMuted, new RectangleF(c1X, y + 40, signColW, 14), sfCenter);
        g.DrawString("İşletme Eğitim Yetkilisi", fLabel, bDark, new RectangleF(c1X, y + signH - 24, signColW, 18), sfCenter);

        // Sütun 2: Koordinatör Öğretmen
        int c2X = x + signColW;
        string teacherName = teacher?.FullName ?? enterprise.TeacherName ?? "-";
        g.DrawString(teacherName, fValueBold, bDark, new RectangleF(c2X, y + 16, signColW, 18), sfCenter);
        if (teacher != null && !string.IsNullOrWhiteSpace(teacher.Branch))
        {
            g.DrawString(teacher.Branch, fSmallItalic, bMuted, new RectangleF(c2X, y + 36, signColW, 14), sfCenter);
        }
        g.DrawString("İmza", fSmallItalic, bMuted, new RectangleF(c2X, y + 54, signColW, 14), sfCenter);
        g.DrawString("Koordinatör Öğretmen", fLabel, bDark, new RectangleF(c2X, y + signH - 24, signColW, 18), sfCenter);

        // Sütun 3: Koordinatör Müd. Yard.
        int c3X = x + (signColW * 2);
        string vpName = string.IsNullOrWhiteSpace(settings.VicePrincipalName) ? "Okul Yönetimi" : settings.VicePrincipalName;
        g.DrawString(vpName, fValueBold, bDark, new RectangleF(c3X, y + 16, signColW3, 18), sfCenter);
        g.DrawString("İmza / Mühür", fSmallItalic, bMuted, new RectangleF(c3X, y + 40, signColW3, 14), sfCenter);
        g.DrawString("Koordinatör Müd. Yard.", fLabel, bDark, new RectangleF(c3X, y + signH - 24, signColW3, 18), sfCenter);

        // 5. Açıklamalar Bölümü (En Altta)
        y = noteY;
        g.FillRectangle(bGrayBg, x, y, w, 20);
        g.DrawRectangle(pDark, x, y, w, noteH);
        g.DrawString("Açıklamalar :", fLabel, bDark, new RectangleF(x + 10, y + 2, w - 20, 16), sfLeft);

        string notes =
            "1- Bu form; koordinatör öğretmenin işletmeye yaptığı her rehberlik ve denetim görevinde işletme yetkilisi ile birlikte doldurulur ve paraflanır.\n" +
            "2- İşletmedeki eğitim sürecini olumsuz etkileyen bir durum varsa derhal gerekli rehberlik yapılır ve okul idaresine bilgi verilir.\n" +
            "3- Ay sonunda bu formlar esas alınarak Aylık Rehberlik ve Denetim Formu (Ek-1) tanzim edilir ve okul müdürlüğüne teslim edilir.";

        g.DrawString(notes, fSmall, bDark, new RectangleF(x + 10, y + 22, w - 20, noteH - 24), sfLeftTop);
    }

    /// <summary>
    /// Renders a compact A5-compatible Daily Guidance Form for 2-per-page saving mode.
    /// </summary>
    public static void DrawCompactDailyGuidancePage(
        Graphics g,
        Rectangle bounds,
        Enterprise enterprise,
        Teacher? teacher,
        List<Student> students,
        AppSettings settings,
        DateTime dutyDate)
    {
        int x = bounds.X + 32;
        int w = bounds.Width - 64;
        int y = bounds.Y + 8;

        using var fSchool = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var fDocTitle = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var fSecTitle = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var fLabel = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var fValue = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var fValueBold = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var fSmall = new Font("Segoe UI", 7f, FontStyle.Regular);
        using var fSmallBold = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var fSmallItalic = new Font("Segoe UI", 6.8f, FontStyle.Italic);

        using var bDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bBlue = new SolidBrush(Color.FromArgb(30, 58, 138));
        using var bMuted = new SolidBrush(Color.FromArgb(71, 85, 105));
        using var bGrayBg = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var bSectionBg = new SolidBrush(Color.FromArgb(239, 246, 255));
        using var bAltBg = new SolidBrush(Color.FromArgb(248, 250, 252));

        using var pThin = new Pen(Color.FromArgb(203, 213, 225), 1);
        using var pDark = new Pen(Color.FromArgb(15, 23, 42), 1f);
        using var pNavy = new Pen(Color.FromArgb(30, 58, 138), 1.5f);

        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        using var sfLeftTop = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };

        // 1. Antetli Başlık
        string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MESLEKİ VE TEKNİK ANADOLU LİSESİ MÜDÜRLÜĞÜ" : settings.SchoolName;
        string gov = !string.IsNullOrWhiteSpace(settings.Valilik) ? settings.Valilik.ToUpper(new CultureInfo("tr-TR")) : "MİLLÎ EĞİTİM BAKANLIĞI";
        string memShort = !string.IsNullOrWhiteSpace(settings.DistrictMem) ? $" - {settings.DistrictMem.ToUpper(new CultureInfo("tr-TR"))}" : "";
        g.DrawString($"T.C.  {gov}{memShort}  -  {schoolName}", fSchool, bDark, new RectangleF(x, y, w, 13), sfCenter);
        y += 13;
        g.DrawString("İŞLETMELERDE MESLEK EĞİTİMİ  -  GÜNLÜK REHBERLİK GÖREV FORMU", fDocTitle, bBlue, new RectangleF(x, y, w, 15), sfCenter);
        y += 16;
        g.DrawLine(pNavy, x, y, x + w, y);
        y += 4;

        // 2. İşletme ve Görev Bilgileri (5 Satır x 16px = 80px)
        int infoH = 80;
        int rowH = 16;
        int leftColW = 230;
        int rightColW = w - leftColW;

        g.FillRectangle(bGrayBg, x, y, leftColW, infoH);
        g.FillRectangle(bAltBg, x + leftColW, y, rightColW, infoH);
        g.DrawRectangle(pDark, x, y, w, infoH);

        for (int i = 1; i < 5; i++)
        {
            g.DrawLine(pThin, x, y + (i * rowH), x + w, y + (i * rowH));
        }
        g.DrawLine(pDark, x + leftColW, y, x + leftColW, y + infoH);

        var firstStudent = students.FirstOrDefault();
        string fieldName = firstStudent != null && !string.IsNullOrWhiteSpace(firstStudent.FieldName)
            ? (!string.IsNullOrWhiteSpace(firstStudent.Classroom) ? $"{firstStudent.Classroom} {firstStudent.FieldName}" : firstStudent.FieldName)
            : (!string.IsNullOrWhiteSpace(enterprise.Sector) ? enterprise.Sector : "Mesleki Eğitim");

        string branchName = !string.IsNullOrWhiteSpace(enterprise.Sector)
            ? enterprise.Sector
            : (firstStudent?.FieldName ?? "Genel");

        int stCount = students.Count;
        string stCountStr = $"{stCount} ({NumberToTurkishWords(stCount)})";

        (string lbl, string val)[] infoRows = [
            ("İşletmenin Adı", enterprise.Name),
            ("İzlemede Sorumlu Olduğu Öğrenci Sayısı", stCountStr),
            ("Meslek Alanı", fieldName),
            ("Meslek Dalı", branchName),
            ("Görev Tarihi", dutyDate.ToString("dd.MM.yyyy"))
        ];

        for (int i = 0; i < infoRows.Length; i++)
        {
            int rowY = y + (i * rowH);
            g.DrawString(infoRows[i].lbl, fLabel, bDark, new RectangleF(x + 6, rowY, leftColW - 10, rowH), sfLeft);
            g.DrawString(infoRows[i].val, (i == 0 || i == 4) ? fValueBold : fValue, (i == 4 ? bBlue : bDark), new RectangleF(x + leftColW + 6, rowY, rightColW - 10, rowH), sfLeft);
        }

        y += infoH + 6;

        // 3. Alt Sınırlara Göre Dinamik Hesaplama (İmza ve Açıklamaları A5 Yarısının En Altına Yanaştırma)
        int targetBottom = bounds.Bottom - 8;
        int noteH = 40;
        int noteY = targetBottom - noteH;
        int signH = 68;
        int signY = noteY - 6 - signH;

        // 3. Aylık Rehberlik Formuna Göre : (3 Madde + Çizgiler - Ferahlatılmış)
        int sec3StartY = y;
        int availableSec3H = (signY - 6) - sec3StartY;
        int secHeaderH = 16;
        int itemH = Math.Max(46, (availableSec3H - secHeaderH) / 3);
        int table2H = secHeaderH + (itemH * 3);

        g.FillRectangle(bSectionBg, x, y, w, secHeaderH);
        g.DrawRectangle(pDark, x, y, w, table2H);
        g.DrawString("Aylık Rehberlik Formuna Göre :", fSecTitle, bBlue, new RectangleF(x + 6, y + 1, w - 12, secHeaderH - 2), sfLeft);
        y += secHeaderH;

        string[] questions = [
            "İşletmede öğrenim gören öğrencilerin eğitimini olumsuz yönde etkileyen hususlar varsa yazınız :",
            "Belirtilen aksaklıklarla ilgili yapılan rehberlik ve alınan önlemler :",
            "Aylık Rehberlik Formunda belirtilmesinde yarar görülen hususlar :"
        ];

        for (int i = 0; i < questions.Length; i++)
        {
            int qY = y + (i * itemH);
            g.DrawString(questions[i], fSmallBold, bDark, new RectangleF(x + 6, qY + 2, w - 12, 14), sfLeft);

            int lineStart = qY + 20;
            int numLines = Math.Max(2, (itemH - 24) / 19);
            int lineSpacing = (itemH - 26) / numLines;
            for (int l = 0; l < numLines; l++)
            {
                int lineY = lineStart + (l * lineSpacing);
                g.DrawLine(pThin, x + 6, lineY, x + w - 6, lineY);
            }

            if (i < questions.Length - 1)
            {
                g.DrawLine(pDark, x, qY + itemH, x + w, qY + itemH);
            }
        }

        // 4. İmza Bölümü (3 Sütun - En Alta Yanaştırılmış)
        y = signY;
        int signColW = w / 3;
        int signColW3 = w - (signColW * 2);

        g.FillRectangle(bAltBg, x, y, w, signH);
        g.DrawRectangle(pDark, x, y, w, signH);
        g.DrawLine(pDark, x + signColW, y, x + signColW, y + signH);
        g.DrawLine(pDark, x + (signColW * 2), y, x + (signColW * 2), y + signH);

        // Sütun 1: İşletme Eğitim Yetkilisi
        int c1X = x;
        string contactPerson = string.IsNullOrWhiteSpace(enterprise.ContactPerson) ? "" : enterprise.ContactPerson;
        if (!string.IsNullOrWhiteSpace(contactPerson))
        {
            g.DrawString(contactPerson, fSmallBold, bDark, new RectangleF(c1X, y + 6, signColW, 12), sfCenter);
        }
        g.DrawString("Kaşe / Islak İmza", fSmallItalic, bMuted, new RectangleF(c1X, y + 22, signColW, 12), sfCenter);
        g.DrawString("İşletme Eğitim Yetkilisi", fLabel, bDark, new RectangleF(c1X, y + signH - 18, signColW, 14), sfCenter);

        // Sütun 2: Koordinatör Öğretmen
        int c2X = x + signColW;
        string teacherName = teacher?.FullName ?? enterprise.TeacherName ?? "-";
        g.DrawString(teacherName, fValueBold, bDark, new RectangleF(c2X, y + 6, signColW, 14), sfCenter);
        g.DrawString("İmza", fSmallItalic, bMuted, new RectangleF(c2X, y + 24, signColW, 12), sfCenter);
        g.DrawString("Koordinatör Öğretmen", fLabel, bDark, new RectangleF(c2X, y + signH - 18, signColW, 14), sfCenter);

        // Sütun 3: Koordinatör Müd. Yard.
        int c3X = x + (signColW * 2);
        string vpName = string.IsNullOrWhiteSpace(settings.VicePrincipalName) ? "Okul Yönetimi" : settings.VicePrincipalName;
        g.DrawString(vpName, fValueBold, bDark, new RectangleF(c3X, y + 6, signColW3, 14), sfCenter);
        g.DrawString("İmza / Mühür", fSmallItalic, bMuted, new RectangleF(c3X, y + 24, signColW3, 12), sfCenter);
        g.DrawString("Koordinatör Müd. Yard.", fLabel, bDark, new RectangleF(c3X, y + signH - 18, signColW3, 14), sfCenter);

        // 5. Açıklamalar (En Altta)
        y = noteY;
        g.FillRectangle(bGrayBg, x, y, w, 14);
        g.DrawRectangle(pDark, x, y, w, noteH);
        g.DrawString("Açıklamalar :", fLabel, bDark, new RectangleF(x + 6, y + 1, w - 12, 12), sfLeft);

        string notes =
            "1- Bu form her koordinatörlük ziyaretinde işletme yetkilisi ile birlikte doldurulup imzalanır.\n" +
            "2- Ay sonunda Aylık Rehberlik ve Denetim Formu (Ek-1) ile birlikte okul müdürlüğüne teslim edilir.";

        g.DrawString(notes, fSmall, bDark, new RectangleF(x + 6, y + 15, w - 12, noteH - 16), sfLeftTop);
    }

    /// <summary>
    /// Renders 2 Daily Guidance Forms on a single A4 page (Top & Bottom halves with middle cut line).
    /// Supports two different enterprises or two different duty dates of the same enterprise.
    /// </summary>
    public static void DrawTwoPerA4DailyGuidancePage(
        Graphics g,
        Rectangle bounds,
        Enterprise ent1,
        Enterprise? ent2,
        Teacher? teacher1,
        Teacher? teacher2,
        List<Student> students1,
        List<Student> students2,
        AppSettings settings,
        DateTime dutyDate1,
        DateTime? dutyDate2)
    {
        int halfH = bounds.Height / 2;
        Rectangle topBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, halfH - 8);
        Rectangle btmBounds = new Rectangle(bounds.X, bounds.Y + halfH + 8, bounds.Width, halfH - 8);

        // 1. Üst İşletme / Görev
        DrawCompactDailyGuidancePage(g, topBounds, ent1, teacher1, students1, settings, dutyDate1);

        // 2. Ayırma Çizgisi (Kesim Çizgisi)
        int yCut = bounds.Y + halfH;
        using var pDotted = new Pen(Color.FromArgb(148, 163, 184), 1f) { DashStyle = DashStyle.Dash };
        g.DrawLine(pDotted, bounds.X + 24, yCut, bounds.Right - 24, yCut);

        string cutText = "✂   BURADAN KESİNİZ (A5 FORMU)   ✂";
        using var fCut = new Font("Segoe UI", 6.8f, FontStyle.Bold);
        using var bCutBg = new SolidBrush(Color.White);
        using var bCutText = new SolidBrush(Color.FromArgb(100, 116, 139));
        var cutSize = g.MeasureString(cutText, fCut);
        int badgeW = (int)cutSize.Width + 18;
        int badgeH = 14;
        int badgeX = bounds.X + (bounds.Width - badgeW) / 2;
        g.FillRectangle(bCutBg, badgeX, yCut - 7, badgeW, badgeH);
        g.DrawRectangle(pDotted, badgeX, yCut - 7, badgeW, badgeH);
        using var sfCut = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(cutText, fCut, bCutText, new RectangleF(badgeX, yCut - 7, badgeW, badgeH), sfCut);

        // 3. Alt İşletme / Görev (Varsa)
        if (ent2 != null && dutyDate2.HasValue)
        {
            DrawCompactDailyGuidancePage(g, btmBounds, ent2, teacher2, students2, settings, dutyDate2.Value);
        }
        else
        {
            using var fEmpty = new Font("Segoe UI", 9f, FontStyle.Italic);
            using var bEmpty = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString("(Bu sayfada eşleşen 2. görev bulunmamaktadır)", fEmpty, bEmpty, btmBounds, sfCut);
        }
    }

    /// <summary>
    /// Renders the official MEB Monthly Attendance Report (Öğrencilerinin İşletmelerde Mesleki Eğitimi Aylık Devam-Devamsızlık Bildirim Çizelgesi)
    /// in standard Landscape or Portrait format with 1..31 days grid, Sabah/Öğle sub-rows, excused/unexcused totals, signatures, and legend table.
    /// </summary>
    public static void DrawMonthlyAttendancePage(
        Graphics g,
        Rectangle bounds,
        Enterprise enterprise,
        Teacher? teacher,
        List<Student> students,
        AppSettings settings,
        DateTime month,
        Dictionary<(int studentId, int day), string> studentDayAttendance,
        Dictionary<int, (double excused, double unexcused)> studentAbsenceTotals,
        bool isLandscape = true)
    {
        DrawAttendanceBlock(g, bounds, enterprise, teacher, students, settings, month, studentDayAttendance, studentAbsenceTotals, isLandscape, isCompact: false);
    }

    /// <summary>
    /// Renders 2 enterprises per A4 sheet (Tasarruflu mod) for Monthly Attendance Report.
    /// </summary>
    public static void DrawTwoPerA4AttendancePage(
        Graphics g,
        Rectangle bounds,
        Enterprise ent1,
        Enterprise? ent2,
        Teacher? teacher1,
        Teacher? teacher2,
        List<Student> st1,
        List<Student> st2,
        AppSettings settings,
        DateTime month,
        Dictionary<(int studentId, int day), string> studentDayAttendance,
        Dictionary<int, (double excused, double unexcused)> studentAbsenceTotals,
        bool isLandscape)
    {
        int halfH = bounds.Height / 2;
        Rectangle topBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, halfH - 8);
        Rectangle btmBounds = new Rectangle(bounds.X, bounds.Y + halfH + 8, bounds.Width, halfH - 8);

        // 1. Üst İşletme
        DrawAttendanceBlock(g, topBounds, ent1, teacher1, st1, settings, month, studentDayAttendance, studentAbsenceTotals, isLandscape, isCompact: true);

        // 2. Ayırma Çizgisi (Kesim Çizgisi)
        int yCut = bounds.Y + halfH;
        using var pDotted = new Pen(Color.FromArgb(148, 163, 184), 1f) { DashStyle = DashStyle.Dash };
        g.DrawLine(pDotted, bounds.X + 24, yCut, bounds.Right - 24, yCut);

        string cutText = "✂   BURADAN KESİNİZ   ✂";
        using var fCut = new Font("Segoe UI", 6.8f, FontStyle.Bold);
        using var bCutBg = new SolidBrush(Color.White);
        using var bCutText = new SolidBrush(Color.FromArgb(100, 116, 139));
        var cutSize = g.MeasureString(cutText, fCut);
        int badgeW = (int)cutSize.Width + 18;
        int badgeH = 14;
        int badgeX = bounds.X + (bounds.Width - badgeW) / 2;
        g.FillRectangle(bCutBg, badgeX, yCut - 7, badgeW, badgeH);
        g.DrawRectangle(pDotted, badgeX, yCut - 7, badgeW, badgeH);
        using var sfCut = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(cutText, fCut, bCutText, new RectangleF(badgeX, yCut - 7, badgeW, badgeH), sfCut);

        // 3. Alt İşletme (Varsa)
        if (ent2 != null)
        {
            DrawAttendanceBlock(g, btmBounds, ent2, teacher2 ?? teacher1, st2, settings, month, studentDayAttendance, studentAbsenceTotals, isLandscape, isCompact: true);
        }
        else
        {
            using var fEmpty = new Font("Segoe UI", 9f, FontStyle.Italic);
            using var bEmpty = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString("(Bu sayfada eşleşen 2. işletme bulunmamaktadır)", fEmpty, bEmpty, btmBounds, sfCut);
        }
    }

    public static void DrawTwoPerA4AttendancePage(
        Graphics g,
        Rectangle bounds,
        Enterprise ent1,
        Enterprise? ent2,
        Teacher? teacher,
        List<Student> st1,
        List<Student> st2,
        AppSettings settings,
        DateTime month,
        Dictionary<(int studentId, int day), string> studentDayAttendance,
        Dictionary<int, (double excused, double unexcused)> studentAbsenceTotals,
        bool isLandscape)
    {
        DrawTwoPerA4AttendancePage(g, bounds, ent1, ent2, teacher, teacher, st1, st2, settings, month, studentDayAttendance, studentAbsenceTotals, isLandscape);
    }

    /// <summary>
    /// Ayın en son iş gününü (Pazartesi-Cuma, hafta sonları ve resmi tatiller hariç) hesaplar.
    /// </summary>
    public static DateTime GetLastWorkingDayOfMonth(DateTime month)
    {
        int lastDay = DateTime.DaysInMonth(month.Year, month.Month);
        DateTime dt = new DateTime(month.Year, month.Month, lastDay);
        while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday || IsOfficialHoliday(dt))
        {
            dt = dt.AddDays(-1);
        }
        return dt;
    }

    private static bool IsOfficialHoliday(DateTime d)
    {
        // Sabit Resmi Tatiller
        if (d.Month == 1 && d.Day == 1) return true; // Yılbaşı
        if (d.Month == 4 && d.Day == 23) return true; // 23 Nisan Ulusal Egemenlik ve Çocuk Bayramı
        if (d.Month == 5 && d.Day == 1) return true; // 1 Mayıs Emek ve Dayanışma Günü
        if (d.Month == 5 && d.Day == 19) return true; // 19 Mayıs Atatürk'ü Anma, Gençlik ve Spor Bayramı
        if (d.Month == 7 && d.Day == 15) return true; // 15 Temmuz Demokrasi ve Milli Birlik Günü
        if (d.Month == 8 && d.Day == 30) return true; // 30 Ağustos Zafer Bayramı
        if (d.Month == 10 && d.Day == 29) return true; // 29 Ekim Cumhuriyet Bayramı
        return false;
    }

    private static void DrawAttendanceBlock(
        Graphics g,
        Rectangle bounds,
        Enterprise enterprise,
        Teacher? teacher,
        List<Student> students,
        AppSettings settings,
        DateTime month,
        Dictionary<(int studentId, int day), string> studentDayAttendance,
        Dictionary<int, (double excused, double unexcused)> studentAbsenceTotals,
        bool isLandscape,
        bool isCompact)
    {
        int padX = isLandscape ? 28 : 22;
        int x = bounds.X + padX;
        int w = bounds.Width - (padX * 2);
        int y = bounds.Y + (isCompact ? 8 : 16);

        float fontScale = isCompact ? 0.85f : 1.0f;
        using var fSchool = new Font("Segoe UI", (isLandscape ? 10f : 9.5f) * fontScale, FontStyle.Bold);
        using var fDocTitle = new Font("Segoe UI", (isLandscape ? 10f : 9.5f) * fontScale, FontStyle.Bold);
        using var fTableHead = new Font("Segoe UI", 8.5f * fontScale, FontStyle.Bold);
        using var fTableVal = new Font("Segoe UI", 8f * fontScale, FontStyle.Regular);
        using var fTableValBold = new Font("Segoe UI", 8f * fontScale, FontStyle.Bold);
        using var fGridHead = new Font("Segoe UI", (isLandscape ? 7.5f : 7f) * fontScale, FontStyle.Bold);
        using var fGridNum = new Font("Segoe UI", (isLandscape ? 7f : 6.5f) * fontScale, FontStyle.Bold);
        using var fCellVal = new Font("Segoe UI", (isLandscape ? 7.5f : 6.5f) * fontScale, FontStyle.Bold);
        using var fSmall = new Font("Segoe UI", 7.5f * fontScale, FontStyle.Regular);
        using var fSmallBold = new Font("Segoe UI", 7.5f * fontScale, FontStyle.Bold);
        using var fSmallItalic = new Font("Segoe UI", 7f * fontScale, FontStyle.Italic);

        using var bDark = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bBlue = new SolidBrush(Color.FromArgb(30, 58, 138));
        using var bMuted = new SolidBrush(Color.FromArgb(71, 85, 105));
        using var bGrayBg = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var bAltBg = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var bWeekendBg = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var bAbsent = new SolidBrush(Color.FromArgb(185, 28, 28));

        using var pThin = new Pen(Color.FromArgb(203, 213, 225), 1f);
        using var pDark = new Pen(Color.FromArgb(15, 23, 42), 1.1f);

        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

        // 1. Antetli Başlık
        string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MESLEKİ VE TEKNİK ANADOLU LİSESİ" : settings.SchoolName;
        int headH1 = isCompact ? 13 : 16;
        int headH2 = isCompact ? 14 : 18;
        g.DrawString(schoolName, fSchool, bDark, new RectangleF(x, y, w, headH1), sfCenter);
        y += headH1;
        g.DrawString("ÖĞRENCİLERİNİN İŞLETMELERDE MESLEKİ EĞİTİMİ AYLIK DEVAM-DEVAMSIZLIK BİLDİRİM ÇİZELGESİ", fDocTitle, bBlue, new RectangleF(x, y, w, headH2), sfCenter);
        y += headH2 + (isCompact ? 4 : 8);

        // 2. İŞLETMENİN Bilgileri Tablosu ("buraları biraz yükseltelim" ve düzenleme tarihi gereksinimleri)
        int rowH1 = isCompact ? 16 : 20;
        int rowH2 = isCompact ? 26 : 30;
        int rowH3 = isCompact ? 28 : 34;
        int entTableH = rowH1 + rowH2 + rowH3;

        g.FillRectangle(bGrayBg, x, y, w, rowH1);
        g.DrawRectangle(pDark, x, y, w, entTableH);
        g.DrawString("İŞLETMENİN", fTableHead, bDark, new RectangleF(x, y, w, rowH1), sfCenter);
        g.DrawLine(pDark, x, y + rowH1, x + w, y + rowH1);

        int[] colWs;
        if (isLandscape)
        {
            int c1W = 210;
            int c2W = 140;
            int c3W = 270;
            int c4W = 135;
            int c5W = 125;
            int c6W = w - (c1W + c2W + c3W + c4W + c5W);
            colWs = [c1W, c2W, c3W, c4W, c5W, c6W];
        }
        else
        {
            int c1W = (int)(w * 0.20f);
            int c2W = (int)(w * 0.15f);
            int c3W = (int)(w * 0.23f);
            int c4W = (int)(w * 0.11f);
            int c5W = (int)(w * 0.12f);
            int c6W = w - (c1W + c2W + c3W + c4W + c5W);
            colWs = [c1W, c2W, c3W, c4W, c5W, c6W];
        }

        string[] colHeaders = [
            "ADI",
            "TELEFONU VE FAKSI",
            "ADRESİ",
            "e-POSTA ADRESİ",
            "AİT OLDUĞU AY",
            "Çizelge Düzenleme Tarihi"
        ];

        int curX = x;
        for (int i = 0; i < colWs.Length; i++)
        {
            g.DrawString(colHeaders[i], fGridHead, bDark, new RectangleF(curX + 2, y + rowH1, colWs[i] - 4, rowH2), sfCenter);
            if (i > 0)
            {
                g.DrawLine(pDark, curX, y + rowH1, curX, y + entTableH);
            }
            curX += colWs[i];
        }

        g.DrawLine(pDark, x, y + rowH1 + rowH2, x + w, y + rowH1 + rowH2);

        int lastDayOfMonth = DateTime.DaysInMonth(month.Year, month.Month);
        DateTime docDate = GetLastWorkingDayOfMonth(month);

        string[] colValues = [
            enterprise.Name,
            enterprise.Phone ?? "-",
            enterprise.Address ?? "-",
            "-",
            month.ToString("MMMM yyyy", new CultureInfo("tr-TR")).ToUpper(new CultureInfo("tr-TR")),
            docDate.ToString("dd.MM.yyyy")
        ];

        curX = x;
        for (int i = 0; i < colWs.Length; i++)
        {
            g.DrawString(colValues[i], fTableValBold, bDark, new RectangleF(curX + 4, y + rowH1 + rowH2 + 1, colWs[i] - 8, rowH3 - 2), (i == 0 || i == 2) ? sfLeft : sfCenter);
            curX += colWs[i];
        }

        y += entTableH + (isCompact ? 4 : 6);

        // 3. Öğrenci Devam-Devamsızlık Çizelgesi Izgarası
        int wName, wNum, wField, wSubCol, wOzurlu, wOzursuz;
        if (isLandscape)
        {
            wName = isCompact ? 130 : 145;
            wNum = 45;
            wField = isCompact ? 95 : 110;
            wSubCol = 20;
            wOzurlu = isCompact ? 46 : 52;
            wOzursuz = isCompact ? 46 : 52;
        }
        else // Dikey
        {
            wName = isCompact ? 105 : 120;
            wNum = 38;
            wField = isCompact ? 75 : 84;
            wSubCol = 18;
            wOzurlu = 35;
            wOzursuz = 35;
        }

        int wInfo = wName + wNum + wField;
        int wTotals = wOzurlu + wOzursuz;
        float daysTotalW = w - wInfo - wSubCol - wTotals;
        float dayW = daysTotalW / 31f;

        int headerH1 = isCompact ? 14 : 18;
        int headerH2 = isCompact ? 15 : 18;
        int totalHeaderH = headerH1 + headerH2;

        int minSlots = isCompact ? 2 : (isLandscape ? 4 : 5);
        int slotCount = Math.Max(minSlots, students.Count);
        int subH = isCompact ? 12 : (slotCount > 6 ? 14 : 17);
        int studentH = subH * 2;
        int gridBodyH = slotCount * studentH;
        int gridTotalH = totalHeaderH + gridBodyH;

        // Header Backgrounds
        g.FillRectangle(bGrayBg, x, y, w, headerH1);
        g.FillRectangle(bAltBg, x, y + headerH1, w, headerH2);

        // Header 1 Titles
        g.DrawString("ÖĞRENCİNİN", fTableHead, bDark, new RectangleF(x, y, wInfo, headerH1), sfCenter);
        g.DrawString("GÜNLER", fTableHead, bDark, new RectangleF(x + wInfo, y, wSubCol + daysTotalW, headerH1), sfCenter);
        g.DrawString("TOPLAM DEVAMSIZLIĞI", fTableHead, bDark, new RectangleF(x + w - wTotals, y, wTotals, headerH1), sfCenter);

        g.DrawLine(pDark, x, y + headerH1, x + w, y + headerH1);

        // Header 2 Sub-Titles: "numarası yerine no yazalım" gereksinimi uygulandı ("NO")
        g.DrawString("ADI SOYADI", fGridHead, bDark, new RectangleF(x, y + headerH1, wName, headerH2), sfCenter);
        g.DrawString("NO", fGridHead, bDark, new RectangleF(x + wName, y + headerH1, wNum, headerH2), sfCenter);
        g.DrawString("ALANI DALI", fGridHead, bDark, new RectangleF(x + wName + wNum, y + headerH1, wField, headerH2), sfCenter);

        for (int d = 1; d <= 31; d++)
        {
            float dayX = x + wInfo + wSubCol + ((d - 1) * dayW);
            g.DrawString(d.ToString(), fGridNum, bDark, new RectangleF(dayX, y + headerH1, dayW, headerH2), sfCenter);
        }

        g.DrawString("ÖZÜRLÜ", fGridHead, bDark, new RectangleF(x + w - wTotals, y + headerH1, wOzurlu, headerH2), sfCenter);
        g.DrawString("ÖZÜRSÜZ", fGridHead, bDark, new RectangleF(x + w - wOzursuz, y + headerH1, wOzursuz, headerH2), sfCenter);

        g.DrawLine(pDark, x, y + totalHeaderH, x + w, y + totalHeaderH);

        // Student Data Rows
        for (int s = 0; s < slotCount; s++)
        {
            float rowTop = y + totalHeaderH + (s * studentH);
            float rowMid = rowTop + subH;
            float rowBottom = rowTop + studentH;

            Student? st = s < students.Count ? students[s] : null;

            if (st != null)
            {
                g.DrawString(st.FullName, fTableValBold, bDark, new RectangleF(x + 3, rowTop, wName - 6, studentH), sfLeft);
                g.DrawString(st.StudentNumber, fTableVal, bDark, new RectangleF(x + wName, rowTop, wNum, studentH), sfCenter);

                string fDal = !string.IsNullOrWhiteSpace(st.FieldName)
                    ? (!string.IsNullOrWhiteSpace(st.Classroom) ? $"{st.Classroom} {st.FieldName}" : st.FieldName)
                    : (enterprise.Sector ?? "-");
                g.DrawString(fDal, fSmall, bDark, new RectangleF(x + wName + wNum + 2, rowTop, wField - 4, studentH), sfCenter);

                g.DrawString("S", fGridNum, bDark, new RectangleF(x + wInfo, rowTop, wSubCol, subH), sfCenter);
                g.DrawString("Ö", fGridNum, bDark, new RectangleF(x + wInfo, rowMid, wSubCol, subH), sfCenter);

                for (int d = 1; d <= 31; d++)
                {
                    float dayCellX = x + wInfo + wSubCol + ((d - 1) * dayW);
                    if (d > lastDayOfMonth)
                    {
                        g.FillRectangle(bWeekendBg, dayCellX, rowTop, dayW, studentH);
                    }
                    else
                    {
                        var dt = new DateTime(month.Year, month.Month, d);
                        bool isWeekend = dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;

                        if (isWeekend)
                        {
                            g.FillRectangle(bWeekendBg, dayCellX, rowTop, dayW, studentH);
                        }

                        string sVal = "";
                        string oVal = "";
                        bool isAbsent = false;

                        if (studentDayAttendance.TryGetValue((st.Id, d), out string? code))
                        {
                            if (code == "D") { sVal = "D"; oVal = "D"; isAbsent = true; }
                            else if (code == "SY") { sVal = "D"; oVal = "X"; isAbsent = true; }
                            else if (code == "ÖY") { sVal = "X"; oVal = "D"; isAbsent = true; }
                            else if (code == "Y") { sVal = "D"; oVal = "X"; isAbsent = true; }
                            else if (code == "İ") { sVal = "İ"; oVal = "İ"; isAbsent = true; }
                            else if (code == "S" || code == "SH" || code == "K") { sVal = "R"; oVal = "R"; isAbsent = true; }
                            else if (code == "T") { sVal = "T"; oVal = "T"; }
                            else if (code == "X") { sVal = "X"; oVal = "X"; }
                            else { sVal = code; oVal = code; }
                        }
                        else
                        {
                            if (!isWeekend)
                            {
                                sVal = "X";
                                oVal = "X";
                            }
                        }

                        var brushCell = isAbsent ? bAbsent : bDark;
                        if (!string.IsNullOrEmpty(sVal))
                            g.DrawString(sVal, fCellVal, brushCell, new RectangleF(dayCellX, rowTop, dayW, subH), sfCenter);
                        if (!string.IsNullOrEmpty(oVal))
                            g.DrawString(oVal, fCellVal, brushCell, new RectangleF(dayCellX, rowMid, dayW, subH), sfCenter);
                    }
                }

                // Totals
                string ozurluStr = "0";
                string ozursuzStr = "0";
                if (studentAbsenceTotals.TryGetValue(st.Id, out var totals))
                {
                    if (totals.excused > 0) ozurluStr = totals.excused.ToString("0.#");
                    if (totals.unexcused > 0) ozursuzStr = totals.unexcused.ToString("0.#");
                }

                g.DrawString(ozurluStr, fTableValBold, bDark, new RectangleF(x + w - wTotals, rowTop, wOzurlu, studentH), sfCenter);
                g.DrawString(ozursuzStr, fTableValBold, bDark, new RectangleF(x + w - wOzursuz, rowTop, wOzursuz, studentH), sfCenter);
            }
            else
            {
                g.DrawString("S", fGridNum, bDark, new RectangleF(x + wInfo, rowTop, wSubCol, subH), sfCenter);
                g.DrawString("Ö", fGridNum, bDark, new RectangleF(x + wInfo, rowMid, wSubCol, subH), sfCenter);

                for (int d = 1; d <= 31; d++)
                {
                    float dayCellX = x + wInfo + wSubCol + ((d - 1) * dayW);
                    if (d > lastDayOfMonth)
                    {
                        g.FillRectangle(bWeekendBg, dayCellX, rowTop, dayW, studentH);
                    }
                    else
                    {
                        var dt = new DateTime(month.Year, month.Month, d);
                        if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
                        {
                            g.FillRectangle(bWeekendBg, dayCellX, rowTop, dayW, studentH);
                        }
                    }
                }
            }

            g.DrawLine(pThin, x + wInfo, rowMid, x + w - wTotals, rowMid);
            g.DrawLine(pDark, x, rowBottom, x + w, rowBottom);
        }

        // Vertical lines
        g.DrawLine(pDark, x + wName, y + headerH1, x + wName, y + gridTotalH);
        g.DrawLine(pDark, x + wName + wNum, y + headerH1, x + wName + wNum, y + gridTotalH);
        g.DrawLine(pDark, x + wInfo, y, x + wInfo, y + gridTotalH);
        g.DrawLine(pDark, x + wInfo + wSubCol, y + headerH1, x + wInfo + wSubCol, y + gridTotalH);

        for (int d = 1; d <= 30; d++)
        {
            float lineX = x + wInfo + wSubCol + (d * dayW);
            g.DrawLine(pThin, lineX, y + headerH1, lineX, y + gridTotalH);
        }

        g.DrawLine(pDark, x + w - wTotals, y, x + w - wTotals, y + gridTotalH);
        g.DrawLine(pThin, x + w - wOzursuz, y + headerH1, x + w - wOzursuz, y + gridTotalH);

        g.DrawRectangle(pDark, x, y, w, gridTotalH);

        y += gridTotalH + (isCompact ? 4 : 6);

        // ── Alt Sınırlara Göre Dinamik Konumlandırma (İmza ve Sembolleri En Alta Yanaştırma) ──
        int signTableH = isCompact ? 68 : 86;
        int noteAreaH = isCompact ? 14 : 32;
        int targetBottom = bounds.Bottom - (isCompact ? 8 : 16);
        int noteY = targetBottom - noteAreaH;
        int signTableY = noteY - (isCompact ? 4 : 8) - signTableH;

        if (signTableY > y)
        {
            y = signTableY;
        }

        // 4. İmzalar ve Semboller Tablosu (Table 3)
        int col1W, col2W, col3W, legW;
        if (isLandscape)
        {
            col1W = isCompact ? 170 : 190;
            col2W = isCompact ? 180 : 200;
            col3W = isCompact ? 200 : 230;
            legW = w - (col1W + col2W + col3W);
        }
        else // Dikey
        {
            col1W = (int)(w * 0.18f);
            col2W = (int)(w * 0.19f);
            col3W = (int)(w * 0.21f);
            legW = w - (col1W + col2W + col3W);
        }

        int legX = x + col1W + col2W + col3W;

        g.DrawRectangle(pDark, x, y, w, signTableH);
        g.DrawLine(pDark, x + col1W, y, x + col1W, y + signTableH);
        g.DrawLine(pDark, x + col1W + col2W, y, x + col1W + col2W, y + signTableH);
        g.DrawLine(pDark, legX, y, legX, y + signTableH);

        // Sütun 1: İŞLETME YETKİLİSİ
        g.DrawString("İŞLETME YETKİLİSİ", fTableHead, bDark, new RectangleF(x, y + 4, col1W, 14), sfCenter);
        string contactPerson = !string.IsNullOrWhiteSpace(enterprise.ContactPerson) ? enterprise.ContactPerson : "Yetkili";
        g.DrawString(contactPerson, fTableValBold, bDark, new RectangleF(x, y + (isCompact ? 22 : 28), col1W, 15), sfCenter);
        g.DrawString("Kaşe / İmza", fSmallItalic, bMuted, new RectangleF(x, y + (isCompact ? 44 : 52), col1W, 14), sfCenter);

        // Sütun 2: KOORDİNATÖR
        int c2X = x + col1W;
        g.DrawString("KOORDİNATÖR", fTableHead, bDark, new RectangleF(c2X, y + 4, col2W, 14), sfCenter);
        string teacherName = teacher?.FullName ?? enterprise.TeacherName ?? "-";
        g.DrawString(teacherName, fTableValBold, bDark, new RectangleF(c2X, y + (isCompact ? 20 : 25), col2W, 15), sfCenter);
        g.DrawString("İmza", fSmallItalic, bMuted, new RectangleF(c2X, y + (isCompact ? 36 : 43), col2W, 13), sfCenter);
        g.DrawString("Koordinatör Öğretmen", fGridHead, bDark, new RectangleF(c2X, y + (isCompact ? 50 : 64), col2W, 15), sfCenter);

        // Sütun 3: İNCELENDİ
        int c3X = x + col1W + col2W;
        g.DrawString("İNCELENDİ", fTableHead, bDark, new RectangleF(c3X, y + 4, col3W, 14), sfCenter);
        g.DrawString($"....... / ...... / {month.Year}", fSmallItalic, bMuted, new RectangleF(c3X, y + (isCompact ? 20 : 23), col3W, 13), sfCenter);
        string vpName = !string.IsNullOrWhiteSpace(settings.VicePrincipalName) ? settings.VicePrincipalName : "Okul Yönetimi";
        g.DrawString(vpName, fTableValBold, bDark, new RectangleF(c3X, y + (isCompact ? 36 : 43), col3W, 15), sfCenter);
        g.DrawString("Koordinatör Müd. Yard.", fGridHead, bDark, new RectangleF(c3X, y + (isCompact ? 50 : 64), col3W, 15), sfCenter);

        // Sütun 4: Semboller Kutusu (Legend)
        int legHeadH = isCompact ? 13 : 15;
        g.FillRectangle(bGrayBg, legX, y, legW, legHeadH);
        g.DrawString("Devamlılığın Gösterileceği Semboller", fGridHead, bDark, new RectangleF(legX, y, legW, legHeadH), sfCenter);
        g.DrawLine(pThin, legX, y + legHeadH, legX + legW, y + legHeadH);
        g.DrawString("X : İşletmede              O : Okulda", fSmall, bDark, new RectangleF(legX, y + legHeadH, legW, isCompact ? 12 : 14), sfCenter);

        int legSec2Y = y + legHeadH + (isCompact ? 12 : 14);
        g.FillRectangle(bGrayBg, legX, legSec2Y, legW, legHeadH);
        g.DrawLine(pThin, legX, legSec2Y, legX + legW, legSec2Y);
        g.DrawString("Devamsızlığın Gösterileceği Semboller", fGridHead, bDark, new RectangleF(legX, legSec2Y, legW, legHeadH), sfCenter);
        int legSec2Bottom = legSec2Y + legHeadH;
        g.DrawLine(pThin, legX, legSec2Bottom, legX + legW, legSec2Bottom);

        int legColW = legW / 3;
        int legY = legSec2Bottom + 1;
        int legLineH = isCompact ? 10 : 12;

        g.DrawString("İ : İzinli", fSmall, bDark, new RectangleF(legX + 6, legY, legColW, legLineH), sfLeft);
        g.DrawString("H : Hasta", fSmall, bDark, new RectangleF(legX + 6, legY + legLineH, legColW, legLineH), sfLeft);
        g.DrawString("R : Raporlu", fSmall, bDark, new RectangleF(legX + 6, legY + (legLineH * 2), legColW, legLineH), sfLeft);

        g.DrawString("D : Özürsüz Dev.", fSmall, bDark, new RectangleF(legX + legColW + 4, legY, legColW + 15, legLineH), sfLeft);
        g.DrawString("S : Sabah", fSmall, bDark, new RectangleF(legX + legColW + 4, legY + legLineH, legColW, legLineH), sfLeft);
        g.DrawString("Ö : Öğle", fSmall, bDark, new RectangleF(legX + legColW + 4, legY + (legLineH * 2), legColW, legLineH), sfLeft);

        g.DrawString("T : Tatil", fSmall, bDark, new RectangleF(legX + (legColW * 2) + 8, legY, legColW, legLineH), sfLeft);

        // 5. Açıklama Notları (En Altta)
        y = Math.Max(y + signTableH + (isCompact ? 4 : 6), noteY);
        if (isCompact)
        {
            string noteCompact = "Bu çizelge işletme tarafından tutulur. (İ), (H), (R) sembolleri özürlü devamsızlık sütununa yazılacaktır.";
            g.DrawString(noteCompact, fSmall, bDark, new RectangleF(x, y, w, 12), sfCenter);
        }
        else
        {
            string note1 = "Bu çizelge, işletme tarafından tutulacak, öğrencinin işletmede bulunması gereken günlere ait devamsızlık durumları ile ilgili sütunda, yanda gösterilen uygun sembolle belirlenecektir.";
            string note2 = "(İ), (H), (R) Sembolleri ile gösterilen devamsızlıklar toplamı özürlü devamsızlık sütununa yazılacaktır.";
            g.DrawString(note1, fSmall, bDark, new RectangleF(x, y, w, 14), sfCenter);
            g.DrawString(note2, fSmall, bDark, new RectangleF(x, y + 14, w, 14), sfCenter);
        }
    }

    private static void DrawPlaceholderPage(Graphics g, Rectangle bounds, string message)
    {
        using var font = new Font("Segoe UI", 11f, FontStyle.Regular);
        using var brush = new SolidBrush(Color.FromArgb(71, 85, 105));
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(message, font, brush, bounds, sf);
    }

    private void BtnPrintCurrent_Click(object? sender, EventArgs e)
    {
        try
        {
            var available = GetAvailableEnterprises();
            if (available.Count == 0)
            {
                MessageBox.Show("Yazdırılacak işletme bulunamadı!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // "Tüm İşletmeler" seçiliyse toplu yazdırmayı çalıştır:
            if (_cmbEnterprise.SelectedIndex <= 0)
            {
                BtnPrintAll_Click(sender, e);
                return;
            }

            var (ent1, ent2, teacher, st1, st2) = GetSelectionForPreview();
            if (ent1 == null)
            {
                MessageBox.Show("Yazdırılacak işletme seçilmedi!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int docTypeIdx = _cmbDocType.SelectedIndex;

            if (docTypeIdx == 1) // 2. Günlük Rehberlik Görev Formu (Ay İçindeki Tüm Görev Günleri)
            {
                var visitDates = GetMonthlyVisitDates(ent1, teacher, st1, _selectedMonth);
                if (visitDates.Count == 0) visitDates.Add(_selectedMonth);

                bool isTwoPerPage = _cmbPageLayout.SelectedIndex == 1;
                int printDateIdx = 0;
                int totalPages = isTwoPerPage ? (int)Math.Ceiling(visitDates.Count / 2.0) : visitDates.Count;

                var pdDaily = new PrintDocument();
                pdDaily.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169); // 100 DPI A4
                pdDaily.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
                pdDaily.BeginPrint += (s, pe) => printDateIdx = 0;

                pdDaily.PrintPage += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    if (isTwoPerPage)
                    {
                        if (printDateIdx < visitDates.Count)
                        {
                            var date1 = visitDates[printDateIdx];
                            DateTime? date2 = (printDateIdx + 1 < visitDates.Count) ? visitDates[printDateIdx + 1] : null;

                            DrawTwoPerA4DailyGuidancePage(pe.Graphics, pe.PageBounds,
                                ent1, date2.HasValue ? ent1 : null,
                                teacher, teacher,
                                st1, st1,
                                _appSettings,
                                date1, date2);

                            printDateIdx += 2;
                            pe.HasMorePages = printDateIdx < visitDates.Count;
                        }
                        else
                        {
                            pe.HasMorePages = false;
                        }
                    }
                    else
                    {
                        if (printDateIdx < visitDates.Count)
                        {
                            var d = visitDates[printDateIdx];
                            DrawDailyGuidancePage(pe.Graphics, pe.PageBounds, ent1, teacher, st1, _appSettings, d);
                            printDateIdx++;
                            pe.HasMorePages = printDateIdx < visitDates.Count;
                        }
                        else
                        {
                            pe.HasMorePages = false;
                        }
                    }
                };

                PrintPreviewHelper.ShowPreview(pdDaily, $"Resmi Yazdırma Önizleme - {ent1.Name} ({visitDates.Count} Görev Günü - {totalPages} Sayfa)", this);
                return;
            }

            if (docTypeIdx == 2) // 3. Aylık Devam-Devamsızlık Bildirim Çizelgesi
            {
                bool isLandscape = _cmbOrientation == null || _cmbOrientation.SelectedIndex == 0;
                bool isTwoPerPage = _cmbPageLayout != null && _cmbPageLayout.SelectedIndex == 1;

                var pdAtt = new PrintDocument();
                pdAtt.DefaultPageSettings.Landscape = isLandscape;
                pdAtt.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
                pdAtt.DefaultPageSettings.Margins = new Margins(20, 20, 20, 20);

                Teacher? tAtt1 = teacher;
                Teacher? tAtt2 = ent2 != null ? ResolveTeacherForEnterprise(ent2, _cmbTeacher?.SelectedIndex > 0 ? teacher : null, st2) : null;

                pdAtt.PrintPage += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    if (isTwoPerPage)
                    {
                        DrawTwoPerA4AttendancePage(pe.Graphics, pe.PageBounds,
                            ent1, ent2, tAtt1, tAtt2, st1, st2, _appSettings, _selectedMonth,
                            _studentDayAttendanceMap, _studentAbsenceTotalsMap, isLandscape);
                    }
                    else
                    {
                        DrawMonthlyAttendancePage(pe.Graphics, pe.PageBounds,
                            ent1, tAtt1, st1, _appSettings, _selectedMonth,
                            _studentDayAttendanceMap, _studentAbsenceTotalsMap, isLandscape);
                    }
                    pe.HasMorePages = false;
                };

                string orientText = isLandscape ? "Yatay" : "Dikey";
                string modeText = isTwoPerPage ? "2 İşletme / Sayfa" : "1 İşletme / Sayfa";
                PrintPreviewHelper.ShowPreview(pdAtt, $"Resmi Yazdırma Önizleme - Aylık Devam-Devamsızlık Çizelgesi ({ent1.Name} - {orientText} - {modeText})", this);
                return;
            }

            // docTypeIdx == 0 (Aylık Form Ek-1)
            var pd = new PrintDocument();
            pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            pd.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);

            Teacher? tEk1_1 = teacher;
            Teacher? tEk1_2 = ent2 != null ? ResolveTeacherForEnterprise(ent2, _cmbTeacher?.SelectedIndex > 0 ? teacher : null, st2) : null;

            pd.PrintPage += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                if (_cmbPageLayout.SelectedIndex == 1) // 2 İşletme / Sayfa
                {
                    DrawTwoPerA4Page(pe.Graphics, pe.PageBounds, ent1, ent2, tEk1_1, tEk1_2, st1, st2, _appSettings, _selectedMonth, _studentMonthlyAbsenceMap);
                }
                else // 1 İşletme / Sayfa
                {
                    DrawEk1Page(pe.Graphics, pe.PageBounds, ent1, tEk1_1, st1, _appSettings, _selectedMonth, _studentMonthlyAbsenceMap);
                }
                pe.HasMorePages = false;
            };

            PrintPreviewHelper.ShowPreview(pd, $"Resmi Yazdırma Önizleme - Ek-1 Ziyaret Formu ({ent1.Name})", this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yazdırma başlatılırken hata oluştu: {ex.Message}", "Yazdırma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnPrintAll_Click(object? sender, EventArgs e)
    {
        try
        {
            int teacherIdx = _cmbTeacher.SelectedIndex;
            Teacher? teacher = teacherIdx > 0 && teacherIdx <= _teachers.Count ? _teachers[teacherIdx - 1] : null;

            List<Enterprise> targetEnterprises = GetAvailableEnterprises();

            if (targetEnterprises.Count == 0)
            {
                MessageBox.Show("Yazdırılacak işletme bulunamadı!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int docTypeIdx = _cmbDocType.SelectedIndex;
            bool isTwoPerPage = _cmbPageLayout.SelectedIndex == 1;

            var trCulture = new System.Globalization.CultureInfo("tr-TR");
            var trComparer = StringComparer.Create(trCulture, true);

            if (docTypeIdx == 1) // Günlük Rehberlik Görev Formu (Tüm İşletmeler ve Ay İçindeki Tüm Görev Günleri)
            {
                // 1. Tüm işletmelerin görevlerini topla ve koordinatör öğretmeni çözümle
                var allDuties = new List<(Enterprise ent, Teacher? teacher, List<Student> students, DateTime dutyDate)>();
                foreach (var ent in targetEnterprises)
                {
                    var stList = _students
                        .Where(st => st.EnterpriseId == ent.Id || string.Equals(st.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(st => st.Classroom)
                        .ThenBy(st => FeeCalculator.ParseStudentNumber(st.StudentNumber))
                        .ToList();

                    var t = ResolveTeacherForEnterprise(ent, teacher, stList);
                    var vDates = GetMonthlyVisitDates(ent, t, stList, _selectedMonth);
                    if (vDates.Count == 0) vDates.Add(_selectedMonth);

                    foreach (var vd in vDates)
                    {
                        allDuties.Add((ent, t, stList, vd));
                    }
                }

                // 2. Görevleri koordinatör öğretmene göre grupla ve Türkçe alfabetik sırala
                var teacherDutyGroups = allDuties
                    .GroupBy(x => x.teacher != null && x.teacher.Id > 0 ? $"ID_{x.teacher.Id}" : $"NAME_{x.teacher?.FullName ?? "Unassigned"}")
                    .OrderBy(g => g.First().teacher?.FullName ?? "ZZZ", trComparer)
                    .ToList();

                // 3. Yazdırma kuyruklarını oluştur
                // 2'li sayfada: Bir öğretmenin görevleri tek sayıda biterse (yarım sayfa kalırsa), sonraki öğretmen MUTLAKA yeni sayfadan başlar!
                var dailyTwoPerPageQueue = new List<(Enterprise ent1, Enterprise? ent2, Teacher? t1, Teacher? t2, List<Student> st1, List<Student> st2, DateTime d1, DateTime? d2)>();
                var dailySinglePageQueue = new List<(Enterprise ent, Teacher? teacher, List<Student> students, DateTime dutyDate)>();

                foreach (var group in teacherDutyGroups)
                {
                    var sortedDuties = group
                        .OrderBy(x => x.ent.Name, trComparer)
                        .ThenBy(x => x.dutyDate)
                        .ToList();

                    dailySinglePageQueue.AddRange(sortedDuties);

                    for (int i = 0; i < sortedDuties.Count; i += 2)
                    {
                        var item1 = sortedDuties[i];
                        var item2 = (i + 1 < sortedDuties.Count) ? sortedDuties[i + 1] : default;

                        dailyTwoPerPageQueue.Add((
                            item1.ent,
                            item2.ent,
                            item1.teacher,
                            item2.teacher,
                            item1.students,
                            item2.students ?? new List<Student>(),
                            item1.dutyDate,
                            item2.dutyDate != default ? item2.dutyDate : null
                        ));
                    }
                }

                int totalDutyCount = allDuties.Count;
                int pageCount = isTwoPerPage ? dailyTwoPerPageQueue.Count : dailySinglePageQueue.Count;
                string modeName = isTwoPerPage ? "2 Form / Sayfa (Tasarruflu)" : "1 Form / Sayfa (Tam)";

                var confirm = MessageBox.Show(
                    $"Toplam {targetEnterprises.Count} adet işletme ({teacherDutyGroups.Count} koordinatör öğretmen) için bu ay görev takvimine göre toplam {totalDutyCount} adet günlük görev formu öğretmene göre sıralı olarak '{modeName}' modunda {pageCount} sayfa yazdırılacaktır.\n\n" +
                    (isTwoPerPage ? "• Not: Öğretmen bazlı sayfa ayrımı korunacak; bir öğretmenin formu yarım sayfada kaldığında yeni öğretmen daima yeni sayfadan başlayacaktır.\n\n" : "") +
                    "Devam etmek istiyor musunuz?",
                    "Toplu Günlük Görev Formu Yazdırma Onayı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes) return;

                int printQueueIdx = 0;
                var pdDaily = new PrintDocument();
                pdDaily.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
                pdDaily.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
                pdDaily.BeginPrint += (s, pe) => printQueueIdx = 0;

                pdDaily.PrintPage += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    if (isTwoPerPage)
                    {
                        if (printQueueIdx < dailyTwoPerPageQueue.Count)
                        {
                            var item = dailyTwoPerPageQueue[printQueueIdx];

                            DrawTwoPerA4DailyGuidancePage(pe.Graphics, pe.PageBounds,
                                item.ent1, item.ent2,
                                item.t1, item.t2,
                                item.st1, item.st2,
                                _appSettings,
                                item.d1, item.d2);

                            printQueueIdx++;
                            pe.HasMorePages = printQueueIdx < dailyTwoPerPageQueue.Count;
                        }
                        else
                        {
                            pe.HasMorePages = false;
                        }
                    }
                    else
                    {
                        if (printQueueIdx < dailySinglePageQueue.Count)
                        {
                            var item = dailySinglePageQueue[printQueueIdx];
                            DrawDailyGuidancePage(pe.Graphics, pe.PageBounds, item.ent, item.teacher, item.students, _appSettings, item.dutyDate);
                            printQueueIdx++;
                            pe.HasMorePages = printQueueIdx < dailySinglePageQueue.Count;
                        }
                        else
                        {
                            pe.HasMorePages = false;
                        }
                    }
                };

                PrintPreviewHelper.ShowPreview(pdDaily, $"Toplu Yazdırma Önizleme - Günlük Rehberlik Formları ({totalDutyCount} Görev - {pageCount} Sayfa)", this);
                return;
            }

            if (docTypeIdx == 2) // 3. Aylık Devam-Devamsızlık Bildirim Çizelgesi (Tüm İşletmeler)
            {
                bool isLandscape = _cmbOrientation == null || _cmbOrientation.SelectedIndex == 0;
                isTwoPerPage = _cmbPageLayout != null && _cmbPageLayout.SelectedIndex == 1;

                // 1. İşletmeleri, öğrencilerini ve öğretmenlerini çözümle
                var attItems = targetEnterprises.Select(ent =>
                {
                    var stList = GetStudentsForEnterprise(ent, teacher);
                    var t = ResolveTeacherForEnterprise(ent, teacher, stList);
                    return new { Enterprise = ent, Teacher = t, Students = stList };
                }).ToList();

                // 2. Koordinatör öğretmene göre grupla ve Türkçe alfabetik sırala
                var teacherAttGroups = attItems
                    .GroupBy(x => x.Teacher != null && x.Teacher.Id > 0 ? $"ID_{x.Teacher.Id}" : $"NAME_{x.Teacher?.FullName ?? "Unassigned"}")
                    .OrderBy(g => g.First().Teacher?.FullName ?? "ZZZ", trComparer)
                    .ToList();

                // 3. Yazdırma kuyruklarını oluştur
                // 2'li sayfada: Bir öğretmenin işletmeleri tek sayıda biterse (yarım sayfa kalırsa), sonraki öğretmen MUTLAKA yeni sayfadan başlar!
                var attTwoPerPageQueue = new List<(Enterprise e1, Enterprise? e2, Teacher? t1, Teacher? t2, List<Student> st1, List<Student> st2)>();
                var attSinglePageQueue = new List<(Enterprise ent, Teacher? teacher, List<Student> students)>();

                foreach (var group in teacherAttGroups)
                {
                    var sortedEnts = group.OrderBy(x => x.Enterprise.Name, trComparer).ToList();
                    attSinglePageQueue.AddRange(sortedEnts.Select(x => (x.Enterprise, x.Teacher, x.Students)));

                    for (int i = 0; i < sortedEnts.Count; i += 2)
                    {
                        var item1 = sortedEnts[i];
                        var item2 = (i + 1 < sortedEnts.Count) ? sortedEnts[i + 1] : null;

                        attTwoPerPageQueue.Add((
                            item1.Enterprise,
                            item2?.Enterprise,
                            item1.Teacher,
                            item2?.Teacher,
                            item1.Students,
                            item2?.Students ?? new List<Student>()
                        ));
                    }
                }

                int pageCountAtt = isTwoPerPage ? attTwoPerPageQueue.Count : attSinglePageQueue.Count;
                string orientText = isLandscape ? "Yatay" : "Dikey";
                string modeText = isTwoPerPage ? "2 İşletme / Sayfa (Tasarruflu)" : "1 İşletme / Sayfa (Tam)";

                var confirmAtt = MessageBox.Show(
                    $"Toplam {targetEnterprises.Count} adet işletme ({teacherAttGroups.Count} koordinatör öğretmen) için 'Aylık Devam-Devamsızlık Bildirim Çizelgesi' belgesi öğretmene göre sıralı olarak '{modeText}' modunda {pageCountAtt} sayfa ({orientText} A4) yazdırılacaktır.\n\n" +
                    (isTwoPerPage ? "• Not: Öğretmen bazlı sayfa ayrımı korunacak; bir öğretmenin formu yarım sayfada kaldığında yeni öğretmen daima yeni sayfadan başlayacaktır.\n\n" : "") +
                    "Devam etmek istiyor musunuz?",
                    "Toplu Devamsızlık Çizelgesi Yazdırma Onayı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmAtt != DialogResult.Yes) return;

                int printQueueIdx = 0;
                var pdAttAll = new PrintDocument();
                pdAttAll.DefaultPageSettings.Landscape = isLandscape;
                pdAttAll.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
                pdAttAll.DefaultPageSettings.Margins = new Margins(20, 20, 20, 20);
                pdAttAll.BeginPrint += (s, pe) => printQueueIdx = 0;

                pdAttAll.PrintPage += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    if (isTwoPerPage)
                    {
                        if (printQueueIdx < attTwoPerPageQueue.Count)
                        {
                            var page = attTwoPerPageQueue[printQueueIdx];

                            DrawTwoPerA4AttendancePage(pe.Graphics, pe.PageBounds,
                                page.e1, page.e2, page.t1, page.t2, page.st1, page.st2,
                                _appSettings, _selectedMonth,
                                _studentDayAttendanceMap, _studentAbsenceTotalsMap, isLandscape);

                            printQueueIdx++;
                            pe.HasMorePages = printQueueIdx < attTwoPerPageQueue.Count;
                        }
                        else
                        {
                            pe.HasMorePages = false;
                        }
                    }
                    else
                    {
                        if (printQueueIdx < attSinglePageQueue.Count)
                        {
                            var page = attSinglePageQueue[printQueueIdx];

                            DrawMonthlyAttendancePage(pe.Graphics, pe.PageBounds,
                                page.ent, page.teacher, page.students, _appSettings, _selectedMonth,
                                _studentDayAttendanceMap, _studentAbsenceTotalsMap, isLandscape);

                            printQueueIdx++;
                            pe.HasMorePages = printQueueIdx < attSinglePageQueue.Count;
                        }
                        else
                        {
                            pe.HasMorePages = false;
                        }
                    }
                };

                PrintPreviewHelper.ShowPreview(pdAttAll, $"Toplu Yazdırma Önizleme - Aylık Devam-Devamsızlık Çizelgeleri ({targetEnterprises.Count} İşletme - {pageCountAtt} Sayfa - {orientText})", this);
                return;
            }

            // docTypeIdx == 0 (Aylık Form Ek-1)
            // 1. İşletmeleri, öğrencilerini ve öğretmenlerini çözümle
            var ek1Items = targetEnterprises.Select(ent =>
            {
                var stList = _students
                    .Where(st => st.EnterpriseId == ent.Id || string.Equals(st.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(st => st.Classroom)
                    .ThenBy(st => FeeCalculator.ParseStudentNumber(st.StudentNumber))
                    .ToList();

                var t = ResolveTeacherForEnterprise(ent, teacher, stList);
                return new { Enterprise = ent, Teacher = t, Students = stList };
            }).ToList();

            // 2. Koordinatör öğretmene göre grupla ve Türkçe alfabetik sırala
            var teacherEk1Groups = ek1Items
                .GroupBy(x => x.Teacher != null && x.Teacher.Id > 0 ? $"ID_{x.Teacher.Id}" : $"NAME_{x.Teacher?.FullName ?? "Unassigned"}")
                .OrderBy(g => g.First().Teacher?.FullName ?? "ZZZ", trComparer)
                .ToList();

            // 3. Yazdırma kuyruklarını oluştur
            // 2'li sayfada: Bir öğretmenin işletmeleri tek sayıda biterse (yarım sayfa kalırsa), sonraki öğretmen MUTLAKA yeni sayfadan başlar!
            var ek1TwoPerPageQueue = new List<(Enterprise ent1, Enterprise? ent2, Teacher? t1, Teacher? t2, List<Student> st1, List<Student> st2)>();
            var ek1SinglePageQueue = new List<(Enterprise ent, Teacher? teacher, List<Student> students)>();

            foreach (var group in teacherEk1Groups)
            {
                var sortedEnts = group.OrderBy(x => x.Enterprise.Name, trComparer).ToList();
                ek1SinglePageQueue.AddRange(sortedEnts.Select(x => (x.Enterprise, x.Teacher, x.Students)));

                for (int i = 0; i < sortedEnts.Count; i += 2)
                {
                    var item1 = sortedEnts[i];
                    var item2 = (i + 1 < sortedEnts.Count) ? sortedEnts[i + 1] : null;

                    ek1TwoPerPageQueue.Add((
                        item1.Enterprise,
                        item2?.Enterprise,
                        item1.Teacher,
                        item2?.Teacher,
                        item1.Students,
                        item2?.Students ?? new List<Student>()
                    ));
                }
            }

            int pageCountEk1 = isTwoPerPage ? ek1TwoPerPageQueue.Count : ek1SinglePageQueue.Count;
            string modeNameEk1 = isTwoPerPage ? "2 İşletme / Sayfa (Tasarruflu)" : "1 İşletme / Sayfa (Tam)";

            var confirmEk1 = MessageBox.Show(
                $"Toplam {targetEnterprises.Count} adet işletme ({teacherEk1Groups.Count} koordinatör öğretmen) için 'Aylık Rehberlik Formu (Ek-1)' belgesi öğretmene göre sıralı olarak '{modeNameEk1}' modunda {pageCountEk1} sayfa yazdırılacaktır.\n\n" +
                (isTwoPerPage ? "• Not: Öğretmen bazlı sayfa ayrımı korunacak; bir öğretmenin formu yarım sayfada kaldığında yeni öğretmen daima yeni sayfadan başlayacaktır.\n\n" : "") +
                "Devam etmek istiyor musunuz?",
                "Toplu Form Yazdırma Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmEk1 != DialogResult.Yes) return;

            int printPageIndex = 0;
            var pd = new PrintDocument();
            pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            pd.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
            pd.BeginPrint += (s, pe) => printPageIndex = 0;

            pd.PrintPage += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                if (isTwoPerPage)
                {
                    if (printPageIndex < ek1TwoPerPageQueue.Count)
                    {
                        var page = ek1TwoPerPageQueue[printPageIndex];

                        DrawTwoPerA4Page(pe.Graphics, pe.PageBounds,
                            page.ent1, page.ent2,
                            page.t1, page.t2,
                            page.st1, page.st2,
                            _appSettings, _selectedMonth, _studentMonthlyAbsenceMap);

                        printPageIndex++;
                        pe.HasMorePages = printPageIndex < ek1TwoPerPageQueue.Count;
                    }
                    else
                    {
                        pe.HasMorePages = false;
                    }
                }
                else
                {
                    if (printPageIndex < ek1SinglePageQueue.Count)
                    {
                        var page = ek1SinglePageQueue[printPageIndex];

                        DrawEk1Page(pe.Graphics, pe.PageBounds,
                            page.ent, page.teacher, page.students,
                            _appSettings, _selectedMonth, _studentMonthlyAbsenceMap);

                        printPageIndex++;
                        pe.HasMorePages = printPageIndex < ek1SinglePageQueue.Count;
                    }
                    else
                    {
                        pe.HasMorePages = false;
                    }
                }
            };

            PrintPreviewHelper.ShowPreview(pd, $"Toplu Yazdırma Önizleme ({pageCountEk1} Sayfa)", this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Toplu yazdırma başlatılırken hata oluştu: {ex.Message}", "Yazdırma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (!Common.LicenseUIHelper.CheckFeature("Koordinatörlük Evraklarını Excel Olarak Dışa Aktarma", this))
            return;

        try
        {
            var available = GetAvailableEnterprises();
            if (available.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak işletme bulunamadı!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool isAll = _cmbEnterprise.SelectedIndex <= 0;
            var (previewEnt, _, _, _, _) = GetSelectionForPreview();
            List<Enterprise> targetEnterprises = isAll ? available : (previewEnt != null ? [previewEnt] : []);
            if (targetEnterprises.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak işletme seçilmedi!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int docTypeIdx = _cmbDocType.SelectedIndex;
            int teacherIdx = _cmbTeacher.SelectedIndex;
            Teacher? teacher = teacherIdx > 0 && teacherIdx <= _teachers.Count ? _teachers[teacherIdx - 1] : null;

            // Dışa aktarılacak işletmeleri koordinatör öğretmene göre alfabetik sıralayalım
            var trCultureExport = new System.Globalization.CultureInfo("tr-TR");
            var trComparerExport = StringComparer.Create(trCultureExport, true);
            targetEnterprises = targetEnterprises
                .Select(ent =>
                {
                    var entStudents = _students
                        .Where(s => s.EnterpriseId == ent.Id || string.Equals(s.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var t = ResolveTeacherForEnterprise(ent, teacher, entStudents);
                    return new { Enterprise = ent, Teacher = t };
                })
                .OrderBy(x => x.Teacher?.FullName ?? "ZZZ", trComparerExport)
                .ThenBy(x => x.Enterprise.Name, trComparerExport)
                .Select(x => x.Enterprise)
                .ToList();

            if (docTypeIdx == 1) // Günlük Rehberlik Görev Formu
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV Dosyası (*.csv)|*.csv",
                    FileName = isAll
                        ? $"Gunluk_Rehberlik_Toplu_{_selectedMonth:yyyy_MM}.csv"
                        : $"Gunluk_Rehberlik_{targetEnterprises[0].Name.Replace(" ", "_")}_{_selectedMonth:yyyy_MM}.csv"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    int totalDuties = 0;

                    foreach (var ent in targetEnterprises)
                    {
                        var entStudents = _students
                            .Where(s => s.EnterpriseId == ent.Id || string.Equals(s.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(s => s.Classroom)
                            .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
                            .ToList();

                        if (teacher != null)
                        {
                            var ts = entStudents.Where(s => s.TeacherId == teacher.Id).ToList();
                            if (ts.Count > 0) entStudents = ts;
                        }

                        var t = ResolveTeacherForEnterprise(ent, teacher, entStudents);
                        var visitDates = GetMonthlyVisitDates(ent, t, entStudents, _selectedMonth);
                        if (visitDates.Count == 0) visitDates.Add(_selectedMonth);

                        var firstStudent = entStudents.FirstOrDefault();
                        string fieldName = firstStudent != null && !string.IsNullOrWhiteSpace(firstStudent.FieldName)
                            ? (!string.IsNullOrWhiteSpace(firstStudent.Classroom) ? $"{firstStudent.Classroom} {firstStudent.FieldName}" : firstStudent.FieldName)
                            : (!string.IsNullOrWhiteSpace(ent.Sector) ? ent.Sector : "Mesleki Eğitim");

                        string branchName = !string.IsNullOrWhiteSpace(ent.Sector)
                            ? ent.Sector
                            : (firstStudent?.FieldName ?? "Genel");

                        for (int i = 0; i < visitDates.Count; i++)
                        {
                            totalDuties++;
                            var dutyDate = visitDates[i];
                            sb.AppendLine($"=== {ent.Name} - {i + 1}. GÖREV FORMU ({dutyDate:dd.MM.yyyy dddd}) ===");
                            sb.AppendLine("T.C.");
                            if (!string.IsNullOrWhiteSpace(_appSettings?.Valilik))
                                sb.AppendLine(_appSettings.Valilik);
                            if (!string.IsNullOrWhiteSpace(_appSettings?.DistrictMem))
                                sb.AppendLine(_appSettings.DistrictMem);
                            sb.AppendLine(_appSettings?.SchoolName ?? "");
                            sb.AppendLine("ISLETMELERDE MESLEK EGITIMI GUNLUK REHBERLIK GOREV FORMU");
                            sb.AppendLine();
                            sb.AppendLine($"Isletmenin Adi:;{ent.Name}");
                            sb.AppendLine($"Izlemede Sorumlu Oldugu Ogrenci Sayisi:;{entStudents.Count} ({NumberToTurkishWords(entStudents.Count)})");
                            sb.AppendLine($"Meslek Alani:;{fieldName}");
                            sb.AppendLine($"Meslek Dali:;{branchName}");
                            sb.AppendLine($"Gorev Tarihi:;{dutyDate:dd.MM.yyyy}");
                            sb.AppendLine();
                            sb.AppendLine("AYLIK REHBERLIK FORMUNA GORE :");
                            sb.AppendLine("1. Isletmede ogrenim goren ogrencilerin egitimini olumsuz yonde etkileyen hususlar varsa yaziniz :;");
                            sb.AppendLine("2. Belirtilen aksakliklarla ilgili yapilan rehberlik ve alinan onlemler :;");
                            sb.AppendLine("3. Aylik Rehberlik Formunda belirtilmesinde yarar gorulen hususlar :;");
                            sb.AppendLine();
                            sb.AppendLine("IMZALAR;");
                            sb.AppendLine($"Isletme Egitim Yetkilisi:;{ent.ContactPerson ?? "Yetkili"}");
                            sb.AppendLine($"Koordinator Ogretmen:;{t?.FullName ?? ent.TeacherName ?? "-"}");
                            sb.AppendLine($"Koordinator Mudur Yardimcisi:;{_appSettings.VicePrincipalName}");
                            sb.AppendLine();
                            sb.AppendLine(new string('-', 50));
                            sb.AppendLine();
                        }
                    }

                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Günlük rehberlik formu ({targetEnterprises.Count} işletme, {totalDuties} görev) başarıyla dışa aktarıldı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (docTypeIdx == 2) // 3. Aylık Devam-Devamsızlık Bildirim Çizelgesi
            {
                using var sfdAtt = new SaveFileDialog
                {
                    Filter = "CSV Dosyası (*.csv)|*.csv",
                    FileName = isAll
                        ? $"Aylik_Devamsizlik_Cizelgesi_Toplu_{_selectedMonth:yyyy_MM}.csv"
                        : $"Aylik_Devamsizlik_Cizelgesi_{targetEnterprises[0].Name.Replace(" ", "_")}_{_selectedMonth:yyyy_MM}.csv"
                };

                if (sfdAtt.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    int daysInMonth = DateTime.DaysInMonth(_selectedMonth.Year, _selectedMonth.Month);

                    foreach (var ent in targetEnterprises)
                    {
                        var entStudents = _students
                            .Where(s => s.EnterpriseId == ent.Id || string.Equals(s.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(s => s.Classroom)
                            .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
                            .ToList();

                        if (teacher != null)
                        {
                            var ts = entStudents.Where(s => s.TeacherId == teacher.Id).ToList();
                            if (ts.Count > 0) entStudents = ts;
                        }

                        var t = ResolveTeacherForEnterprise(ent, teacher, entStudents);

                        sb.AppendLine("T.C.");
                        if (!string.IsNullOrWhiteSpace(_appSettings?.Valilik))
                            sb.AppendLine(_appSettings.Valilik);
                        if (!string.IsNullOrWhiteSpace(_appSettings?.DistrictMem))
                            sb.AppendLine(_appSettings.DistrictMem);
                        sb.AppendLine(_appSettings?.SchoolName ?? "");
                        sb.AppendLine("OGRENCILERININ ISLETMELERDE MESLEKI EGITIMI AYLIK DEVAM-DEVAMSIZLIK BILDIRIM CIZELGESI");
                        sb.AppendLine();
                        sb.AppendLine($"Isletme Adi:;{ent.Name};Telefon:;{ent.Phone};Adres:;{ent.Address};Ay:;{_selectedMonth:MMMM yyyy};Cizelge Duzenleme Tarihi:;{GetLastWorkingDayOfMonth(_selectedMonth):dd.MM.yyyy}");
                        sb.AppendLine();

                        var headerCols = new List<string> { "Adi Soyadi", "No", "Alani Dali", "Ogun" };
                        for (int d = 1; d <= 31; d++) headerCols.Add(d.ToString());
                        headerCols.Add("Ozurlu Dev.");
                        headerCols.Add("Ozursuz Dev.");
                        sb.AppendLine(string.Join(";", headerCols));

                        foreach (var st in entStudents)
                        {
                            var sRow = new List<string> { st.FullName, st.StudentNumber, st.FieldName ?? "", "S" };
                            var oRow = new List<string> { st.FullName, st.StudentNumber, st.FieldName ?? "", "O" };

                            for (int d = 1; d <= 31; d++)
                            {
                                if (d > daysInMonth)
                                {
                                    sRow.Add("");
                                    oRow.Add("");
                                    continue;
                                }

                                var dt = new DateTime(_selectedMonth.Year, _selectedMonth.Month, d);
                                string sVal = "";
                                string oVal = "";

                                if (_studentDayAttendanceMap.TryGetValue((st.Id, d), out string? code))
                                {
                                    if (code == "D") { sVal = "D"; oVal = "D"; }
                                    else if (code == "SY") { sVal = "D"; oVal = "X"; }
                                    else if (code == "ÖY") { sVal = "X"; oVal = "D"; }
                                    else if (code == "Y") { sVal = "D"; oVal = "X"; }
                                    else if (code == "İ") { sVal = "İ"; oVal = "İ"; }
                                    else if (code == "S" || code == "SH" || code == "K") { sVal = "R"; oVal = "R"; }
                                    else if (code == "T") { sVal = "T"; oVal = "T"; }
                                    else if (code == "X") { sVal = "X"; oVal = "X"; }
                                    else { sVal = code; oVal = code; }
                                }
                                else
                                {
                                    if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
                                    {
                                        sVal = "";
                                        oVal = "";
                                    }
                                    else
                                    {
                                        sVal = "X";
                                        oVal = "X";
                                    }
                                }

                                sRow.Add(sVal);
                                oRow.Add(oVal);
                            }

                            string ozurlu = "0";
                            string ozursuz = "0";
                            if (_studentAbsenceTotalsMap.TryGetValue(st.Id, out var totals))
                            {
                                if (totals.excused > 0) ozurlu = totals.excused.ToString("0.#");
                                if (totals.unexcused > 0) ozursuz = totals.unexcused.ToString("0.#");
                            }

                            sRow.Add(ozurlu);
                            sRow.Add(ozursuz);
                            oRow.Add("");
                            oRow.Add("");

                            sb.AppendLine(string.Join(";", sRow));
                            sb.AppendLine(string.Join(";", oRow));
                        }

                        sb.AppendLine();
                        sb.AppendLine(new string('-', 50));
                        sb.AppendLine();
                    }

                    System.IO.File.WriteAllText(sfdAtt.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Aylık devam-devamsızlık bildirim çizelgesi ({targetEnterprises.Count} işletme) başarıyla dışa aktarıldı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            // docTypeIdx == 0 (Aylık Ek-1)
            using var sfdEk1 = new SaveFileDialog
            {
                Filter = "CSV Dosyası (*.csv)|*.csv",
                FileName = isAll
                    ? $"Ek1_Ziyaret_Formu_Toplu_{_selectedMonth:yyyy_MM}.csv"
                    : $"Ek1_Ziyaret_Formu_{targetEnterprises[0].Name.Replace(" ", "_")}_{_selectedMonth:yyyy_MM}.csv"
            };

            if (sfdEk1.ShowDialog() == DialogResult.OK)
            {
                var sb = new StringBuilder();

                foreach (var ent in targetEnterprises)
                {
                    var entStudents = _students
                        .Where(s => s.EnterpriseId == ent.Id || string.Equals(s.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(s => s.Classroom)
                        .ThenBy(s => FeeCalculator.ParseStudentNumber(s.StudentNumber))
                        .ToList();

                    if (teacher != null)
                    {
                        var ts = entStudents.Where(s => s.TeacherId == teacher.Id).ToList();
                        if (ts.Count > 0) entStudents = ts;
                    }

                    var t = ResolveTeacherForEnterprise(ent, teacher, entStudents);

                    sb.AppendLine("T.C.");
                    if (!string.IsNullOrWhiteSpace(_appSettings?.Valilik))
                        sb.AppendLine(_appSettings.Valilik);
                    if (!string.IsNullOrWhiteSpace(_appSettings?.DistrictMem))
                        sb.AppendLine(_appSettings.DistrictMem);
                    sb.AppendLine(_appSettings?.SchoolName ?? "");
                    sb.AppendLine("ISLETMELERDE MESLEKI EGITIM / STAJ AYLIK REHBERLİK VE DENETIM FORMU (EK-1)");
                    sb.AppendLine($"Donem: {_selectedMonth:MMMM yyyy}");
                    sb.AppendLine();
                    sb.AppendLine($"Isletme Adi:;{ent.Name}");
                    sb.AppendLine($"Adres:;{ent.Address}");
                    sb.AppendLine($"Telefon:;{ent.Phone}");
                    sb.AppendLine($"Koordinator Ogretmen:;{t?.FullName ?? ent.TeacherName ?? "-"}");
                    sb.AppendLine($"Egitici Personel / Usta Ogretici:;{ent.ContactPerson ?? "-"}");
                    sb.AppendLine($"Ziyaret Tarihleri:;{FormatVisitDates(ent, t, entStudents, _selectedMonth)}");
                    sb.AppendLine();
                    sb.AppendLine("Sira;Okul No;TC Kimlik No;Adi Soyadi;Sinifi;Alani;Devamsizlik;Mesleki Gelisim");

                    int idx = 1;
                    foreach (var st in entStudents)
                    {
                        string abs = "Devamli";
                        if (_studentMonthlyAbsenceMap.TryGetValue(st.Id, out double dCount) && dCount > 0)
                            abs = $"{dCount:0.#} Gun";

                        sb.AppendLine($"{idx++};{st.StudentNumber};{st.TcKimlikNo};{st.FullName};{st.Classroom};{st.FieldName};{abs};Uyumlu");
                    }

                    sb.AppendLine();
                    sb.AppendLine("DENETIM VE REHBERLIK KONULARI");
                    sb.AppendLine("1. Calisma ve dinlenme saatleri mevzuata uygun mu?;EVET");
                    sb.AppendLine("2. ISG onlemleri alinmis ve donanimlar kullaniyor mu?;EVET");
                    sb.AppendLine("3. Alana/dala uygun temrin yaptiriliyor mu?;EVET");
                    sb.AppendLine("4. Gelisim tablosu ve staj dosyasi duzenli tutuluyor mu?;EVET");
                    sb.AppendLine("5. Aylik ogrenci ucreti odenip dekont iletildi mi?;EVET");
                    sb.AppendLine("6. Usta ogretici ve isletme ile koordine saglaniyor mu?;EVET");
                    sb.AppendLine();
                    sb.AppendLine(new string('-', 50));
                    sb.AppendLine();
                }

                System.IO.File.WriteAllText(sfdEk1.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Ziyaret formu ({targetEnterprises.Count} işletme) başarıyla dışa aktarıldı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
