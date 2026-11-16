using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class TeachersView : UserControl
{
    private readonly ITeacherRepository _teacherRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly IStudentRepository? _studentRepo;
    private readonly IEnterpriseRepository? _enterpriseRepo;

    private List<Teacher> _allTeachers = [];
    private List<Student> _allStudents = [];
    private List<Enterprise> _allEnterprises = [];
    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbStatusFilter = null!;

    public TeachersView(
        ITeacherRepository teacherRepo,
        IFieldRepository fieldRepo,
        IStudentRepository? studentRepo = null,
        IEnterpriseRepository? enterpriseRepo = null)
    {
        _teacherRepo = teacherRepo;
        _fieldRepo = fieldRepo;
        _studentRepo = studentRepo;
        _enterpriseRepo = enterpriseRepo;

        InitializeComponent();
        _ = LoadTeachersAsync();
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
            Text = "👨‍🏫 Koordinatör Öğretmen Yönetimi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Koordinatör öğretmenler ve haftalık görev alabilecekleri uygun günler",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        // Header Action Buttons
        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 460,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnAddTeacher = new Button
        {
            Text = "➕ Yeni Öğretmen Ekle",
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnAddTeacher.FlatAppearance.BorderSize = 0;
        btnAddTeacher.Click += (s, e) => OpenTeacherDialog(null);

        var btnUploadExcel = new Button
        {
            Text = "📊 Excel'den Yükle",
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(5, 150, 105), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnUploadExcel.FlatAppearance.BorderSize = 0;
        btnUploadExcel.Click += BtnUploadExcel_Click;

        var btnDownloadTemplate = new Button
        {
            Text = "📥 Şablon İndir",
            Size = new Size(110, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnDownloadTemplate.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

        pnlTopButtons.Controls.AddRange([btnAddTeacher, btnUploadExcel, btnDownloadTemplate]);

        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
        {
            btnAddTeacher.Visible = false;
            btnUploadExcel.Visible = false;
        }

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Search and Notes Bar
        var pnlSearch = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlSearch.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlSearch.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlSearch.Height - 1, pnlSearch.Width, pnlSearch.Height - 1);
        };

        _txtSearch = new TextBox
        {
            PlaceholderText = "🔍 Öğretmen veya branş ara...",
            Location = new Point(24, 12),
            Width = 240,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _cmbStatusFilter = new ComboBox
        {
            Location = new Point(275, 12),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbStatusFilter.Items.AddRange(["Tüm Öğretmenler", "Sadece Aktifler", "Sadece Pasifler"]);
        _cmbStatusFilter.SelectedIndex = 0;
        _cmbStatusFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        var lblMebNote = new Label
        {
            Text = "* MEB standartlarına göre koordinatörlük günleri haftalık boş güne göre seçilmelidir.",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            AutoSize = true,
            Location = new Point(440, 16)
        };

        pnlSearch.Controls.AddRange([_txtSearch, _cmbStatusFilter, lblMebNote]);

        // Grid Container
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 16, 24, 16)
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
            RowTemplate = { Height = 40 }
        };
        UITheme.ApplyModernGridStyle(_grid);

        SetupGridColumns();
        _grid.CellContentClick += Grid_CellContentClick;

        pnlGrid.Controls.Add(_grid);

        Controls.Add(pnlGrid);
        Controls.Add(pnlSearch);
        Controls.Add(pnlHeader);
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();

        var colIdx = new DataGridViewTextBoxColumn
        {
            Name = "colIdx",
            HeaderText = "#",
            Width = 40,
            FillWeight = 5,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "Öğretmen Adı Soyadı",
            FillWeight = 26,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colBranch = new DataGridViewTextBoxColumn
        {
            Name = "colBranch",
            HeaderText = "Alanı / Branşı",
            FillWeight = 22
        };

        var colDays = new DataGridViewTextBoxColumn
        {
            Name = "colDays",
            HeaderText = "Koordinatörlük Günleri",
            FillWeight = 24
        };

        var colHours = new DataGridViewTextBoxColumn
        {
            Name = "colHours",
            HeaderText = "Aldığı / Azami",
            FillWeight = 14,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colStatus = new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "Durum",
            FillWeight = 11,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colAction = new DataGridViewButtonColumn
        {
            Name = "colAction",
            HeaderText = "İşlemler",
            Text = "✏️ Düzenle",
            UseColumnTextForButtonValue = true,
            FillWeight = 10
        };

        var colDelete = new DataGridViewButtonColumn
        {
            Name = "colDelete",
            HeaderText = "",
            Text = "🗑️ Sil",
            UseColumnTextForButtonValue = true,
            FillWeight = 8
        };

        if (MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
        {
            _grid.Columns.AddRange([colIdx, colName, colBranch, colDays, colHours, colStatus, colAction, colDelete]);
        }
        else
        {
            _grid.Columns.AddRange([colIdx, colName, colBranch, colDays, colHours, colStatus]);
        }
    }

    public async Task LoadTeachersAsync()
    {
        _allTeachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();
        if (_studentRepo != null) _allStudents = (await _studentRepo.GetAllAsync()).Where(s => s.IsActive).ToList();
        if (_enterpriseRepo != null) _allEnterprises = (await _enterpriseRepo.GetAllAsync()).Where(e => e.IsActive).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        int statusFilter = _cmbStatusFilter?.SelectedIndex ?? 0; // 0: Tümü, 1: Aktifler, 2: Pasifler

        var filtered = _allTeachers.Where(t =>
        {
            if (statusFilter == 1 && !t.IsActive) return false;
            if (statusFilter == 2 && t.IsActive) return false;

            if (string.IsNullOrWhiteSpace(q)) return true;
            return t.FullName.ToLowerInvariant().Contains(q) ||
                   (t.Branch ?? "").ToLowerInvariant().Contains(q) ||
                   (t.AvailableDays ?? "").ToLowerInvariant().Contains(q);
        }).ToList();

        _grid.Rows.Clear();
        int idx = 1;
        foreach (var t in filtered)
        {
            int coordHours = t.CoordinatorFeeHours;
            int maxHours = t.MaxWeeklyHours > 0 ? t.MaxWeeklyHours : 16;
            string hoursStr = coordHours == 0
                ? $"0 / {maxHours} Saat (Görev Almaz)"
                : $"{coordHours} / {maxHours} Saat";

            var rowIndex = _grid.Rows.Add(
                idx++,
                t.FullName,
                t.Branch ?? "-",
                t.AvailableDays ?? "Belirtilmemiş",
                hoursStr,
                t.IsActive ? "Aktif" : "Pasif"
            );

            _grid.Rows[rowIndex].Tag = t;
            _grid.Rows[rowIndex].Cells["colStatus"].Style.ForeColor = t.IsActive ? Color.FromArgb(22, 101, 52) : Color.FromArgb(185, 28, 28);
            _grid.Rows[rowIndex].Cells["colStatus"].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            // Renklendirme ve Bilgilendirici İpuçları (Öneri B - Ders Programı Aldığı / Azami)
            double assignedEntHours = CalculateTeacherAssignedHours(t);
            string entHoursInfo = assignedEntHours > 0
                ? $" • İşletmelerden fiili yük: {assignedEntHours:0.#} Saat"
                : " • Henüz işletme yükü oluşmadı (0 Saat)";

            var cellHours = _grid.Rows[rowIndex].Cells["colHours"];
            if (coordHours == 0)
            {
                cellHours.Style.ForeColor = Color.FromArgb(100, 116, 139); // slate-500 belirgin gri
                cellHours.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                cellHours.ToolTipText = $"Bu öğretmen koordinatörlük görevi almamaktadır (0 Saat).{entHoursInfo}";
            }
            else if (coordHours > maxHours)
            {
                cellHours.Style.ForeColor = Color.FromArgb(220, 38, 38); // red-600 aşım uyarısı
                cellHours.Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                cellHours.ToolTipText = $"Uyarı: Ders programından aldığı koordinatörlük saati ({coordHours} Saat), yasal azami sınırı ({maxHours} Saat) aşıyor!{entHoursInfo}";
            }
            else if (coordHours == maxHours)
            {
                cellHours.Style.ForeColor = Color.FromArgb(5, 150, 105); // emerald-600 tam kapasite
                cellHours.Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                cellHours.ToolTipText = $"Tam kapasite: Ders programında {coordHours} saat koordinatörlük tanımlı.{entHoursInfo}";
            }
            else
            {
                cellHours.Style.ForeColor = Color.FromArgb(30, 64, 175); // blue-700 aktif yük
                cellHours.Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                cellHours.ToolTipText = $"Ders Programında Aldığı: {coordHours} Saat / Boş Kontenjan: {maxHours - coordHours} Saat{entHoursInfo}";
            }
        }
    }

    private double CalculateTeacherAssignedHours(Teacher teacher)
    {
        if (_allStudents.Count == 0 && _allEnterprises.Count == 0)
            return 0;

        // Öğretmenle ilişkili işletmeleri bul (doğrudan veya öğrencileri üzerinden)
        var teacherEnterprises = _allEnterprises.Where(e =>
            e.TeacherId == teacher.Id ||
            _allStudents.Any(s => IsStudentInEnterprise(s, e) && IsStudentOfTeacher(s, teacher))
        ).DistinctBy(e => e.Id).ToList();

        double totalHours = 0;
        var calculator = new MebCoordinationCalculator();

        if (teacherEnterprises.Count > 0)
        {
            foreach (var ent in teacherEnterprises)
            {
                int stCount = _allStudents.Count(s => IsStudentInEnterprise(s, ent) && IsStudentOfTeacher(s, teacher));
                if (stCount == 0 && ent.TeacherId == teacher.Id)
                {
                    stCount = _allStudents.Count(s => IsStudentInEnterprise(s, ent));
                }

                if (stCount > 0)
                {
                    totalHours += calculator.CalculateWeeklyHours(stCount, ent.DistanceType);
                }
            }
        }
        else
        {
            int directStudents = _allStudents.Count(s => IsStudentOfTeacher(s, teacher));
            if (directStudents > 0)
            {
                totalHours += calculator.CalculateWeeklyHours(directStudents, DistanceType.MucavirAlanIci);
            }
        }

        return totalHours;
    }

    private static bool IsStudentInEnterprise(Student s, Enterprise ent)
    {
        if (s.EnterpriseId.HasValue && s.EnterpriseId.Value == ent.Id)
            return true;
        if (!string.IsNullOrWhiteSpace(s.EnterpriseName) && string.Equals(s.EnterpriseName, ent.Name, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool IsStudentOfTeacher(Student s, Teacher t)
    {
        if (s.TeacherId.HasValue && s.TeacherId.Value == t.Id)
            return true;
        if (!string.IsNullOrWhiteSpace(s.TeacherName) && string.Equals(s.TeacherName, t.FullName, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not Teacher t) return;

        if (e.ColumnIndex == _grid.Columns["colAction"].Index)
        {
            OpenTeacherDialog(t);
        }
        else if (e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            var res = MessageBox.Show(
                $"{t.FullName} isimli öğretmeni silmek istediğinize emin misiniz?",
                "Öğretmen Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                _ = DeleteTeacherAsync(t.Id);
            }
        }
    }

    private async Task DeleteTeacherAsync(int id)
    {
        await _teacherRepo.DeleteAsync(id);
        await LoadTeachersAsync();
    }

    private async void OpenTeacherDialog(Teacher? existing)
    {
        using var dlg = new TeacherEditDialog(_fieldRepo, existing);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await SaveTeacherAsync(dlg.TeacherResult);
        }
    }

    private async Task SaveTeacherAsync(Teacher t)
    {
        try
        {
            if (t.Id == 0)
                await _teacherRepo.AddAsync(t);
            else
                await _teacherRepo.UpdateAsync(t);

            await LoadTeachersAsync();
            MessageBox.Show("Öğretmen bilgileri başarıyla kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Öğretmen kaydedilirken hata oluştu:\n{ex.Message}", "Kayıt Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnUploadExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Excel ve CSV Dosyaları (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel Dosyaları (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Dosyaları (*.csv)|*.csv|Tüm Dosyalar (*.*)|*.*",
            Title = "Öğretmen Listesi Dosyasını Seçin"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var parseResult = TeacherTemplateService.ParseFile(ofd.FileName);
                if (!parseResult.Success)
                {
                    MessageBox.Show(parseResult.ErrorMessage ?? "Dosya okunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var existingTeachers = (await _teacherRepo.GetAllAsync()).ToList();
                var fields = (await _fieldRepo.GetAllAsync()).ToList();

                int addedCount = 0;
                int updatedCount = 0;

                foreach (var item in parseResult.Teachers)
                {
                    // Alan / Branş ID eşleştirmesi
                    var matchedField = fields.FirstOrDefault(f => string.Equals(f.Name, item.Branch, StringComparison.OrdinalIgnoreCase));
                    int deptId = matchedField?.Id ?? 1;

                    // Mevcut öğretmen eşleştirmesi: Önce TC'ye göre, yoksa Ad + Soyad + Branş'a göre
                    Teacher? existing = null;
                    if (!string.IsNullOrWhiteSpace(item.TcKimlikNo))
                    {
                        existing = existingTeachers.FirstOrDefault(t => string.Equals(t.TcKimlikNo, item.TcKimlikNo, StringComparison.OrdinalIgnoreCase));
                    }

                    if (existing == null)
                    {
                        existing = existingTeachers.FirstOrDefault(t =>
                            string.Equals(t.FirstName.Trim(), item.FirstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(t.LastName.Trim(), item.LastName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrWhiteSpace(item.Branch) || string.Equals(t.Branch?.Trim(), item.Branch.Trim(), StringComparison.OrdinalIgnoreCase)));
                    }

                    if (existing != null)
                    {
                        if (!string.IsNullOrWhiteSpace(item.TcKimlikNo)) existing.TcKimlikNo = item.TcKimlikNo;
                        existing.FirstName = item.FirstName;
                        existing.LastName = item.LastName;
                        existing.Branch = item.Branch;
                        existing.DepartmentName = item.Branch;
                        existing.DepartmentId = deptId;
                        existing.AvailableDays = item.AvailableDays;
                        existing.MaxCoordinationHours = item.MaxCoordinationHours;
                        existing.TargetHours = item.MaxCoordinationHours;
                        existing.CoordinatorFeeHours = item.CoordinatorFeeHours;
                        if (!string.IsNullOrWhiteSpace(item.Phone)) existing.Phone = item.Phone;
                        if (!string.IsNullOrWhiteSpace(item.Email)) existing.Email = item.Email;
                        existing.IsActive = item.IsActive;

                        await _teacherRepo.UpdateAsync(existing);
                        updatedCount++;
                    }
                    else
                    {
                        var newTeacher = new Teacher
                        {
                            TcKimlikNo = item.TcKimlikNo,
                            FirstName = item.FirstName,
                            LastName = item.LastName,
                            DepartmentId = deptId,
                            Branch = item.Branch,
                            DepartmentName = item.Branch,
                            AvailableDays = item.AvailableDays,
                            EmploymentType = EmploymentType.Kadrolu,
                            MaxCoordinationHours = item.MaxCoordinationHours,
                            TargetHours = item.MaxCoordinationHours,
                            CoordinatorFeeHours = item.CoordinatorFeeHours,
                            Phone = item.Phone,
                            Email = item.Email,
                            IsActive = item.IsActive
                        };

                        var newId = await _teacherRepo.AddAsync(newTeacher);
                        newTeacher.Id = newId;
                        existingTeachers.Add(newTeacher);
                        addedCount++;
                    }
                }

                MessageBox.Show(
                    $"Öğretmen listesi başarıyla aktarıldı!\n\n➕ Yeni Eklenen: {addedCount}\n🔄 Güncellenen: {updatedCount}\nToplam İşlenen: {addedCount + updatedCount}",
                    "İçe Aktarma Tamamlandı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadTeachersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya işlenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Örnek Öğretmen Şablonunu Kaydedin",
            Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx|CSV Dosyası (*.csv)|*.csv",
            FileName = "Ornek_Ogretmenler.xlsx"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                if (sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    var content = TeacherTemplateService.GenerateSampleCsv();
                    File.WriteAllText(sfd.FileName, content, new UTF8Encoding(true));
                }
                else
                {
                    var bytes = TeacherTemplateService.GenerateSampleExcelXlsx();
                    File.WriteAllBytes(sfd.FileName, bytes);
                }

                MessageBox.Show($"Örnek şablon başarıyla kaydedildi:\n{sfd.FileName}", "Şablon Kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
