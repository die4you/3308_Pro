using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class StudentAuditDialog : Form
{
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository? _fieldRepo;
    private readonly ISettingRepository? _settingRepo;

    // UI Controls
    private TextBox _txtFilePath = null!;
    private Button _btnBrowse = null!;
    private Button _btnPasteText = null!;
    private ComboBox _cmbEducationType = null!;
    private Button _btnAnalyze = null!;

    // Status Cards / Badges
    private Label _lblBadgeMissingInDb = null!;
    private Label _lblBadgeMissingInList = null!;
    private Label _lblBadgeMatched = null!;

    // Tabs
    private TabControl _tabControl = null!;
    private TabPage _tabMissingInDb = null!;
    private TabPage _tabMissingInList = null!;
    private TabPage _tabMatched = null!;

    // Grids
    private DataGridView _gridMissingInDb = null!;
    private DataGridView _gridMissingInList = null!;
    private DataGridView _gridMatched = null!;

    // Buttons
    private Button _btnAddMissingToDb = null!;
    private Button _btnMarkSurekliDevamsiz = null!;
    private Button _btnDeactivateMissingInList = null!;
    private Button _btnUpdateClassrooms = null!;
    private Button _btnClose = null!;

    // Data structures
    private List<Student> _allDbStudents = [];
    private List<SchoolClass> _allClasses = [];
    private List<AuditItem> _lastExternalItems = [];
    private List<AuditItem> _missingInDbItems = [];
    private List<AuditItem> _missingInListItems = [];
    private List<AuditItem> _matchedItems = [];

    public int ChangesCount { get; private set; }

    public StudentAuditDialog(
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository? fieldRepo = null,
        ISettingRepository? settingRepo = null)
    {
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _settingRepo = settingRepo;

        InitializeComponent();
        LoadDbStudentsAsync();
    }

    private void InitializeComponent()
    {
        Text = "🔍 Öğrenci Kontrolü ve Liste Karşılaştırma (Audit)";
        Size = new Size(1100, 760);
        MinimumSize = new Size(880, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = Color.White,
            Padding = new Padding(20, 8, 20, 8)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "🔍 Öğrenci Kontrol ve Karşılaştırma",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 8),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Verilen e-Okul / Excel listesi ile veritabanındaki kayıtları karşılaştırarak eksik, nakil giden ve sınıfı değişen öğrencileri tespit eder.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(20, 32),
            AutoSize = true
        };

        var lblReportNote = new Label
        {
            Text = "💡 (e-Okul > Öğrenci İşleri > Raporlar bölümünden OOG01001R020 Şube Listesi (Öğrenci No Sıralı) raporu alarak kontrol yapabilirsiniz.)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38), // Red-600
            Location = new Point(20, 54),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, lblReportNote]);

        // Input Selector Bar
        var pnlInput = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlInput.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, pnlInput.Height - 1, pnlInput.Width, pnlInput.Height - 1);
        };

        var lblFile = new Label
        {
            Text = "Liste / Dosya:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(16, 18),
            AutoSize = true
        };

        _txtFilePath = new TextBox
        {
            Location = new Point(106, 14),
            Width = 330,
            Font = new Font("Segoe UI", 9f),
            PlaceholderText = "e-Okul Excel dosyasını (.xls/.xlsx) seçiniz veya metin yapıştırınız..."
        };

        _btnBrowse = new Button
        {
            Text = "📁 Dosya Seç",
            Location = new Point(444, 13),
            Size = new Size(98, 30),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnBrowse.FlatAppearance.BorderSize = 0;
        _btnBrowse.Click += BtnBrowse_Click;

        _btnPasteText = new Button
        {
            Text = "📋 Metin Yapıştır",
            Location = new Point(548, 13),
            Size = new Size(110, 30),
            BackColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnPasteText.FlatAppearance.BorderSize = 0;
        _btnPasteText.Click += BtnPasteText_Click;

        var lblScope = new Label
        {
            Text = "🎓 Kapsam:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(668, 18),
            AutoSize = true
        };

        _cmbEducationType = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(744, 14),
            Width = 180,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbEducationType.Items.AddRange(["Tüm Okul (Tümü)", "Sadece Örgün Eğitim", "Sadece MESEM"]);
        _cmbEducationType.SelectedIndex = 0;
        _cmbEducationType.SelectedIndexChanged += (s, e) =>
        {
            if (_lastExternalItems.Count > 0)
            {
                PerformAudit(_lastExternalItems);
            }
        };

        _btnAnalyze = new Button
        {
            Text = "⚡ Karşılaştır",
            Location = new Point(932, 13),
            Size = new Size(125, 30),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnAnalyze.FlatAppearance.BorderSize = 0;
        _btnAnalyze.Click += (s, e) => AnalyzeCurrentSource();

        pnlInput.Controls.AddRange([lblFile, _txtFilePath, _btnBrowse, _btnPasteText, lblScope, _cmbEducationType, _btnAnalyze]);

        // KPI Badges Bar
        var pnlBadges = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.White,
            Padding = new Padding(20, 8, 20, 8)
        };

        _lblBadgeMissingInDb = CreateBadgeLabel("🔴 Veritabanında Eksik: 0 Öğrenci", Color.FromArgb(254, 226, 226), Color.FromArgb(185, 28, 28));
        _lblBadgeMissingInDb.Location = new Point(20, 8);

        _lblBadgeMissingInList = CreateBadgeLabel("🟡 Listede Olmayan: 0 Öğrenci", Color.FromArgb(254, 243, 199), Color.FromArgb(180, 83, 9));
        _lblBadgeMissingInList.Location = new Point(280, 8);

        _lblBadgeMatched = CreateBadgeLabel("🟢 Tam Uyumlu: 0 Öğrenci", Color.FromArgb(220, 252, 231), Color.FromArgb(21, 128, 61));
        _lblBadgeMatched.Location = new Point(530, 8);

        pnlBadges.Controls.AddRange([_lblBadgeMissingInDb, _lblBadgeMissingInList, _lblBadgeMatched]);

        // Main Tab Control
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        // Tab 1: Missing in DB
        _tabMissingInDb = new TabPage("🔴 Veritabanında Eksik (Dış Listede Var)")
        {
            BackColor = Color.White
        };
        _gridMissingInDb = CreateDataGrid();
        _btnAddMissingToDb = new Button
        {
            Text = "➕ Seçilenleri Veritabanına Ekle",
            Size = new Size(220, 34),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnAddMissingToDb.FlatAppearance.BorderSize = 0;
        _btnAddMissingToDb.Click += BtnAddMissingToDb_Click;

        SetupTabContent(_tabMissingInDb, _gridMissingInDb, _btnAddMissingToDb);

        // Tab 2: Missing in List
        _tabMissingInList = new TabPage("🟡 Listede Olmayan (Veritabanında Var)")
        {
            BackColor = Color.White
        };
        _gridMissingInList = CreateDataGrid();
        _btnMarkSurekliDevamsiz = new Button
        {
            Text = "⚠️ Seçilenleri Sürekli Devamsıza Ayır",
            Size = new Size(245, 34),
            BackColor = Color.FromArgb(217, 119, 6), // amber-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnMarkSurekliDevamsiz.FlatAppearance.BorderSize = 0;
        _btnMarkSurekliDevamsiz.Click += BtnMarkSurekliDevamsiz_Click;

        _btnDeactivateMissingInList = new Button
        {
            Text = "📁 Seçilenleri Pasife Al / Arşive Taşı",
            Size = new Size(240, 34),
            BackColor = Color.FromArgb(220, 38, 38), // red-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnDeactivateMissingInList.FlatAppearance.BorderSize = 0;
        _btnDeactivateMissingInList.Click += BtnDeactivateMissingInList_Click;

        SetupTabContent(_tabMissingInList, _gridMissingInList, _btnMarkSurekliDevamsiz, _btnDeactivateMissingInList);

        // Tab 3: Matched
        _tabMatched = new TabPage("🟢 Uyumlu / Sınıf Kontrolü")
        {
            BackColor = Color.White
        };
        _gridMatched = CreateDataGrid();
        _btnUpdateClassrooms = new Button
        {
            Text = "🔄 Sınıf Değişikliklerini Güncelle",
            Size = new Size(230, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnUpdateClassrooms.FlatAppearance.BorderSize = 0;
        _btnUpdateClassrooms.Click += BtnUpdateClassrooms_Click;

        SetupTabContent(_tabMatched, _gridMatched, _btnUpdateClassrooms);

        _tabControl.TabPages.AddRange([_tabMissingInDb, _tabMissingInList, _tabMatched]);

        // Bottom Footer Panel
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            BackColor = Color.White,
            Padding = new Padding(20, 8, 20, 8)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        var btnExportExcel = new Button
        {
            Text = "📊 Fark Raporunu Kaydet (Excel)",
            Size = new Size(220, 34),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(20, 9)
        };
        btnExportExcel.FlatAppearance.BorderSize = 0;
        btnExportExcel.Click += BtnExportExcel_Click;

        var btnViewReport = new Button
        {
            Text = "📄 Detaylı Raporu Aç (Not Defteri)",
            Size = new Size(235, 34),
            BackColor = Color.FromArgb(51, 65, 85), // slate-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(248, 9)
        };
        btnViewReport.FlatAppearance.BorderSize = 0;
        btnViewReport.Click += BtnViewReport_Click;

        _btnClose = new Button
        {
            Text = "Kapat",
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(965, 9)
        };
        _btnClose.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnClose.Click += (s, e) => DialogResult = ChangesCount > 0 ? DialogResult.OK : DialogResult.Cancel;

        pnlFooter.Controls.AddRange([btnExportExcel, btnViewReport, _btnClose]);

        Controls.Add(_tabControl);
        Controls.Add(pnlBadges);
        Controls.Add(pnlInput);
        Controls.Add(pnlHeader);
        Controls.Add(pnlFooter);
    }

    private static Label CreateBadgeLabel(string text, Color backColor, Color textColor)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = backColor,
            ForeColor = textColor,
            Padding = new Padding(10, 4, 10, 4),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private static DataGridView CreateDataGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            RowTemplate = { Height = 34 }
        };

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        grid.EnableHeadersVisualStyles = false;

        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex == -1 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "colSelect")
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                bool allChecked = grid.Rows.Count > 0;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.Cells["colSelect"] == null || !Convert.ToBoolean(row.Cells["colSelect"].Value))
                    {
                        allChecked = false;
                        break;
                    }
                }

                var state = allChecked ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                Size checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                Point checkLocation = new Point(
                    e.CellBounds.Left + (e.CellBounds.Width - checkSize.Width) / 2,
                    e.CellBounds.Top + (e.CellBounds.Height - checkSize.Height) / 2
                );

                CheckBoxRenderer.DrawCheckBox(e.Graphics, checkLocation, state);
                e.Handled = true;
            }
        };

        grid.ColumnHeaderMouseClick += (s, e) =>
        {
            if (e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "colSelect")
            {
                grid.EndEdit();
                bool allChecked = grid.Rows.Count > 0;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.Cells["colSelect"] == null || !Convert.ToBoolean(row.Cells["colSelect"].Value))
                    {
                        allChecked = false;
                        break;
                    }
                }

                bool targetVal = !allChecked;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.Cells["colSelect"] != null)
                    {
                        row.Cells["colSelect"].Value = targetVal;
                    }
                }
                grid.EndEdit();
                grid.InvalidateCell(e.ColumnIndex, -1);
            }
        };

        return grid;
    }

    private static void SetupTabContent(TabPage page, DataGridView grid, params Button[] actionBtns)
    {
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };
        pnlGridContainer.Controls.Add(grid);

        var pnlAction = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12, 8, 12, 8)
        };

        void PositionButtons()
        {
            if (pnlAction.ClientSize.Width > 0 && actionBtns.Length > 0)
            {
                int currentRight = pnlAction.ClientSize.Width - 16;
                for (int i = actionBtns.Length - 1; i >= 0; i--)
                {
                    var btn = actionBtns[i];
                    int x = currentRight - btn.Width;
                    int y = (pnlAction.ClientSize.Height - btn.Height) / 2;
                    btn.Location = new Point(Math.Max(12, x), Math.Max(4, y));
                    btn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    currentRight = btn.Left - 10;
                }
            }
        }

        pnlAction.Resize += (s, e) => PositionButtons();
        page.Resize += (s, e) => PositionButtons();
        PositionButtons();

        foreach (var btn in actionBtns)
        {
            pnlAction.Controls.Add(btn);
        }

        page.Controls.Add(pnlGridContainer);
        page.Controls.Add(pnlAction);
    }

    private async void LoadDbStudentsAsync()
    {
        try
        {
            _allDbStudents = (await _studentRepo.GetAllAsync()).Where(s => s.IsActive).ToList();
            _allClasses = (await _classRepo.GetAllAsync()).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Veritabanı verileri okunurken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool IsMesemStudent(Student s)
    {
        var cls = _allClasses.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
        return FeeCalculator.IsMesem(s, cls);
    }

    private bool IsMesemItem(AuditItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.EducationType) && item.EducationType.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (!string.IsNullOrWhiteSpace(item.Classroom) && item.Classroom.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (item.ExistingStudent != null)
        {
            return IsMesemStudent(item.ExistingStudent);
        }
        return false;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog
        {
            Filter = "e-Okul Excel Dosyaları (*.xls;*.xlsx)|*.xls;*.xlsx|Tüm Dosyalar (*.*)|*.*",
            Title = "e-Okul Öğrenci Listesi Seçiniz"
        };

        if (openFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtFilePath.Text = openFileDialog.FileName;
            AnalyzeCurrentSource();
        }
    }

    private void BtnPasteText_Click(object? sender, EventArgs e)
    {
        using var dlg = new Form
        {
            Text = "📋 Metin Yapıştırarak Karşılaştır",
            Size = new Size(600, 450),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = UITheme.Background
        };

        var lbl = new Label
        {
            Text = "Excel veya Web sayfasından kopyaladığınız öğrenci listesini (Okul No, Ad Soyad, Sınıf) aşağıdaki kutuya yapıştırınız:",
            Location = new Point(16, 12),
            Size = new Size(550, 30),
            Font = new Font("Segoe UI", 9f)
        };

        var txtPaste = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Location = new Point(16, 45),
            Size = new Size(550, 300),
            Font = new Font("Consolas", 9f)
        };

        var btnOk = new Button
        {
            Text = "Karşılaştır",
            DialogResult = DialogResult.OK,
            Location = new Point(360, 360),
            Size = new Size(100, 32),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        var btnCancel = new Button
        {
            Text = "İptal",
            DialogResult = DialogResult.Cancel,
            Location = new Point(468, 360),
            Size = new Size(95, 32)
        };

        dlg.Controls.AddRange([lbl, txtPaste, btnOk, btnCancel]);

        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txtPaste.Text))
        {
            _txtFilePath.Text = "[Metin Yapıştırıldı]";
            AnalyzePastedText(txtPaste.Text);
        }
    }

    private void AnalyzeCurrentSource()
    {
        string path = _txtFilePath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show("Lütfen bir Excel dosyası seçiniz veya metin yapıştırınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (File.Exists(path))
        {
            var parseResult = EOkulClassListParser.ParseFile(path);
            if (!parseResult.Success || parseResult.AllStudents.Count == 0)
            {
                MessageBox.Show($"Dosya okunamadı veya öğrenci bulunamadı:\n{parseResult.ErrorMessage}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _lastExternalItems = parseResult.AllStudents.Select(s => new AuditItem
            {
                StudentNumber = s.StudentNumber,
                FullName = s.FullName,
                Classroom = s.Classroom,
                Grade = s.Grade,
                FieldName = s.FieldName,
                EducationType = s.EducationType
            }).ToList();

            // Akıllı Kapsam Önerisi (Dosyadaki öğrencilerin türüne göre)
            int mesemCount = _lastExternalItems.Count(IsMesemItem);
            if (mesemCount > 0 && mesemCount == _lastExternalItems.Count)
            {
                _cmbEducationType.SelectedIndex = 2; // Sadece MESEM
            }
            else if (mesemCount == 0 && _lastExternalItems.Count > 0)
            {
                _cmbEducationType.SelectedIndex = 1; // Sadece Örgün Eğitim
            }

            PerformAudit(_lastExternalItems);
        }
    }

    private void AnalyzePastedText(string text)
    {
        var items = new List<AuditItem>();
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split(new[] { '\t', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string p0 = parts[0].Trim();
                string p1 = parts[1].Trim();
                string p2 = parts.Length > 2 ? parts[2].Trim() : "";

                string stdNo = "";
                string fullName = "";
                string classroom = "";

                if (Regex.IsMatch(p0, @"^\d+$"))
                {
                    stdNo = p0;
                    fullName = p1;
                    classroom = p2;
                }
                else if (Regex.IsMatch(p1, @"^\d+$"))
                {
                    stdNo = p1;
                    fullName = p0;
                    classroom = p2;
                }
                else
                {
                    fullName = p0;
                    classroom = p1;
                }

                if (!string.IsNullOrEmpty(fullName) || !string.IsNullOrEmpty(stdNo))
                {
                    items.Add(new AuditItem
                    {
                        StudentNumber = stdNo,
                        FullName = fullName,
                        Classroom = classroom
                    });
                }
            }
        }

        if (items.Count == 0)
        {
            MessageBox.Show("Metinden ayrıştırılabilir öğrenci satırı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _lastExternalItems = items;
        PerformAudit(_lastExternalItems);
    }

    private static string CleanStudentNo(string? val)
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

    private void PerformAudit(List<AuditItem> externalItems)
    {
        _lastExternalItems = externalItems;
        _missingInDbItems.Clear();
        _missingInListItems.Clear();
        _matchedItems.Clear();

        int selectedScopeIndex = _cmbEducationType?.SelectedIndex ?? 0; // 0: Tümü, 1: Sadece Örgün, 2: Sadece MESEM

        // Kapsama göre veritabanı öğrencilerini filtrele
        var dbStudentsToAudit = _allDbStudents.Where(s =>
        {
            bool isMesem = IsMesemStudent(s);
            if (selectedScopeIndex == 1 && isMesem) return false;  // Sadece Örgün istendi
            if (selectedScopeIndex == 2 && !isMesem) return false; // Sadece MESEM istendi
            return true;
        }).ToList();

        // Kapsama göre dış liste (Excel) öğrencilerini filtrele
        var externalItemsToAudit = externalItems.Where(item =>
        {
            bool isMesem = IsMesemItem(item);
            if (selectedScopeIndex == 1 && isMesem) return false;  // Sadece Örgün istendi
            if (selectedScopeIndex == 2 && !isMesem) return false; // Sadece MESEM istendi
            return true;
        }).ToList();

        // Güvenli sözlükler (Tekrarlanan anahtar hatası vermez)
        var dbNoMap = new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);
        var dbCleanNoMap = new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);
        var dbNameMap = new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in dbStudentsToAudit)
        {
            if (!string.IsNullOrWhiteSpace(s.StudentNumber))
            {
                string raw = s.StudentNumber.Trim();
                if (!dbNoMap.ContainsKey(raw)) dbNoMap[raw] = s;

                string clean = CleanStudentNo(raw);
                if (!string.IsNullOrEmpty(clean) && !dbCleanNoMap.ContainsKey(clean)) dbCleanNoMap[clean] = s;
            }

            string cleanName = CleanName(s.FullName);
            if (!string.IsNullOrEmpty(cleanName) && !dbNameMap.ContainsKey(cleanName)) dbNameMap[cleanName] = s;
        }

        var externalNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var externalCleanNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Dış listedeki (Excel) öğrencileri DB ile karşılaştır
        foreach (var item in externalItemsToAudit)
        {
            string rawNo = (item.StudentNumber ?? "").Trim();
            string cleanNo = CleanStudentNo(rawNo);

            if (!string.IsNullOrEmpty(rawNo)) externalNos.Add(rawNo);
            if (!string.IsNullOrEmpty(cleanNo)) externalCleanNos.Add(cleanNo);

            Student? matchedDbStudent = null;

            if (!string.IsNullOrEmpty(cleanNo) && dbCleanNoMap.TryGetValue(cleanNo, out var sByCleanNo))
            {
                matchedDbStudent = sByCleanNo;
            }
            else if (!string.IsNullOrEmpty(rawNo) && dbNoMap.TryGetValue(rawNo, out var sByNo))
            {
                matchedDbStudent = sByNo;
            }
            else if (!string.IsNullOrEmpty(item.FullName) && dbNameMap.TryGetValue(CleanName(item.FullName), out var sByName))
            {
                matchedDbStudent = sByName;
            }

            if (matchedDbStudent != null)
            {
                item.ExistingStudent = matchedDbStudent;
                item.DbClassroom = matchedDbStudent.Classroom ?? "";
                _matchedItems.Add(item);
            }
            else
            {
                _missingInDbItems.Add(item);
            }
        }

        // 2. DB'deki öğrencileri dış liste ile karşılaştır
        foreach (var dbStudent in dbStudentsToAudit)
        {
            bool found = false;
            string dbRawNo = (dbStudent.StudentNumber ?? "").Trim();
            string dbCleanNo = CleanStudentNo(dbRawNo);

            if (!string.IsNullOrEmpty(dbCleanNo) && externalCleanNos.Contains(dbCleanNo))
            {
                found = true;
            }
            else if (!string.IsNullOrEmpty(dbRawNo) && externalNos.Contains(dbRawNo))
            {
                found = true;
            }
            else if (_matchedItems.Any(m => m.ExistingStudent?.Id == dbStudent.Id))
            {
                found = true;
            }

            if (!found)
            {
                _missingInListItems.Add(new AuditItem
                {
                    ExistingStudent = dbStudent,
                    StudentNumber = dbStudent.StudentNumber ?? "",
                    FullName = dbStudent.FullName,
                    Classroom = dbStudent.Classroom ?? "",
                    DbClassroom = dbStudent.Classroom ?? "",
                    EducationType = IsMesemStudent(dbStudent) ? "MESEM" : "Örgün Eğitim"
                });
            }
        }

        // Sayaçları ve etiketleri güncelle
        string scopeSuffix = selectedScopeIndex == 1 ? " (Örgün)" : (selectedScopeIndex == 2 ? " (MESEM)" : "");
        _lblBadgeMissingInDb.Text = $"🔴 Excel'de Var / DB'de Yok: {_missingInDbItems.Count} Öğrenci{scopeSuffix}";
        _lblBadgeMissingInList.Text = $"🟡 DB'de Var / Excel'de Yok: {_missingInListItems.Count} Öğrenci{scopeSuffix}";
        _lblBadgeMatched.Text = $"🟢 Eşleşen: {_matchedItems.Count} Öğrenci{scopeSuffix}";

        BindGrids();

        if (_settingRepo != null)
        {
            _ = _settingRepo.SetValueAsync("LastStudentAuditDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "Son e-Okul Öğrenci Kontrol Tarihi");
        }
    }

    private static string CleanName(string name)
    {
        return Regex.Replace(name ?? "", @"\s+", " ").Trim().ToUpper(new System.Globalization.CultureInfo("tr-TR"));
    }

    private void BindGrids()
    {
        // Bind Tab 1: Missing in DB
        _gridMissingInDb.Columns.Clear();
        _gridMissingInDb.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSelect", HeaderText = "", Width = 38 });
        _gridMissingInDb.Columns.Add("colNo", "Okul No");
        _gridMissingInDb.Columns.Add("colName", "Ad Soyad");
        _gridMissingInDb.Columns.Add("colClass", "Sınıf/Şube");
        _gridMissingInDb.Columns.Add("colField", "Alan");
        _gridMissingInDb.Columns.Add("colEduType", "Eğitim Türü");

        foreach (var item in _missingInDbItems)
        {
            int idx = _gridMissingInDb.Rows.Add(true, item.StudentNumber, item.FullName, item.Classroom, item.FieldName, item.EducationType);
            _gridMissingInDb.Rows[idx].Tag = item;
        }

        // Bind Tab 2: Missing in List
        _gridMissingInList.Columns.Clear();
        _gridMissingInList.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSelect", HeaderText = "", Width = 38 });
        _gridMissingInList.Columns.Add("colNo", "Okul No");
        _gridMissingInList.Columns.Add("colName", "Ad Soyad");
        _gridMissingInList.Columns.Add("colClass", "DB Sınıfı");
        _gridMissingInList.Columns.Add("colStatus", "Durum");

        foreach (var item in _missingInListItems)
        {
            string status = "Aktif (Listede Yok)";
            bool isSurekliDevamsiz = item.ExistingStudent?.IsSurekliDevamsiz == true;
            if (isSurekliDevamsiz)
            {
                string exitInfo = item.ExistingStudent!.SgkExitDate.HasValue
                    ? $"SGK Çıkış: {item.ExistingStudent.SgkExitDate.Value:dd.MM.yyyy}"
                    : "SGK Çıkışı Bekliyor";
                status = $"🚫 Sürekli Devamsız ({exitInfo})";
            }

            int idx = _gridMissingInList.Rows.Add(!isSurekliDevamsiz, item.StudentNumber, item.FullName, item.DbClassroom, status);
            _gridMissingInList.Rows[idx].Tag = item;

            if (isSurekliDevamsiz)
            {
                _gridMissingInList.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
                _gridMissingInList.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(100, 116, 139);
            }
        }

        // Bind Tab 3: Matched
        _gridMatched.Columns.Clear();
        _gridMatched.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSelect", HeaderText = "", Width = 38 });
        _gridMatched.Columns.Add("colNo", "Okul No");
        _gridMatched.Columns.Add("colName", "Ad Soyad");
        _gridMatched.Columns.Add("colDbClass", "Mevcut DB Sınıfı");
        _gridMatched.Columns.Add("colListClass", "Listeden Gelen Sınıf");
        _gridMatched.Columns.Add("colMatchStatus", "Uyum Durumu");

        foreach (var item in _matchedItems)
        {
            bool classDiffers = !string.IsNullOrWhiteSpace(item.Classroom) &&
                                !string.Equals(item.Classroom.Trim(), item.DbClassroom.Trim(), StringComparison.OrdinalIgnoreCase);

            string status = classDiffers ? "⚠️ Sınıfı Değişmiş" : "🟢 Tam Uyumlu";
            bool isSurekliDevamsiz = item.ExistingStudent?.IsSurekliDevamsiz == true;
            if (isSurekliDevamsiz)
            {
                string exitInfo = item.ExistingStudent!.SgkExitDate.HasValue
                    ? $"SGK Çıkış: {item.ExistingStudent.SgkExitDate.Value:dd.MM.yyyy}"
                    : "SGK Çıkışı Bekliyor";
                status = $"🚫 Sürekli Devamsız ({exitInfo})";
            }

            int idx = _gridMatched.Rows.Add(classDiffers, item.StudentNumber, item.FullName, item.DbClassroom, item.Classroom, status);
            _gridMatched.Rows[idx].Tag = item;

            if (isSurekliDevamsiz)
            {
                _gridMatched.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
                _gridMatched.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(100, 116, 139);
            }
            else if (classDiffers)
            {
                _gridMatched.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
            }
        }
    }

    private async void BtnAddMissingToDb_Click(object? sender, EventArgs e)
    {
        _gridMissingInDb.EndEdit();
        var selectedItems = new List<AuditItem>();
        foreach (DataGridViewRow row in _gridMissingInDb.Rows)
        {
            if (row.Cells["colSelect"] != null && Convert.ToBoolean(row.Cells["colSelect"].Value) && row.Tag is AuditItem item)
            {
                selectedItems.Add(item);
            }
        }

        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Lütfen veritabanına eklenecek öğrencileri seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Seçilen {selectedItems.Count} yeni öğrenci veritabanına eklenecektir.\n\nDevam etmek istiyor musunuz?",
            "Öğrenci Ekleme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            int added = 0;
            foreach (var item in selectedItems)
            {
                var names = SplitFullName(item.FullName);
                var student = new Student
                {
                    StudentNumber = item.StudentNumber,
                    FirstName = names.FirstName,
                    LastName = names.LastName,
                    Classroom = item.Classroom,
                    Grade = item.Grade,
                    EducationType = string.IsNullOrWhiteSpace(item.EducationType) ? "Örgün Eğitim" : item.EducationType,
                    InternshipDays = item.InternshipDays,
                    IsActive = true
                };

                await _studentRepo.AddAsync(student);
                added++;
            }

            ChangesCount += added;
            MessageBox.Show($"{added} öğrenci başarıyla veritabanına eklendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadDbStudentsAsync();
            AnalyzeCurrentSource();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Öğrenciler eklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnMarkSurekliDevamsiz_Click(object? sender, EventArgs e)
    {
        _gridMissingInList.EndEdit();
        var selectedItems = new List<AuditItem>();
        foreach (DataGridViewRow row in _gridMissingInList.Rows)
        {
            if (row.Cells["colSelect"] != null && Convert.ToBoolean(row.Cells["colSelect"].Value) && row.Tag is AuditItem item)
            {
                if (item.ExistingStudent != null && !item.ExistingStudent.IsSurekliDevamsiz)
                {
                    selectedItems.Add(item);
                }
            }
        }

        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Lütfen sürekli devamsıza ayrılacak öğrencileri seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Seçilen {selectedItems.Count} öğrenci 'Sürekli Devamsız' olarak işaretlenecek ve 'Sigorta İşlemleri > Sürekli Devamsızlar' ekranına aktarılacaktır.\n\n" +
            "• Öğrenciler aktif sınıf listesinde kalacak (e-Okul tekrar sorgularında eksik uyarısı vermeyecektir).\n" +
            "• Aylık devamsızlık ve ücret/staj bordrolarına dahil edilmeyecektir.\n" +
            "• SGK çıkış işlemlerini 'Sigorta İşlemleri > Sürekli Devamsızlar' ekranından yürütebilirsiniz.\n\n" +
            "Onaylıyor musunuz?",
            "Sürekli Devamsıza Ayırma Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            int updated = 0;
            foreach (var item in selectedItems)
            {
                if (item.ExistingStudent != null)
                {
                    item.ExistingStudent.IsSurekliDevamsiz = true;
                    await _studentRepo.UpdateAsync(item.ExistingStudent);
                    updated++;
                }
            }

            ChangesCount += updated;
            MessageBox.Show($"{updated} öğrenci başarıyla 'Sürekli Devamsız' olarak işaretlendi.\n\nSGK çıkış tarihlerini 'Sigorta İşlemleri > Sürekli Devamsızlar' bölümünden girebilirsiniz.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadDbStudentsAsync();
            AnalyzeCurrentSource();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"İşlem sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDeactivateMissingInList_Click(object? sender, EventArgs e)
    {
        _gridMissingInList.EndEdit();
        var selectedItems = new List<AuditItem>();
        foreach (DataGridViewRow row in _gridMissingInList.Rows)
        {
            if (row.Cells["colSelect"] != null && Convert.ToBoolean(row.Cells["colSelect"].Value) && row.Tag is AuditItem item)
            {
                selectedItems.Add(item);
            }
        }

        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Lütfen pasife alınacak öğrencileri seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Seçilen {selectedItems.Count} öğrenci pasife alınacak (IsActive = false yapılacaktır).\n\nDevam etmek istiyor musunuz?",
            "Pasife Alma Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            int updated = 0;
            foreach (var item in selectedItems)
            {
                if (item.ExistingStudent != null)
                {
                    item.ExistingStudent.IsActive = false;
                    await _studentRepo.UpdateAsync(item.ExistingStudent);
                    updated++;
                }
            }

            ChangesCount += updated;
            MessageBox.Show($"{updated} öğrenci pasife alındı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadDbStudentsAsync();
            AnalyzeCurrentSource();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"İşlem sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnUpdateClassrooms_Click(object? sender, EventArgs e)
    {
        _gridMatched.EndEdit();
        var selectedItems = new List<AuditItem>();
        foreach (DataGridViewRow row in _gridMatched.Rows)
        {
            if (row.Cells["colSelect"] != null && Convert.ToBoolean(row.Cells["colSelect"].Value) && row.Tag is AuditItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.Classroom) && item.ExistingStudent != null &&
                    !string.Equals(item.Classroom.Trim(), item.DbClassroom.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    selectedItems.Add(item);
                }
            }
        }

        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Sınıfı değişen ve güncelleme yapılacak öğrenci seçilmedi.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Seçilen {selectedItems.Count} öğrencinin sınıf/şube bilgisi güncellenecektir.\n\nDevam etmek istiyor musunuz?",
            "Sınıf Güncelleme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            int updated = 0;
            foreach (var item in selectedItems)
            {
                if (item.ExistingStudent != null)
                {
                    item.ExistingStudent.Classroom = item.Classroom;
                    await _studentRepo.UpdateAsync(item.ExistingStudent);
                    updated++;
                }
            }

            ChangesCount += updated;
            MessageBox.Show($"{updated} öğrencinin sınıf bilgisi güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadDbStudentsAsync();
            AnalyzeCurrentSource();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Güncelleme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (_missingInDbItems.Count == 0 && _missingInListItems.Count == 0 && _matchedItems.Count == 0)
        {
            MessageBox.Show("Aktarılacak karşılaştırma verisi bulunamadı. Lütfen önce bir dosya veya metin karşılaştırınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Uyumlu CSV Dosyası (*.csv)|*.csv|Tüm Dosyalar (*.*)|*.*",
            FileName = $"Ogrenci_Karsilastirma_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            Title = "Karşılaştırma Raporunu Kaydet"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("\uFEFFKategori;Okul No;Adı Soyadı;Sınıf Bilgisi;Ek Bilgi / Alan;Durum");

                foreach (var item in _missingInDbItems)
                {
                    sb.AppendLine($"Veritabanında Eksik;\"{item.StudentNumber}\";\"{item.FullName}\";\"{item.Classroom}\";\"{item.FieldName}\";Yeni / Nakil Gelen");
                }

                foreach (var item in _missingInListItems)
                {
                    sb.AppendLine($"Listede Olmayan;\"{item.StudentNumber}\";\"{item.FullName}\";\"{item.DbClassroom}\";-;Mezun / Nakil Giden (Pasif)");
                }

                foreach (var item in _matchedItems)
                {
                    bool diff = !string.IsNullOrWhiteSpace(item.Classroom) && !string.Equals(item.Classroom.Trim(), item.DbClassroom.Trim(), StringComparison.OrdinalIgnoreCase);
                    string status = diff ? $"Sınıf Değişmiş (DB: {item.DbClassroom} -> Liste: {item.Classroom})" : "Tam Uyumlu";
                    sb.AppendLine($"Uyumlu Öğrenci;\"{item.StudentNumber}\";\"{item.FullName}\";\"{item.DbClassroom}\";\"{item.FieldName}\";{status}");
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show($"Karşılaştırma raporu başarıyla kaydedildi:\n{sfd.FileName}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnViewReport_Click(object? sender, EventArgs e)
    {
        if (_missingInDbItems.Count == 0 && _missingInListItems.Count == 0 && _matchedItems.Count == 0)
        {
            MessageBox.Show("Rapor oluşturulacak karşılaştırma verisi bulunamadı. Lütfen önce bir Excel dosyası seçiniz veya metin yapıştırınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("==========================================================================================");
        sb.AppendLine("            MEB KOORDİNATÖRLÜK SİSTEMİ - e-OKUL ÖĞRENCİ KONTROL VE KARŞILAŞTIRMA RAPORU");
        sb.AppendLine("==========================================================================================");
        sb.AppendLine($"Tarih / Saat       : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine($"İncelenen Dosya    : {Path.GetFileName(_txtFilePath.Text)}");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        sb.AppendLine($"Excel'deki Toplam Öğrenci Sayısı     : {_missingInDbItems.Count + _matchedItems.Count}");
        sb.AppendLine($"Veritabanındaki Toplam Öğrenci Sayısı: {_allDbStudents.Count}");
        sb.AppendLine($"Tam Uyumlu / Eşleşen Öğrenci Sayısı  : {_matchedItems.Count}");
        sb.AppendLine($"🔴 Excel'de Olup Sistemde Olmayan     : {_missingInDbItems.Count} (Yeni veya Nakil Gelen)");
        sb.AppendLine($"🟡 Sistemde Olup Excel'de Olmayan     : {_missingInListItems.Count} (Ayrılmış / Nakil Gitmiş / Pasif)");
        sb.AppendLine("==========================================================================================");
        sb.AppendLine();

        // 1. Excel'de Olup Sistemde Olmayanlar
        sb.AppendLine("1. 🔴 EXCEL'DE OLUP SİSTEMDE KAYITLI OLMAYAN ÖĞRENCİLER (EKSİK / YENİ ÖĞRENCİLER):");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        if (_missingInDbItems.Count == 0)
        {
            sb.AppendLine("   (Tebrikler! Excel'deki tüm öğrenciler sistemde kayıtlı.)");
        }
        else
        {
            sb.AppendLine(string.Format("{0,-6} | {1,-10} | {2,-26} | {3,-12} | {4,-20}",
                "Sıra", "Okul No", "Adı Soyadı", "Sınıf/Şube", "Alan / Bölüm"));
            sb.AppendLine("------------------------------------------------------------------------------------------");
            int sNo = 1;
            foreach (var item in _missingInDbItems)
            {
                sb.AppendLine(string.Format("{0,-6} | {1,-10} | {2,-26} | {3,-12} | {4,-20}",
                    sNo++,
                    string.IsNullOrWhiteSpace(item.StudentNumber) ? "-" : item.StudentNumber,
                    item.FullName,
                    string.IsNullOrWhiteSpace(item.Classroom) ? "-" : item.Classroom,
                    string.IsNullOrWhiteSpace(item.FieldName) ? "-" : item.FieldName));
            }
        }
        sb.AppendLine();

        // 2. Sistemde Olup Excel'de Olmayanlar
        sb.AppendLine("2. 🟡 SİSTEMDE KAYITLI OLUP EXCEL LİSTESİNDE BULUNMAYAN ÖĞRENCİLER (NAKİL GİDEN / AYRILAN):");
        sb.AppendLine("------------------------------------------------------------------------------------------");
        if (_missingInListItems.Count == 0)
        {
            sb.AppendLine("   (Tebrikler! Sistemdeki tüm öğrenciler Excel listesinde mevcut.)");
        }
        else
        {
            sb.AppendLine(string.Format("{0,-6} | {1,-10} | {2,-26} | {3,-12} | {4,-20}",
                "Sıra", "Okul No", "Adı Soyadı", "Sınıf/Şube", "Staj İşletmesi"));
            sb.AppendLine("------------------------------------------------------------------------------------------");
            int sNo = 1;
            foreach (var item in _missingInListItems)
            {
                string ent = item.ExistingStudent?.EnterpriseName ?? "-";
                sb.AppendLine(string.Format("{0,-6} | {1,-10} | {2,-26} | {3,-12} | {4,-20}",
                    sNo++,
                    string.IsNullOrWhiteSpace(item.StudentNumber) ? "-" : item.StudentNumber,
                    item.FullName,
                    string.IsNullOrWhiteSpace(item.DbClassroom) ? "-" : item.DbClassroom,
                    ent));
            }
        }
        sb.AppendLine();

        // 3. Sınıfı Değişenler
        var diffClassItems = _matchedItems.Where(m =>
            !string.IsNullOrWhiteSpace(m.Classroom) &&
            !string.Equals(m.Classroom.Trim(), m.DbClassroom.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (diffClassItems.Count > 0)
        {
            sb.AppendLine("3. 🔄 SINIF BİLGİSİ FARKLI OLAN ÖĞRENCİLER (GÜNCELLENMESİ GEREKENLER):");
            sb.AppendLine("------------------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-6} | {1,-10} | {2,-26} | {3,-14} | {4,-14}",
                "Sıra", "Okul No", "Adı Soyadı", "Sistemdeki Sınıf", "Excel'deki Sınıf"));
            sb.AppendLine("------------------------------------------------------------------------------------------");
            int sNo = 1;
            foreach (var item in diffClassItems)
            {
                sb.AppendLine(string.Format("{0,-6} | {1,-10} | {2,-26} | {3,-14} | {4,-14}",
                    sNo++,
                    item.StudentNumber,
                    item.FullName,
                    item.DbClassroom,
                    item.Classroom));
            }
            sb.AppendLine();
        }

        sb.AppendLine("==========================================================================================");
        sb.AppendLine("ÖNERİLEN İŞLEMLER:");
        sb.AppendLine("1. 'Veritabanında Eksik' sekmesinden yeni öğrencileri tek tıkla sisteme aktarabilirsiniz.");
        sb.AppendLine("2. 'Listede Olmayan' sekmesinden nakil giden/ayrılan öğrencileri pasife alabilirsiniz.");
        sb.AppendLine("3. 'Uyumlu / Sınıf Kontrolü' sekmesinden sınıfı değişenlerin sınıflarını güncelleyebilirsiniz.");
        sb.AppendLine("==========================================================================================");

        string reportPath = Path.Combine(Path.GetTempPath(), $"Ogrenci_Karsilastirma_Raporu_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
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
        catch
        {
            MessageBox.Show($"Rapor oluşturuldu:\n{reportPath}", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        fullName = (fullName ?? "").Trim();
        if (string.IsNullOrEmpty(fullName)) return ("-", "-");

        int lastSpace = fullName.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            string first = fullName[..lastSpace].Trim();
            string last = fullName[(lastSpace + 1)..].Trim();
            return (first, last);
        }
        return (fullName, "-");
    }

    private class AuditItem
    {
        public Student? ExistingStudent { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public string DbClassroom { get; set; } = string.Empty;
        public string Grade { get; set; } = "9";
        public string FieldName { get; set; } = string.Empty;
        public string EducationType { get; set; } = "Örgün Eğitim";

        public string InternshipDays
        {
            get
            {
                bool isMesem = string.Equals(EducationType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                               (!string.IsNullOrWhiteSpace(Classroom) && Classroom.Contains("MESEM", StringComparison.OrdinalIgnoreCase));
                if (isMesem) return "Pazartesi,Salı,Çarşamba,Perşembe";
                if (Grade == "12" || (!string.IsNullOrWhiteSpace(Classroom) && Classroom.StartsWith("12"))) return "Pazartesi,Salı,Çarşamba";
                return string.Empty;
            }
        }
    }
}
