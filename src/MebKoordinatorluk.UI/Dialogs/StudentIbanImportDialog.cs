using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class IbanMatchItem
{
    public int RowIndex { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string RawIban { get; set; } = string.Empty;
    public string CleanedIban { get; set; } = string.Empty;
    public Student? ExistingStudent { get; set; }
    public IbanMatchStatus Status { get; set; }

    public string SystemStudentName => ExistingStudent != null
        ? $"{ExistingStudent.FullName} ({ExistingStudent.Classroom})"
        : "-";

    public string ExistingIban => ExistingStudent?.Iban ?? "(Yok)";

    public string StatusText => Status switch
    {
        IbanMatchStatus.WillUpdate => ExistingStudent?.Iban == null || string.IsNullOrWhiteSpace(ExistingStudent.Iban)
            ? "➕ Yeni IBAN Eklenecek"
            : "🔄 IBAN Güncellenecek",
        IbanMatchStatus.AlreadySame => "✓ Zaten Aynı (Güncel)",
        IbanMatchStatus.NotFoundInDb => "❌ Sistemde Öğrenci Yok",
        IbanMatchStatus.InvalidIban => "⚠️ Geçersiz / Boş IBAN",
        _ => "-"
    };
}

public class StudentIbanImportDialog : Form
{
    private readonly IStudentRepository _studentRepo;

    private List<Student> _dbStudents = [];
    private List<ParsedIbanItem> _parsedItems = [];
    private List<IbanMatchItem> _matchItems = [];
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

    public StudentIbanImportDialog(IStudentRepository studentRepo, string? initialFilePath = null)
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
        Text = "💳 Excel'den Öğrenci IBAN Aktar (.xls / .xlsx / .csv)";
        Size = new Size(1060, 720);
        MinimumSize = new Size(880, 580);
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
            Text = "💳 Excel'den Öğrenci IBAN Numarası Aktarma",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 10),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Excel dosyasındaki Okul Numarasına göre eşleştirme yapılarak öğrencilerin IBAN banka hesap bilgileri sisteme aktarılır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(20, 34),
            AutoSize = true
        };

        var lblTemplateNote = new Label
        {
            Text = "💡 Şablon Formatı: 1. Sütun: Okul No | 2. Sütun: IBAN (Örn: TR120006200000012345678901)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(13, 148, 136), // teal-600
            Location = new Point(20, 56),
            AutoSize = true
        };

        _btnDownloadTemplate = new Button
        {
            Text = "📥 Örnek Şablon İndir",
            Size = new Size(165, 34),
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
            PlaceholderText = "Lütfen IBAN içeren Excel (.xlsx / .xls) veya CSV dosyasını seçiniz..."
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
        _lblInvalid = CreateBadgeLabel("🔴 Geçersiz/Boş: 0", Color.FromArgb(254, 226, 226), Color.FromArgb(185, 28, 28));

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
            Text = "💾 IBAN Numaralarını Aktar",
            Size = new Size(230, 36),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnApply.FlatAppearance.BorderSize = 0;
        _btnApply.Click += async (s, e) => await ApplyIbanImportAsync();

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
            FillWeight = 5,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colNo",
            HeaderText = "Okul No",
            Width = 80,
            FillWeight = 8,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStudent",
            HeaderText = "Sistemdeki Öğrenci & Sınıfı",
            FillWeight = 26
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colFileIban",
            HeaderText = "Dosyadaki IBAN",
            Width = 220,
            FillWeight = 24,
            DefaultCellStyle = { Font = new Font("Consolas", 9f, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleLeft }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colExistingIban",
            HeaderText = "Mevcut Sistem IBAN",
            Width = 200,
            FillWeight = 20,
            DefaultCellStyle = { Font = new Font("Consolas", 8.5f), ForeColor = Color.FromArgb(100, 116, 139) }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "Eşleşme Durumu",
            FillWeight = 20
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
            Title = "Örnek Öğrenci IBAN Şablonunu Kaydedin",
            Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx|CSV Dosyası (*.csv)|*.csv",
            FileName = "Ogrenci_IBAN_Sablonu.xlsx"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                if (sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    var csvContent = StudentIbanParser.GenerateSampleCsv();
                    File.WriteAllText(sfd.FileName, csvContent, new UTF8Encoding(true));
                }
                else
                {
                    var xlsxBytes = StudentIbanParser.GenerateSampleExcelXlsx();
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
            Title = "IBAN İçeren Excel veya CSV Dosyasını Seçin",
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
            var parseResult = StudentIbanParser.ParseFile(filePath);
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

        // Numaraya göre hızlı haritalama (direkt ve öndeki sıfırlar temizlenmiş)
        var dbMap = new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in _dbStudents)
        {
            var cleanNo = s.StudentNumber.Trim();
            dbMap[cleanNo] = s;
            var trimmedZero = cleanNo.TrimStart('0');
            if (!string.IsNullOrWhiteSpace(trimmedZero) && !dbMap.ContainsKey(trimmedZero))
            {
                dbMap[trimmedZero] = s;
            }
        }

        foreach (var parsed in _parsedItems)
        {
            var cleanNo = parsed.StudentNumber.Trim();
            var trimmedZero = cleanNo.TrimStart('0');

            Student? matched = null;
            if (dbMap.TryGetValue(cleanNo, out var directMatch))
            {
                matched = directMatch;
            }
            else if (!string.IsNullOrWhiteSpace(trimmedZero) && dbMap.TryGetValue(trimmedZero, out var zeroMatch))
            {
                matched = zeroMatch;
            }

            var item = new IbanMatchItem
            {
                RowIndex = parsed.RowIndex,
                StudentNumber = parsed.StudentNumber,
                RawIban = parsed.RawIban,
                CleanedIban = parsed.CleanedIban,
                ExistingStudent = matched
            };

            if (string.IsNullOrWhiteSpace(parsed.CleanedIban) || !StudentIbanParser.IsValidIban(parsed.CleanedIban))
            {
                item.Status = IbanMatchStatus.InvalidIban;
            }
            else if (matched == null)
            {
                item.Status = IbanMatchStatus.NotFoundInDb;
            }
            else
            {
                var existingClean = StudentIbanParser.NormalizeIban(matched.Iban);
                if (string.Equals(existingClean, parsed.CleanedIban, StringComparison.OrdinalIgnoreCase))
                {
                    item.Status = IbanMatchStatus.AlreadySame;
                }
                else
                {
                    item.Status = IbanMatchStatus.WillUpdate;
                }
            }

            _matchItems.Add(item);
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();

        int willUpdateCount = _matchItems.Count(m => m.Status == IbanMatchStatus.WillUpdate);
        int alreadySameCount = _matchItems.Count(m => m.Status == IbanMatchStatus.AlreadySame);
        int notFoundCount = _matchItems.Count(m => m.Status == IbanMatchStatus.NotFoundInDb);
        int invalidCount = _matchItems.Count(m => m.Status == IbanMatchStatus.InvalidIban);

        _lblTotalExcel.Text = $"📄 Toplam Okunan: {_matchItems.Count}";
        _lblWillUpdate.Text = $"🟢 Güncellenecek: {willUpdateCount}";
        _lblAlreadySame.Text = $"⚪ Zaten Güncel: {alreadySameCount}";
        _lblNotFound.Text = $"🟠 Sistemde Yok: {notFoundCount}";
        _lblInvalid.Text = $"🔴 Geçersiz/Boş: {invalidCount}";

        _btnApply.Enabled = willUpdateCount > 0;
        _btnOpenNotepad.Enabled = _matchItems.Count > 0;

        int idx = 1;
        foreach (var item in _matchItems)
        {
            var rowIdx = _grid.Rows.Add(
                idx++,
                item.StudentNumber,
                item.SystemStudentName,
                item.CleanedIban,
                item.ExistingIban,
                item.StatusText
            );

            var row = _grid.Rows[rowIdx];
            if (item.Status == IbanMatchStatus.WillUpdate)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244); // light green
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(22, 101, 52);
                row.Cells["colStatus"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
            else if (item.Status == IbanMatchStatus.AlreadySame)
            {
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(30, 64, 175); // blue
            }
            else if (item.Status == IbanMatchStatus.InvalidIban)
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

    private async Task ApplyIbanImportAsync()
    {
        var toUpdate = _matchItems.Where(m => m.Status == IbanMatchStatus.WillUpdate && m.ExistingStudent != null).ToList();
        if (toUpdate.Count == 0)
        {
            MessageBox.Show("Güncellenecek eşleşen öğrenci bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Toplam {toUpdate.Count} öğrencinin IBAN numarası sistemde güncellenecektir.\n\nİşlemi onaylıyor musunuz?",
            "IBAN Aktarım Onayı",
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
                student.Iban = item.CleanedIban;
                await _studentRepo.UpdateAsync(student);
                updatedCount++;
                item.Status = IbanMatchStatus.AlreadySame;
            }

            ImportedCount = updatedCount;
            PopulateGrid();

            var askNotepad = MessageBox.Show(
                $"İşlem Başarıyla Tamamlandı!\n\nToplam {updatedCount} öğrencinin IBAN numarası sisteme eklendi ve kaydedildi.\n\nDetaylı aktarım raporunu Not Defterinde görüntülemek ister misiniz?",
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
            MessageBox.Show($"IBAN numaraları kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            string tempFile = Path.Combine(Path.GetTempPath(), $"meb_iban_aktarim_raporu_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
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
        string divider = new('=', 105);
        string subDivider = new('-', 105);

        sb.AppendLine(divider);
        sb.AppendLine("                   T.C. MİLLÎ EĞİTİM BAKANLIĞI — KOORDİNATÖRLÜK SİSTEMİ");
        sb.AppendLine("                         ÖĞRENCİ IBAN NUMARASI AKTARIM RAPORU");
        sb.AppendLine(divider);
        sb.AppendLine($"Rapor Tarihi / Saat : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine($"Kaynak Dosya        : {Path.GetFileName(_selectedFilePath ?? "Bilinmiyor")}");
        sb.AppendLine($"Dosya Yolu          : {_selectedFilePath ?? "-"}");
        sb.AppendLine(subDivider);

        int willUpdateCount = _matchItems.Count(m => m.Status == IbanMatchStatus.WillUpdate);
        int alreadySameCount = _matchItems.Count(m => m.Status == IbanMatchStatus.AlreadySame);
        int notFoundCount = _matchItems.Count(m => m.Status == IbanMatchStatus.NotFoundInDb);
        int invalidCount = _matchItems.Count(m => m.Status == IbanMatchStatus.InvalidIban);

        sb.AppendLine($"Toplam Okunan Satır Sayısı  : {_matchItems.Count}");
        sb.AppendLine($"Aktarılan / Güncellenen     : {willUpdateCount}");
        sb.AppendLine($"Zaten Güncel (Aynı IBAN)   : {alreadySameCount}");
        sb.AppendLine($"Sistemde Bulunmayan         : {notFoundCount}");
        sb.AppendLine($"Geçersiz / Boş IBAN         : {invalidCount}");
        sb.AppendLine(divider);
        sb.AppendLine();

        // 1. EKLENEN / GÜNCELLENEN ÖĞRENCİLER
        var updatedList = _matchItems.Where(m => m.Status == IbanMatchStatus.WillUpdate).ToList();
        if (updatedList.Count > 0)
        {
            sb.AppendLine($"[1] IBAN NUMARASI AKTARILAN / GÜNCELLENEN ÖĞRENCİLER ({updatedList.Count} Öğrenci)");
            sb.AppendLine(subDivider);
            sb.AppendLine(string.Format("{0,-6} {1,-28} {2,-28} {3,-26} {4}", "NO", "YENİ IBAN", "ESKİ IBAN", "ADI SOYADI", "SINIF"));
            sb.AppendLine(subDivider);
            foreach (var item in updatedList)
            {
                var s = item.ExistingStudent!;
                sb.AppendLine(string.Format("{0,-6} {1,-28} {2,-28} {3,-26} {4}",
                    item.StudentNumber,
                    item.CleanedIban,
                    Truncate(item.ExistingIban, 26),
                    Truncate(s.FullName, 24),
                    Truncate(s.Classroom, 12)));
            }
            sb.AppendLine();
        }

        // 2. ZATEN GÜNCEL OLANLAR
        var sameList = _matchItems.Where(m => m.Status == IbanMatchStatus.AlreadySame).ToList();
        if (sameList.Count > 0)
        {
            sb.AppendLine($"[2] ZATEN GÜNCEL OLAN ÖĞRENCİLER (AYNI IBAN) ({sameList.Count} Öğrenci)");
            sb.AppendLine(subDivider);
            sb.AppendLine(string.Format("{0,-6} {1,-28} {2,-28} {3}", "NO", "IBAN NUMARASI", "ADI SOYADI", "SINIF"));
            sb.AppendLine(subDivider);
            foreach (var item in sameList)
            {
                var s = item.ExistingStudent;
                string fullName = s != null ? s.FullName : "-";
                string classroom = s != null ? s.Classroom : "-";
                sb.AppendLine(string.Format("{0,-6} {1,-28} {2,-28} {3}",
                    item.StudentNumber,
                    item.CleanedIban,
                    Truncate(fullName, 26),
                    Truncate(classroom, 12)));
            }
            sb.AppendLine();
        }

        // 3. SİSTEMDE BULUNAMAYANLAR
        var notFoundList = _matchItems.Where(m => m.Status == IbanMatchStatus.NotFoundInDb).ToList();
        if (notFoundList.Count > 0)
        {
            sb.AppendLine($"[3] SİSTEMDE BULUNAMAYAN OKUL NUMARALARI ({notFoundList.Count} Kayıt)");
            sb.AppendLine(subDivider);
            sb.AppendLine(string.Format("{0,-6} {1,-28} {2}", "NO", "DOSYADAKİ IBAN", "DURUM"));
            sb.AppendLine(subDivider);
            foreach (var item in notFoundList)
            {
                sb.AppendLine(string.Format("{0,-6} {1,-28} {2}",
                    item.StudentNumber,
                    item.CleanedIban,
                    "Bu numarada öğrenci sistemde kayıtlı değil."));
            }
            sb.AppendLine();
        }

        // 4. GEÇERSİZ / BOŞ IBAN
        var invalidList = _matchItems.Where(m => m.Status == IbanMatchStatus.InvalidIban).ToList();
        if (invalidList.Count > 0)
        {
            sb.AppendLine($"[4] GEÇERSİZ VEYA BOŞ IBAN İÇEREN SATIRLAR ({invalidList.Count} Kayıt)");
            sb.AppendLine(subDivider);
            sb.AppendLine(string.Format("{0,-6} {1,-28} {2}", "NO", "DOSYADAKİ DEĞER", "DURUM"));
            sb.AppendLine(subDivider);
            foreach (var item in invalidList)
            {
                sb.AppendLine(string.Format("{0,-6} {1,-28} {2}",
                    item.StudentNumber,
                    string.IsNullOrWhiteSpace(item.RawIban) ? "(Boş)" : item.RawIban,
                    "IBAN formatı geçersiz (TR + 24 rakam olmalı)."));
            }
            sb.AppendLine();
        }

        sb.AppendLine(divider);
        sb.AppendLine("Bu rapor T.C. Millî Eğitim Bakanlığı Koordinatörlük Yönetim Sistemi tarafından otomatik üretilmiştir.");
        sb.AppendLine(divider);

        return sb.ToString();
    }

    private static string Truncate(string? val, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(val)) return "-";
        return val.Length <= maxLen ? val : val[..(maxLen - 2)] + "..";
    }
}
