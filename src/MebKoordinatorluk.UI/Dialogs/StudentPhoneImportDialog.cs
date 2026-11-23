using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class PhoneMatchItem
{
    public int RowIndex { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string? TcKimlikNo { get; set; }
    public string? OptionalName { get; set; }

    public string RawPhone { get; set; } = string.Empty;
    public string CleanedPhone { get; set; } = string.Empty;

    public string RawParentName { get; set; } = string.Empty;
    public string CleanedParentName { get; set; } = string.Empty;

    public string RawParentPhone { get; set; } = string.Empty;
    public string CleanedParentPhone { get; set; } = string.Empty;

    public Student? ExistingStudent { get; set; }
    public PhoneMatchStatus Status { get; set; }

    public string SystemStudentName => ExistingStudent != null
        ? $"{ExistingStudent.FullName} ({ExistingStudent.Classroom})"
        : "-";

    public string ChangesSummary { get; set; } = string.Empty;

    public string StatusText => Status switch
    {
        PhoneMatchStatus.WillUpdate => "🔄 Bilgiler Güncellenecek",
        PhoneMatchStatus.AlreadySame => "✓ Zaten Güncel",
        PhoneMatchStatus.NotFoundInDb => "❌ Sistemde Öğrenci Yok",
        PhoneMatchStatus.NoDataProvided => "⚠️ Bilgi Yok (Boş)",
        PhoneMatchStatus.InvalidData => "⚠️ Geçersiz Numara",
        _ => "-"
    };
}

public class StudentPhoneImportDialog : Form
{
    private readonly IStudentRepository _studentRepo;

    private List<Student> _dbStudents = [];
    private List<ParsedPhoneItem> _parsedItems = [];
    private List<PhoneMatchItem> _matchItems = [];
    private string? _selectedFilePath;

    public int ImportedCount { get; private set; }

    // UI Controls
    private TextBox _txtFilePath = null!;
    private Button _btnBrowse = null!;
    private Button _btnDownloadTemplate = null!;
    private Label _lblTotalExcel = null!;
    private Label _lblWillUpdate = null!;
    private Label _lblAlreadySame = null!;
    private Label _lblNotFound = null!;
    private Label _lblInvalid = null!;
    private DataGridView _grid = null!;
    private Button _btnApply = null!;
    private Button _btnOpenNotepad = null!;
    private Button _btnClose = null!;

    public StudentPhoneImportDialog(IStudentRepository studentRepo, string? initialFilePath = null)
    {
        _studentRepo = studentRepo;
        _selectedFilePath = initialFilePath;

        InitializeComponent();
        Load += async (s, e) =>
        {
            await LoadDbStudentsAsync();
            if (!string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
            {
                _txtFilePath.Text = _selectedFilePath;
                ProcessFile(_selectedFilePath);
            }
        };
    }

    private void InitializeComponent()
    {
        Text = "📞 Excel'den Öğrenci & Veli Telefon Bilgisi Aktar (.xls / .xlsx / .csv)";
        Size = new Size(1120, 720);
        MinimumSize = new Size(920, 580);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "📞 Excel'den Öğrenci & Veli Telefon Bilgilerini Aktarma",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 10),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Excel dosyasındaki Okul Numarasına göre eşleştirme yapılarak öğrenci telefonu, veli adı ve veli telefonu sisteme aktarılır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(20, 34),
            AutoSize = true
        };

        var lblTemplateNote = new Label
        {
            Text = "💡 Şablon Formatı: Okul No | Öğrenci Telefonu | Veli Adı | Veli Telefonu (Örnek şablon indirerek doldurabilirsiniz)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(13, 148, 136), // teal-600
            Location = new Point(20, 56),
            AutoSize = true
        };

        _btnDownloadTemplate = new Button
        {
            Text = "📥 Örnek Şablon İndir",
            Size = new Size(170, 34),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnDownloadTemplate.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, lblTemplateNote, _btnDownloadTemplate]);

        pnlHeader.Resize += (s, e) =>
        {
            _btnDownloadTemplate.Location = new Point(pnlHeader.Width - _btnDownloadTemplate.Width - 20, 24);
        };

        // Main Container Panel
        var pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 12, 20, 12)
        };

        // File Selection Row
        var pnlFile = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 8)
        };
        pnlFile.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlFile.Width - 1, pnlFile.Height - 1);
        };

        var lblFileTitle = new Label
        {
            Text = "📁 Excel / CSV Dosyası:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(12, 16),
            AutoSize = true
        };

        _txtFilePath = new TextBox
        {
            Location = new Point(165, 13),
            Width = 620,
            Font = new Font("Segoe UI", 9.5f),
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 250, 252),
            PlaceholderText = "Lütfen telefon ve veli bilgilerini içeren Excel (.xlsx / .xls) veya CSV dosyasını seçiniz..."
        };

        _btnBrowse = new Button
        {
            Text = "📂 Dosya Seç...",
            Size = new Size(130, 30),
            Location = new Point(795, 12),
            BackColor = UITheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnBrowse.FlatAppearance.BorderSize = 0;
        _btnBrowse.Click += BtnBrowse_Click;

        pnlFile.Controls.AddRange([lblFileTitle, _txtFilePath, _btnBrowse]);

        pnlFile.Resize += (s, e) =>
        {
            int btnW = 135;
            int rightMargin = 12;
            _btnBrowse.Location = new Point(pnlFile.Width - btnW - rightMargin, 12);
            _txtFilePath.Width = Math.Max(200, _btnBrowse.Left - _txtFilePath.Left - 10);
        };

        // Stat Cards Bar
        var pnlStats = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 4),
            WrapContents = false
        };

        _lblTotalExcel = CreateBadgeLabel("📄 Toplam Okunan: 0", Color.FromArgb(241, 245, 249), Color.FromArgb(51, 65, 85));
        _lblWillUpdate = CreateBadgeLabel("🟢 Güncellenecek: 0", Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52));
        _lblAlreadySame = CreateBadgeLabel("⚪ Zaten Güncel: 0", Color.FromArgb(239, 246, 255), Color.FromArgb(30, 64, 175));
        _lblNotFound = CreateBadgeLabel("🟠 Sistemde Yok: 0", Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
        _lblInvalid = CreateBadgeLabel("🔴 Boş / Geçersiz: 0", Color.FromArgb(254, 226, 226), Color.FromArgb(185, 28, 28));

        pnlStats.Controls.AddRange([_lblTotalExcel, _lblWillUpdate, _lblAlreadySame, _lblNotFound, _lblInvalid]);

        // Grid Panel
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1)
        };
        pnlGrid.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlGrid.Width - 1, pnlGrid.Height - 1);
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
            RowTemplate = { Height = 36 }
        };
        UITheme.ApplyModernGridStyle(_grid);
        SetupGridColumns();
        pnlGrid.Controls.Add(_grid);

        // Bottom Action Bar
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        _btnOpenNotepad = new Button
        {
            Text = "📝 Not Defterinde Rapor Aç",
            Size = new Size(195, 36),
            Location = new Point(20, 12),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnOpenNotepad.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnOpenNotepad.Click += (s, e) => OpenReportInNotepad();

        _btnApply = new Button
        {
            Text = "💾 Telefon Bilgilerini Aktar",
            Size = new Size(240, 36),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnApply.FlatAppearance.BorderSize = 0;
        _btnApply.Click += async (s, e) => await ApplyPhoneImportAsync();

        _btnClose = new Button
        {
            Text = "Kapat",
            Size = new Size(95, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnClose.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnClose.Click += (s, e) => Close();

        pnlBottom.Controls.AddRange([_btnOpenNotepad, _btnApply, _btnClose]);

        pnlBottom.Resize += (s, e) =>
        {
            _btnClose.Location = new Point(pnlBottom.Width - _btnClose.Width - 20, 12);
            _btnApply.Location = new Point(_btnClose.Left - _btnApply.Width - 10, 12);
        };

        // Layout Assembly
        pnlMain.Controls.Add(pnlGrid);
        pnlMain.Controls.Add(pnlStats);
        pnlMain.Controls.Add(pnlFile);

        Controls.Add(pnlMain);
        Controls.Add(pnlBottom);
        Controls.Add(pnlHeader);
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colIdx",
            HeaderText = "#",
            Width = 45,
            FillWeight = 4,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colNo",
            HeaderText = "Okul No",
            Width = 75,
            FillWeight = 7,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStudent",
            HeaderText = "Sistemdeki Öğrenci & Sınıfı",
            Width = 170,
            FillWeight = 18
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colPhone",
            HeaderText = "Öğrenci Tel",
            Width = 125,
            FillWeight = 12,
            DefaultCellStyle = { Font = new Font("Consolas", 9f, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleLeft }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colParentName",
            HeaderText = "Veli Adı Soyadı",
            Width = 135,
            FillWeight = 13
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colParentPhone",
            HeaderText = "Veli Tel",
            Width = 125,
            FillWeight = 12,
            DefaultCellStyle = { Font = new Font("Consolas", 9f), Alignment = DataGridViewContentAlignment.MiddleLeft }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colChanges",
            HeaderText = "Aktarılacak Bilgiler",
            FillWeight = 20,
            DefaultCellStyle = { Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(71, 85, 105) }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "Eşleşme Durumu",
            Width = 145,
            FillWeight = 14
        });
    }

    private static Label CreateBadgeLabel(string text, Color backColor, Color foreColor)
    {
        return new Label
        {
            Text = text,
            BackColor = backColor,
            ForeColor = foreColor,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 10, 0),
            AutoSize = true
        };
    }

    private async Task LoadDbStudentsAsync()
    {
        try
        {
            _dbStudents = (await _studentRepo.GetAllAsync()).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sistemdeki öğrenci listesi yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Örnek Öğrenci & Veli Telefon Şablonunu Kaydedin",
            Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx|CSV Dosyası (*.csv)|*.csv",
            FileName = "Ogrenci_Veli_Telefon_Sablonu.xlsx"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                if (sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    var csvContent = StudentPhoneParser.GenerateSampleCsv();
                    File.WriteAllText(sfd.FileName, csvContent, new UTF8Encoding(true));
                }
                else
                {
                    var xlsxBytes = StudentPhoneParser.GenerateSampleExcelXlsx();
                    File.WriteAllBytes(sfd.FileName, xlsxBytes);
                }

                MessageBox.Show($"Örnek şablon başarıyla oluşturuldu:\n{sfd.FileName}", "Şablon Kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Telefon Bilgileri İçeren Excel veya CSV Dosyasını Seçin",
            Filter = "Excel ve CSV Dosyaları (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Tüm Dosyalar (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _selectedFilePath = ofd.FileName;
            _txtFilePath.Text = _selectedFilePath;
            ProcessFile(_selectedFilePath);
        }
    }

    private void ProcessFile(string filePath)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            var parseResult = StudentPhoneParser.ParseFile(filePath);
            if (!parseResult.Success)
            {
                MessageBox.Show(parseResult.ErrorMessage ?? "Dosya okunamadı.", "Ayrıştırma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _parsedItems = parseResult.Items;
            MatchStudents();
            PopulateGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dosya işlenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void MatchStudents()
    {
        _matchItems = [];

        // Numaraya ve TCKN'ye göre hızlı haritalama
        var dbByNo = new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);
        var dbByTc = new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in _dbStudents)
        {
            var cleanNo = s.StudentNumber.Trim();
            dbByNo[cleanNo] = s;
            var trimmedZero = cleanNo.TrimStart('0');
            if (!string.IsNullOrWhiteSpace(trimmedZero) && !dbByNo.ContainsKey(trimmedZero))
            {
                dbByNo[trimmedZero] = s;
            }

            if (!string.IsNullOrWhiteSpace(s.TcKimlikNo))
            {
                dbByTc[s.TcKimlikNo.Trim()] = s;
            }
        }

        foreach (var parsed in _parsedItems)
        {
            var cleanNo = parsed.StudentNumber.Trim();
            var trimmedZero = cleanNo.TrimStart('0');

            Student? matched = null;
            if (dbByNo.TryGetValue(cleanNo, out var directMatch))
            {
                matched = directMatch;
            }
            else if (!string.IsNullOrWhiteSpace(trimmedZero) && dbByNo.TryGetValue(trimmedZero, out var zeroMatch))
            {
                matched = zeroMatch;
            }
            else if (!string.IsNullOrWhiteSpace(parsed.TcKimlikNo) && dbByTc.TryGetValue(parsed.TcKimlikNo, out var tcMatch))
            {
                matched = tcMatch;
            }

            var item = new PhoneMatchItem
            {
                RowIndex = parsed.RowIndex,
                StudentNumber = parsed.StudentNumber,
                TcKimlikNo = parsed.TcKimlikNo,
                OptionalName = parsed.OptionalName,
                RawPhone = parsed.RawPhone,
                CleanedPhone = parsed.CleanedPhone,
                RawParentName = parsed.RawParentName,
                CleanedParentName = parsed.CleanedParentName,
                RawParentPhone = parsed.RawParentPhone,
                CleanedParentPhone = parsed.CleanedParentPhone,
                ExistingStudent = matched
            };

            if (matched == null)
            {
                item.Status = PhoneMatchStatus.NotFoundInDb;
                item.ChangesSummary = "Sistemde bu numara ile kayıtlı öğrenci bulunamadı.";
            }
            else if (!parsed.HasAnyData)
            {
                item.Status = PhoneMatchStatus.NoDataProvided;
                item.ChangesSummary = "Aktarılacak telefon veya veli verisi yok.";
            }
            else
            {
                var changes = new List<string>();

                // Öğrenci telefonu karşılaştır
                if (!string.IsNullOrWhiteSpace(parsed.CleanedPhone))
                {
                    var existingPhoneNorm = StudentPhoneParser.NormalizePhone(matched.Phone);
                    if (!string.Equals(existingPhoneNorm, parsed.CleanedPhone, StringComparison.OrdinalIgnoreCase))
                    {
                        var oldVal = string.IsNullOrEmpty(existingPhoneNorm) ? "(Yok)" : existingPhoneNorm;
                        changes.Add($"Öğr. Tel: {oldVal} ➔ {parsed.CleanedPhone}");
                    }
                }

                // Veli adı karşılaştır
                if (!string.IsNullOrWhiteSpace(parsed.CleanedParentName))
                {
                    var existingParent = matched.ParentName?.Trim() ?? "";
                    if (!string.Equals(existingParent, parsed.CleanedParentName, StringComparison.OrdinalIgnoreCase))
                    {
                        var oldVal = string.IsNullOrEmpty(existingParent) ? "(Yok)" : existingParent;
                        changes.Add($"Veli Adı: {oldVal} ➔ {parsed.CleanedParentName}");
                    }
                }

                // Veli telefonu karşılaştır
                if (!string.IsNullOrWhiteSpace(parsed.CleanedParentPhone))
                {
                    var existingParentPhoneNorm = StudentPhoneParser.NormalizePhone(matched.ParentPhone);
                    if (!string.Equals(existingParentPhoneNorm, parsed.CleanedParentPhone, StringComparison.OrdinalIgnoreCase))
                    {
                        var oldVal = string.IsNullOrEmpty(existingParentPhoneNorm) ? "(Yok)" : existingParentPhoneNorm;
                        changes.Add($"Veli Tel: {oldVal} ➔ {parsed.CleanedParentPhone}");
                    }
                }

                if (changes.Count > 0)
                {
                    item.Status = PhoneMatchStatus.WillUpdate;
                    item.ChangesSummary = string.Join(" | ", changes);
                }
                else
                {
                    item.Status = PhoneMatchStatus.AlreadySame;
                    item.ChangesSummary = "Mevcut sistem bilgileriyle birebir aynı.";
                }
            }

            _matchItems.Add(item);
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();

        int willUpdateCount = _matchItems.Count(m => m.Status == PhoneMatchStatus.WillUpdate);
        int alreadySameCount = _matchItems.Count(m => m.Status == PhoneMatchStatus.AlreadySame);
        int notFoundCount = _matchItems.Count(m => m.Status == PhoneMatchStatus.NotFoundInDb);
        int invalidCount = _matchItems.Count(m => m.Status is PhoneMatchStatus.NoDataProvided or PhoneMatchStatus.InvalidData);

        _lblTotalExcel.Text = $"📄 Toplam Okunan: {_matchItems.Count}";
        _lblWillUpdate.Text = $"🟢 Güncellenecek: {willUpdateCount}";
        _lblAlreadySame.Text = $"⚪ Zaten Güncel: {alreadySameCount}";
        _lblNotFound.Text = $"🟠 Sistemde Yok: {notFoundCount}";
        _lblInvalid.Text = $"🔴 Boş / Geçersiz: {invalidCount}";

        _btnApply.Enabled = willUpdateCount > 0;
        _btnOpenNotepad.Enabled = _matchItems.Count > 0;

        int idx = 1;
        foreach (var item in _matchItems)
        {
            var rowIdx = _grid.Rows.Add(
                idx++,
                item.StudentNumber,
                item.SystemStudentName,
                string.IsNullOrWhiteSpace(item.CleanedPhone) ? "-" : item.CleanedPhone,
                string.IsNullOrWhiteSpace(item.CleanedParentName) ? "-" : item.CleanedParentName,
                string.IsNullOrWhiteSpace(item.CleanedParentPhone) ? "-" : item.CleanedParentPhone,
                item.ChangesSummary,
                item.StatusText
            );

            var row = _grid.Rows[rowIdx];
            if (item.Status == PhoneMatchStatus.WillUpdate)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244); // light green
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(22, 101, 52);
                row.Cells["colStatus"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
            else if (item.Status == PhoneMatchStatus.AlreadySame)
            {
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(30, 64, 175); // blue
            }
            else if (item.Status is PhoneMatchStatus.NoDataProvided or PhoneMatchStatus.InvalidData)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242); // light red
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(185, 28, 28);
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 252, 232); // light yellow
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(180, 83, 9);
            }
        }
    }

    private async Task ApplyPhoneImportAsync()
    {
        var toUpdate = _matchItems.Where(m => m.Status == PhoneMatchStatus.WillUpdate && m.ExistingStudent != null).ToList();
        if (toUpdate.Count == 0)
        {
            MessageBox.Show("Güncellenecek eşleşen öğrenci bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Toplam {toUpdate.Count} öğrencinin telefon ve veli bilgileri sistemde güncellenecektir.\n\nİşlemi onaylıyor musunuz?",
            "Telefon Bilgisi Aktarım Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        Cursor = Cursors.WaitCursor;
        _btnApply.Enabled = false;

        int updatedCount = 0;
        try
        {
            foreach (var item in toUpdate)
            {
                var student = item.ExistingStudent!;

                if (!string.IsNullOrWhiteSpace(item.CleanedPhone))
                {
                    student.Phone = item.CleanedPhone;
                }

                if (!string.IsNullOrWhiteSpace(item.CleanedParentName))
                {
                    student.ParentName = item.CleanedParentName;
                }

                if (!string.IsNullOrWhiteSpace(item.CleanedParentPhone))
                {
                    student.ParentPhone = item.CleanedParentPhone;
                }

                await _studentRepo.UpdateAsync(student);
                updatedCount++;
                item.Status = PhoneMatchStatus.AlreadySame;
                item.ChangesSummary = "Başarıyla güncellendi.";
            }

            ImportedCount = updatedCount;
            PopulateGrid();

            var askNotepad = MessageBox.Show(
                $"İşlem Başarıyla Tamamlandı!\n\nToplam {updatedCount} öğrencinin telefon ve veli bilgileri sisteme aktarıldı ve kaydedildi.\n\nDetaylı aktarım raporunu Not Defterinde görüntülemek ister misiniz?",
                "Aktarım Başarılı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (askNotepad == DialogResult.Yes)
            {
                OpenReportInNotepad();
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Telefon bilgileri kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnApply.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private void OpenReportInNotepad()
    {
        if (_matchItems.Count == 0)
        {
            MessageBox.Show("Görüntülenecek rapor verisi bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            string reportContent = GenerateTextReport();
            string tempFile = Path.Combine(Path.GetTempPath(), $"meb_telefon_aktarim_raporu_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(tempFile, reportContent, new UTF8Encoding(true));

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{tempFile}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Not defteri açılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string GenerateTextReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("           3308 PRO - ÖĞRENCİ VE VELİ TELEFON AKTARIM RAPORU                    ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Tarih / Saat : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine($"Kaynak Dosya : {_selectedFilePath ?? "Bilinmiyor"}");
        sb.AppendLine($"Toplam Satır : {_matchItems.Count}");
        sb.AppendLine($"Güncellenen  : {_matchItems.Count(m => m.Status == PhoneMatchStatus.WillUpdate || (ImportedCount > 0 && m.ChangesSummary.Contains("Başarıyla güncellendi")))}");
        sb.AppendLine($"Zaten Güncel : {_matchItems.Count(m => m.Status == PhoneMatchStatus.AlreadySame && !m.ChangesSummary.Contains("Başarıyla güncellendi"))}");
        sb.AppendLine($"Sistemde Yok : {_matchItems.Count(m => m.Status == PhoneMatchStatus.NotFoundInDb)}");
        sb.AppendLine($"Boş/Geçersiz : {_matchItems.Count(m => m.Status is PhoneMatchStatus.NoDataProvided or PhoneMatchStatus.InvalidData)}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine();

        sb.AppendLine("1. DETAYLI LİSTE:");
        sb.AppendLine(string.Format("{0,-6} | {1,-8} | {2,-25} | {3,-15} | {4,-18} | {5,-15} | {6}",
            "Sıra", "Okul No", "Sistemdeki Öğrenci", "Öğrenci Tel", "Veli Adı", "Veli Tel", "Durum / Değişiklik"));
        sb.AppendLine(new string('-', 120));

        int idx = 1;
        foreach (var item in _matchItems)
        {
            var studentName = item.ExistingStudent != null ? item.ExistingStudent.FullName : "-";
            if (studentName.Length > 24) studentName = studentName[..24];

            var parentName = string.IsNullOrWhiteSpace(item.CleanedParentName) ? "-" : item.CleanedParentName;
            if (parentName.Length > 17) parentName = parentName[..17];

            var phone = string.IsNullOrWhiteSpace(item.CleanedPhone) ? "-" : item.CleanedPhone;
            var parentPhone = string.IsNullOrWhiteSpace(item.CleanedParentPhone) ? "-" : item.CleanedParentPhone;

            sb.AppendLine(string.Format("{0,-6} | {1,-8} | {2,-25} | {3,-15} | {4,-18} | {5,-15} | {6}",
                idx++,
                item.StudentNumber,
                studentName,
                phone,
                parentName,
                parentPhone,
                $"{item.StatusText} ({item.ChangesSummary})"));
        }

        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("Rapor Sonu - 3308 Pro Koordinatörlük Sistemi");
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }
}
