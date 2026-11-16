using System.Data;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Views;

public class StudentArchiveView : UserControl
{
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly ISettingRepository _settingRepo;

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbClassFilter = null!;
    private Label _lblCountBadge = null!;
    private Button _btnRefresh = null!;
    private Button _btnExportExcel = null!;

    private List<Student> _allArchivedStudents = [];
    private List<SchoolClass> _classes = [];
    private bool _isLoading = false;

    public StudentArchiveView(
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        ISettingRepository settingRepo)
    {
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _settingRepo = settingRepo;

        InitializeComponent();
        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // 1. Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.White,
            Padding = new Padding(24, 12, 24, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblIcon = new Label
        {
            Text = "🗄️",
            Font = new Font("Segoe UI", 20f),
            Location = new Point(24, 14),
            AutoSize = true
        };

        var lblTitle = new Label
        {
            Text = "Öğrenci Arşivi (Ayrılan / Pasif Öğrenciler)",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(68, 14),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Stajı sona eren, ilişiği kesilen veya SGK çıkışı yapılarak arşivlenen öğrenciler. Orijinal numaraları '-P-YIL' ile serbest bırakılmıştır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(70, 40),
            AutoSize = true
        };

        // Header Buttons
        _btnExportExcel = new Button
        {
            Text = "📄 Excel'e Aktar",
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(71, 85, 105), // slate-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlHeader.Width - 250, 18)
        };
        _btnExportExcel.FlatAppearance.BorderSize = 0;
        _btnExportExcel.Click += (s, e) => ExportToExcel();

        _btnRefresh = new Button
        {
            Text = "🔄 Yenile",
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlHeader.Width - 110, 18)
        };
        _btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();

        pnlHeader.Resize += (s, e) =>
        {
            _btnRefresh.Location = new Point(pnlHeader.Width - 110, 18);
            _btnExportExcel.Location = new Point(pnlHeader.Width - 250, 18);
        };

        pnlHeader.Controls.AddRange([lblIcon, lblTitle, lblSubtitle, _btnExportExcel, _btnRefresh]);

        // 2. Filter Bar Panel
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.White,
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        var lblSearch = new Label
        {
            Text = "🔍 Ara:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(24, 18),
            AutoSize = true
        };

        _txtSearch = new TextBox
        {
            Location = new Point(75, 14),
            Width = 240,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "İsim, numara, TC veya işletme..."
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        var lblClass = new Label
        {
            Text = "Sınıf / Şube:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(335, 18),
            AutoSize = true
        };

        _cmbClassFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(420, 14),
            Width = 160,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cmbClassFilter.Items.Add("Tüm Sınıflar");
        _cmbClassFilter.SelectedIndex = 0;
        _cmbClassFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        _lblCountBadge = new Label
        {
            Text = "Toplam 0 arşiv kaydı",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138), // blue-900
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlFilter.Width - 260, 16),
            Size = new Size(236, 24)
        };

        pnlFilter.Resize += (s, e) =>
        {
            _lblCountBadge.Location = new Point(pnlFilter.Width - 260, 16);
        };

        pnlFilter.Controls.AddRange([lblSearch, _txtSearch, lblClass, _cmbClassFilter, _lblCountBadge]);

        // 3. DataGridView Container
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
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
            Padding = new Padding(8, 4, 8, 4)
        };
        _grid.ColumnHeadersHeight = 36;

        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            SelectionBackColor = Color.FromArgb(224, 242, 254),
            SelectionForeColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(8, 2, 8, 2)
        };
        _grid.RowTemplate.Height = 34;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

        // Columns
        var colNumber = new DataGridViewTextBoxColumn
        {
            Name = "colNumber",
            HeaderText = "Arşiv No",
            Width = 95,
            ReadOnly = true
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "Öğrenci Adı Soyadı",
            Width = 200,
            ReadOnly = true
        };

        var colTcNo = new DataGridViewTextBoxColumn
        {
            Name = "colTcNo",
            HeaderText = "T.C. Kimlik No",
            Width = 115,
            ReadOnly = true
        };

        var colClass = new DataGridViewTextBoxColumn
        {
            Name = "colClass",
            HeaderText = "Sınıf / Şube",
            Width = 100,
            ReadOnly = true
        };

        var colField = new DataGridViewTextBoxColumn
        {
            Name = "colField",
            HeaderText = "Alan / Dal",
            Width = 140,
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
            HeaderText = "Son Staj İşletmesi",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        };

        var colReactivate = new DataGridViewButtonColumn
        {
            Name = "colReactivate",
            HeaderText = "İşlem",
            Text = "↩️ Aktife Al",
            UseColumnTextForButtonValue = true,
            Width = 115,
            FlatStyle = FlatStyle.Flat
        };

        var colDelete = new DataGridViewButtonColumn
        {
            Name = "colDelete",
            HeaderText = "",
            Text = "🗑️ Sil",
            UseColumnTextForButtonValue = true,
            Width = 70,
            FlatStyle = FlatStyle.Flat,
            Visible = CurrentUserSession.IsAdmin
        };

        _grid.Columns.AddRange([colNumber, colName, colTcNo, colClass, colField, colExitDate, colEnterprise, colReactivate, colDelete]);

        _grid.CellContentClick += Grid_CellContentClick;

        pnlGrid.Controls.Add(_grid);

        Controls.Add(pnlGrid);
        Controls.Add(pnlFilter);
        Controls.Add(pnlHeader);
    }

    public async Task LoadDataAsync()
    {
        if (_isLoading) return;
        try
        {
            _isLoading = true;

            if (_grid.Columns.Contains("colDelete"))
            {
                _grid.Columns["colDelete"].Visible = CurrentUserSession.IsAdmin;
            }

            // Sınıfları yükle
            _classes = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();
            _cmbClassFilter.Items.Clear();
            _cmbClassFilter.Items.Add("Tüm Sınıflar");
            foreach (var c in _classes)
            {
                _cmbClassFilter.Items.Add(c.Name);
            }
            _cmbClassFilter.SelectedIndex = 0;

            // Arşivdeki öğrencileri yükle: Doğrudan GetArchivedStudentsAsync()
            var archived = await _studentRepo.GetArchivedStudentsAsync();
            _allArchivedStudents = archived
                .OrderBy(s => s.Classroom)
                .ThenBy(s => s.FullName)
                .ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Arşiv verileri yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var search = _txtSearch.Text.Trim();
        bool hasSearch = !string.IsNullOrWhiteSpace(search);

        string? selectedClass = _cmbClassFilter.SelectedIndex > 0 ? _cmbClassFilter.SelectedItem?.ToString() : null;

        bool Matches(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;
            return val.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   val.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        var filtered = _allArchivedStudents.Where(s =>
        {
            if (!string.IsNullOrWhiteSpace(selectedClass) && !string.Equals(s.Classroom, selectedClass, StringComparison.OrdinalIgnoreCase))
                return false;

            if (hasSearch)
            {
                return Matches(s.FullName) ||
                       Matches(s.StudentNumber) ||
                       Matches(s.TcKimlikNo) ||
                       Matches(s.Classroom) ||
                       Matches(s.FieldName) ||
                       Matches(s.EnterpriseName);
            }

            return true;
        }).ToList();

        _grid.Rows.Clear();
        foreach (var s in filtered)
        {
            int rowIdx = _grid.Rows.Add(
                s.StudentNumber,
                s.FullName,
                string.IsNullOrWhiteSpace(s.TcKimlikNo) ? "-" : s.TcKimlikNo,
                s.Classroom,
                string.IsNullOrWhiteSpace(s.FieldName) ? "-" : s.FieldName,
                s.SgkExitDate.HasValue ? s.SgkExitDate.Value.ToString("dd.MM.yyyy") : "-",
                string.IsNullOrWhiteSpace(s.EnterpriseName) ? "-" : s.EnterpriseName
            );

            _grid.Rows[rowIdx].Tag = s;

            // Renklendirme
            _grid.Rows[rowIdx].Cells["colNumber"].Style.ForeColor = Color.FromArgb(185, 28, 28); // red-700
            _grid.Rows[rowIdx].Cells["colNumber"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        _lblCountBadge.Text = $"Toplam {filtered.Count} arşiv kaydı";
    }

    private async void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;

        if (_grid.Rows[e.RowIndex].Tag is not Student student) return;

        if (e.ColumnIndex == _grid.Columns["colReactivate"].Index)
        {
            await ReactivateStudentAsync(student);
        }
        else if (_grid.Columns.Contains("colDelete") && e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            await DeleteStudentPermanentlyAsync(student);
        }
    }

    private async Task ReactivateStudentAsync(Student student)
    {
        // 1. Orijinal numarayı belirle (-P ekini temizle)
        string originalNumber = Regex.Replace(student.StudentNumber, @"(?i)(-p(-\d{4})?)+$", "").Trim();
        if (string.IsNullOrWhiteSpace(originalNumber))
        {
            originalNumber = "0";
        }

        // 2. Aktif öğrenciler arasında bu numara başkasına verilmiş mi kontrol et
        var allStudents = await _studentRepo.GetAllAsync();
        var conflictingActiveStudent = allStudents.FirstOrDefault(other =>
            other.IsActive &&
            other.Id != student.Id &&
            string.Equals(other.StudentNumber.Trim(), originalNumber, StringComparison.OrdinalIgnoreCase));

        string targetNumber = originalNumber;

        if (conflictingActiveStudent != null)
        {
            // Numara çakışması var! Kullanıcıdan yeni bir numara iste
            using var numDialog = new PromptNewStudentNumberDialog(
                student.FullName,
                originalNumber,
                conflictingActiveStudent.FullName,
                conflictingActiveStudent.Classroom,
                allStudents.Where(s => s.IsActive).Select(s => s.StudentNumber.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase));

            if (numDialog.ShowDialog(this) != DialogResult.OK)
            {
                // İptal edildi
                return;
            }

            targetNumber = numDialog.SelectedNumber;
        }
        else
        {
            // Çakışma yok, kullanıcıdan onay al
            var confirm = MessageBox.Show(
                $"{student.FullName} isimli öğrenciyi '{originalNumber}' okul numarasıyla tekrar aktif stajyer listelerine almak istiyor musunuz?\n\n" +
                "• Eski işletme ve koordinatör öğretmen bilgileri sıfırlanacaktır.\n" +
                "• Öğrencinin yeni staj atamasını Öğrenci Listesi ekranından yapabilirsiniz.",
                "Öğrenciyi Aktife Al",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;
        }

        try
        {
            // 3. Öğrenciyi aktife al ve işletme/öğretmen bilgilerini sıfırla
            student.StudentNumber = targetNumber;
            student.IsActive = true;
            student.EnterpriseId = null;
            student.EnterpriseName = null;
            student.TeacherId = null;
            student.TeacherName = null;
            student.DutyDays = string.Empty;

            await _studentRepo.UpdateAsync(student);

            MessageBox.Show(
                $"{student.FullName} başarıyla aktif öğrenci listelerine taşındı!\n\n" +
                $"Okul Numarası: {student.StudentNumber}\n" +
                "İşletme ve koordinatör öğretmen bilgileri sıfırlanmıştır. Yeni staj yerini 'Öğrenci Listesi' ekranından atayabilirsiniz.",
                "Öğrenci Aktifleştirildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Öğrenci aktife alınırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteStudentPermanentlyAsync(Student student)
    {
        if (!CurrentUserSession.IsAdmin) return;

        var confirm = MessageBox.Show(
            $"{student.FullName} ({student.StudentNumber}) arşivden ve sistemden kalıcı olarak silinecektir.\n\n" +
            "DİKKAT: Bu işlem geri alınamaz! Öğrencinin tüm kayıtları veritabanından tamamen silinecektir.\n\n" +
            "Silmek istediğinize emin misiniz?",
            "Kalıcı Silme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            await _studentRepo.DeletePermanentlyAsync(student.Id);
            MessageBox.Show($"{student.FullName} başarıyla silindi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Öğrenci silinirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportToExcel()
    {
        try
        {
            if (_allArchivedStudents.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak arşiv kaydı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Uyumlu CSV Dosyası (*.csv)|*.csv|Tüm Dosyalar (*.*)|*.*",
                FileName = $"Ogrenci_Arsivi_Listesi_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Öğrenci Arşivini Excel'e Kaydet"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("\uFEFFArşiv No;Öğrenci Adı Soyadı;T.C. Kimlik No;Sınıf;Alan / Dal;SGK Çıkış Tarihi;Son İşletmesi");

                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Tag is Student s)
                    {
                        string exitDate = s.SgkExitDate.HasValue ? s.SgkExitDate.Value.ToString("dd.MM.yyyy") : "";
                        sb.AppendLine($"\"{s.StudentNumber}\";\"{s.FullName}\";\"{s.TcKimlikNo}\";\"{s.Classroom}\";\"{s.FieldName}\";\"{exitDate}\";\"{s.EnterpriseName}\"");
                    }
                }

                System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show($"Öğrenci arşivi başarıyla kaydedildi:\n{sfd.FileName}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// Numara çakışması durumunda yeni numara isteme diyalogu
/// </summary>
public class PromptNewStudentNumberDialog : Form
{
    private readonly HashSet<string> _activeNumbers;
    private TextBox _txtNewNumber = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public string SelectedNumber { get; private set; } = string.Empty;

    public PromptNewStudentNumberDialog(
        string studentName,
        string oldNumber,
        string otherStudentName,
        string otherStudentClass,
        HashSet<string> activeNumbers)
    {
        _activeNumbers = activeNumbers;

        Text = "⚠️ Okul Numarası Çakışması";
        Size = new Size(480, 260);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        var lblMsg = new Label
        {
            Text = $"{studentName} isimli öğrencinin eski numarası ({oldNumber}) şu anda aktif olan '{otherStudentName}' ({otherStudentClass}) tarafından kullanılmaktadır.\n\nÖğrencinin aktife alınabilmesi için lütfen boşta olan yeni bir numara giriniz:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(24, 20),
            Size = new Size(420, 70)
        };

        var lblInput = new Label
        {
            Text = "Yeni Okul Numarası:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(24, 105),
            AutoSize = true
        };

        _txtNewNumber = new TextBox
        {
            Location = new Point(160, 102),
            Width = 180,
            Font = new Font("Segoe UI", 10f)
        };

        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        _btnSave = new Button
        {
            Text = "Aktife Al",
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(240, 9),
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            Text = "İptal",
            Size = new Size(80, 34),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(360, 9),
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        pnlBottom.Controls.AddRange([_btnSave, _btnCancel]);

        Controls.AddRange([lblMsg, lblInput, _txtNewNumber, pnlBottom]);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var input = _txtNewNumber.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show("Lütfen bir numara giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtNewNumber.Focus();
            return;
        }

        if (_activeNumbers.Contains(input))
        {
            MessageBox.Show($"Girdiğiniz numara ({input}) şu anda başka bir aktif öğrenci tarafından kullanılmaktadır. Lütfen farklı bir numara giriniz.", "Numara Kullanımda", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtNewNumber.Focus();
            return;
        }

        SelectedNumber = input;
        DialogResult = DialogResult.OK;
        Close();
    }
}
