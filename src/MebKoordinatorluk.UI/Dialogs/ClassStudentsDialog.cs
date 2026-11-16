using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class ClassStudentsDialog : Form
{
    private readonly SchoolClass _schoolClass;
    private readonly List<Student> _students;
    private readonly AppSettings _settings;

    private DataGridView _grid = null!;
    private Label _lblTitle = null!;
    private Label _lblSubtitle = null!;
    private Button _btnPrint = null!;
    private Button _btnClose = null!;
    private Label _lblSummary = null!;

    // Dynamic Column Selection Checkboxes
    private CheckBox _chkColNumber = null!;
    private CheckBox _chkColTc = null!;
    private CheckBox _chkColName = null!;
    private CheckBox _chkColEnterprise = null!;
    private CheckBox _chkColDays = null!;
    private CheckBox _chkColPhone = null!;
    private CheckBox _chkColSignature = null!;

    public ClassStudentsDialog(SchoolClass schoolClass, List<Student> students, AppSettings? settings = null)
    {
        _schoolClass = schoolClass;
        _students = students.OrderBy(s => s.StudentNumber).ThenBy(s => s.FullName).ToList();
        _settings = settings ?? new AppSettings();

        InitializeComponent();
        PopulateGrid();
    }

    private void InitializeComponent()
    {
        Text = $"🏫 {_schoolClass.Name} Sınıfı Öğrenci Listesi";
        Size = new Size(980, 650);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(850, 520);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        _lblTitle = new Label
        {
            Text = $"🏫 {_schoolClass.Name} Sınıfı Öğrenci Listesi",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 10),
            AutoSize = true
        };

        string fieldStr = string.IsNullOrWhiteSpace(_schoolClass.FieldName) ? "Alan Belirtilmemiş" : _schoolClass.FieldName;
        _lblSubtitle = new Label
        {
            Text = $"Alanı: {fieldStr}  |  Kurum: {_schoolClass.InstitutionType}  |  Öğretim: {_schoolClass.EducationType}  |  Staj Günleri: {_schoolClass.InternshipDays}",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(22, 38),
            AutoSize = true
        };

        _btnPrint = new Button
        {
            Text = "🖨️ Yazdır / Çıktı Al",
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(37, 99, 235), // Blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnPrint.FlatAppearance.BorderSize = 0;
        _btnPrint.Click += BtnPrint_Click;

        _btnClose = new Button
        {
            Text = "✖ Kapat",
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };
        _btnClose.FlatAppearance.BorderSize = 0;

        void PositionHeaderButtons()
        {
            _btnClose.Location = new Point(pnlHeader.ClientSize.Width - 110, 16);
            _btnPrint.Location = new Point(_btnClose.Left - 170, 16);
        }
        pnlHeader.Resize += (s, e) => PositionHeaderButtons();
        PositionHeaderButtons();

        pnlHeader.Controls.AddRange([_lblTitle, _lblSubtitle, _btnPrint, _btnClose]);

        // Option Selection Panel (Sub Header)
        var pnlOptions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 8, 20, 8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var lblOptText = new Label
        {
            Text = "Yazdırılacak / Gösterilecek Alanlar:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Margin = new Padding(0, 3, 10, 0)
        };

        _chkColNumber = new CheckBox { Text = "Okul No", Checked = true, AutoSize = true, Margin = new Padding(0, 2, 12, 0) };
        _chkColTc = new CheckBox { Text = "TC Kimlik No", Checked = false, AutoSize = true, Margin = new Padding(0, 2, 12, 0) };
        _chkColName = new CheckBox { Text = "Adı Soyadı", Checked = true, Enabled = false, AutoSize = true, Margin = new Padding(0, 2, 12, 0) };
        _chkColEnterprise = new CheckBox { Text = "Staj İşletmesi", Checked = false, AutoSize = true, Margin = new Padding(0, 2, 12, 0) };
        _chkColDays = new CheckBox { Text = "Staj Günleri", Checked = false, AutoSize = true, Margin = new Padding(0, 2, 12, 0) };
        _chkColPhone = new CheckBox { Text = "Telefon", Checked = false, AutoSize = true, Margin = new Padding(0, 2, 12, 0) };
        _chkColSignature = new CheckBox { Text = "İmza Sütunu (Baskı)", Checked = true, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };

        EventHandler onCheckChanged = (s, e) => UpdateGridColumnsVisibility();
        _chkColNumber.CheckedChanged += onCheckChanged;
        _chkColTc.CheckedChanged += onCheckChanged;
        _chkColEnterprise.CheckedChanged += onCheckChanged;
        _chkColDays.CheckedChanged += onCheckChanged;
        _chkColPhone.CheckedChanged += onCheckChanged;

        pnlOptions.Controls.AddRange([lblOptText, _chkColNumber, _chkColTc, _chkColName, _chkColEnterprise, _chkColDays, _chkColPhone, _chkColSignature]);

        // Bottom Summary Panel
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        _lblSummary = new Label
        {
            Text = $"Toplam {_students.Count} öğrenci listeleniyor.",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            Location = new Point(20, 12),
            AutoSize = true
        };
        pnlBottom.Controls.Add(_lblSummary);

        // DataGrid Container
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 12, 20, 12)
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
            RowTemplate = { Height = 34 }
        };
        UITheme.ApplyModernGridStyle(_grid);

        SetupGridColumns();

        pnlGrid.Controls.Add(_grid);

        Controls.Add(pnlGrid);
        Controls.Add(pnlBottom);
        Controls.Add(pnlOptions);
        Controls.Add(pnlHeader);

        CancelButton = _btnClose;
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();
        _grid.Columns.Add("RowNo", "Sıra No");
        _grid.Columns["RowNo"].Width = 65;
        _grid.Columns["RowNo"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        _grid.Columns.Add("StudentNumber", "Okul No");
        _grid.Columns["StudentNumber"].Width = 90;
        _grid.Columns["StudentNumber"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        _grid.Columns.Add("TcKimlikNo", "TC Kimlik No");
        _grid.Columns["TcKimlikNo"].Width = 110;
        _grid.Columns["TcKimlikNo"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        _grid.Columns.Add("FullName", "Adı Soyadı");
        _grid.Columns["FullName"].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        _grid.Columns.Add("EnterpriseName", "Staj İşletmesi");

        _grid.Columns.Add("InternshipDays", "Staj Günleri");
        _grid.Columns["InternshipDays"].Width = 150;

        _grid.Columns.Add("Phone", "Telefon");
        _grid.Columns["Phone"].Width = 110;
    }

    private void UpdateGridColumnsVisibility()
    {
        if (_grid.Columns["StudentNumber"] != null) _grid.Columns["StudentNumber"].Visible = _chkColNumber.Checked;
        if (_grid.Columns["TcKimlikNo"] != null) _grid.Columns["TcKimlikNo"].Visible = _chkColTc.Checked;
        if (_grid.Columns["EnterpriseName"] != null) _grid.Columns["EnterpriseName"].Visible = _chkColEnterprise.Checked;
        if (_grid.Columns["InternshipDays"] != null) _grid.Columns["InternshipDays"].Visible = _chkColDays.Checked;
        if (_grid.Columns["Phone"] != null) _grid.Columns["Phone"].Visible = _chkColPhone.Checked;
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        int idx = 1;
        foreach (var s in _students)
        {
            string ent = string.IsNullOrWhiteSpace(s.EnterpriseName) ? "-" : s.EnterpriseName;
            string days = string.IsNullOrWhiteSpace(s.InternshipDays) ? "-" : s.InternshipDays;
            string phone = string.IsNullOrWhiteSpace(s.Phone) ? (string.IsNullOrWhiteSpace(s.ParentPhone) ? "-" : s.ParentPhone) : s.Phone;
            _grid.Rows.Add(idx++, s.StudentNumber, s.TcKimlikNo, s.FullName, ent, days, phone);
        }
        UpdateGridColumnsVisibility();
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        try
        {
            using var printDoc = new PrintDocument();
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.DocumentName = $"{_schoolClass.Name}_Ogrenci_Listesi";

            int currentPrintRowIndex = 0;

            printDoc.BeginPrint += (s, ev) =>
            {
                currentPrintRowIndex = 0;
            };

            printDoc.PrintPage += (s, ev) =>
            {
                var g = ev.Graphics;
                if (g == null) return;

                int leftMargin = ev.MarginBounds.Left - 20;
                int topMargin = ev.MarginBounds.Top - 30;
                int rightMargin = ev.MarginBounds.Right + 20;
                int printableWidth = rightMargin - leftMargin;

                using var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
                using var subtitleFont = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var bodyFont = new Font("Segoe UI", 8.5f);
                using var headerFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);

                int y = topMargin;

                // Header section
                string schoolName = string.IsNullOrWhiteSpace(_settings.SchoolName) ? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ" : _settings.SchoolName;
                string academicYear = string.IsNullOrWhiteSpace(_settings.AcademicYear) ? $"{DateTime.Now.Year}-{DateTime.Now.Year + 1}" : _settings.AcademicYear;

                g.DrawString(schoolName, titleFont, Brushes.Black, new RectangleF(leftMargin, y, printableWidth, 25), new StringFormat { Alignment = StringAlignment.Center });
                y += 24;

                g.DrawString($"{_schoolClass.Name} SINIFI ÖĞRENCİ LİSTESİ ({academicYear})", subtitleFont, Brushes.Black, new RectangleF(leftMargin, y, printableWidth, 20), new StringFormat { Alignment = StringAlignment.Center });
                y += 22;

                string metaStr = $"Alanı: {_schoolClass.FieldName ?? "-"}   |   Kurum Türü: {_schoolClass.InstitutionType}   |   Tarih: {DateTime.Now:dd.MM.yyyy}";
                g.DrawString(metaStr, bodyFont, Brushes.DimGray, new RectangleF(leftMargin, y, printableWidth, 18), new StringFormat { Alignment = StringAlignment.Center });
                y += 26;

                // Build Selected Columns dynamically based on CheckBoxes
                var activeCols = new List<(string Header, int RelWidth, Func<Student, string> Getter, bool Center)>();

                // Always include # Sıra No
                activeCols.Add(("#", 40, st => (currentPrintRowIndex + 1).ToString(), true));

                if (_chkColNumber.Checked) activeCols.Add(("Okul No", 75, st => st.StudentNumber ?? "", true));
                if (_chkColTc.Checked) activeCols.Add(("TC Kimlik No", 105, st => st.TcKimlikNo ?? "", true));
                if (_chkColName.Checked) activeCols.Add(("Adı Soyadı", 180, st => st.FullName, false));
                if (_chkColEnterprise.Checked) activeCols.Add(("Staj İşletmesi", 180, st => string.IsNullOrWhiteSpace(st.EnterpriseName) ? "-" : st.EnterpriseName, false));
                if (_chkColDays.Checked) activeCols.Add(("Staj Günleri", 120, st => string.IsNullOrWhiteSpace(st.InternshipDays) ? "-" : st.InternshipDays, false));
                if (_chkColPhone.Checked) activeCols.Add(("Telefon", 100, st => string.IsNullOrWhiteSpace(st.Phone) ? (st.ParentPhone ?? "-") : st.Phone, false));
                if (_chkColSignature.Checked) activeCols.Add(("İmza", 90, st => "", false));

                // Calculate actual widths scaled to printableWidth
                int totalRel = activeCols.Sum(c => c.RelWidth);
                int[] actualWidths = new int[activeCols.Count];
                int sumW = 0;
                for (int i = 0; i < activeCols.Count; i++)
                {
                    if (i == activeCols.Count - 1)
                    {
                        actualWidths[i] = printableWidth - sumW;
                    }
                    else
                    {
                        actualWidths[i] = (int)Math.Round((double)activeCols[i].RelWidth / totalRel * printableWidth);
                        sumW += actualWidths[i];
                    }
                }

                // Draw Table Header
                int x = leftMargin;
                using var headerBrush = new SolidBrush(Color.FromArgb(240, 240, 240));
                g.FillRectangle(headerBrush, leftMargin, y, printableWidth, 22);
                g.DrawRectangle(Pens.Black, leftMargin, y, printableWidth, 22);

                for (int i = 0; i < activeCols.Count; i++)
                {
                    g.DrawRectangle(Pens.Black, x, y, actualWidths[i], 22);
                    var sf = new StringFormat { Alignment = activeCols[i].Center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    g.DrawString(activeCols[i].Header, headerFont, Brushes.Black, new RectangleF(x + 4, y, actualWidths[i] - 8, 22), sf);
                    x += actualWidths[i];
                }
                y += 22;

                // Draw Rows
                while (currentPrintRowIndex < _students.Count)
                {
                    if (y + 24 > ev.MarginBounds.Bottom + 20)
                    {
                        ev.HasMorePages = true;
                        return;
                    }

                    var st = _students[currentPrintRowIndex];
                    x = leftMargin;

                    g.DrawRectangle(Pens.Black, leftMargin, y, printableWidth, 22);

                    for (int i = 0; i < activeCols.Count; i++)
                    {
                        g.DrawRectangle(Pens.Black, x, y, actualWidths[i], 22);
                        string val = activeCols[i].Getter(st);
                        var sf = new StringFormat { Alignment = activeCols[i].Center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Center };
                        g.DrawString(val, bodyFont, Brushes.Black, new RectangleF(x + 4, y, actualWidths[i] - 8, 22), sf);
                        x += actualWidths[i];
                    }

                    y += 22;
                    currentPrintRowIndex++;
                }

                // Signatures at bottom of last page
                y += 30;
                if (y + 60 <= ev.MarginBounds.Bottom + 40)
                {
                    int colW = printableWidth / 2;
                    g.DrawString("Koordinatör Öğretmen\r\nAdı Soyadı / İmza", bodyFont, Brushes.Black, new RectangleF(leftMargin, y, colW, 40), new StringFormat { Alignment = StringAlignment.Center });
                    string principalName = string.IsNullOrWhiteSpace(_settings.PrincipalName) ? "Okul Müdürü" : _settings.PrincipalName;
                    g.DrawString($"Okul Müdürü\r\n{principalName}", bodyFont, Brushes.Black, new RectangleF(leftMargin + colW, y, colW, 40), new StringFormat { Alignment = StringAlignment.Center });
                }

                ev.HasMorePages = false;
            };

            PrintPreviewHelper.ShowPreview(printDoc, $"{_schoolClass.Name} Sınıfı Öğrenci Listesi", this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yazdırma işlemi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
