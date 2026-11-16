using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Common;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Views;

public class FormsView : UserControl
{
    private readonly ITeacherRepository _teacherRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly ILicenseService? _licenseService;

    private List<Teacher> _teachers = [];
    private List<Student> _students = [];
    private List<Enterprise> _enterprises = [];
    private AppSettings _settings = new();

    private ComboBox _cmbTeacher = null!;
    private Panel _pnlScroll = null!;
    private Panel _pnlA4 = null!;
    private readonly List<Panel> _pagePanels = [];

    public FormsView(
        ITeacherRepository teacherRepo,
        IStudentRepository studentRepo,
        IEnterpriseRepository enterpriseRepo,
        ISettingRepository settingRepo,
        ILicenseService? licenseService = null)
    {
        _teacherRepo = teacherRepo;
        _studentRepo = studentRepo;
        _enterpriseRepo = enterpriseRepo;
        _settingRepo = settingRepo;
        _licenseService = licenseService;

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
            Text = "🖨️ Görev Bildirim Formu Yazdırma Alanı",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Resmi MEB formatında antetli, işletme/öğrenci tablolu ve imzalı görev bildirim belgeleri",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        // Header Action Buttons
        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 520,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnPrintCurrent = new Button
        {
            Text = "🖨️ Seçili Formu Yazdır",
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnPrintCurrent.FlatAppearance.BorderSize = 0;
        btnPrintCurrent.Click += BtnPrintCurrent_Click;

        var btnPrintAll = new Button
        {
            Text = "🖨️ Tümünü Yazdır",
            Size = new Size(140, 36),
            BackColor = Color.FromArgb(79, 70, 229), // indigo-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnPrintAll.FlatAppearance.BorderSize = 0;
        btnPrintAll.Click += BtnPrintAll_Click;

        var btnPrintMaster = new Button
        {
            Text = "📑 Genel İcmal Çizelgesi",
            Size = new Size(170, 36),
            BackColor = Color.FromArgb(51, 65, 85), // slate-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnPrintMaster.FlatAppearance.BorderSize = 0;
        btnPrintMaster.Click += BtnPrintMaster_Click;

        pnlTopButtons.Controls.AddRange([btnPrintCurrent, btnPrintAll, btnPrintMaster]);
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Teacher Selector Bar
        var pnlSelect = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlSelect.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlSelect.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlSelect.Height - 1, pnlSelect.Width, pnlSelect.Height - 1);
        };

        var lblTeacherSelect = new Label
        {
            Text = "Formu Görüntülenecek Öğretmen:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            Location = new Point(24, 16)
        };

        _cmbTeacher = new ComboBox
        {
            Location = new Point(240, 12),
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cmbTeacher.SelectedIndexChanged += (s, e) => RenderA4Preview();

        var lblTip = new Label
        {
            Text = "💡 Yazdırma penceresinde 'Microsoft Print to PDF' seçerek doğrudan resmi PDF kaydedebilirsiniz.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Location = new Point(520, 16)
        };

        pnlSelect.Controls.AddRange([lblTeacherSelect, _cmbTeacher, lblTip]);

        // Preview Scroll Container (Arka plan açık gri)
        _pnlScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(226, 232, 240), // slate-200
            Padding = new Padding(0, 24, 0, 40)
        };

        _pnlA4 = CreateA4PagePanel();
        _pagePanels.Add(_pnlA4);
        _pnlScroll.Controls.Add(_pnlA4);
        _pnlScroll.Resize += (s, e) => CenterA4Paper();

        Controls.Add(_pnlScroll);
        Controls.Add(pnlSelect);
        Controls.Add(pnlHeader);

        CenterA4Paper();
    }

    public async Task LoadDataAsync()
    {
        _settings = await _settingRepo.GetAppSettingsAsync();
        _teachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();
        _students = (await _studentRepo.GetAllAsync())
            .Where(s => s.IsActive && !s.StudentNumber.Contains("-P", StringComparison.OrdinalIgnoreCase))
            .ToList();
        _enterprises = (await _enterpriseRepo.GetAllAsync()).ToList();

        _cmbTeacher.Items.Clear();
        foreach (var t in _teachers)
        {
            _cmbTeacher.Items.Add(t.FullName);
        }

        if (_teachers.Count > 0)
        {
            int defaultIdx = 0;
            if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin && MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser != null)
            {
                var curUser = MebKoordinatorluk.Core.Security.CurrentUserSession.CurrentUser;
                var matchIdx = _teachers.FindIndex(t =>
                    (curUser.TeacherId.HasValue && t.Id == curUser.TeacherId.Value) ||
                    string.Equals(t.FullName, curUser.FullName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.FullName, curUser.TeacherName, StringComparison.OrdinalIgnoreCase));
                if (matchIdx >= 0) defaultIdx = matchIdx;
            }
            _cmbTeacher.SelectedIndex = defaultIdx;
        }
    }

    private void RenderA4Preview()
    {
        foreach (var p in _pagePanels)
        {
            _pnlScroll.Controls.Remove(p);
            p.Dispose();
        }
        _pagePanels.Clear();

        _pnlA4 = CreateA4PagePanel();
        _pagePanels.Add(_pnlA4);
        _pnlScroll.Controls.Add(_pnlA4);

        if (_cmbTeacher.SelectedIndex < 0 || _cmbTeacher.SelectedIndex >= _teachers.Count)
        {
            CenterA4Paper();
            return;
        }

        var teacher = _teachers[_cmbTeacher.SelectedIndex];
        var teacherStudents = _students.Where(s => s.TeacherId == teacher.Id).ToList();

        // Distinct assigned enterprises
        var teacherEnterpriseIds = teacherStudents.Where(s => s.EnterpriseId.HasValue).Select(s => s.EnterpriseId!.Value).Distinct().ToList();
        var teacherEnterprises = _enterprises.Where(e => teacherEnterpriseIds.Contains(e.Id)).ToList();

        int y = 20;

        // 1. Antet
        string val = !string.IsNullOrWhiteSpace(_settings.DistrictGovernorate)
            ? _settings.DistrictGovernorate.ToUpperTr()
            : (!string.IsNullOrWhiteSpace(_settings.Valilik) ? _settings.Valilik.ToUpperTr() : "İSTANBUL VALİLİĞİ");
        string mem = !string.IsNullOrWhiteSpace(_settings.DistrictMem) ? _settings.DistrictMem.ToUpperTr() : "";
        string school = !string.IsNullOrWhiteSpace(_settings.SchoolName) ? _settings.SchoolName : "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ";
        string antetText = "T.C.\n" + val + (!string.IsNullOrWhiteSpace(mem) ? "\n" + mem : "") + "\n" + school;
        int antetH = !string.IsNullOrWhiteSpace(mem) ? 74 : 56;

        var lblTc = new Label
        {
            Text = antetText,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.TopCenter,
            Location = new Point(40, y),
            Size = new Size(760, antetH)
        };
        _pnlA4.Controls.Add(lblTc);
        y += antetH + 10;

        // Sayı / Konu / Tarih
        var lblSayi = new Label
        {
            Text = $"Sayı : {_settings.OfficialLetterNumber}\nKonu : Koordinatörlük Görevlendirmesi",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Black,
            Location = new Point(40, y),
            Size = new Size(400, 36)
        };
        var lblTarih = new Label
        {
            Text = $"Tarih : {(_settings.LetterDate ?? DateTime.Today.ToString("dd/MM/yyyy"))}",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.TopRight,
            Location = new Point(540, y),
            Size = new Size(260, 36)
        };
        _pnlA4.Controls.AddRange([lblSayi, lblTarih]);
        y += 46;

        // Hitap
        var teacherTitle = !string.IsNullOrWhiteSpace(_settings.TeacherTitle)
            ? _settings.TeacherTitle
            : $"{teacher.Branch} Öğretmeni";

        var lblHitap = new Label
        {
            Text = $"Sayın: {teacher.FullName}\n({teacherTitle})",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.TopCenter,
            Location = new Point(40, y),
            Size = new Size(760, 38)
        };
        _pnlA4.Controls.Add(lblHitap);
        y += 46;

        // Mevzuat Gövde Metni
        var bodyText = _settings.FormBodyText ?? "Öğretim yılında 3308 sayılı Mesleki Eğitim Kanunu ve Millî Eğitim Bakanlığı Ortaöğretim Kurumları Yönetmeliği'nin 88 inci maddesi gereğince tarafınıza verilen işletme koordinatörlük görevleri aşağıda belirtilmiştir.";
        bodyText = bodyText.Replace("{yil}", _settings.AcademicYear ?? "2024-2025")
                           .Replace("{donem}", _settings.Term ?? "1.")
                           .Replace("{tarih}", _settings.ValidityDate ?? _settings.LetterDate ?? DateTime.Today.ToString("dd/MM/yyyy"));

        var lblBody = new Label
        {
            Text = bodyText,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.Black,
            Location = new Point(40, y),
            Size = new Size(760, 48)
        };
        _pnlA4.Controls.Add(lblBody);
        y += 54;

        // İşletme verilerini hazırla:
        // Aynı işletmede farklı güne atanmış öğrenciler ayrı satır olarak gösterilir.
        var rawItems = new List<(string name, string loc, string students, string rawDay, string days, int stCount)>();
        foreach (var ent in teacherEnterprises)
        {
            var stList = teacherStudents.Where(s => s.EnterpriseId == ent.Id).ToList();

            if (stList.Count == 0)
            {
                // İşletme atanmış ama öğrenci yoksa tek boş satır
                var fallbackDay = teacher.AvailableDays ?? "Salı";
                rawItems.Add((ent.Name, ent.District ?? "-", "-", fallbackDay, TurkishTextHelper.FormatDays(fallbackDay), 1));
                continue;
            }

            // Öğrencileri "ilk görev günleri"ne göre grupla (Perşembe / Cuma gibi ayrımlar)
            var byDay = stList
                .GroupBy(s =>
                {
                    // İlk geçerli DutyDays değerini al; yoksa öğretmenin gününü kullan
                    var raw = !string.IsNullOrWhiteSpace(s.DutyDays) ? s.DutyDays : (teacher.AvailableDays ?? "Salı");
                    // Virgüllü listeyse ilk günü al
                    var first = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? raw;
                    return TurkishTextHelper.NormalizeDayName(first); // normalize et (Perşembe, Cuma…)
                })
                .OrderBy(g => TurkishTextHelper.DayOrder(g.Key));

            foreach (var grp in byDay)
            {
                var grpStudents = grp.ToList();
                var stNames = string.Join(Environment.NewLine, grpStudents.Select((s, sIdx) =>
                {
                    var numPart = string.IsNullOrWhiteSpace(s.StudentNumber) ? "" : $"{s.StudentNumber} - ";
                    var classPart = string.IsNullOrWhiteSpace(s.Classroom) ? "" : $" ({s.Classroom})";
                    return $"{sIdx + 1}. {numPart}{s.FullName}{classPart}";
                }));

                rawItems.Add((ent.Name, ent.District ?? "-", stNames, grp.Key, grp.Key, grpStudents.Count));
            }
        }

        // Pazartesiden başlayarak görev gününe göre sırala (aynı gündeki işletmeler kendi aralarında ada göre)
        var allItems = rawItems
            .OrderBy(it => TurkishTextHelper.FirstDayOrder(it.rawDay))
            .ThenBy(it => it.name, StringComparer.CurrentCultureIgnoreCase)
            .Select((it, i) => (idx: i + 1, it.name, it.loc, it.students, it.days, it.stCount))
            .ToList();


        // Toplam tablo yüksekliğini hesapla
        int totalTableHeight = 32 + allItems.Sum(it => EstimateItemHeight(it.name, it.stCount));

        // 1 sayfaya sığıyor mu kontrol et (tablo başlangıç y ~ 238, alt bilgi alanı ~ 260px, toplam A4 = 1188px -> limit ~ 675px)
        const int MAX_P1_TABLE_HEIGHT_WITH_FOOTER = 675;
        const int MAX_P1_TABLE_HEIGHT_WITHOUT_FOOTER = 850;

        var page1Items = new List<(int idx, string name, string loc, string students, string days, int stCount)>();
        var page2Items = new List<(int idx, string name, string loc, string students, string days, int stCount)>();

        if (totalTableHeight <= MAX_P1_TABLE_HEIGHT_WITH_FOOTER || allItems.Count <= 1)
        {
            page1Items.AddRange(allItems);
        }
        else
        {
            // 2 sayfaya böl: Sayfa 1'e sığabildiği kadar al (en az 1 işletme 2. sayfaya kalsın)
            int curH = 32;
            for (int i = 0; i < allItems.Count; i++)
            {
                var it = allItems[i];
                int h = EstimateItemHeight(it.name, it.stCount);
                if (curH + h <= MAX_P1_TABLE_HEIGHT_WITHOUT_FOOTER && (allItems.Count - i) > 1)
                {
                    page1Items.Add(it);
                    curH += h;
                }
                else
                {
                    page2Items.Add(it);
                }
            }
        }

        // Sayfa 1 Tablosu
        var grid1 = CreateEnterpriseGrid(y, page1Items);
        _pnlA4.Controls.Add(grid1);

        if (page2Items.Count == 0)
        {
            // Tek Sayfa Modu: Bilgi kutusu ve İmzalar Sayfa 1'de
            int receiptY = Math.Max(grid1.Bottom + 12, 806);
            var pnlReceipt = CreateDeliveryReceiptPanel(receiptY, teacher);
            _pnlA4.Controls.Add(pnlReceipt);

            int coordY = receiptY + 34;
            var pnlCoordInfo = CreateCoordInfoPanel(coordY, teacher);
            _pnlA4.Controls.Add(pnlCoordInfo);

            int signY = Math.Max(pnlCoordInfo.Bottom + 35, 990);
            var pnlSignatures = CreateSignaturesPanel(signY);
            _pnlA4.Controls.Add(pnlSignatures);

            var lblPageNum = new Label
            {
                Text = "Sayfa 1 / 1",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(40, 1144),
                Size = new Size(760, 20)
            };
            _pnlA4.Controls.Add(lblPageNum);
        }
        else
        {
            // 2 Sayfa Modu: Sayfa 1 altına devam uyarısı
            var lblContinuation = new Label
            {
                Text = "➔ Görevlendirilen işletmelerin devamı 2. sayfadadır. (Sayfa 1 / 2)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold | FontStyle.Italic),
                ForeColor = Color.FromArgb(30, 64, 175),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(40, 1144),
                Size = new Size(760, 22)
            };
            _pnlA4.Controls.Add(lblContinuation);

            // Sayfa 2 Oluştur
            var pnlP2 = CreateA4PagePanel();
            _pagePanels.Add(pnlP2);
            _pnlScroll.Controls.Add(pnlP2);

            int y2 = 24;
            int p2AntetH = !string.IsNullOrWhiteSpace(mem) ? 70 : 52;
            var lblP2Tc = new Label
            {
                Text = antetText,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.Black,
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(40, y2),
                Size = new Size(760, p2AntetH)
            };
            pnlP2.Controls.Add(lblP2Tc);
            y2 += p2AntetH + 6;

            var lblP2Title = new Label
            {
                Text = $"KOORDİNATÖRLÜK GÖREV ONAY FORMU (DEVAM)\nSayın: {teacher.FullName} ({teacherTitle})",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.Black,
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(40, y2),
                Size = new Size(760, 40)
            };
            pnlP2.Controls.Add(lblP2Title);
            y2 += 48;

            // Sayfa 2 Tablosu
            var grid2 = CreateEnterpriseGrid(y2, page2Items);
            pnlP2.Controls.Add(grid2);

            int receiptY2 = Math.Max(grid2.Bottom + 12, 806);
            var pnlReceipt2 = CreateDeliveryReceiptPanel(receiptY2, teacher);
            pnlP2.Controls.Add(pnlReceipt2);

            int coordY2 = receiptY2 + 34;
            var pnlCoordInfo2 = CreateCoordInfoPanel(coordY2, teacher);
            pnlP2.Controls.Add(pnlCoordInfo2);

            int signY2 = Math.Max(pnlCoordInfo2.Bottom + 35, 990);
            var pnlSignatures2 = CreateSignaturesPanel(signY2);
            pnlP2.Controls.Add(pnlSignatures2);

            var lblP2Num = new Label
            {
                Text = "Sayfa 2 / 2",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(40, 1144),
                Size = new Size(760, 20)
            };
            pnlP2.Controls.Add(lblP2Num);
        }

        CenterA4Paper();
    }

    private Panel CreateA4PagePanel()
    {
        var pnl = new Panel
        {
            Size = new Size(840, 1188),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        pnl.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(203, 213, 225), 1);
            e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
        };
        return pnl;
    }

    private static int EstimateItemHeight(string name, int stCount)
    {
        int nameLines = (int)Math.Ceiling(name.Length / 24.0);
        int effectiveLines = Math.Max(stCount, nameLines);
        return Math.Max(38, 14 + effectiveLines * 18);
    }

    private DataGridView CreateEnterpriseGrid(int y, List<(int idx, string name, string loc, string students, string days, int stCount)> items)
    {
        var grid = new DataGridView
        {
            Location = new Point(40, y),
            Width = 760,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.Single,
            GridColor = Color.FromArgb(148, 163, 184),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ScrollBars = ScrollBars.None
        };
        UITheme.ApplyModernGridStyle(grid);
        grid.ScrollBars = ScrollBars.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.GridColor = Color.FromArgb(148, 163, 184);
        grid.DefaultCellStyle.ForeColor = Color.Black;
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        grid.Columns.Add("cIdx", "#");
        grid.Columns["cIdx"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        grid.Columns["cIdx"].Width = 30;
        grid.Columns["cIdx"].Resizable = DataGridViewTriState.False;
        grid.Columns["cIdx"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns["cIdx"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        grid.Columns.Add("cEnt", "İşletme Adı");
        grid.Columns["cEnt"].FillWeight = 28;
        grid.Columns["cEnt"].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        grid.Columns.Add("cAddr", "Konum / İlçe");
        grid.Columns["cAddr"].FillWeight = 16;

        grid.Columns.Add("cStudents", "Öğrenciler (Sınıf)");
        grid.Columns["cStudents"].FillWeight = 42;
        grid.Columns["cStudents"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        grid.Columns.Add("cDays", "Görev Günü");
        grid.Columns["cDays"].FillWeight = 14;
        grid.Columns["cDays"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        int totalH = grid.ColumnHeadersHeight;
        foreach (var it in items)
        {
            int rIdx = grid.Rows.Add(it.idx, it.name, it.loc, it.students, it.days);
            int rowH = EstimateItemHeight(it.name, it.stCount);
            grid.Rows[rIdx].Height = rowH;
            totalH += rowH;
        }

        if (items.Count == 0)
        {
            int rIdx = grid.Rows.Add(1, "(Atanmış işletme bulunamadı)", "-", "-", "-");
            grid.Rows[rIdx].Height = 36;
            totalH += 36;
        }

        grid.Height = totalH + 4;
        return grid;
    }

    private Panel CreateDeliveryReceiptPanel(int y, Teacher teacher)
    {
        var pnl = new Panel
        {
            Location = new Point(40, y),
            Size = new Size(760, 26),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };

        var lbl = new Label
        {
            Text = $"Formun bir nüshasını elden teslim aldım.   |   Teslim Alan: {teacher.FullName}   |   İmza: .......................................",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        pnl.Controls.Add(lbl);
        return pnl;
    }

    private Panel CreateCoordInfoPanel(int y, Teacher teacher)
    {
        int coordHours = teacher.CoordinatorFeeHours;
        string coordHoursText = HoursToText(coordHours);

        var pnl = new Panel
        {
            Location = new Point(40, y),
            Size = new Size(760, 46),
            BackColor = Color.FromArgb(239, 246, 255),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lbl = new Label
        {
            Text = $"Toplam koordinatörlük saatiniz  {coordHours} ({coordHoursText})  saattir.",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 64, 175),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        pnl.Controls.Add(lbl);
        return pnl;
    }

    private Panel CreateSignaturesPanel(int y)
    {
        var pnl = new Panel
        {
            Location = new Point(40, y),
            Size = new Size(760, 95)
        };

        var lblSign1 = new Label
        {
            Text = $"{_settings.VicePrincipalName ?? "Müdür Yardımcısı"}\nKoordinatör Müdür Yrd.\n\nİmza",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.TopCenter,
            Location = new Point(30, 8),
            Size = new Size(260, 80)
        };

        var lblSign2 = new Label
        {
            Text = $"{_settings.PrincipalName ?? "Okul Müdürü"}\nOkul Müdürü\n\nİmza - Mühür",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.TopCenter,
            Location = new Point(470, 8),
            Size = new Size(260, 80)
        };

        pnl.Controls.AddRange([lblSign1, lblSign2]);
        return pnl;
    }

    /// <summary>Verilen saati Türkçe yazıya çevirir (örn: 16 → "on altı").</summary>
    private static string HoursToText(int hours)
    {
        if (hours <= 0) return "sıfır";

        string[] birler = { "", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz" };
        string[] onlar  = { "", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan" };

        if (hours < 10) return birler[hours];
        if (hours < 100)
        {
            int o = hours / 10;
            int b = hours % 10;
            return b == 0 ? onlar[o] : onlar[o] + " " + birler[b];
        }
        int y = hours / 100;
        int kalan = hours % 100;
        string yuzStr = y == 1 ? "yüz" : birler[y] + " yüz";
        if (kalan == 0) return yuzStr;
        return yuzStr + " " + HoursToText(kalan);
    }

    private void CenterA4Paper()
    {
        if (_pnlScroll == null || _pnlScroll.ClientSize.Width <= 0 || _pagePanels.Count == 0)
            return;

        int scrollY = -_pnlScroll.AutoScrollPosition.Y;
        _pnlScroll.AutoScrollPosition = new Point(0, 0);

        int targetX = Math.Max(24, (_pnlScroll.ClientSize.Width - 840) / 2);

        for (int i = 0; i < _pagePanels.Count; i++)
        {
            var pnl = _pagePanels[i];
            pnl.Location = new Point(targetX, 24 + i * (1188 + 32));
        }

        if (scrollY > 0)
        {
            _pnlScroll.AutoScrollPosition = new Point(0, scrollY);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CenterA4Paper();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            CenterA4Paper();
        }
    }

    private sealed class FormPageData
    {
        public Teacher Teacher { get; set; } = null!;
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public List<(int idx, string name, string loc, string students, string days, int stCount)> PageItems { get; set; } = [];
        public bool ShowFooterInfo { get; set; }
    }

    private List<FormPageData> GetFormPagesForTeacher(Teacher teacher)
    {
        var teacherStudents = _students.Where(s => s.TeacherId == teacher.Id).ToList();
        var teacherEnterpriseIds = teacherStudents.Where(s => s.EnterpriseId.HasValue).Select(s => s.EnterpriseId!.Value).Distinct().ToList();
        var teacherEnterprises = _enterprises.Where(e => teacherEnterpriseIds.Contains(e.Id)).ToList();

        var rawItems = new List<(string name, string loc, string students, string rawDay, string days, int stCount)>();
        foreach (var ent in teacherEnterprises)
        {
            var stList = teacherStudents.Where(s => s.EnterpriseId == ent.Id).ToList();
            if (stList.Count == 0)
            {
                var fallbackDay = teacher.AvailableDays ?? "Salı";
                rawItems.Add((ent.Name, ent.District ?? "-", "-", fallbackDay, TurkishTextHelper.FormatDays(fallbackDay), 1));
                continue;
            }

            var byDay = stList
                .GroupBy(s =>
                {
                    var raw = !string.IsNullOrWhiteSpace(s.DutyDays) ? s.DutyDays : (teacher.AvailableDays ?? "Salı");
                    var first = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? raw;
                    return TurkishTextHelper.NormalizeDayName(first);
                })
                .OrderBy(g => TurkishTextHelper.DayOrder(g.Key));

            foreach (var grp in byDay)
            {
                var grpStudents = grp.ToList();
                var stNames = string.Join(Environment.NewLine, grpStudents.Select((s, sIdx) =>
                {
                    var numPart = string.IsNullOrWhiteSpace(s.StudentNumber) ? "" : $"{s.StudentNumber} - ";
                    var classPart = string.IsNullOrWhiteSpace(s.Classroom) ? "" : $" ({s.Classroom})";
                    return $"{sIdx + 1}. {numPart}{s.FullName}{classPart}";
                }));

                rawItems.Add((ent.Name, ent.District ?? "-", stNames, grp.Key, grp.Key, grpStudents.Count));
            }
        }

        var allItems = rawItems
            .OrderBy(it => TurkishTextHelper.FirstDayOrder(it.rawDay))
            .ThenBy(it => it.name, StringComparer.CurrentCultureIgnoreCase)
            .Select((it, i) => (idx: i + 1, it.name, it.loc, it.students, it.days, it.stCount))
            .ToList();

        int totalTableHeight = 32 + allItems.Sum(it => EstimateItemHeight(it.name, it.stCount));
        const int MAX_P1_TABLE_HEIGHT_WITH_FOOTER = 675;
        const int MAX_P1_TABLE_HEIGHT_WITHOUT_FOOTER = 850;

        var page1Items = new List<(int idx, string name, string loc, string students, string days, int stCount)>();
        var page2Items = new List<(int idx, string name, string loc, string students, string days, int stCount)>();

        if (totalTableHeight <= MAX_P1_TABLE_HEIGHT_WITH_FOOTER || allItems.Count <= 1)
        {
            page1Items.AddRange(allItems);
        }
        else
        {
            int curH = 32;
            for (int i = 0; i < allItems.Count; i++)
            {
                var it = allItems[i];
                int h = EstimateItemHeight(it.name, it.stCount);
                if (curH + h <= MAX_P1_TABLE_HEIGHT_WITHOUT_FOOTER && (allItems.Count - i) > 1)
                {
                    page1Items.Add(it);
                    curH += h;
                }
                else
                {
                    page2Items.Add(it);
                }
            }
        }

        var result = new List<FormPageData>();
        if (page2Items.Count == 0)
        {
            result.Add(new FormPageData
            {
                Teacher = teacher,
                PageIndex = 1,
                TotalPages = 1,
                PageItems = page1Items,
                ShowFooterInfo = true
            });
        }
        else
        {
            result.Add(new FormPageData
            {
                Teacher = teacher,
                PageIndex = 1,
                TotalPages = 2,
                PageItems = page1Items,
                ShowFooterInfo = false
            });
            result.Add(new FormPageData
            {
                Teacher = teacher,
                PageIndex = 2,
                TotalPages = 2,
                PageItems = page2Items,
                ShowFooterInfo = true
            });
        }

        return result;
    }

    private void DrawFormPage(Graphics g, Rectangle bounds, FormPageData pageData)
    {
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float scaleX = (float)bounds.Width / 840f;
        float scaleY = (float)bounds.Height / 1188f;
        float scale = Math.Min(scaleX, scaleY);

        int drawW = (int)(840 * scale);
        int drawH = (int)(1188 * scale);
        int startX = bounds.X + (bounds.Width - drawW) / 2;
        int startY = bounds.Y + Math.Max(0, (bounds.Height - drawH) / 2);

        var state = g.Save();
        g.TranslateTransform(startX, startY);
        g.ScaleTransform(scale, scale);

        using var fHeader = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var fSub = new Font("Segoe UI", 9f, FontStyle.Regular);
        using var fHitap = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var fBody = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var fTableHead = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var fTableCell = new Font("Segoe UI", 9f, FontStyle.Regular);
        using var fTableCellBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var fCoord = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var fSign = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var fFooter = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var fFooterBoldItalic = new Font("Segoe UI", 9f, FontStyle.Bold | FontStyle.Italic);

        using var bBlack = new SolidBrush(Color.Black);
        using var bBlue = new SolidBrush(Color.FromArgb(30, 64, 175));
        using var bFooterText = new SolidBrush(Color.FromArgb(148, 163, 184));
        using var bHdrBg = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var bCoordBg = new SolidBrush(Color.FromArgb(239, 246, 255));

        using var pGridPen = new Pen(Color.FromArgb(148, 163, 184), 1f);

        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var sfTopCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
        using var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
        using var sfRightCenter = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        var teacher = pageData.Teacher;
        var teacherTitle = !string.IsNullOrWhiteSpace(_settings.TeacherTitle)
            ? _settings.TeacherTitle
            : $"{teacher.Branch} Öğretmeni";

        int y = 20;

        string val = !string.IsNullOrWhiteSpace(_settings.DistrictGovernorate)
            ? _settings.DistrictGovernorate.ToUpperTr()
            : (!string.IsNullOrWhiteSpace(_settings.Valilik) ? _settings.Valilik.ToUpperTr() : "İSTANBUL VALİLİĞİ");
        string mem = !string.IsNullOrWhiteSpace(_settings.DistrictMem) ? _settings.DistrictMem.ToUpperTr() : "";
        string school = !string.IsNullOrWhiteSpace(_settings.SchoolName) ? _settings.SchoolName : "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ";
        string tcStr = "T.C.\n" + val + (!string.IsNullOrWhiteSpace(mem) ? "\n" + mem : "") + "\n" + school;

        if (pageData.PageIndex == 1)
        {
            // 1. Antet
            int antetH = !string.IsNullOrWhiteSpace(mem) ? 74 : 56;
            g.DrawString(tcStr, fHeader, bBlack, new RectangleF(40, y, 760, antetH), sfTopCenter);
            y += antetH + 10;

            // Sayı / Tarih
            string sayiStr = $"Sayı : {_settings.OfficialLetterNumber}\nKonu : Koordinatörlük Görevlendirmesi";
            string tarihStr = $"Tarih : {(_settings.LetterDate ?? DateTime.Today.ToString("dd/MM/yyyy"))}";
            g.DrawString(sayiStr, fSub, bBlack, new RectangleF(40, y, 400, 36), sfLeft);
            g.DrawString(tarihStr, fSub, bBlack, new RectangleF(540, y, 260, 36), sfRight);
            y += 46;

            // Hitap
            string hitapStr = $"Sayın: {teacher.FullName}\n({teacherTitle})";
            g.DrawString(hitapStr, fHitap, bBlack, new RectangleF(40, y, 760, 38), sfTopCenter);
            y += 46;

            // Mevzuat Gövde Metni
            var bodyText = _settings.FormBodyText ?? "Öğretim yılında 3308 sayılı Mesleki Eğitim Kanunu ve Millî Eğitim Bakanlığı Ortaöğretim Kurumları Yönetmeliği'nin 88 inci maddesi gereğince tarafınıza verilen işletme koordinatörlük görevleri aşağıda belirtilmiştir.";
            bodyText = bodyText.Replace("{yil}", _settings.AcademicYear ?? "2024-2025")
                               .Replace("{donem}", _settings.Term ?? "1.")
                               .Replace("{tarih}", _settings.ValidityDate ?? _settings.LetterDate ?? DateTime.Today.ToString("dd/MM/yyyy"));
            g.DrawString(bodyText, fBody, bBlack, new RectangleF(40, y, 760, 48), sfLeft);
            y += 54;
        }
        else
        {
            y = 24;
            int p2AntetH = !string.IsNullOrWhiteSpace(mem) ? 70 : 52;
            g.DrawString(tcStr, fHeader, bBlack, new RectangleF(40, y, 760, p2AntetH), sfTopCenter);
            y += p2AntetH + 6;

            string p2TitleStr = $"KOORDİNATÖRLÜK GÖREV ONAY FORMU (DEVAM)\nSayın: {teacher.FullName} ({teacherTitle})";
            g.DrawString(p2TitleStr, fHitap, bBlack, new RectangleF(40, y, 760, 40), sfTopCenter);
            y += 48;
        }

        // Tablo Çizimi
        int[] cw = [30, 210, 120, 300, 100];
        string[] headers = ["#", "İşletme Adı", "Konum / İlçe", "Öğrenciler (Sınıf)", "Görev Günü"];
        int hdrH = 28;

        g.FillRectangle(bHdrBg, 40, y, 760, hdrH);
        g.DrawRectangle(pGridPen, 40, y, 760, hdrH);

        int cx = 40;
        for (int i = 0; i < headers.Length; i++)
        {
            var sfHdr = (i == 0 || i == 4) ? sfCenter : sfLeft;
            g.DrawString(headers[i], fTableHead, bBlack, new RectangleF(cx + 4, y, cw[i] - 8, hdrH), sfHdr);
            if (i < headers.Length - 1)
                g.DrawLine(pGridPen, cx + cw[i], y, cx + cw[i], y + hdrH);
            cx += cw[i];
        }
        y += hdrH;

        if (pageData.PageItems.Count == 0)
        {
            int rowH = 36;
            g.DrawRectangle(pGridPen, 40, y, 760, rowH);
            g.DrawString("1", fTableCell, bBlack, new RectangleF(40, y, cw[0], rowH), sfCenter);
            g.DrawString("(Atanmış işletme bulunamadı)", fTableCell, bBlack, new RectangleF(40 + cw[0] + 4, y, cw[1] - 8, rowH), sfLeft);
            g.DrawString("-", fTableCell, bBlack, new RectangleF(40 + cw[0] + cw[1], y, cw[2], rowH), sfLeft);
            g.DrawString("-", fTableCell, bBlack, new RectangleF(40 + cw[0] + cw[1] + cw[2], y, cw[3], rowH), sfLeft);
            g.DrawString("-", fTableCell, bBlack, new RectangleF(40 + cw[0] + cw[1] + cw[2] + cw[3], y, cw[4], rowH), sfCenter);
            y += rowH;
        }
        else
        {
            foreach (var it in pageData.PageItems)
            {
                int rowH = EstimateItemHeight(it.name, it.stCount);
                g.DrawRectangle(pGridPen, 40, y, 760, rowH);

                cx = 40;
                // Col 0: #
                g.DrawString(it.idx.ToString(), fTableCell, bBlack, new RectangleF(cx, y, cw[0], rowH), sfCenter);
                g.DrawLine(pGridPen, cx + cw[0], y, cx + cw[0], y + rowH);
                cx += cw[0];

                // Col 1: İşletme Adı
                g.DrawString(it.name, fTableCellBold, bBlack, new RectangleF(cx + 4, y + 4, cw[1] - 8, rowH - 8), sfLeft);
                g.DrawLine(pGridPen, cx + cw[1], y, cx + cw[1], y + rowH);
                cx += cw[1];

                // Col 2: Konum / İlçe
                g.DrawString(it.loc, fTableCell, bBlack, new RectangleF(cx + 4, y + 4, cw[2] - 8, rowH - 8), sfLeft);
                g.DrawLine(pGridPen, cx + cw[2], y, cx + cw[2], y + rowH);
                cx += cw[2];

                // Col 3: Öğrenciler (Sınıf)
                g.DrawString(it.students, fTableCell, bBlack, new RectangleF(cx + 4, y + 4, cw[3] - 8, rowH - 8), sfLeft);
                g.DrawLine(pGridPen, cx + cw[3], y, cx + cw[3], y + rowH);
                cx += cw[3];

                // Col 4: Görev Günü
                g.DrawString(it.days, fTableCell, bBlack, new RectangleF(cx, y, cw[4], rowH), sfCenter);

                y += rowH;
            }
        }

        if (pageData.ShowFooterInfo)
        {
            int coordHours = teacher.CoordinatorFeeHours;
            string coordHoursText = HoursToText(coordHours);

            int receiptY = Math.Max(y + 12, 808);
            using var bReceiptText = new SolidBrush(Color.FromArgb(51, 65, 85));
            using var fReceipt = new Font("Segoe UI", 9f, FontStyle.Regular);

            string receiptText = $"Formun bir nüshasını elden teslim aldım.   |   Teslim Alan: {teacher.FullName}   |   İmza: .......................................";
            g.DrawString(receiptText, fReceipt, bReceiptText, new RectangleF(40, receiptY, 760, 26), sfCenter);

            int coordY = receiptY + 32;
            g.FillRectangle(bCoordBg, 40, coordY, 760, 46);
            g.DrawRectangle(pGridPen, 40, coordY, 760, 46);
            g.DrawString($"Toplam koordinatörlük saatiniz  {coordHours} ({coordHoursText})  saattir.", fCoord, bBlue, new RectangleF(40, coordY, 760, 46), sfCenter);

            int signY = Math.Max(coordY + 66, 990);
            string sign1Str = $"{_settings.VicePrincipalName ?? "Müdür Yardımcısı"}\nKoordinatör Müdür Yrd.\n\nİmza";
            string sign2Str = $"{_settings.PrincipalName ?? "Okul Müdürü"}\nOkul Müdürü\n\nİmza - Mühür";

            g.DrawString(sign1Str, fSign, bBlack, new RectangleF(70, signY, 260, 80), sfTopCenter);
            g.DrawString(sign2Str, fSign, bBlack, new RectangleF(510, signY, 260, 80), sfTopCenter);
        }

        if (pageData.PageIndex == 1 && pageData.TotalPages > 1)
        {
            g.DrawString("➔ Görevlendirilen işletmelerin devamı 2. sayfadadır. (Sayfa 1 / 2)", fFooterBoldItalic, bBlue, new RectangleF(40, 1144, 760, 22), sfRightCenter);
        }
        else
        {
            g.DrawString($"Sayfa {pageData.PageIndex} / {pageData.TotalPages}", fFooter, bFooterText, new RectangleF(40, 1144, 760, 20), sfRightCenter);
        }

        if (_licenseService != null && !_licenseService.CurrentLicense.IsActive)
        {
            using var fWatermark = new Font("Segoe UI", 32f, FontStyle.Bold);
            using var bWatermark = new SolidBrush(Color.FromArgb(40, 239, 68, 68));
            var wmState = g.Save();
            g.TranslateTransform(420, 600);
            g.RotateTransform(-30);
            var sfWm = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("3308 PRO - LİSANSSIZ DEMO SÜRÜMÜ\nMaksimum 5 Öğrenci / 3 İşletme", fWatermark, bWatermark, 0, 0, sfWm);
            g.Restore(wmState);
        }

        g.Restore(state);
    }

    private PrintDocument BuildPrintDocument()
    {
        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

        if (_cmbTeacher.SelectedIndex < 0 || _cmbTeacher.SelectedIndex >= _teachers.Count)
            return pd;

        var teacher = _teachers[_cmbTeacher.SelectedIndex];
        var pages = GetFormPagesForTeacher(teacher);

        int printPageIdx = 0;

        pd.BeginPrint += (s, ev) =>
        {
            printPageIdx = 0;
        };

        pd.PrintPage += (s, ev) =>
        {
            if (ev.Graphics == null || printPageIdx >= pages.Count)
            {
                ev.HasMorePages = false;
                return;
            }

            DrawFormPage(ev.Graphics, ev.MarginBounds, pages[printPageIdx]);

            printPageIdx++;
            ev.HasMorePages = printPageIdx < pages.Count;
        };

        return pd;
    }

    private void BtnPrintCurrent_Click(object? sender, EventArgs e)
    {
        if (_cmbTeacher.SelectedIndex < 0 || _cmbTeacher.SelectedIndex >= _teachers.Count)
        {
            MessageBox.Show("Lütfen önce bir öğretmen seçiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var pd = BuildPrintDocument();
        PrintPreviewHelper.ShowPreview(pd, $"Görev Formu Önizleme — {_cmbTeacher.SelectedItem}", this);
    }

    private void BtnPrintAll_Click(object? sender, EventArgs e)
    {
        if (_teachers.Count == 0)
        {
            MessageBox.Show("Yazdırılacak öğretmen kaydı bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"{_teachers.Count} öğretmen için tüm görev formları tek önizleme penceresinde gösterilecek.\nOradan tümünü yazdırabilirsiniz. Devam edilsin mi?",
            "Toplu Yazdır",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        var allPages = new List<FormPageData>();
        foreach (var teacher in _teachers)
        {
            allPages.AddRange(GetFormPagesForTeacher(teacher));
        }

        int pageIdx = 0;
        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Margins   = new Margins(40, 40, 40, 40);

        pd.BeginPrint += (s2, e2) => pageIdx = 0;

        pd.PrintPage += (s2, ev) =>
        {
            if (ev.Graphics == null || pageIdx >= allPages.Count)
            {
                ev.HasMorePages = false;
                return;
            }

            DrawFormPage(ev.Graphics, ev.MarginBounds, allPages[pageIdx]);
            pageIdx++;
            ev.HasMorePages = pageIdx < allPages.Count;
        };

        PrintPreviewHelper.ShowPreview(pd, $"Toplu Görev Formu Önizleme — {_teachers.Count} Öğretmen", this);
    }

    private void BtnPrintMaster_Click(object? sender, EventArgs e)
    {
        if (_students.Count == 0)
        {
            MessageBox.Show("Yazdırılacak öğrenci kaydı bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var pd = BuildIcmalPrintDocument();
        PrintPreviewHelper.ShowPreview(pd, "Genel Dağıtım İcmal Çizelgesi Önizleme", this);
    }

    private PrintDocument BuildIcmalPrintDocument()
    {
        // Öğrencileri Alan → Sınıf → Ad sıralamasıyla hazırla
        var sorted = _students
            .OrderBy(s => s.FieldName ?? "")
            .ThenBy(s  => s.Classroom)
            .ThenBy(s  => s.FullName)
            .ToList();

        // Enterprise lookup (District için)
        var entDict = _enterprises.ToDictionary(x => x.Id, x => x);

        int _pageIdx  = 0;
        int _rowStart = 0;
        const int PAGE1_ROW_LIMIT = 20;
        const int OTHER_PAGE_ROW_LIMIT = 24;

        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Landscape = false;
        pd.DefaultPageSettings.Margins   = new Margins(40, 40, 45, 45);

        pd.BeginPrint += (s, e) => { _pageIdx = 0; _rowStart = 0; };

        pd.PrintPage += (s, ev) =>
        {
            if (ev.Graphics == null) return;
            var g      = ev.Graphics;
            var bounds = ev.MarginBounds;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // ── Font & Brush tanımları ─────────────────────────────────────
            using var fTc       = new Font("Segoe UI", 9.5f,   FontStyle.Bold);
            using var fSchool   = new Font("Segoe UI", 11f,    FontStyle.Bold);
            using var fTitle    = new Font("Segoe UI", 8.5f,   FontStyle.Bold);
            using var fSummary  = new Font("Segoe UI", 8f,     FontStyle.Bold);
            using var fHead     = new Font("Segoe UI", 7.5f,   FontStyle.Bold);
            using var fCell     = new Font("Segoe UI", 7f,     FontStyle.Regular);
            using var fCellB    = new Font("Segoe UI", 7f,     FontStyle.Bold);
            using var fSignTit  = new Font("Segoe UI", 8.5f,   FontStyle.Bold);
            using var fSignRole = new Font("Segoe UI", 8f,     FontStyle.Bold);
            using var fSignName = new Font("Segoe UI", 8f,     FontStyle.Bold);
            using var fSignSub  = new Font("Segoe UI", 7f,     FontStyle.Regular);

            using var bBlack    = new SolidBrush(Color.Black);
            using var bGrayText = new SolidBrush(Color.FromArgb(71, 85, 105));
            using var bRed      = new SolidBrush(Color.FromArgb(220, 38, 38)); // Atanmadı uyarısı
            using var bBlue     = new SolidBrush(Color.FromArgb(29, 78, 216)); // Görev günü
            using var bWhite    = new SolidBrush(Color.White);
            using var bHdrBg    = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var bAltBg    = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var pBorder   = new Pen(Color.FromArgb(30, 41, 59), 1.2f);
            using var pThin     = new Pen(Color.FromArgb(148, 163, 184), 0.75f);

            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            var sfLeft   = new StringFormat { Alignment = StringAlignment.Near,   LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            var sfWrapL  = new StringFormat { Alignment = StringAlignment.Near,   LineAlignment = StringAlignment.Center };
            var sfWrapC  = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var sfRight  = new StringFormat { Alignment = StringAlignment.Far,    LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            int x = bounds.X;
            int w = bounds.Width;
            int y = bounds.Y;

            bool isFirstPage = _pageIdx == 0;

            if (isFirstPage)
            {
                // ── T.C. Başlık ───────────────────────────────────────────
                string tc       = "T.C.";
                string val      = _settings.Valilik.ToUpperTr();
                string mem      = !string.IsNullOrWhiteSpace(_settings.DistrictMem) ? _settings.DistrictMem.ToUpperTr() : "";
                string school   = _settings.SchoolName;
                string titleStr = $"{_settings.AcademicYear} {_settings.Term} DÖNEMİ İŞLETMELERDE MESLEKİ EĞİTİM KOORDİNATÖRLÜK GENEL DAĞITIM İCMAL ÇİZELGESİ";

                g.DrawString(tc,     fTc,     bBlack, new RectangleF(x, y, w, 15), sfCenter); y += 15;
                g.DrawString(val,    fTc,     bBlack, new RectangleF(x, y, w, 15), sfCenter); y += 15;
                if (!string.IsNullOrWhiteSpace(mem))
                {
                    g.DrawString(mem, fTc,     bBlack, new RectangleF(x, y, w, 15), sfCenter); y += 15;
                }
                g.DrawString(school, fSchool, bBlack, new RectangleF(x, y, w, 18), sfCenter); y += 18;

                // Başlık metni ve alt çizgisi
                var titleSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(titleStr, fTitle, bBlack, new RectangleF(x, y, w, 18), titleSf);
                var ts = g.MeasureString(titleStr, fTitle, w, titleSf);
                float tx = x + (w - ts.Width) / 2f;
                using var pUnderline = new Pen(Color.Black, 1.2f);
                g.DrawLine(pUnderline, tx, y + 17, tx + ts.Width, y + 17);
                y += 24;

                // ── Özet Bilgi Satırı (4 Parça) ───────────────────────────
                int activeTeachers = _teachers.Count(t => _students.Any(s => s.TeacherId == t.Id));
                int totalEnterprises = _students.Where(s => s.EnterpriseId.HasValue).Select(s => s.EnterpriseId).Distinct().Count();
                string dateStr = !string.IsNullOrWhiteSpace(_settings.FormDate) ? _settings.FormDate : DateTime.Today.ToString("dd.MM.yyyy");

                int sumH = 20;
                g.FillRectangle(new SolidBrush(Color.FromArgb(243, 244, 246)), x, y, w, sumH);
                g.DrawRectangle(pBorder, x, y, w, sumH);

                float colW4 = w / 4f;
                g.DrawString($"Toplam Öğrenci: {sorted.Count}", fSummary, bBlack, new RectangleF(x + 8, y, colW4 - 8, sumH), sfLeft);
                g.DrawString($"Toplam İşletme: {totalEnterprises}", fSummary, bBlack, new RectangleF(x + colW4, y, colW4, sumH), sfLeft);
                g.DrawString($"Aktif Koordinatör Öğretmen: {activeTeachers}", fSummary, bBlack, new RectangleF(x + colW4 * 2, y, colW4 + 10, sumH), sfLeft);
                g.DrawString($"Tarih: {dateStr}", fSummary, bBlack, new RectangleF(x + colW4 * 3, y, colW4 - 8, sumH), sfRight);
                y += sumH + 8;
            }

            // ── Sütun Tanımları ve Genişlik Oranları ───────────────────────
            // S.No | Okul No | Öğrenci Adı Soyadı | Sınıf | Alanı / Dalı | Staj Günleri | İşletme Adı | İlçe | Koordinatör Öğretmen | Görev Günü
            int[] cw = [30, 46, 120, 40, 96, 75, 110, 48, 105, 58];
            string[] headers = ["S.No", "Okul No", "Öğrenci Adı Soyadı", "Sınıf", "Alanı / Dalı", "Staj Günleri", "İşletme Adı", "İlçe", "Koordinatör Öğretmen", "Görev Günü"];

            int totalCW = cw.Sum();
            float sc    = (float)w / totalCW;
            var sw      = cw.Select(c => (int)(c * sc)).ToArray();
            // Kalan pixel farkını son sütuna ekle
            sw[sw.Length - 1] += (w - sw.Sum());

            int rowH = 23;
            int hdrH = 24;

            // ── Tablo Başlığı ──────────────────────────────────────────────
            int tableTopY = y;
            int cx = x;
            g.FillRectangle(bHdrBg, x, y, w, hdrH);
            g.DrawRectangle(pBorder, x, y, w, hdrH);

            for (int i = 0; i < headers.Length; i++)
            {
                g.DrawString(headers[i], fHead, bBlack, new RectangleF(cx + 2, y, sw[i] - 4, hdrH), sfCenter);
                if (i < headers.Length - 1)
                    g.DrawLine(pBorder, cx + sw[i], y, cx + sw[i], y + hdrH);
                cx += sw[i];
            }
            y += hdrH;

            // ── Satırlar ─────────────────────────────────────────────────
            int maxRowsThisPage = isFirstPage ? PAGE1_ROW_LIMIT : OTHER_PAGE_ROW_LIMIT;
            int rowsDrawn = 0;

            for (int r = _rowStart; r < sorted.Count; r++)
            {
                // Sayfaya sığma kontrolü
                int neededBottom = bounds.Bottom - (isFirstPage ? 0 : 0);
                bool willFitSignature = (_rowStart + rowsDrawn + 1 >= sorted.Count);
                if (willFitSignature) neededBottom = bounds.Bottom - 85;

                if (y + rowH > neededBottom || rowsDrawn >= maxRowsThisPage)
                {
                    _rowStart = r;
                    goto NextPage;
                }

                var stu = sorted[r];
                bool alt = rowsDrawn % 2 == 1;
                if (alt) g.FillRectangle(bAltBg, x, y, w, rowH);

                // İşletme & İlçe
                string entName   = stu.EnterpriseName ?? "";
                string districtV = "-";
                if (stu.EnterpriseId.HasValue && entDict.TryGetValue(stu.EnterpriseId.Value, out var ent))
                {
                    if (!string.IsNullOrWhiteSpace(ent.District))
                        districtV = ent.District;
                }
                bool noEnt = !stu.EnterpriseId.HasValue || string.IsNullOrWhiteSpace(entName);

                // Gün metinleri
                var internDaysList = stu.GetDaysList();
                string internDays = internDaysList.Count > 0 ? TurkishTextHelper.FormatDays(string.Join(", ", internDaysList)) : "-";

                var dutyDaysList = stu.GetDutyDaysList();
                string dutyDays = dutyDaysList.Count > 0 ? TurkishTextHelper.FormatDays(string.Join(", ", dutyDaysList)) : "-";
                bool hasDutyDay = dutyDays != "-";

                string[] cells =
                [
                    (r + 1).ToString(),
                    stu.StudentNumber ?? "",
                    stu.FullName ?? "",
                    stu.Classroom ?? "",
                    stu.FieldName ?? "-",
                    internDays,
                    noEnt ? "Atanmadı" : entName,
                    districtV,
                    stu.TeacherName ?? "-",
                    dutyDays
                ];

                cx = x;
                for (int i = 0; i < cells.Length; i++)
                {
                    Brush curBrush = bBlack;
                    Font curFont = fCell;

                    if (i == 1) // Okul No
                    {
                        curFont = fCellB;
                    }
                    else if (i == 2) // Öğrenci Adı Soyadı
                    {
                        curFont = fCellB;
                    }
                    else if (i == 6 && noEnt) // İşletme Atanmadı
                    {
                        curBrush = bRed;
                    }
                    else if (i == 8) // Koordinatör Öğretmen
                    {
                        curFont = fCellB;
                    }
                    else if (i == 9 && hasDutyDay) // Görev Günü
                    {
                        curBrush = bBlue;
                    }

                    // Hizalama kuralı
                    StringFormat curSf;
                    if (i == 0 || i == 1 || i == 3 || i == 7 || i == 9)
                        curSf = sfWrapC; // Merkez
                    else
                        curSf = sfWrapL; // Sola hizalı

                    g.DrawString(cells[i], curFont, curBrush,
                        new RectangleF(cx + 3, y + 1, sw[i] - 6, rowH - 2), curSf);

                    if (i < cells.Length - 1)
                        g.DrawLine(pThin, cx + sw[i], y, cx + sw[i], y + rowH);

                    cx += sw[i];
                }

                g.DrawLine(pThin, x, y + rowH, x + w, y + rowH);
                y += rowH;
                rowsDrawn++;

                if (r == sorted.Count - 1) _rowStart = sorted.Count;
            }

            // Tablo dış çerçevesini koyulaştır
            g.DrawRectangle(pBorder, x, tableTopY, w, y - tableTopY);

            // ── İmza Bölümü (Yalnızca son sayfada) ─────────────────────────
            if (_rowStart >= sorted.Count)
            {
                int sy = bounds.Bottom - 75;
                int sw3 = w / 3;

                string[] titles = ["DÜZENLEYEN", "KONTROL EDEN", "ONAYLAYAN"];
                string[] roles  = ["Alan Şefi / Komisyon", "Koordinatör Müdür Yardımcısı", "Okul Müdürü"];
                string[] names  = ["", _settings.VicePrincipalName, _settings.PrincipalName];
                string[] subs   = ["İmza / Tarih", "İmza / Tarih", "Mühür / İmza / Tarih"];

                for (int col = 0; col < 3; col++)
                {
                    int sx = x + col * sw3;
                    g.DrawString(titles[col], fSignTit,  bBlack, new RectangleF(sx, sy,      sw3, 14), sfCenter);
                    g.DrawString(roles[col],  fSignRole, bBlack, new RectangleF(sx, sy + 13, sw3, 13), sfCenter);
                    if (!string.IsNullOrEmpty(names[col]))
                        g.DrawString(names[col], fSignName, bBlack, new RectangleF(sx, sy + 38, sw3, 13), sfCenter);
                    g.DrawString(subs[col],   fSignSub,  bGrayText, new RectangleF(sx, sy + 52, sw3, 12), sfCenter);
                }
            }

            if (_licenseService != null && !_licenseService.CurrentLicense.IsActive)
            {
                using var fWatermark = new Font("Segoe UI", 36f, FontStyle.Bold);
                using var bWatermark = new SolidBrush(Color.FromArgb(40, 239, 68, 68));
                var wmState = g.Save();
                g.TranslateTransform(bounds.Left + w / 2, bounds.Top + bounds.Height / 2);
                g.RotateTransform(-25);
                var sfWm = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("3308 PRO - LİSANSSIZ DEMO SÜRÜMÜ", fWatermark, bWatermark, 0, 0, sfWm);
                g.Restore(wmState);
            }

            NextPage:
            _pageIdx++;
            ev.HasMorePages = _rowStart < sorted.Count;
        };

        return pd;
    }
}

