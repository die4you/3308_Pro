using System.Data;
using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class ArchivePromptDialog : Form
{
    private readonly List<Student> _exitingStudents;
    private readonly IStudentRepository _studentRepo;
    private readonly string _periodText;

    private DataGridView _grid = null!;
    private CheckBox _chkSelectAll = null!;
    private Button _btnArchive = null!;
    private Button _btnSkip = null!;
    private Label _lblSummary = null!;

    public int ArchivedCount { get; private set; } = 0;

    public ArchivePromptDialog(List<Student> exitingStudents, IStudentRepository studentRepo, string periodText)
    {
        _exitingStudents = exitingStudents;
        _studentRepo = studentRepo;
        _periodText = periodText;

        InitializeComponent();
        PopulateGrid();
    }

    private void InitializeComponent()
    {
        Text = "🗄️ Ayrılan Öğrencileri Arşive Taşıma Önerisi";
        Size = new Size(820, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.White,
            Padding = new Padding(24, 12, 24, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "🗄️ Ayrılan Öğrencileri Arşive Taşıma Önerisi",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 12),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = $"{_periodText} döneminde sigorta çıkışı yapılmış {_exitingStudents.Count} öğrenci bulundu. Son primleri hesaplanmıştır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(24, 38),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle]);

        // Main Container
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 12, 24, 12)
        };

        // Notice Box
        var pnlNotice = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.FromArgb(239, 246, 255), // blue-50
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 10)
        };
        pnlNotice.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(191, 219, 254)); // blue-200
            e.Graphics.DrawRectangle(pen, 0, 0, pnlNotice.Width - 1, pnlNotice.Height - 1);
        };

        var lblNotice = new Label
        {
            Text = $"ℹ️ Arşive aktarılan öğrencilerin numaralarının sonuna '-P-{DateTime.Now.Year}' eklenir (örn: 105 ➔ 105-P-{DateTime.Now.Year}), durumları 'Pasif' yapılır ve orijinal numaraları yeni öğrenciler için boşa çıkar. İstendiğinde sol menüdeki 'Öğrenci Arşivi' sekmesinden görüntülenebilir veya aktife alınabilir.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(30, 58, 138), // blue-900
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlNotice.Controls.Add(lblNotice);

        // Grid Container
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0)
        };

        var pnlGridTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32
        };

        _chkSelectAll = new CheckBox
        {
            Text = "Tümünü Seç / Kaldır",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(4, 6),
            AutoSize = true,
            Checked = true
        };
        _chkSelectAll.CheckedChanged += (s, e) =>
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.Cells["colSelect"].Value = _chkSelectAll.Checked;
            }
            UpdateSelectionSummary();
        };

        _lblSummary = new Label
        {
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235),
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Right,
            Width = 250
        };

        pnlGridTop.Controls.AddRange([_chkSelectAll, _lblSummary]);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            EnableHeadersVisualStyles = false,
            Font = new Font("Segoe UI", 9f)
        };

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 4, 6, 4)
        };
        _grid.ColumnHeadersHeight = 34;

        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            SelectionBackColor = Color.FromArgb(224, 242, 254),
            SelectionForeColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(6, 2, 6, 2)
        };
        _grid.RowTemplate.Height = 30;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

        // Columns
        var colSelect = new DataGridViewCheckBoxColumn
        {
            Name = "colSelect",
            HeaderText = "Seç",
            Width = 45,
            Resizable = DataGridViewTriState.False
        };

        var colNumber = new DataGridViewTextBoxColumn
        {
            Name = "colNumber",
            HeaderText = "Okul No",
            Width = 90,
            ReadOnly = true
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "Öğrenci Adı Soyadı",
            Width = 200,
            ReadOnly = true
        };

        var colClass = new DataGridViewTextBoxColumn
        {
            Name = "colClass",
            HeaderText = "Sınıf / Şube",
            Width = 100,
            ReadOnly = true
        };

        var colExitDate = new DataGridViewTextBoxColumn
        {
            Name = "colExitDate",
            HeaderText = "SGK Çıkış Tarihi",
            Width = 115,
            ReadOnly = true
        };

        var colEnterprise = new DataGridViewTextBoxColumn
        {
            Name = "colEnterprise",
            HeaderText = "Staj Yaptığı İşletme",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        };

        _grid.Columns.AddRange([colSelect, colNumber, colName, colClass, colExitDate, colEnterprise]);

        _grid.CellValueChanged += (s, e) =>
        {
            if (e.ColumnIndex == _grid.Columns["colSelect"].Index)
            {
                UpdateSelectionSummary();
            }
        };
        _grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        pnlGridContainer.Controls.Add(_grid);
        pnlGridContainer.Controls.Add(pnlGridTop);

        pnlBody.Controls.Add(pnlGridContainer);
        pnlBody.Controls.Add(pnlNotice);

        // Footer Action Panel
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.White,
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        _btnArchive = new Button
        {
            Text = "✅ Seçilenleri Arşive Taşı (-P)",
            Size = new Size(220, 36),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlFooter.Width - 244, 10)
        };
        _btnArchive.FlatAppearance.BorderSize = 0;
        _btnArchive.Click += BtnArchive_Click;

        _btnSkip = new Button
        {
            Text = "Daha Sonra / Atla",
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlFooter.Width - 384, 10)
        };
        _btnSkip.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnSkip.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        pnlFooter.Resize += (s, e) =>
        {
            _btnArchive.Location = new Point(pnlFooter.Width - 244, 10);
            _btnSkip.Location = new Point(pnlFooter.Width - 384, 10);
        };

        pnlFooter.Controls.AddRange([_btnSkip, _btnArchive]);

        Controls.Add(pnlBody);
        Controls.Add(pnlHeader);
        Controls.Add(pnlFooter);
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        foreach (var s in _exitingStudents)
        {
            int rowIdx = _grid.Rows.Add(
                true, // Checked by default
                s.StudentNumber,
                s.FullName,
                s.Classroom,
                s.SgkExitDate.HasValue ? s.SgkExitDate.Value.ToString("dd.MM.yyyy") : "-",
                string.IsNullOrWhiteSpace(s.EnterpriseName) ? "-" : s.EnterpriseName
            );
            _grid.Rows[rowIdx].Tag = s;
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        int checkedCount = 0;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"].Value is true) checkedCount++;
        }

        _lblSummary.Text = $"{checkedCount} / {_grid.Rows.Count} öğrenci seçildi";
        _btnArchive.Enabled = checkedCount > 0;
    }

    private async void BtnArchive_Click(object? sender, EventArgs e)
    {
        var selectedStudents = new List<Student>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"].Value is true && row.Tag is Student s)
            {
                selectedStudents.Add(s);
            }
        }

        if (selectedStudents.Count == 0)
        {
            MessageBox.Show("Lütfen arşive taşınacak en az bir öğrenci seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Seçilen {selectedStudents.Count} öğrenci Arşiv'e taşınacaktır.\n\n" +
            $"• Öğrenci numaralarının sonuna '-P-{DateTime.Now.Year}' eklenecektir.\n" +
            "• Orijinal numaralar yeni öğrenciler için boşa çıkacaktır.\n" +
            "• Durumları 'Pasif' yapılacak ve aktif staj/yoklama listelerinden düşürülecektir.\n\n" +
            "İşlemi onaylıyor musunuz?",
            "Arşive Taşıma Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            _btnArchive.Enabled = false;
            _btnSkip.Enabled = false;

            foreach (var student in selectedStudents)
            {
                // Okul numarasının sonuna -P-{Yıl} ekle (eğer yoksa)
                if (!student.StudentNumber.Contains("-P", StringComparison.OrdinalIgnoreCase))
                {
                    student.StudentNumber = $"{student.StudentNumber.Trim()}-P-{DateTime.Now.Year}";
                }

                student.IsActive = false;
                await _studentRepo.UpdateAsync(student);
            }

            ArchivedCount = selectedStudents.Count;

            MessageBox.Show(
                $"Toplam {ArchivedCount} öğrenci başarıyla Öğrenci Arşivi'ne taşındı!\n\n" +
                "Numaraları serbest bırakıldı ve tüm aktif listelerden kaldırıldı.\n" +
                "İstediğiniz zaman sol menüdeki '🗄️ Öğrenci Arşivi' sekmesinden görüntüleyebilir veya gerekirse tekrar aktife alabilirsiniz.",
                "Arşivleme Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Arşive taşıma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnArchive.Enabled = true;
            _btnSkip.Enabled = true;
        }
    }
}
