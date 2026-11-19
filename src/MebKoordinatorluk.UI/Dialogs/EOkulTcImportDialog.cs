using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class EOkulTcImportDialog : Form
{
    private readonly IStudentRepository _studentRepo;

    private List<Student> _dbStudents = [];
    private List<EOkulParsedTcStudent> _parsedStudents = [];
    private List<TcMatchItem> _matchItems = [];
    private string? _selectedFilePath;

    public int ImportedCount { get; private set; }

    // UI Controls
    private TextBox _txtFilePath = null!;
    private Button _btnBrowse = null!;
    private Label _lblTotalExcel = null!;
    private Label _lblWillUpdate = null!;
    private Label _lblAlreadySame = null!;
    private Label _lblNotFound = null!;
    private DataGridView _grid = null!;
    private Button _btnApply = null!;
    private Button _btnOpenNotepad = null!;
    private Button _btnClose = null!;

    public EOkulTcImportDialog(IStudentRepository studentRepo, string? initialFilePath = null)
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
        Text = "🆔 e-Okul'dan T.C. Kimlik No & Doğum Tarihi Aktar (.xls / .xlsx)";
        Size = new Size(1100, 720);
        MinimumSize = new Size(950, 580);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
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
            Text = "🆔 e-Okul'dan T.C. Kimlik Numarası & Doğum Tarihi Aktarma",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 8),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "e-Okul'dan indirilen öğrenci bilgi formlarından (Excel .xls / .xlsx) T.C. Kimlik Numaralarını ve Doğum Tarihlerini okul numarasına göre otomatik eşleştirip aktarır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(20, 32),
            AutoSize = true
        };

        var lblEokulNote = new Label
        {
            Text = "(e-Okul Öğrenci Bilgileri > YAZDIR ekranından 90 numaralı raporu veya sınıf listesini Excel/Veri olarak indirip seçiniz.)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38), // Koyu Kırmızı (Red-600)
            Location = new Point(20, 54),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, lblEokulNote]);

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
            Text = "📁 Excel Dosyası:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(12, 16),
            AutoSize = true
        };

        _txtFilePath = new TextBox
        {
            Location = new Point(125, 13),
            Width = 620,
            Font = new Font("Segoe UI", 9.5f),
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 250, 252),
            PlaceholderText = "Lütfen e-Okul raporu (.xls veya .xlsx) dosyasını seçiniz..."
        };

        _btnBrowse = new Button
        {
            Text = "📂 Dosya Seç...",
            Size = new Size(130, 30),
            Location = new Point(755, 12),
            BackColor = UITheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnBrowse.FlatAppearance.BorderSize = 0;
        _btnBrowse.Click += BtnBrowse_Click;

        pnlFile.Controls.AddRange([lblFileTitle, _txtFilePath, _btnBrowse]);

        // Responsive resizing for file row
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

        pnlStats.Controls.AddRange([_lblTotalExcel, _lblWillUpdate, _lblAlreadySame, _lblNotFound]);

        // Grid
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
            Text = "💾 T.C. No & Doğum Tarihlerini Aktar",
            Size = new Size(260, 36),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnApply.FlatAppearance.BorderSize = 0;
        _btnApply.Click += async (s, e) => await ApplyTcImportAsync();

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
        _btnClose.Click += (s, e) =>
        {
            if (ImportedCount > 0)
            {
                DialogResult = DialogResult.OK;
            }
            Close();
        };

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
            Width = 40,
            FillWeight = 4,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colNo",
            HeaderText = "Okul No",
            Width = 70,
            FillWeight = 8,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colExcelName",
            HeaderText = "Excel'deki Adı Soyadı",
            FillWeight = 20
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colSystemStudent",
            HeaderText = "Sistemdeki Öğrenci & Sınıfı",
            FillWeight = 22
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colTc",
            HeaderText = "T.C. Kimlik No",
            Width = 115,
            FillWeight = 13,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colBirth",
            HeaderText = "Doğum Tarihi",
            Width = 100,
            FillWeight = 11,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "Eşleşme Durumu",
            FillWeight = 22
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

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "e-Okul Rapor Dosyasını Seçin (.xls / .xlsx)",
            Filter = "Excel Dosyaları (*.xls;*.xlsx)|*.xls;*.xlsx|Tüm Dosyalar (*.*)|*.*",
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
            var parseResult = EOkulTcKimlikParser.ParseFile(filePath);
            if (!parseResult.Success)
            {
                MessageBox.Show(parseResult.ErrorMessage ?? "Dosya okunamadı.", "Ayrıştırma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _parsedStudents = parseResult.Students;
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

        // Hızlı arama için sistemdeki öğrencileri numaraya göre haritala (boşluksuz ve öndeki sıfırlar temizlenmiş)
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

        foreach (var parsed in _parsedStudents)
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

            var birthDt = EOkulClassListParser.ParseTurkishDate(parsed.BirthDate);

            var item = new TcMatchItem
            {
                StudentNumber = parsed.StudentNumber,
                ExcelName = parsed.FullName,
                TcKimlikNo = parsed.TcKimlikNo,
                ExcelBirthDate = parsed.BirthDate,
                ParsedBirthDate = birthDt,
                ExistingStudent = matched
            };

            if (matched == null)
            {
                item.Status = TcMatchStatus.NotFoundInDb;
            }
            else
            {
                bool tcDiff = !string.IsNullOrWhiteSpace(parsed.TcKimlikNo) &&
                              !string.Equals(matched.TcKimlikNo?.Trim(), parsed.TcKimlikNo.Trim(), StringComparison.OrdinalIgnoreCase);

                bool birthDiff = birthDt.HasValue &&
                                 (!matched.BirthDate.HasValue || matched.BirthDate.Value.Date != birthDt.Value.Date);

                if (tcDiff || birthDiff)
                {
                    item.Status = TcMatchStatus.WillUpdate;
                }
                else
                {
                    item.Status = TcMatchStatus.AlreadySame;
                }
            }

            _matchItems.Add(item);
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();

        int willUpdateCount = _matchItems.Count(m => m.Status == TcMatchStatus.WillUpdate);
        int alreadySameCount = _matchItems.Count(m => m.Status == TcMatchStatus.AlreadySame);
        int notFoundCount = _matchItems.Count(m => m.Status == TcMatchStatus.NotFoundInDb);

        _lblTotalExcel.Text = $"📄 Toplam Okunan: {_matchItems.Count}";
        _lblWillUpdate.Text = $"🟢 Güncellenecek: {willUpdateCount}";
        _lblAlreadySame.Text = $"⚪ Zaten Güncel: {alreadySameCount}";
        _lblNotFound.Text = $"🟠 Sistemde Yok: {notFoundCount}";

        _btnApply.Enabled = willUpdateCount > 0;
        _btnOpenNotepad.Enabled = _matchItems.Count > 0;

        int idx = 1;
        foreach (var item in _matchItems)
        {
            string birthDisplay = item.ParsedBirthDate.HasValue
                ? item.ParsedBirthDate.Value.ToString("dd.MM.yyyy")
                : (!string.IsNullOrWhiteSpace(item.ExcelBirthDate) ? item.ExcelBirthDate : "-");

            var rowIdx = _grid.Rows.Add(
                idx++,
                item.StudentNumber,
                string.IsNullOrWhiteSpace(item.ExcelName) ? "-" : item.ExcelName,
                item.SystemName,
                item.TcKimlikNo,
                birthDisplay,
                item.StatusText
            );

            var row = _grid.Rows[rowIdx];
            if (item.Status == TcMatchStatus.WillUpdate)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244); // light green
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(22, 101, 52);
                row.Cells["colStatus"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
            else if (item.Status == TcMatchStatus.AlreadySame)
            {
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(30, 64, 175); // blue
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 252, 232); // light yellow/orange
                row.Cells["colStatus"].Style.ForeColor = Color.FromArgb(180, 83, 9);
            }
        }
    }

    private async Task ApplyTcImportAsync()
    {
        var toUpdate = _matchItems.Where(m => m.Status == TcMatchStatus.WillUpdate && m.ExistingStudent != null).ToList();
        if (toUpdate.Count == 0)
        {
            MessageBox.Show("Güncellenecek eşleşen öğrenci bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Toplam {toUpdate.Count} öğrencinin T.C. Kimlik Numarası ve/veya Doğum Tarihi sistemde güncellenecektir.\n\nİşlemi onaylıyor musunuz?",
            "T.C. Kimlik No & Doğum Tarihi Aktarım Onayı",
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
                if (!string.IsNullOrWhiteSpace(item.TcKimlikNo))
                {
                    student.TcKimlikNo = item.TcKimlikNo;
                }
                if (item.ParsedBirthDate.HasValue)
                {
                    student.BirthDate = item.ParsedBirthDate.Value;
                }
                await _studentRepo.UpdateAsync(student);
                updatedCount++;
                item.Status = TcMatchStatus.AlreadySame;
            }

            ImportedCount = updatedCount;
            DialogResult = DialogResult.OK;
            PopulateGrid();

            var askNotepad = MessageBox.Show(
                $"İşlem Başarıyla Tamamlandı!\n\nToplam {updatedCount} öğrencinin T.C. Kimlik Numarası / Doğum Tarihi sisteme aktarıldı ve kaydedildi.\n\nDetaylı aktarım raporunu Not Defterinde görüntülemek ister misiniz?",
                "Aktarım Başarılı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (askNotepad == DialogResult.Yes)
            {
                OpenReportInNotepad();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Bilgiler kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnApply.Enabled = _matchItems.Any(m => m.Status == TcMatchStatus.WillUpdate && m.ExistingStudent != null);
            if (!_btnApply.Enabled && updatedCount > 0)
            {
                _btnApply.Text = "✅ Aktarıldı";
                _btnApply.BackColor = Color.FromArgb(203, 213, 225);
                _btnApply.ForeColor = Color.FromArgb(71, 85, 105);
            }
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
            string tempFile = Path.Combine(Path.GetTempPath(), $"meb_tc_aktarim_raporu_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
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
        sb.AppendLine("               E-OKUL T.C. KİMLİK NUMARASI & DOĞUM TARİHİ AKTARIM RAPORU");
        sb.AppendLine(divider);
        sb.AppendLine($"Rapor Tarihi / Saat : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine($"Kaynak Excel Dosyası: {Path.GetFileName(_selectedFilePath ?? "Bilinmiyor")}");
        sb.AppendLine($"Dosya Yolu          : {_selectedFilePath ?? "-"}");
        sb.AppendLine(subDivider);

        int willUpdateCount = _matchItems.Count(m => m.Status == TcMatchStatus.WillUpdate);
        int alreadySameCount = _matchItems.Count(m => m.Status == TcMatchStatus.AlreadySame);
        int notFoundCount = _matchItems.Count(m => m.Status == TcMatchStatus.NotFoundInDb);

        sb.AppendLine($"Toplam Okunan Kayıt Sayısı : {_matchItems.Count}");
        sb.AppendLine($"Eşleşen & Güncellenen      : {willUpdateCount}");
        sb.AppendLine($"Zaten Güncel               : {alreadySameCount}");
        sb.AppendLine($"Sistemde Bulunmayan        : {notFoundCount}");
        sb.AppendLine(divider);
        sb.AppendLine();

        // 1. EKLENEN / GÜNCELLENEN ÖĞRENCİLER
        var updatedList = _matchItems.Where(m => m.Status == TcMatchStatus.WillUpdate).ToList();
        if (updatedList.Count > 0)
        {
            sb.AppendLine($"[1] T.C. KİMLİK / DOĞUM TARİHİ AKTARILAN ÖĞRENCİLER ({updatedList.Count} Öğrenci)");
            sb.AppendLine(subDivider);
            sb.AppendLine(string.Format("{0,-6} {1,-14} {2,-12} {3,-26} {4,-14} {5}", "NO", "T.C. KİMLİK NO", "DOĞUM TRH.", "ADI SOYADI", "SINIF", "DURUM"));
            sb.AppendLine(subDivider);
            foreach (var item in updatedList)
            {
                var s = item.ExistingStudent!;
                string bStr = item.ParsedBirthDate.HasValue
                    ? item.ParsedBirthDate.Value.ToString("dd.MM.yyyy")
                    : (s.BirthDate.HasValue ? s.BirthDate.Value.ToString("dd.MM.yyyy") : "-");

                sb.AppendLine(string.Format("{0,-6} {1,-14} {2,-12} {3,-26} {4,-14} {5}",
                    item.StudentNumber,
                    item.TcKimlikNo,
                    bStr,
                    Truncate(s.FullName, 24),
                    Truncate(s.Classroom, 12),
                    "✓ Güncellendi"));
            }
            sb.AppendLine(subDivider);
            sb.AppendLine();
        }

        // 2. ZATEN GÜNCEL OLANLAR
        var sameList = _matchItems.Where(m => m.Status == TcMatchStatus.AlreadySame).ToList();
        if (sameList.Count > 0)
        {
            sb.AppendLine($"[2] ZATEN GÜNCEL BİLGİYE SAHİP OLAN ÖĞRENCİLER ({sameList.Count} Öğrenci)");
            sb.AppendLine(subDivider);
            sb.AppendLine(string.Format("{0,-6} {1,-14} {2,-12} {3,-26} {4,-14} {5}", "NO", "T.C. KİMLİK NO", "DOĞUM TRH.", "ADI SOYADI", "SINIF", "DURUM"));
            sb.AppendLine(subDivider);
            foreach (var item in sameList)
            {
                var name = item.ExistingStudent?.FullName ?? item.ExcelName;
                var cls = item.ExistingStudent?.Classroom ?? "-";
                string bStr = item.ParsedBirthDate.HasValue
                    ? item.ParsedBirthDate.Value.ToString("dd.MM.yyyy")
                    : (item.ExistingStudent?.BirthDate.HasValue == true ? item.ExistingStudent.BirthDate.Value.ToString("dd.MM.yyyy") : "-");

                sb.AppendLine(string.Format("{0,-6} {1,-14} {2,-12} {3,-26} {4,-14} {5}",
                    item.StudentNumber,
                    item.TcKimlikNo,
                    bStr,
                    Truncate(name, 24),
                    Truncate(cls, 12),
                    "⚪ Zaten Mevcut"));
            }
            sb.AppendLine(subDivider);
            sb.AppendLine();
        }

        // 3. SİSTEMDE BULUNMAYANLAR
        var notFoundList = _matchItems.Where(m => m.Status == TcMatchStatus.NotFoundInDb).ToList();
        if (notFoundList.Count > 0)
        {
            sb.AppendLine($"[3] SİSTEMDE KAYITLI OLMAYAN ÖĞRENCİLER ({notFoundList.Count} Öğrenci)");
            sb.AppendLine(subDivider);
            sb.AppendLine(string.Format("{0,-6} {1,-14} {2,-12} {3,-26} {4}", "NO", "T.C. KİMLİK NO", "DOĞUM TRH.", "EXCEL'DEKİ ADI SOYADI", "DURUM"));
            sb.AppendLine(subDivider);
            foreach (var item in notFoundList)
            {
                string bStr = item.ParsedBirthDate.HasValue
                    ? item.ParsedBirthDate.Value.ToString("dd.MM.yyyy")
                    : (!string.IsNullOrWhiteSpace(item.ExcelBirthDate) ? item.ExcelBirthDate : "-");

                sb.AppendLine(string.Format("{0,-6} {1,-14} {2,-12} {3,-26} {4}",
                    item.StudentNumber,
                    item.TcKimlikNo,
                    bStr,
                    Truncate(item.ExcelName, 24),
                    "🟠 Sistemde Yok"));
            }
            sb.AppendLine(subDivider);
            sb.AppendLine();
        }

        sb.AppendLine(divider);
        sb.AppendLine("Bu rapor Koordinatörlük Yönetim Sistemi e-Okul Aktarım Sihirbazı tarafından oluşturulmuştur.");
        sb.AppendLine(divider);

        return sb.ToString();
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLen ? text : text[..(maxLen - 2)] + "..";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (ImportedCount > 0)
        {
            DialogResult = DialogResult.OK;
        }
    }
}

public class TcMatchItem
{
    public string StudentNumber { get; set; } = string.Empty;
    public string ExcelName { get; set; } = string.Empty;
    public string TcKimlikNo { get; set; } = string.Empty;
    public string ExcelBirthDate { get; set; } = string.Empty;
    public DateTime? ParsedBirthDate { get; set; }
    public Student? ExistingStudent { get; set; }
    public string SystemName => ExistingStudent != null ? $"{ExistingStudent.FullName} ({ExistingStudent.Classroom})" : "-";
    public TcMatchStatus Status { get; set; }
    public string StatusText => Status switch
    {
        TcMatchStatus.WillUpdate => GetWillUpdateStatusText(),
        TcMatchStatus.AlreadySame => "⚪ Zaten Güncel",
        TcMatchStatus.NotFoundInDb => "🟠 Sistemde Bulunamadı",
        _ => "-"
    };

    private string GetWillUpdateStatusText()
    {
        bool tcChange = !string.IsNullOrWhiteSpace(TcKimlikNo) &&
                        !string.Equals(ExistingStudent?.TcKimlikNo?.Trim(), TcKimlikNo.Trim(), StringComparison.OrdinalIgnoreCase);

        bool birthChange = ParsedBirthDate.HasValue &&
                           (ExistingStudent?.BirthDate == null || ExistingStudent.BirthDate.Value.Date != ParsedBirthDate.Value.Date);

        if (tcChange && birthChange) return "🟢 T.C. No & Doğum Tarihi Güncellenecek";
        if (tcChange) return "🟢 T.C. Kimlik No Güncellenecek";
        if (birthChange) return "🟢 Doğum Tarihi Güncellenecek";
        return "🟢 Güncellenecek";
    }
}

public enum TcMatchStatus
{
    WillUpdate,
    AlreadySame,
    NotFoundInDb
}
