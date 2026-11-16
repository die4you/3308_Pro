using System.Drawing.Drawing2D;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.UI.Views;

public class DashboardView : UserControl
{
    private readonly ITeacherRepository _teacherRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly ICalendarTaskRepository _calendarTaskRepo;
    private readonly Action<string> _navigateAction;
    private readonly Action _reloadAllAction;

    // Stat labels
    private Label _lblTeachers = null!;
    private Label _lblEnterprises = null!;
    private Label _lblCapacity = null!;
    private Label _lblOrgunStudents = null!;
    private Label _lblMesemStudents = null!;
    private Label _lblStudents = null!;
    private Label _lblAssigned = null!;
    private Label _lblPending = null!;

    private Label _lblMissingTcCount = null!;
    private Label _lblMissingTcDesc = null!;
    private Button _btnGoTc = null!;
    private Color _tcAccentColor = Color.FromArgb(225, 29, 72);
    private int _lastMissingTcCount = 0;

    private Label _lblMissingEntryCount = null!;
    private Label _lblMissingEntryDesc = null!;
    private Button _btnGoEntry = null!;
    private Color _entryAccentColor = Color.FromArgb(217, 119, 6);

    private Label _lblAuditStatus = null!;
    private Label _lblAuditDesc = null!;
    private Button _btnGoAudit = null!;
    private Color _auditAccentColor = Color.FromArgb(79, 70, 229);

    // Monthly Tasks UI components
    private readonly List<CalendarTask> _currentMonthTasks = new();
    private DateTime _selectedCalendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _termStartDate = new DateTime(DateTime.Today.Year, 9, 1);
    private DateTime _termEndDate = new DateTime(DateTime.Today.Year + 1, 6, 30);
    private bool _isLoadingCalendarTasks = false;
    private readonly Dictionary<int, Panel> _taskRowPanels = new();
    private readonly Dictionary<int, CheckBox> _taskCheckBoxes = new();
    private readonly Dictionary<int, Label> _taskTitleLabels = new();
    private readonly Dictionary<int, Label> _taskSubtitleLabels = new();
    private readonly Dictionary<int, Label> _taskStatusBadges = new();
    private Label _lblCalendarTitle = null!;
    private Button _btnPrevMonth = null!;
    private Button _btnNextMonth = null!;
    private Button _btnCurrentMonth = null!;
    private Button _btnManageTasks = null!;
    private TableLayoutPanel _pnlTasks = null!;
    private Label _lblTaskProgress = null!;
    private Label _lblCurrentPeriodBadge = null!;

    public DashboardView(
        ITeacherRepository teacherRepo,
        IEnterpriseRepository enterpriseRepo,
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        ISettingRepository settingRepo,
        ICalendarTaskRepository calendarTaskRepo,
        Action<string> navigateAction,
        Action reloadAllAction)
    {
        _teacherRepo = teacherRepo;
        _enterpriseRepo = enterpriseRepo;
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _settingRepo = settingRepo;
        _calendarTaskRepo = calendarTaskRepo;
        _navigateAction = navigateAction;
        _reloadAllAction = reloadAllAction;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = Color.FromArgb(248, 250, 252); // slate-50
        this.AutoScroll = true;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24),
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 1. Karşılama ve Durum Hero Kartı (Gradient Mavi - En Üstte)
        var heroCard = CreateHeroCard();
        mainPanel.Controls.Add(heroCard, 0, 0);

        // 2. 8 KPI Stat Kartı
        var statsPanel = CreateStatsPanel();
        mainPanel.Controls.Add(statsPanel, 0, 1);

        // 3. %50 - %50 İki Sütunlu Panel (Sol: Aylık İdari Takvim, Sağ: Eksik Bilgi & Uyarı Kartları)
        var middleSplit = CreateMiddleSplitPanel();
        mainPanel.Controls.Add(middleSplit, 0, 2);

        this.Controls.Add(mainPanel);
    }

    private Panel CreateHeroCard()
    {
        var panel = new DoubleBufferedPanel
        {
            Height = 135,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(29, 78, 216),
            Margin = new Padding(0, 0, 0, 20)
        };

        var btnRefresh = new Button
        {
            Text = "🔄 Verileri Yenile",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(79, 70, 229), // indigo-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 42,
            Width = 140,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(panel.Width - 165, 46),
            Cursor = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);
        btnRefresh.FlatAppearance.BorderSize = 1;
        btnRefresh.Click += async (s, e) => await LoadDataAsync();

        panel.Resize += (s, e) =>
        {
            btnRefresh.Left = Math.Max(290, panel.Width - 165);
            panel.Invalidate();
        };

        panel.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Arka plan degrade geçişi (Mavi - İndigo)
            using var brush = new LinearGradientBrush(
                panel.ClientRectangle,
                Color.FromArgb(29, 78, 216), // blue-700
                Color.FromArgb(67, 56, 202), // indigo-700
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, panel.ClientRectangle);

            // 1. Üst Rozet / Ön Başlık Metni
            using var fontSub = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            TextRenderer.DrawText(
                e.Graphics,
                "MİLLİ EĞİTİM BAKANLIĞI STANDARTLARINDA",
                fontSub,
                new Point(24, 20),
                Color.FromArgb(191, 219, 254),
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);

            // 2. Ana Başlık
            using var fontTitle = new Font("Segoe UI", 14f, FontStyle.Bold);
            TextRenderer.DrawText(
                e.Graphics,
                "3308 Pro - İşletmelerde Beceri Eğitimi & Koordinatörlük Sistemi",
                fontTitle,
                new Point(24, 44),
                Color.White,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);

            // 3. Açıklama Metni (Font küçültüldü ve Öneri 1 uygulandı)
            using var fontDesc = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            TextRenderer.DrawText(
                e.Graphics,
                "Öğrenci, işletme ve koordinatör öğretmen eşleştirmelerini yönetin; SGK bildirgeleri, ek ders ve resmi formları tek tıkla hazırlayın.",
                fontDesc,
                new Point(24, 82),
                Color.FromArgb(224, 231, 255),
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        };

        panel.Controls.Add(btnRefresh);

        return panel;
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private Panel CreateMonthlyTasksCard()
    {
        var card = new DoubleBufferedPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 20),
            Padding = new Padding(20, 16, 20, 16)
        };

        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);

            // Sol şerit: Indigo degrade
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, 5, card.Height),
                Color.FromArgb(59, 130, 246), // blue-500
                Color.FromArgb(99, 102, 241), // indigo-500
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, 0, 0, 5, card.Height);
        };

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.Transparent
        };

        _btnPrevMonth = new Button
        {
            Text = "◀",
            Size = new Size(26, 24),
            Location = new Point(0, 1),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnPrevMonth.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnPrevMonth.Click += (s, e) => ChangeCalendarMonth(-1);

        _btnNextMonth = new Button
        {
            Text = "▶",
            Size = new Size(26, 24),
            Location = new Point(28, 1),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnNextMonth.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnNextMonth.Click += (s, e) => ChangeCalendarMonth(1);

        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        string monthName = _selectedCalendarMonth.ToString("MMMM yyyy", trCulture).ToUpper(trCulture);

        _lblCalendarTitle = new Label
        {
            Text = $"📌 {monthName} — AYLIK İDARİ İŞLEM VE GÖREV TAKVİMİ",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42), // slate-900
            AutoSize = true,
            Location = new Point(60, 3),
            UseMnemonic = false
        };

        _btnCurrentMonth = new Button
        {
            Text = "🔘 Bu Ay",
            Size = new Size(68, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(238, 242, 255), // indigo-50
            ForeColor = Color.FromArgb(67, 56, 202), // indigo-700
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnCurrentMonth.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);
        _btnCurrentMonth.Click += (s, e) => ResetCalendarToCurrentMonth();

        var lblSub = new Label
        {
            Text = "MEB & MYS resmi iş akışına göre aylık devamsızlık, SGK, dekont ve ödeme takip çizelgesi",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            AutoSize = true,
            Location = new Point(0, 27),
            UseMnemonic = false
        };

        _lblCurrentPeriodBadge = new Label
        {
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4)
        };

        _lblTaskProgress = new Label
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true
        };

        _lblCalendarTitle.AutoEllipsis = true;
        lblSub.AutoEllipsis = true;

        UpdatePeriodBadge();
        UpdateTasksProgress();

        _btnManageTasks = new Button
        {
            Text = "⚙️ Görevleri Yönet...",
            Size = new Size(140, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnManageTasks.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnManageTasks.Click += (s, e) => _navigateAction("task-calendar");

        pnlHeader.Controls.AddRange([_btnPrevMonth, _btnNextMonth, _lblCalendarTitle, _btnCurrentMonth, _btnManageTasks, lblSub, _lblCurrentPeriodBadge, _lblTaskProgress]);

        pnlHeader.Resize += (s, e) =>
        {
            int r = pnlHeader.Width - 8;
            _lblCurrentPeriodBadge.Location = new Point(Math.Max(10, r - _lblCurrentPeriodBadge.PreferredWidth), 0);
            _lblTaskProgress.Location = new Point(Math.Max(10, r - _lblTaskProgress.PreferredWidth), 25);

            int leftPos = _lblCalendarTitle.Right + 8;
            if (_btnCurrentMonth.Visible)
            {
                _btnCurrentMonth.Location = new Point(leftPos, 1);
                leftPos = _btnCurrentMonth.Right + 6;
            }
            _btnManageTasks.Location = new Point(leftPos, 1);
        };

        contentLayout.Controls.Add(pnlHeader, 0, 0);

        // Tasks container (Dinamik Tablo)
        _pnlTasks = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0)
        };
        _pnlTasks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        contentLayout.Controls.Add(_pnlTasks, 0, 1);
        card.Controls.Add(contentLayout);

        return card;
    }

    private Panel CreateTaskRowPanel(CalendarTask task)
    {
        bool isCurrentMonth = _selectedCalendarMonth.Year == DateTime.Today.Year && _selectedCalendarMonth.Month == DateTime.Today.Month;
        int todayDay = DateTime.Today.Day;
        bool isActivePeriod = isCurrentMonth && todayDay >= task.StartDay && todayDay <= task.EndDay;

        var row = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = isActivePeriod ? Color.FromArgb(248, 250, 252) : Color.White,
            Margin = new Padding(0, 3, 0, 3),
            Padding = new Padding(8, 4, 8, 4)
        };

        row.Paint += (s, e) =>
        {
            bool isCurrent = _selectedCalendarMonth.Year == DateTime.Today.Year && _selectedCalendarMonth.Month == DateTime.Today.Month;
            int curDay = DateTime.Today.Day;
            bool active = isCurrent && curDay >= task.StartDay && curDay <= task.EndDay;

            using var pen = new Pen(active ? Color.FromArgb(199, 210, 254) : Color.FromArgb(241, 245, 249), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);

            if (active)
            {
                using var brush = new SolidBrush(Color.FromArgb(99, 102, 241));
                e.Graphics.FillRectangle(brush, 0, 0, 3, row.Height);
            }
        };

        var chk = new CheckBox
        {
            Location = new Point(10, 16),
            Size = new Size(18, 18),
            Cursor = Cursors.Hand
        };
        _taskCheckBoxes[task.Id] = chk;

        var lblRange = new Label
        {
            Text = task.DisplayRange,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = isActivePeriod ? Color.FromArgb(67, 56, 202) : Color.FromArgb(100, 116, 139),
            BackColor = isActivePeriod ? Color.FromArgb(224, 231, 255) : Color.FromArgb(241, 245, 249),
            AutoSize = false,
            Size = new Size(84, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(34, 13)
        };

        var lblTitle = new Label
        {
            Text = task.Title,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoEllipsis = true,
            Location = new Point(126, 7),
            Height = 20
        };
        _taskTitleLabels[task.Id] = lblTitle;

        var lblSubtitle = new Label
        {
            Text = task.Subtitle,
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoEllipsis = true,
            Location = new Point(126, 28),
            Height = 18
        };
        _taskSubtitleLabels[task.Id] = lblSubtitle;

        var lblStatusBadge = new Label
        {
            Text = "BEKLİYOR",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(6, 2, 6, 2),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(row.Width - 230, 15)
        };
        _taskStatusBadges[task.Id] = lblStatusBadge;

        Button? btnNav = null;
        if (!string.IsNullOrEmpty(task.NavTarget))
        {
            btnNav = new Button
            {
                Text = task.NavBtnText ?? "Git",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(125, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(row.Width - 135, 12),
                Cursor = Cursors.Hand
            };
            btnNav.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnNav.Click += (s, e) => _navigateAction(task.NavTarget);
        }

        chk.CheckedChanged += async (s, e) =>
        {
            ApplyTaskCheckStyle(task.Id, chk.Checked);
            UpdateTasksProgress();
            if (!_isLoadingCalendarTasks)
            {
                await SaveTaskStatusAsync(task.Id, chk.Checked, task.Title);
            }
        };

        row.Controls.Add(chk);
        row.Controls.Add(lblRange);
        row.Controls.Add(lblTitle);
        row.Controls.Add(lblSubtitle);
        row.Controls.Add(lblStatusBadge);
        if (btnNav != null) row.Controls.Add(btnNav);

        row.Resize += (s, e) =>
        {
            int rightAnchor = row.Width - 10;
            if (btnNav != null)
            {
                btnNav.Left = Math.Max(120, rightAnchor - btnNav.Width);
                lblStatusBadge.Left = Math.Max(110, btnNav.Left - lblStatusBadge.PreferredWidth - 8);
            }
            else
            {
                lblStatusBadge.Left = Math.Max(110, rightAnchor - lblStatusBadge.PreferredWidth);
            }

            int available = lblStatusBadge.Left - lblTitle.Left - 8;
            if (available > 40)
            {
                lblTitle.Width = available;
                lblSubtitle.Width = available;
            }
        };

        return row;
    }

    private async void ChangeCalendarMonth(int offset)
    {
        DateTime target = _selectedCalendarMonth.AddMonths(offset);
        DateTime minMonth = new DateTime(_termStartDate.Year, _termStartDate.Month, 1);
        DateTime maxMonth = new DateTime(_termEndDate.Year, _termEndDate.Month, 1);

        if (offset < 0 && target < minMonth) return;
        if (offset > 0 && target > maxMonth) return;

        _selectedCalendarMonth = target;
        UpdateCalendarHeaderUI();
        await RebuildTasksListAsync();
    }

    private async void ResetCalendarToCurrentMonth()
    {
        _selectedCalendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        UpdateCalendarHeaderUI();
        await RebuildTasksListAsync();
    }

    private void UpdateCalendarHeaderUI()
    {
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        string monthName = _selectedCalendarMonth.ToString("MMMM yyyy", trCulture).ToUpper(trCulture);
        _lblCalendarTitle.Text = $"📌 {monthName} — AYLIK İDARİ İŞLEM VE GÖREV TAKVİMİ";

        bool isCurrentMonth = _selectedCalendarMonth.Year == DateTime.Today.Year && _selectedCalendarMonth.Month == DateTime.Today.Month;
        _btnCurrentMonth.Visible = !isCurrentMonth;

        int leftPos = _lblCalendarTitle.Right + 8;
        if (_btnCurrentMonth.Visible)
        {
            _btnCurrentMonth.Location = new Point(leftPos, 1);
            leftPos = _btnCurrentMonth.Right + 6;
        }
        if (_btnManageTasks != null)
        {
            _btnManageTasks.Location = new Point(leftPos, 1);
        }

        DateTime minMonth = new DateTime(_termStartDate.Year, _termStartDate.Month, 1);
        DateTime maxMonth = new DateTime(_termEndDate.Year, _termEndDate.Month, 1);

        bool canGoPrev = _selectedCalendarMonth.AddMonths(-1) >= minMonth;
        bool canGoNext = _selectedCalendarMonth.AddMonths(1) <= maxMonth;

        _btnPrevMonth.Enabled = canGoPrev;
        _btnNextMonth.Enabled = canGoNext;
        _btnPrevMonth.ForeColor = canGoPrev ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
        _btnNextMonth.ForeColor = canGoNext ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
        _btnPrevMonth.Cursor = canGoPrev ? Cursors.Hand : Cursors.Default;
        _btnNextMonth.Cursor = canGoNext ? Cursors.Hand : Cursors.Default;

        UpdatePeriodBadge();
        RefreshTaskRowVisuals();
    }

    private void RefreshTaskRowVisuals()
    {
        bool isCurrentMonth = _selectedCalendarMonth.Year == DateTime.Today.Year && _selectedCalendarMonth.Month == DateTime.Today.Month;
        int todayDay = DateTime.Today.Day;

        foreach (var task in _currentMonthTasks)
        {
            bool isActivePeriod = isCurrentMonth && todayDay >= task.StartDay && todayDay <= task.EndDay;
            if (_taskRowPanels.TryGetValue(task.Id, out var row))
            {
                row.BackColor = isActivePeriod ? Color.FromArgb(248, 250, 252) : Color.White;
                row.Invalidate();
            }

            if (_taskCheckBoxes.TryGetValue(task.Id, out var chk))
            {
                ApplyTaskCheckStyle(task.Id, chk.Checked);
            }
        }
    }

    private void ApplyTaskCheckStyle(int taskId, bool isChecked)
    {
        if (_taskTitleLabels.TryGetValue(taskId, out var lblTitle))
        {
            lblTitle.Font = new Font("Segoe UI", 9f, isChecked ? FontStyle.Strikeout : FontStyle.Bold);
            lblTitle.ForeColor = isChecked ? Color.FromArgb(148, 163, 184) : Color.FromArgb(30, 41, 59);
        }

        if (_taskSubtitleLabels.TryGetValue(taskId, out var lblSubtitle))
        {
            lblSubtitle.ForeColor = isChecked ? Color.FromArgb(203, 213, 225) : Color.FromArgb(148, 163, 184);
        }

        if (_taskStatusBadges.TryGetValue(taskId, out var badge))
        {
            if (isChecked)
            {
                badge.Text = "✓ TAMAMLANDI";
                badge.ForeColor = Color.FromArgb(21, 128, 61); // green-700
                badge.BackColor = Color.FromArgb(220, 252, 231); // green-100
            }
            else
            {
                var task = _currentMonthTasks.FirstOrDefault(t => t.Id == taskId);
                bool isCurrentMonth = _selectedCalendarMonth.Year == DateTime.Today.Year && _selectedCalendarMonth.Month == DateTime.Today.Month;
                int todayDay = DateTime.Today.Day;
                bool isActive = task != null && isCurrentMonth && todayDay >= task.StartDay && todayDay <= task.EndDay;
                if (isActive)
                {
                    badge.Text = "⏳ AKTİF SÜREÇ";
                    badge.ForeColor = Color.FromArgb(180, 83, 9); // amber-700
                    badge.BackColor = Color.FromArgb(254, 243, 199); // amber-100
                }
                else
                {
                    badge.Text = "BEKLİYOR";
                    badge.ForeColor = Color.FromArgb(148, 163, 184);
                    badge.BackColor = Color.FromArgb(241, 245, 249);
                }
            }
        }
    }

    private void UpdatePeriodBadge()
    {
        bool isCurrentMonth = _selectedCalendarMonth.Year == DateTime.Today.Year && _selectedCalendarMonth.Month == DateTime.Today.Month;
        if (!isCurrentMonth)
        {
            if (_selectedCalendarMonth < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))
            {
                _lblCurrentPeriodBadge.Text = "📁 Geçmiş Dönem (Arşiv Kaydı)";
                _lblCurrentPeriodBadge.ForeColor = Color.FromArgb(71, 85, 105); // slate-600
                _lblCurrentPeriodBadge.BackColor = Color.FromArgb(241, 245, 249); // slate-100
            }
            else
            {
                _lblCurrentPeriodBadge.Text = "🗓️ Gelecek Dönem (Planlanan)";
                _lblCurrentPeriodBadge.ForeColor = Color.FromArgb(14, 116, 144); // cyan-700
                _lblCurrentPeriodBadge.BackColor = Color.FromArgb(207, 250, 254); // cyan-100
            }
            return;
        }

        int day = DateTime.Today.Day;
        if (day <= 5)
        {
            _lblCurrentPeriodBadge.Text = "🔴 [1 – 5 Günleri] SGK Hesaplama & Ödenek Süreci Aktif";
            _lblCurrentPeriodBadge.ForeColor = Color.FromArgb(190, 18, 60); // rose-700
            _lblCurrentPeriodBadge.BackColor = Color.FromArgb(255, 228, 230); // rose-100
        }
        else if (day <= 8)
        {
            _lblCurrentPeriodBadge.Text = "🟠 [1 – 8 Günleri] Dekont Toplama Süreci Aktif";
            _lblCurrentPeriodBadge.ForeColor = Color.FromArgb(180, 83, 9); // amber-700
            _lblCurrentPeriodBadge.BackColor = Color.FromArgb(254, 243, 199); // amber-100
        }
        else if (day >= 15)
        {
            _lblCurrentPeriodBadge.Text = "🔵 [15 – 30 Günleri] MYS Ödeme Süreci Aktif";
            _lblCurrentPeriodBadge.ForeColor = Color.FromArgb(29, 78, 216); // blue-700
            _lblCurrentPeriodBadge.BackColor = Color.FromArgb(219, 234, 254); // blue-100
        }
        else
        {
            _lblCurrentPeriodBadge.Text = "🟢 İdari Takip Süreci Devam Ediyor";
            _lblCurrentPeriodBadge.ForeColor = Color.FromArgb(21, 128, 61); // green-700
            _lblCurrentPeriodBadge.BackColor = Color.FromArgb(220, 252, 231); // green-100
        }
    }

    private void UpdateTasksProgress()
    {
        if (_lblTaskProgress == null) return;
        int total = _currentMonthTasks.Count;
        int completed = _taskCheckBoxes.Values.Count(c => c.Checked);
        _lblTaskProgress.Text = $"{completed}/{total} Görev Tamamlandı";
        if (total > 0 && completed == total)
        {
            _lblTaskProgress.ForeColor = Color.FromArgb(22, 101, 52); // green-800
        }
        else
        {
            _lblTaskProgress.ForeColor = Color.FromArgb(71, 85, 105);
        }
    }

    private async Task SaveTaskStatusAsync(int taskId, bool isChecked, string taskTitle)
    {
        if (_settingRepo == null || _isLoadingCalendarTasks) return;
        try
        {
            string key = $"MonthlyTask_{_selectedCalendarMonth:yyyy_MM}_{taskId}";
            await _settingRepo.SetValueAsync(key, isChecked ? "1" : "0", $"Aylık Görev {taskId} ({_selectedCalendarMonth:yyyy-MM}): {taskTitle}");
        }
        catch { }
    }

    private async Task RebuildTasksListAsync()
    {
        if (_pnlTasks == null) return;
        try
        {
            _isLoadingCalendarTasks = true;
            IEnumerable<CalendarTask> tasks;
            try
            {
                if (_calendarTaskRepo != null)
                {
                    tasks = await _calendarTaskRepo.GetTasksForMonthAsync(_selectedCalendarMonth);
                }
                else
                {
                    tasks = GetStandardTasksForMonth(_selectedCalendarMonth);
                }
            }
            catch
            {
                tasks = GetStandardTasksForMonth(_selectedCalendarMonth);
            }

            _currentMonthTasks.Clear();
            _currentMonthTasks.AddRange(tasks);

            _pnlTasks.SuspendLayout();
            _pnlTasks.Controls.Clear();
            _taskRowPanels.Clear();
            _taskCheckBoxes.Clear();
            _taskTitleLabels.Clear();
            _taskSubtitleLabels.Clear();
            _taskStatusBadges.Clear();

            _pnlTasks.RowCount = Math.Max(1, _currentMonthTasks.Count);

            if (_currentMonthTasks.Count == 0)
            {
                var pnlEmpty = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 60,
                    BackColor = Color.FromArgb(248, 250, 252),
                    Padding = new Padding(16, 12, 16, 12)
                };
                var lblEmpty = new Label
                {
                    Text = "ℹ️  Bu ay için tanımlanmış veya aktif bir idari görev bulunmuyor. Yeni görev eklemek veya gizlenenleri açmak için [⚙️ Görevleri Yönet] butonunu kullanabilirsiniz.",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                pnlEmpty.Controls.Add(lblEmpty);
                _pnlTasks.Controls.Add(pnlEmpty, 0, 0);
            }
            else
            {
                for (int i = 0; i < _currentMonthTasks.Count; i++)
                {
                    var task = _currentMonthTasks[i];
                    var rowPanel = CreateTaskRowPanel(task);
                    _taskRowPanels[task.Id] = rowPanel;
                    _pnlTasks.Controls.Add(rowPanel, 0, i);
                }
            }

            _pnlTasks.ResumeLayout();

            // Checkbox durumlarını veritabanından yükle
            string monthPrefix = $"MonthlyTask_{_selectedCalendarMonth:yyyy_MM}_";
            foreach (var task in _currentMonthTasks)
            {
                if (_taskCheckBoxes.TryGetValue(task.Id, out var chk))
                {
                    string key = $"{monthPrefix}{task.Id}";
                    var val = await _settingRepo.GetValueAsync(key);
                    bool isChecked = val == "1";
                    chk.Checked = isChecked;
                    ApplyTaskCheckStyle(task.Id, isChecked);
                }
            }

            UpdateTasksProgress();
            UpdatePeriodBadge();
        }
        catch { }
        finally
        {
            _isLoadingCalendarTasks = false;
        }
    }

    private static List<CalendarTask> GetStandardTasksForMonth(DateTime month)
    {
        var list = new List<CalendarTask>
        {
            // 5 Temel Aylık Rutin MEB Görevi (Her Ay Geçerli)
            new()
            {
                Id = 1,
                RangeText = "1 – 5 Arası",
                Title = "Devamsızlıklar kontrol edildi, SGK primleri hesaplandı, e-Bildirge onaylandı ve tahakkuk fişi alındı",
                Subtitle = "Aylık devamsızlık ve SGK e-Bildirge tahakkuk işlemleri",
                StartDay = 1,
                EndDay = 5,
                NavTarget = "toplu-isegiris-cikis",
                NavBtnText = "⚡ Sigorta İşlemleri"
            },
            new()
            {
                Id = 2,
                RangeText = "1 – 5 Arası",
                Title = "SGK Prim Ödenek Talebi Yapıldı",
                Subtitle = "MEBBİS ➔ Ödenek İşlemleri ➔ Personel Giderleri",
                StartDay = 1,
                EndDay = 5
            },
            new()
            {
                Id = 3,
                RangeText = "1 – 8 Arası",
                Title = "İşletmelerden staj dekontları toplandı ve sisteme girildi",
                Subtitle = "Stajyer öğrenci ücret dekontlarının kontrolü ve sisteme işlenmesi",
                StartDay = 1,
                EndDay = 8,
                NavTarget = "fees",
                NavBtnText = "💳 Ücret & Dekont"
            },
            new()
            {
                Id = 4,
                RangeText = "15 – 30 Arası",
                Title = "Devlet Desteği Ödemeleri MYS üzerinden Yapıldı",
                Subtitle = "Harcama Yönetim Sistemi (MYS) üzerinden işletme devlet desteği aktarımı",
                StartDay = 15,
                EndDay = 31,
                NavTarget = "fees",
                NavBtnText = "📋 Devlet Desteği"
            },
            new()
            {
                Id = 5,
                RangeText = "15 – 30 Arası",
                Title = "SGK Prim Ödemeleri MYS üzerinden Yapıldı",
                Subtitle = "SGK prim ödemelerinin MYS üzerinden muhasebeleştirilmesi",
                StartDay = 15,
                EndDay = 31
            }
        };

        // Dönemsel Standart MEB Görevleri (İlgili Aylarda Otomatik Eklenir)
        if (month.Month == 9) // Eylül
        {
            list.Add(new()
            {
                Id = 6,
                RangeText = "1 – 15 Eylül",
                Title = "İşletme Sözleşmeleri & Koordinatörlük Görev Dağılımı",
                Subtitle = "Yeni eğitim öğretim yılı sözleşmelerinin imzalanması ve öğretmen görev dağılımı",
                StartDay = 1,
                EndDay = 15,
                NavTarget = "coordinator-assignments",
                NavBtnText = "📋 Görevlendirme"
            });
            list.Add(new()
            {
                Id = 7,
                RangeText = "1 – 20 Eylül",
                Title = "Staja Başlayan Öğrencilerin SGK İşe Giriş Bildirgeleri",
                Subtitle = "e-Sigorta üzerinden stajyer işe giriş bildirgelerinin onaylanması",
                StartDay = 1,
                EndDay = 20,
                NavTarget = "toplu-isegiris-cikis",
                NavBtnText = "⚡ İşe Giriş"
            });
        }
        else if (month.Month == 1) // Ocak
        {
            list.Add(new()
            {
                Id = 8,
                RangeText = "10 – 20 Ocak",
                Title = "1. Dönem Koordinatörlük Not Fişleri ve Devamsızlık Kapanışı",
                Subtitle = "1. yarıyıl staj notlarının e-Okul sistemine girilmesi ve devamsızlık kontrolü",
                StartDay = 10,
                EndDay = 20,
                NavTarget = "attendance",
                NavBtnText = "📝 Devamsızlık"
            });
        }
        else if (month.Month == 2) // Şubat
        {
            list.Add(new()
            {
                Id = 9,
                RangeText = "1 – 15 Şubat",
                Title = "2. Dönem Koordinatörlük & İşletme Planlama Güncellemesi",
                Subtitle = "2. yarıyıl işletme değişiklikleri ve koordinatörlük görev onayları",
                StartDay = 1,
                EndDay = 15,
                NavTarget = "coordinator-assignments",
                NavBtnText = "📋 Görevlendirme"
            });
        }
        else if (month.Month == 6) // Haziran
        {
            list.Add(new()
            {
                Id = 10,
                RangeText = "1 – 15 Haziran",
                Title = "Yıl Sonu Beceri Eğitimi Sınav Evrakları & Staj Dosyaları Teslimi",
                Subtitle = "Öğrenci staj dosyalarının ve beceri eğitimi sınav not fişlerinin idareye teslimi",
                StartDay = 1,
                EndDay = 15
            });
            list.Add(new()
            {
                Id = 11,
                RangeText = "15 – 30 Haziran",
                Title = "Mezun Olan Öğrencilerin SGK İşten Ayrılış Bildirgeleri",
                Subtitle = "12. sınıf mezunlarının SGK işten ayrılış bildirgelerinin e-Sigorta'dan verilmesi",
                StartDay = 15,
                EndDay = 30,
                NavTarget = "toplu-isegiris-cikis",
                NavBtnText = "⚡ SGK Çıkış"
            });
        }

        return list.OrderBy(t => t.StartDay).ThenBy(t => t.Id).ToList();
    }

    private TableLayoutPanel CreateStatsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            ColumnCount = 8,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 20)
        };

        for (int i = 0; i < 8; i++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));

        var c1 = CreateStatCard("AKTİF ÖĞRETMEN", out _lblTeachers, Color.FromArgb(29, 78, 216), "Toplam / Aktif");
        var c2 = CreateStatCard("İŞLETMELER", out _lblEnterprises, Color.FromArgb(124, 58, 237), "Kayıtlı Kurum");
        var c3 = CreateStatCard("TOPLAM KONTENJAN", out _lblCapacity, Color.FromArgb(79, 70, 229), "İşletme Kapasitesi");
        var c4 = CreateStatCard("ÖRGÜN EĞİTİM", out _lblOrgunStudents, Color.FromArgb(37, 99, 235), "");
        var c5 = CreateStatCard("MESEM ÖĞRENCİ", out _lblMesemStudents, Color.FromArgb(13, 148, 136), "");
        var c6 = CreateStatCard("TOPLAM ÖĞRENCİ", out _lblStudents, Color.FromArgb(30, 41, 59), "");
        var c7 = CreateStatCard("TAM EŞLEŞEN", out _lblAssigned, Color.FromArgb(16, 185, 129), "İşletme & Öğretmen Tam");
        var c8 = CreateStatCard("BEKLEYEN / EKSİK", out _lblPending, Color.FromArgb(217, 119, 6), "Atama Bekliyor");

        panel.Controls.Add(c1, 0, 0);
        panel.Controls.Add(c2, 1, 0);
        panel.Controls.Add(c3, 2, 0);
        panel.Controls.Add(c4, 3, 0);
        panel.Controls.Add(c5, 4, 0);
        panel.Controls.Add(c6, 5, 0);
        panel.Controls.Add(c7, 6, 0);
        panel.Controls.Add(c8, 7, 0);

        return panel;
    }

    private Panel CreateStatCard(string title, out Label valueLabel, Color accentColor, string subText)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(4),
            Padding = new Padding(12)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Top,
            Height = 18
        };

        valueLabel = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = accentColor,
            Dock = DockStyle.Top,
            Height = 36
        };

        var lblSub = new Label
        {
            Text = subText,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Bottom,
            Height = 18
        };

        card.Controls.Add(lblSub);
        card.Controls.Add(valueLabel);
        card.Controls.Add(lblTitle);

        return card;
    }

    private TableLayoutPanel CreateMiddleSplitPanel()
    {
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 20)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Sol Sütun (%50): Aylık İdari İşlem ve Görev Takvimi
        var monthlyTasksCard = CreateMonthlyTasksCard();
        monthlyTasksCard.Dock = DockStyle.Fill;
        monthlyTasksCard.Margin = Padding.Empty;

        // Sağ Sütun (%50): Eksik Bilgi & SGK Uyarı Kartları (Üst üste 2 kart)
        var rightAlertsPanel = CreateStackedAlertsPanel();
        rightAlertsPanel.Dock = DockStyle.Fill;
        rightAlertsPanel.Margin = Padding.Empty;

        split.Controls.Add(monthlyTasksCard, 0, 0);
        split.Controls.Add(rightAlertsPanel, 2, 0);

        return split;
    }

    private TableLayoutPanel CreateStackedAlertsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));

        var cardAudit = CreateAlertCard(
            title: "🔍  E-OKUL ÖĞRENCİ KONTROLÜ & E-BİLDİRGE UYUMU",
            btnText: "🔍 Kontrolü Aç",
            btnColor: Color.FromArgb(79, 70, 229),
            onAction: () => _navigateAction("student-audit"),
            () => _auditAccentColor,
            out _lblAuditStatus,
            out _lblAuditDesc,
            out _btnGoAudit
        );
        cardAudit.Margin = new Padding(0, 0, 0, 6);

        var cardTc = CreateAlertCard(
            title: "🆔  T.C. KİMLİK NUMARASI EKSİK ÖĞRENCİLER",
            btnText: "🔍 Eksikleri Tamamla",
            btnColor: Color.FromArgb(225, 29, 72),
            onAction: () => _navigateAction(_lastMissingTcCount > 0 ? "students:missing-tc" : "students"),
            () => _tcAccentColor,
            out _lblMissingTcCount,
            out _lblMissingTcDesc,
            out _btnGoTc
        );
        cardTc.Margin = new Padding(0, 3, 0, 3);

        var cardEntry = CreateAlertCard(
            title: "📅  SGK İŞE GİRİŞ TARİHİ EKSİK ÖĞRENCİLER",
            btnText: "⚡ Toplu Tarih Ekle",
            btnColor: Color.FromArgb(217, 119, 6),
            onAction: () => _navigateAction("toplu-isegiris-cikis"),
            () => _entryAccentColor,
            out _lblMissingEntryCount,
            out _lblMissingEntryDesc,
            out _btnGoEntry
        );
        cardEntry.Margin = new Padding(0, 6, 0, 0);

        panel.Controls.Add(cardAudit, 0, 0);
        panel.Controls.Add(cardTc, 0, 1);
        panel.Controls.Add(cardEntry, 0, 2);

        return panel;
    }

    private Panel CreateAlertCard(
        string title,
        string btnText,
        Color btnColor,
        Action onAction,
        Func<Color> getAccentColor,
        out Label countLabel,
        out Label descLabel,
        out Button actionBtn)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18, 12, 18, 12)
        };

        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);

            // Sol renkli vurgu şeridi
            using var brush = new SolidBrush(getAccentColor());
            e.Graphics.FillRectangle(brush, 0, 0, 5, card.Height);
        };

        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Left,
            AutoSize = true
        };
        pnlTop.Controls.Add(lblTitle);

        var pnlBottom = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        countLabel = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = btnColor,
            Location = new Point(0, 4),
            AutoSize = true
        };

        descLabel = new Label
        {
            Text = "Yükleniyor...",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(0, 38),
            Size = new Size(340, 24),
            AutoEllipsis = true
        };

        actionBtn = new Button
        {
            Text = btnText,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = btnColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(165, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        actionBtn.FlatAppearance.BorderSize = 0;
        actionBtn.Click += (s, e) => onAction();

        var localBtn = actionBtn;
        var localDesc = descLabel;
        void PositionControls()
        {
            localBtn.Location = new Point(Math.Max(180, pnlBottom.ClientSize.Width - localBtn.Width), 12);
            localDesc.Width = Math.Max(150, localBtn.Left - 10);
        }
        pnlBottom.Resize += (s, e) => PositionControls();
        PositionControls();

        pnlBottom.Controls.AddRange([countLabel, descLabel, actionBtn]);

        card.Controls.Add(pnlBottom);
        card.Controls.Add(pnlTop);

        return card;
    }



    public async Task LoadDataAsync()
    {
        try
        {
            var settings = await _settingRepo.GetAppSettingsAsync();
            if (settings != null)
            {
                if (DateTime.TryParse(settings.OrgunStartDate, out var sDate)) _termStartDate = sDate;
                if (DateTime.TryParse(settings.OrgunEndDate, out var eDate)) _termEndDate = eDate;
                UpdateCalendarHeaderUI();
            }
        }
        catch { }

        await RebuildTasksListAsync();

        var teachers = (await _teacherRepo.GetAllAsync()).ToList();
        var enterprises = (await _enterpriseRepo.GetAllAsync()).ToList();
        var students = (await _studentRepo.GetAllAsync()).ToList();
        var classes = (await _classRepo.GetAllAsync()).ToList();

        _lblTeachers.Text = teachers.Count.ToString();
        _lblEnterprises.Text = enterprises.Count.ToString();
        _lblCapacity.Text = enterprises.Sum(e => e.Capacity).ToString();

        int mesemCount = students.Count(s =>
        {
            var cls = classes.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
            return FeeCalculator.IsMesem(s, cls);
        });
        int orgunCount = students.Count - mesemCount;

        _lblOrgunStudents.Text = orgunCount.ToString();
        _lblMesemStudents.Text = mesemCount.ToString();
        _lblStudents.Text = students.Count.ToString();

        int assigned = students.Count(s => s.TeacherId.HasValue && s.EnterpriseId.HasValue);
        _lblAssigned.Text = assigned.ToString();
        _lblPending.Text = (students.Count - assigned).ToString();

        // Eksik Bilgi & SGK Uyarı Hesaplamaları
        int missingTc = students.Count(s => string.IsNullOrWhiteSpace(s.TcKimlikNo) || s.TcKimlikNo.Trim().Length != 11);
        _lastMissingTcCount = missingTc;
        int missingEntry = students.Count(s => !s.SgkEntryDate.HasValue);

        if (_lblMissingTcCount != null && _lblMissingTcDesc != null)
        {
            if (missingTc > 0)
            {
                _tcAccentColor = Color.FromArgb(225, 29, 72); // rose-600
                _lblMissingTcCount.Text = $"{missingTc} Öğrenci Eksik";
                _lblMissingTcCount.ForeColor = _tcAccentColor;
                _lblMissingTcDesc.Text = "⚠️ T.C. Kimlik No eksik! Sigorta ve e-Bildirge işlemleri yapılamaz.";
                _lblMissingTcDesc.ForeColor = Color.FromArgb(190, 18, 60);
                if (_btnGoTc != null)
                {
                    _btnGoTc.BackColor = _tcAccentColor;
                    _btnGoTc.Text = "🔍 Eksikleri Tamamla";
                }
            }
            else
            {
                _tcAccentColor = Color.FromArgb(16, 185, 129); // emerald-600
                _lblMissingTcCount.Text = "Tümü Tamam (0 Eksik)";
                _lblMissingTcCount.ForeColor = _tcAccentColor;
                _lblMissingTcDesc.Text = "✅ Tüm öğrencilerin T.C. Kimlik Numarası eksiksiz tanımlıdır.";
                _lblMissingTcDesc.ForeColor = Color.FromArgb(71, 85, 105);
                if (_btnGoTc != null)
                {
                    _btnGoTc.BackColor = Color.FromArgb(71, 85, 105);
                    _btnGoTc.Text = "👥 Öğrenci Listesi";
                }
            }
            _lblMissingTcCount.Parent?.Parent?.Invalidate();
        }

        if (_lblMissingEntryCount != null && _lblMissingEntryDesc != null)
        {
            if (missingEntry > 0)
            {
                _entryAccentColor = Color.FromArgb(217, 119, 6); // amber-600
                _lblMissingEntryCount.Text = $"{missingEntry} Öğrenci Eksik";
                _lblMissingEntryCount.ForeColor = _entryAccentColor;
                _lblMissingEntryDesc.Text = "⚠️ SGK işe giriş bildirgesi verilebilmesi için işe giriş tarihi tanımlanmalıdır.";
                _lblMissingEntryDesc.ForeColor = Color.FromArgb(180, 83, 9);
                if (_btnGoEntry != null)
                {
                    _btnGoEntry.BackColor = _entryAccentColor;
                    _btnGoEntry.Text = "⚡ Toplu Tarih Ekle";
                }
            }
            else
            {
                _entryAccentColor = Color.FromArgb(16, 185, 129); // emerald-600
                _lblMissingEntryCount.Text = "Tümü Tamam (0 Eksik)";
                _lblMissingEntryCount.ForeColor = _entryAccentColor;
                _lblMissingEntryDesc.Text = "✅ Tüm öğrencilerin SGK işe giriş tarihleri eksiksiz tanımlıdır.";
                _lblMissingEntryDesc.ForeColor = Color.FromArgb(71, 85, 105);
                if (_btnGoEntry != null)
                {
                    _btnGoEntry.BackColor = Color.FromArgb(71, 85, 105);
                    _btnGoEntry.Text = "📅 Tarihleri İncele";
                }
            }
            _lblMissingEntryCount.Parent?.Parent?.Invalidate();
        }

        // e-Okul Öğrenci Kontrolü (Audit) Hatırlatma Mantığı
        string? lastAuditStr = null;
        try { lastAuditStr = await _settingRepo.GetValueAsync("LastStudentAuditDate"); } catch { }

        DateTime? lastAuditDate = null;
        if (DateTime.TryParse(lastAuditStr, out var parsedDt)) lastAuditDate = parsedDt;

        int currentDay = DateTime.Today.Day;
        bool isAuditRecentThisMonth = lastAuditDate.HasValue &&
                                     lastAuditDate.Value.Year == DateTime.Today.Year &&
                                     lastAuditDate.Value.Month == DateTime.Today.Month &&
                                     (DateTime.Today - lastAuditDate.Value.Date).TotalDays <= 20;

        if (_lblAuditStatus != null && _lblAuditDesc != null)
        {
            if (isAuditRecentThisMonth)
            {
                _auditAccentColor = Color.FromArgb(16, 185, 129); // emerald-600
                _lblAuditStatus.Text = $"Güncel ({lastAuditDate!.Value:dd.MM.yyyy})";
                _lblAuditStatus.ForeColor = _auditAccentColor;
                _lblAuditDesc.Text = "✅ e-Okul sınıf listesi kontrolü güncel. Veritabanı ve e-Bildirge uyumlu.";
                _lblAuditDesc.ForeColor = Color.FromArgb(71, 85, 105);
                if (_btnGoAudit != null)
                {
                    _btnGoAudit.BackColor = Color.FromArgb(71, 85, 105);
                    _btnGoAudit.Text = "🔍 Yeniden Karşılaştır";
                }
            }
            else if (currentDay >= 20 || currentDay <= 5)
            {
                // SGK Bildirge Dönemi (Her ayın 20'si - takip eden ayın 5'i)
                _auditAccentColor = Color.FromArgb(225, 29, 72); // rose-600
                _lblAuditStatus.Text = lastAuditDate.HasValue ? $"Kontrol Gerekli ({lastAuditDate.Value:dd.MM.yyyy})" : "⚠️ Bu Ay Kontrol Edilmedi!";
                _lblAuditStatus.ForeColor = _auditAccentColor;
                _lblAuditDesc.Text = "⚠️ SGK bildirge dönemi aktif! Nakil veya ayrılan öğrencileri tespit etmek için kontrol yapınız.";
                _lblAuditDesc.ForeColor = Color.FromArgb(190, 18, 60);
                if (_btnGoAudit != null)
                {
                    _btnGoAudit.BackColor = _auditAccentColor;
                    _btnGoAudit.Text = "🔍 Şimdi Kontrol Et";
                }
            }
            else
            {
                // Ayın normal günleri (6 - 19 arası)
                _auditAccentColor = Color.FromArgb(217, 119, 6); // amber-600
                _lblAuditStatus.Text = lastAuditDate.HasValue ? $"Son: {lastAuditDate.Value:dd.MM.yyyy}" : "Henüz Kontrol Edilmedi";
                _lblAuditStatus.ForeColor = _auditAccentColor;
                _lblAuditDesc.Text = "ℹ️ Ay sonu SGK dönemi öncesinde e-Okul Şube Listesi ile veritabanını eşleştirebilirsiniz.";
                _lblAuditDesc.ForeColor = Color.FromArgb(180, 83, 9);
                if (_btnGoAudit != null)
                {
                    _btnGoAudit.BackColor = _auditAccentColor;
                    _btnGoAudit.Text = "🔍 Öğrenci Kontrolü";
                }
            }
            _lblAuditStatus.Parent?.Parent?.Invalidate();
        }
    }

    private async Task RunQuickAutoDistributeAsync()
    {
        var teachers = (await _teacherRepo.GetAllAsync()).ToList();
        var enterprises = (await _enterpriseRepo.GetAllAsync()).ToList();
        var students = (await _studentRepo.GetAllAsync()).ToList();

        var options = new DistributionOptions
        {
            PrioritizeDayMatch = true,
            PrioritizeBranchMatch = true,
            ClusterEnterprises = true,
            ClusterAddressGroups = true,
            BalanceWorkload = true,
            AssignUnassignedEnterprises = false
        };

        var result = SmartDistributionEngine.RunAutoDistribution(options, teachers, enterprises, students);

        // Değişiklikleri veritabanına kaydet
        foreach (var s in students)
        {
            await _studentRepo.UpdateAsync(s);
        }
        foreach (var e in enterprises)
        {
            await _enterpriseRepo.UpdateAsync(e);
        }

        MessageBox.Show(result.Message, "Akıllı Dağıtım Sonucu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await LoadDataAsync();
        _reloadAllAction?.Invoke();
    }
}
