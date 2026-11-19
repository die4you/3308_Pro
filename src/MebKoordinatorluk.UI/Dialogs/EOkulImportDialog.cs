using System.Data;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class EOkulImportDialog : Form
{
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;

    // Controls
    private TextBox _txtFilePath = null!;
    private Button _btnBrowse = null!;
    private Label _lblSummaryStudents = null!;
    private Label _lblSummaryClasses = null!;
    private Label _lblSummaryFields = null!;
    private RadioButton _rbAppend = null!;
    private RadioButton _rbReplaceAll = null!;
    private CheckBox _chkCleanClassesAndFields = null!;
    private CheckBox _chkAutoCreateClassAndField = null!;
    private CheckBox _chkSelectAll = null!;
    private DataGridView _gridPreview = null!;
    private Label _lblStatus = null!;
    private Button _btnExecute = null!;
    private Button _btnCancel = null!;

    private EOkulParseResult? _currentResult;
    private readonly List<EOkulParsedStudent> _displayList = [];
    private HashSet<string> _existingStudentNumbers = new(StringComparer.OrdinalIgnoreCase);

    public int ImportedCount { get; private set; }
    public int UpdatedCount { get; private set; }

    public EOkulImportDialog(
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo)
    {
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;

        InitializeComponent();
        LoadExistingStudentNumbersAsync();
    }

    private void InitializeComponent()
    {
        Text = "📥 e-Okul Sınıf Listesinden Öğrenci Yükle (.xls / .xlsx)";
        Size = new Size(1060, 750);
        MinimumSize = new Size(820, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
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
            Text = "📥 e-Okul Sınıf Listesi İçe Aktarma",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 8),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "MEB e-Okul'dan indirilen sınıf listelerini (.xls / .xlsx) otomatik ayrıştırarak öğrenci, sınıf ve alan bilgilerini aktarır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(20, 32),
            AutoSize = true
        };

        var lblEokulNote = new Label
        {
            Text = "(e-Okul Öğrenci Bilgileri > YAZDIR ekranından OOG01001R020 Şube Listesi raporunu Excel olarak indiriniz.\nT.C. Kimlik No ve Doğum Tarihlerini aktarmak için 090 nolu raporu 'T.C. Kimlik No & Doğum Tarihi Aktar' menüsünden aktarabilirsiniz.)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38), // Koyu Kırmızı (Red-600)
            Location = new Point(20, 52),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, lblEokulNote]);

        // Main Container Panel
        var pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 15, 20, 12)
        };

        // File Selection Row (Responsive)
        var pnlFile = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(0, 4, 0, 4)
        };

        var lblFile = new Label
        {
            Text = "Dosya Seç:",
            Dock = DockStyle.Left,
            Width = 85,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary
        };

        _btnBrowse = new Button
        {
            Text = "📁 Dosya Seç...",
            Dock = DockStyle.Right,
            Width = 140
        };
        UITheme.ApplyPrimaryButton(_btnBrowse);
        _btnBrowse.Click += OnBrowseClicked;

        var pnlTxtWrap = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 2, 8, 2)
        };

        _txtFilePath = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9.5f)
        };
        pnlTxtWrap.Controls.Add(_txtFilePath);

        pnlFile.Controls.AddRange([pnlTxtWrap, _btnBrowse, lblFile]);

        // KPI Summary Cards Panel
        var pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 65,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 8)
        };

        var cardStudents = CreateStatCard("🎓 Toplam Öğrenci", "0", out _lblSummaryStudents, Color.FromArgb(37, 99, 235));
        var cardClasses = CreateStatCard("🏫 Tespit Edilen Sınıf", "0", out _lblSummaryClasses, Color.FromArgb(16, 185, 129));
        var cardFields = CreateStatCard("📚 Tespit Edilen Alan", "0", out _lblSummaryFields, Color.FromArgb(139, 92, 246));

        pnlCards.Controls.AddRange([cardStudents, cardClasses, cardFields]);

        // Options Box (Mode Selection & Auto Create)
        var gbOptions = new GroupBox
        {
            Text = "⚙️ Aktarım Seçenekleri",
            Dock = DockStyle.Top,
            Height = 110,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Padding = new Padding(12, 4, 12, 4)
        };

        var tblOptions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        tblOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        tblOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
        tblOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        tblOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        tblOptions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.4f));

        _rbAppend = new RadioButton
        {
            Text = "Mevcut öğrencileri koru, bu listedekileri ekle (Aynı numaralılar güncellenir)",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Dock = DockStyle.Fill,
            ForeColor = UITheme.TextPrimary,
            Cursor = Cursors.Hand
        };
        _rbAppend.CheckedChanged += (s, e) =>
        {
            if (_rbAppend.Checked) _chkCleanClassesAndFields.Checked = false;
            RefreshStatusAndGridRemarks();
        };

        _rbReplaceAll = new RadioButton
        {
            Text = "Mevcut TÜM öğrenci, sınıf ve alanları sil ve sıfırdan ekle (Temiz yükleme)",
            Checked = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = UITheme.Danger,
            Cursor = Cursors.Hand
        };
        _rbReplaceAll.CheckedChanged += (s, e) =>
        {
            if (_rbReplaceAll.Checked) _chkCleanClassesAndFields.Checked = true;
            RefreshStatusAndGridRemarks();
        };

        _chkCleanClassesAndFields = new CheckBox
        {
            Text = "Önceki sınıf ve alanları sil (Temiz liste oluştur)",
            Checked = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(190, 24, 93), // rose-700
            Cursor = Cursors.Hand
        };

        _chkAutoCreateClassAndField = new CheckBox
        {
            Text = "Dosyadaki sınıf ve alanları otomatik oluştur",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Dock = DockStyle.Fill,
            ForeColor = UITheme.TextPrimary,
            Cursor = Cursors.Hand
        };

        _chkSelectAll = new CheckBox
        {
            Text = "Tümünü Seç / Kaldır",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = UITheme.Primary,
            Cursor = Cursors.Hand
        };
        _chkSelectAll.CheckedChanged += OnSelectAllChanged;

        tblOptions.Controls.Add(_rbAppend, 0, 0);
        tblOptions.Controls.Add(_rbReplaceAll, 0, 1);
        tblOptions.Controls.Add(_chkCleanClassesAndFields, 1, 0);
        tblOptions.Controls.Add(_chkAutoCreateClassAndField, 1, 1);
        tblOptions.Controls.Add(_chkSelectAll, 1, 2);
        gbOptions.Controls.Add(tblOptions);

        // Preview Grid
        _gridPreview = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White
        };
        UITheme.ApplyGridStyle(_gridPreview);

        SetupGridColumns();

        // Bottom Controls Panel
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            BackColor = Color.White,
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border, 1.5f);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var pnlBottomActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };

        _btnExecute = new Button
        {
            Text = "📥 Verileri Aktar",
            Size = new Size(165, 40),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(12, 0, 0, 0)
        };
        _btnExecute.FlatAppearance.BorderSize = 0;
        _btnExecute.Click += OnExecuteClicked;

        _btnCancel = new Button
        {
            Text = "❌ İptal",
            Size = new Size(110, 40),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 0)
        };
        UITheme.ApplySecondaryButton(_btnCancel);
        _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlBottomActions.Controls.AddRange([_btnExecute, _btnCancel]);

        _lblStatus = new Label
        {
            Text = "Lütfen e-Okul Sınıf Listesi Excel dosyasını seçiniz (.xls veya .xlsx).",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = UITheme.TextSecondary,
            AutoEllipsis = true
        };

        pnlBottom.Controls.AddRange([_lblStatus, pnlBottomActions]);

        // Wrap Grid inside a styled container
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0)
        };
        pnlGridContainer.Controls.Add(_gridPreview);

        pnlMain.Controls.Add(pnlGridContainer);
        pnlMain.Controls.Add(gbOptions);
        pnlMain.Controls.Add(pnlCards);
        pnlMain.Controls.Add(pnlFile);

        Controls.Add(pnlMain);
        Controls.Add(pnlBottom);
        Controls.Add(pnlHeader);

        pnlBottom.BringToFront();
        pnlHeader.BringToFront();
        pnlMain.BringToFront();
    }

    private void SetupGridColumns()
    {
        _gridPreview.Columns.Clear();

        var colCheck = new DataGridViewCheckBoxColumn
        {
            Name = "ColSelected",
            HeaderText = "Seç",
            Width = 45,
            DataPropertyName = "IsSelected"
        };

        var colSeq = new DataGridViewTextBoxColumn
        {
            Name = "ColSeq",
            HeaderText = "Sıra",
            Width = 50,
            ReadOnly = true,
            DataPropertyName = "SequenceNumber"
        };

        var colNo = new DataGridViewTextBoxColumn
        {
            Name = "ColNo",
            HeaderText = "Okul No",
            Width = 85,
            ReadOnly = true,
            DataPropertyName = "StudentNumber"
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "ColName",
            HeaderText = "Adı Soyadı",
            Width = 180,
            ReadOnly = true,
            DataPropertyName = "FullName"
        };

        var colClass = new DataGridViewTextBoxColumn
        {
            Name = "ColClass",
            HeaderText = "Sınıfı",
            Width = 70,
            ReadOnly = true,
            DataPropertyName = "Classroom"
        };

        var colField = new DataGridViewTextBoxColumn
        {
            Name = "ColField",
            HeaderText = "Alanı / Dalı",
            Width = 160,
            ReadOnly = true,
            DataPropertyName = "FieldName"
        };

        var colEduType = new DataGridViewTextBoxColumn
        {
            Name = "ColEduType",
            HeaderText = "Öğretim Türü",
            Width = 110,
            ReadOnly = true,
            DataPropertyName = "EducationType"
        };

        var colGender = new DataGridViewTextBoxColumn
        {
            Name = "ColGender",
            HeaderText = "Cinsiyet",
            Width = 75,
            ReadOnly = true,
            DataPropertyName = "Gender"
        };

        var colBirth = new DataGridViewTextBoxColumn
        {
            Name = "ColBirth",
            HeaderText = "Doğum Tarihi",
            Width = 95,
            ReadOnly = true
        };

        var colDays = new DataGridViewTextBoxColumn
        {
            Name = "ColDays",
            HeaderText = "İşletme Günleri",
            Width = 145,
            ReadOnly = true,
            DataPropertyName = "InternshipDays"
        };

        var colStatus = new DataGridViewTextBoxColumn
        {
            Name = "ColStatus",
            HeaderText = "Durum / İşlem",
            Width = 140,
            ReadOnly = true
        };

        _gridPreview.Columns.AddRange([
            colCheck, colSeq, colNo, colName, colClass, colField, colEduType, colGender, colBirth, colDays, colStatus
        ]);

        _gridPreview.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _displayList.Count) return;
            var item = _displayList[e.RowIndex];

            if (_gridPreview.Columns[e.ColumnIndex].Name == "ColBirth")
            {
                e.Value = item.BirthDate.HasValue ? item.BirthDate.Value.ToString("dd.MM.yyyy") : "-";
                e.FormattingApplied = true;
            }
            else if (_gridPreview.Columns[e.ColumnIndex].Name == "ColDays")
            {
                if (string.IsNullOrWhiteSpace(item.InternshipDays))
                {
                    e.Value = "— (Staj Yok)";
                    e.CellStyle.ForeColor = UITheme.TextSecondary;
                }
                else
                {
                    e.Value = item.InternshipDays;
                    e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129); // emerald green
                }
                e.FormattingApplied = true;
            }
            else if (_gridPreview.Columns[e.ColumnIndex].Name == "ColStatus")
            {
                if (_rbReplaceAll.Checked)
                {
                    e.Value = "Yeni Eklenecek";
                    e.CellStyle.ForeColor = UITheme.Primary;
                }
                else
                {
                    bool exists = _existingStudentNumbers.Contains(item.StudentNumber);
                    if (exists)
                    {
                        e.Value = "🔄 Güncellenecek";
                        e.CellStyle.ForeColor = Color.FromArgb(217, 119, 6); // amber-600
                    }
                    else
                    {
                        e.Value = "➕ Yeni Eklenecek";
                        e.CellStyle.ForeColor = UITheme.Success;
                    }
                }
                e.FormattingApplied = true;
            }
        };

        _gridPreview.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex >= 0 && _gridPreview.Columns[e.ColumnIndex].Name == "ColSelected")
            {
                var val = _gridPreview.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                if (val is bool b && e.RowIndex < _displayList.Count)
                {
                    _displayList[e.RowIndex].IsSelected = b;
                    UpdateSelectedCountStatus();
                }
            }
        };
        _gridPreview.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_gridPreview.IsCurrentCellDirty && _gridPreview.CurrentCell is DataGridViewCheckBoxCell)
            {
                _gridPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    private static Panel CreateStatCard(string title, string value, out Label lblValue, Color accentColor)
    {
        var panel = new Panel
        {
            Width = 220,
            Height = 58,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 15, 0),
            Padding = new Padding(12, 8, 12, 8)
        };
        panel.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            using var brush = new SolidBrush(accentColor);
            e.Graphics.FillRectangle(brush, 0, 0, 4, panel.Height);
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(12, 6),
            AutoSize = true
        };

        lblValue = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(12, 25),
            AutoSize = true
        };

        panel.Controls.AddRange([lblTitle, lblValue]);
        return panel;
    }

    private async void LoadExistingStudentNumbersAsync()
    {
        try
        {
            var students = await _studentRepo.GetAllAsync();
            _existingStudentNumbers = new HashSet<string>(
                students.Where(s => s.IsActive && !string.IsNullOrWhiteSpace(s.StudentNumber))
                        .Select(s => s.StudentNumber.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Ignore failure on initial check
        }
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "e-Okul Sınıf Listesi Excel Dosyası Seçin",
            Filter = "Excel Dosyaları (*.xls;*.xlsx)|*.xls;*.xlsx|Tüm Dosyalar (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        _txtFilePath.Text = ofd.FileName;
        ProcessFile(ofd.FileName);
    }

    private void ProcessFile(string filePath)
    {
        Cursor = Cursors.WaitCursor;
        _lblStatus.Text = "Excel dosyası ayrıştırılıyor, lütfen bekleyiniz...";
        _lblStatus.ForeColor = UITheme.Primary;
        Application.DoEvents();

        try
        {
            _currentResult = EOkulClassListParser.ParseFile(filePath);

            if (!_currentResult.Success)
            {
                Cursor = Cursors.Default;
                _lblStatus.Text = "Hata: Dosya ayrıştırılamadı.";
                _lblStatus.ForeColor = UITheme.Danger;
                MessageBox.Show(
                    _currentResult.ErrorMessage ?? "Dosya içeriği ayrıştırılamadı.",
                    "Ayrıştırma Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            _displayList.Clear();
            _displayList.AddRange(_currentResult.AllStudents);

            _lblSummaryStudents.Text = _currentResult.TotalStudentsCount.ToString();
            _lblSummaryClasses.Text = _currentResult.TotalClassesCount.ToString();
            _lblSummaryFields.Text = _currentResult.TotalFieldsCount.ToString();

            _gridPreview.DataSource = null;
            _gridPreview.DataSource = _displayList;

            _btnExecute.Enabled = _displayList.Count > 0;
            _chkSelectAll.Checked = true;

            UpdateSelectedCountStatus();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Hata: {ex.Message}";
            _lblStatus.ForeColor = UITheme.Danger;
            MessageBox.Show($"Dosya okunurken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void OnSelectAllChanged(object? sender, EventArgs e)
    {
        bool state = _chkSelectAll.Checked;
        foreach (var s in _displayList)
        {
            s.IsSelected = state;
        }
        _gridPreview.Refresh();
        UpdateSelectedCountStatus();
    }

    private void RefreshStatusAndGridRemarks()
    {
        _gridPreview.Invalidate();
        UpdateSelectedCountStatus();
    }

    private void UpdateSelectedCountStatus()
    {
        int selected = _displayList.Count(s => s.IsSelected);
        int total = _displayList.Count;

        if (total == 0)
        {
            _lblStatus.Text = "Lütfen e-Okul Sınıf Listesi Excel dosyasını seçiniz (.xls veya .xlsx).";
            _lblStatus.ForeColor = UITheme.TextSecondary;
            return;
        }

        string modeText = _rbReplaceAll.Checked
            ? "⚠️ TÜM MEVCUT ÖĞRENCİLER SİLİNECEK"
            : "Mevcut öğrenciler korunacak";

        _lblStatus.Text = $"Seçili: {selected} / Toplam: {total} öğrenci. Mod: {modeText}";
        _lblStatus.ForeColor = _rbReplaceAll.Checked ? UITheme.Danger : UITheme.TextPrimary;
    }

    private async void OnExecuteClicked(object? sender, EventArgs e)
    {
        if (_displayList.Count == 0)
        {
            MessageBox.Show("Lütfen önce yukarıdaki '📁 Dosya Seç...' butonuna tıklayarak e-Okul Sınıf Listesi Excel dosyanızı (.xls veya .xlsx) seçiniz.", "Dosya Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedStudents = _displayList.Where(s => s.IsSelected).ToList();
        if (selectedStudents.Count == 0)
        {
            MessageBox.Show("Lütfen içe aktarmak için listeden en az bir öğrenci seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Kullanıcı tümünü silmeyi seçtiyse ek onay iste
        if (_rbReplaceAll.Checked || _chkCleanClassesAndFields.Checked)
        {
            string itemsToDelete = _rbReplaceAll.Checked
                ? "TÜM MEVCUT ÖĞRENCİLER, SINIFLAR VE ALANLAR"
                : "TÜM ÖNCEKİ SINIFLAR VE ALANLAR";

            var confirm = MessageBox.Show(
                $"DİKKAT: Veritabanında kayıtlı olan {itemsToDelete} kalıcı olarak silinecektir!\n\n" +
                $"Ardından dosyadaki {selectedStudents.Count} öğrenci ve bunlara ait sınıf/alanlar sisteme sıfırdan eklenecektir.\n\n" +
                "Bu işlem geri alınamaz! Devam etmek istediğinize emin misiniz?",
                "⚠️ Temiz Yükleme Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;
        }
        else
        {
            var confirm = MessageBox.Show(
                $"{selectedStudents.Count} öğrenci içe aktarılacaktır.\n\n" +
                "• Mevcut öğrenciler korunacak,\n" +
                "• Aynı okul numaralı öğrenciler güncellenecektir.\n\n" +
                "İşlemi başlatmak istiyor musunuz?",
                "Aktarım Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;
        }

        _btnExecute.Enabled = false;
        _btnCancel.Enabled = false;
        _btnBrowse.Enabled = false;
        Cursor = Cursors.WaitCursor;
        _lblStatus.Text = "Aktarım işlemi yapılıyor, lütfen bekleyiniz...";
        _lblStatus.ForeColor = UITheme.Primary;
        Application.DoEvents();

        try
        {
            int inserted = 0;
            int updated = 0;
            int createdClassCount = 0;
            int createdFieldCount = 0;
            int matchedClassCount = 0;
            int matchedFieldCount = 0;

            // 1. Gerekirse tüm mevcut öğrencileri sil
            if (_rbReplaceAll.Checked)
            {
                await _studentRepo.DeleteAllAsync();
                _existingStudentNumbers.Clear();
            }

            // Önceki sınıf ve alanları temizle (seçiliyse veya tümünü sil modundaysa)
            bool cleanClassesAndFields = _chkCleanClassesAndFields.Checked || _rbReplaceAll.Checked;
            if (cleanClassesAndFields)
            {
                await _classRepo.DeleteAllAsync();
                await _fieldRepo.DeleteAllAsync();
            }

            // 2. Mevcut Sınıf ve Alanları yükle
            var existingClasses = cleanClassesAndFields ? [] : (await _classRepo.GetAllAsync()).ToList();
            var existingFields = cleanClassesAndFields ? [] : (await _fieldRepo.GetAllAsync()).ToList();

            var classMap = new Dictionary<string, SchoolClass>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in existingClasses)
            {
                if (!string.IsNullOrWhiteSpace(c.Name) && !classMap.ContainsKey(c.Name))
                {
                    classMap[c.Name] = c;
                }
            }

            var fieldMap = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in existingFields)
            {
                if (!string.IsNullOrWhiteSpace(f.Name) && !fieldMap.ContainsKey(f.Name))
                {
                    fieldMap[f.Name] = f;
                }
            }

            // 3. Eksik Sınıf ve Alanları otomatik oluştur (seçenek işaretliyse veya temiz moddaysa)
            if (_chkAutoCreateClassAndField.Checked || cleanClassesAndFields)
            {
                // Önce Alanları kontrol et ve oluştur
                var requiredFieldNames = selectedStudents
                    .Select(s => s.FieldName)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var fName in requiredFieldNames)
                {
                    if (!fieldMap.ContainsKey(fName))
                    {
                        var newField = new Field
                        {
                            Name = fName,
                            Description = fName,
                            IsActive = true
                        };
                        var fid = await _fieldRepo.AddAsync(newField);
                        newField.Id = fid;
                        fieldMap[fName] = newField;
                        createdFieldCount++;
                    }
                    else
                    {
                        matchedFieldCount++;
                    }
                }

                // Sonra Sınıfları kontrol et ve oluştur
                var distinctClasses = selectedStudents
                    .GroupBy(s => s.Classroom, StringComparer.OrdinalIgnoreCase)
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key));

                foreach (var grp in distinctClasses)
                {
                    string className = grp.Key;
                    if (!classMap.ContainsKey(className))
                    {
                        var sample = grp.First();
                        fieldMap.TryGetValue(sample.FieldName, out var matchedField);

                        var newClass = new SchoolClass
                        {
                            Name = className,
                            FieldId = matchedField?.Id,
                            FieldName = sample.FieldName,
                            EducationType = sample.EducationType,
                            InstitutionType = sample.EducationType == "MESEM" ? "MESEM" : "Anadolu Meslek Programı",
                            InternshipDays = sample.InternshipDays,
                            IsActive = true
                        };
                        var cid = await _classRepo.AddAsync(newClass);
                        newClass.Id = cid;
                        classMap[className] = newClass;
                        createdClassCount++;
                    }
                    else
                    {
                        var existingClass = classMap[className];
                        var sample = grp.First();
                        if (existingClass != null && existingClass.InternshipDays != sample.InternshipDays)
                        {
                            existingClass.InternshipDays = sample.InternshipDays;
                            await _classRepo.UpdateAsync(existingClass);
                        }
                        matchedClassCount++;
                    }
                }
            }

            // 4. Mevcut öğrencileri haritala (Güncelleme modu için)
            Dictionary<string, Student> studentByNo = new(StringComparer.OrdinalIgnoreCase);
            if (!_rbReplaceAll.Checked)
            {
                var existingStudents = await _studentRepo.GetAllAsync();
                foreach (var st in existingStudents)
                {
                    if (!string.IsNullOrWhiteSpace(st.StudentNumber) && !studentByNo.ContainsKey(st.StudentNumber.Trim()))
                    {
                        studentByNo[st.StudentNumber.Trim()] = st;
                    }
                }
            }

            // 5. Öğrencileri kaydet / güncelle
            foreach (var item in selectedStudents)
            {
                classMap.TryGetValue(item.Classroom, out var matchedClass);
                fieldMap.TryGetValue(item.FieldName, out var matchedField);

                int? fieldId = matchedField?.Id ?? matchedClass?.FieldId;
                string? fieldName = !string.IsNullOrWhiteSpace(item.FieldName) ? item.FieldName : matchedClass?.FieldName;
                string studentDays = item.InternshipDays;

                if (!_rbReplaceAll.Checked && studentByNo.TryGetValue(item.StudentNumber.Trim(), out var existing))
                {
                    // Güncelle
                    existing.FirstName = item.FirstName;
                    existing.LastName = item.LastName;
                    existing.Gender = item.Gender;
                    if (item.BirthDate.HasValue) existing.BirthDate = item.BirthDate;

                    if (!string.IsNullOrWhiteSpace(item.Classroom))
                    {
                        existing.Classroom = item.Classroom;
                        existing.Grade = item.Grade;
                        if (matchedClass != null) existing.ClassId = matchedClass.Id;
                    }

                    if (fieldId.HasValue)
                    {
                        existing.FieldId = fieldId;
                    }
                    if (!string.IsNullOrWhiteSpace(fieldName))
                    {
                        existing.FieldName = fieldName;
                    }
                    if (!string.IsNullOrWhiteSpace(item.EducationType))
                    {
                        existing.EducationType = item.EducationType;
                    }

                    existing.InternshipDays = studentDays;
                    if (string.IsNullOrWhiteSpace(studentDays))
                    {
                        existing.DutyDays = string.Empty;
                    }

                    await _studentRepo.UpdateAsync(existing);
                    updated++;
                }
                else
                {
                    // Yeni Ekle
                    var newStudent = new Student
                    {
                        StudentNumber = item.StudentNumber,
                        FirstName = item.FirstName,
                        LastName = item.LastName,
                        Gender = item.Gender,
                        BirthDate = item.BirthDate,
                        ClassId = matchedClass?.Id,
                        Classroom = item.Classroom,
                        Grade = item.Grade,
                        FieldId = fieldId,
                        FieldName = fieldName,
                        DepartmentId = 1,
                        EducationType = item.EducationType,
                        InternshipDays = studentDays,
                        DutyDays = string.Empty,
                        IsActive = true
                    };

                    await _studentRepo.AddAsync(newStudent);
                    inserted++;
                }
            }

            ImportedCount = inserted;
            UpdatedCount = updated;

            Cursor = Cursors.Default;
            _lblStatus.Text = "Aktarım başarıyla tamamlandı!";
            _lblStatus.ForeColor = UITheme.Success;

            string classDetail = matchedClassCount > 0
                ? $"🏫 Sınıflar: {createdClassCount} yeni eklendi ({matchedClassCount} mevcut eşleşti)"
                : $"🏫 Eklenen Yeni Sınıf: {createdClassCount}";

            string fieldDetail = matchedFieldCount > 0
                ? $"📚 Alanlar: {createdFieldCount} yeni eklendi ({matchedFieldCount} mevcut eşleşti)"
                : $"📚 Eklenen Yeni Alan: {createdFieldCount}";

            MessageBox.Show(
                $"İçe aktarma işlemi başarıyla tamamlandı!\n\n" +
                $"➕ Yeni Eklenen Öğrenci: {inserted}\n" +
                $"🔄 Güncellenen Öğrenci: {updated}\n" +
                $"{classDetail}\n" +
                $"{fieldDetail}",
                "İşlem Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            _lblStatus.Text = $"Aktarım hatası: {ex.Message}";
            _lblStatus.ForeColor = UITheme.Danger;
            MessageBox.Show($"Aktarım sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnExecute.Enabled = true;
            _btnCancel.Enabled = true;
            _btnBrowse.Enabled = true;
        }
    }
}
