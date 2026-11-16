using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class ClassesView : UserControl
{
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly ISettingRepository? _settingRepo;
    private readonly IAttendanceRepository? _attendanceRepo;

    private List<SchoolClass> _allClasses = [];
    private List<Student> _allStudents = [];

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbFilterInstitution = null!;
    private ComboBox _cmbFilterEducation = null!;
    private Label _lblFilterCount = null!;

    public ClassesView(
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        IStudentRepository studentRepo,
        ISettingRepository? settingRepo = null,
        IAttendanceRepository? attendanceRepo = null)
    {
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _studentRepo = studentRepo;
        _settingRepo = settingRepo;
        _attendanceRepo = attendanceRepo;

        InitializeComponent();
        using var _ = LoadClassesAsync();
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
            Text = "🏫 Sınıf ve Şube Yönetimi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Kurum türü, öğretim türü, alan ve işletme staj günleri tanımlanan sınıflar",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        var pnlHeaderButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 360,
            Height = 45,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnAddClass = new Button
        {
            Text = "➕ Yeni Sınıf Ekle",
            Size = new Size(150, 36),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnAddClass.FlatAppearance.BorderSize = 0;
        btnAddClass.Visible = MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin;
        btnAddClass.Click += (s, e) => OpenClassDialog(null);

        var btnPrintClassLists = new Button
        {
            Text = "🖨️ Sınıf Listeleri Yazdır",
            Size = new Size(175, 36),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnPrintClassLists.FlatAppearance.BorderSize = 0;
        btnPrintClassLists.Click += (s, e) =>
        {
            if (_settingRepo == null) return;
            using var dlg = new ClassListPrintDialog(_classRepo, _studentRepo, _settingRepo);
            dlg.ShowDialog(this);
        };

        pnlHeaderButtons.Controls.AddRange([btnAddClass, btnPrintClassLists]);
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlHeaderButtons]);

        // Filter Bar Panel
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFilter.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        _txtSearch = new TextBox
        {
            PlaceholderText = "🔍 Sınıf adı veya alan ara...",
            Location = new Point(24, 12),
            Width = 240,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _cmbFilterInstitution = new ComboBox
        {
            Location = new Point(274, 12),
            Width = 190,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterInstitution.Items.AddRange(["Tüm Kurum Türleri", "Anadolu Meslek Programı", "MESEM"]);
        _cmbFilterInstitution.SelectedIndex = 0;
        _cmbFilterInstitution.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterEducation = new ComboBox
        {
            Location = new Point(474, 12),
            Width = 170,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterEducation.Items.AddRange(["Tüm Öğretim Türleri", "Örgün Eğitim", "Özel Eğitim", "MESEM"]);
        _cmbFilterEducation.SelectedIndex = 0;
        _cmbFilterEducation.SelectedIndexChanged += (s, e) => ApplyFilter();

        _lblFilterCount = new Label
        {
            Text = "Toplam 0 sınıf",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(660, 16)
        };

        pnlFilter.Controls.AddRange([_txtSearch, _cmbFilterInstitution, _cmbFilterEducation, _lblFilterCount]);

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
        Controls.Add(pnlFilter);
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
            FillWeight = 4,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "Sınıf Adı",
            FillWeight = 14,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colField = new DataGridViewTextBoxColumn
        {
            Name = "colField",
            HeaderText = "Alanı",
            FillWeight = 22
        };

        var colInstitution = new DataGridViewTextBoxColumn
        {
            Name = "colInstitution",
            HeaderText = "Kurum Türü",
            FillWeight = 16,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colEducation = new DataGridViewTextBoxColumn
        {
            Name = "colEducation",
            HeaderText = "Öğretim Türü",
            FillWeight = 14,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colDays = new DataGridViewTextBoxColumn
        {
            Name = "colDays",
            HeaderText = "İşletme Günleri",
            FillWeight = 20
        };

        var colDates = new DataGridViewTextBoxColumn
        {
            Name = "colDates",
            HeaderText = "İşe Başlama / Bitiş",
            FillWeight = 16,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colStudents = new DataGridViewTextBoxColumn
        {
            Name = "colStudents",
            HeaderText = "Öğrenci Sayısı",
            FillWeight = 12,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colStudentsBtn = new DataGridViewButtonColumn
        {
            Name = "colStudentsBtn",
            HeaderText = "İşlemler",
            Text = "👥 Öğrenciler",
            UseColumnTextForButtonValue = true,
            FillWeight = 11
        };

        var colAction = new DataGridViewButtonColumn
        {
            Name = "colAction",
            HeaderText = "",
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
            _grid.Columns.AddRange([colIdx, colName, colField, colInstitution, colEducation, colDates, colDays, colStudents, colStudentsBtn, colAction, colDelete]);
        }
        else
        {
            _grid.Columns.AddRange([colIdx, colName, colField, colInstitution, colEducation, colDates, colDays, colStudents, colStudentsBtn]);
        }
    }

    public async Task LoadClassesAsync()
    {
        _allClasses = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();
        _allStudents = (await _studentRepo.GetAllAsync()).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        var inst = _cmbFilterInstitution.SelectedIndex > 0 ? _cmbFilterInstitution.SelectedItem?.ToString() : null;
        var edu = _cmbFilterEducation.SelectedIndex > 0 ? _cmbFilterEducation.SelectedItem?.ToString() : null;

        var filtered = _allClasses.Where(c =>
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var match = c.Name.ToLowerInvariant().Contains(q) ||
                            (c.FieldName ?? "").ToLowerInvariant().Contains(q);
                if (!match) return false;
            }

            if (!string.IsNullOrWhiteSpace(inst) && !string.Equals(c.InstitutionType, inst, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(edu) && !string.Equals(c.EducationType, edu, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }).ToList();

        _lblFilterCount.Text = $"Toplam {filtered.Count} sınıf";

        _grid.Rows.Clear();
        int idx = 1;
        foreach (var c in filtered)
        {
            var studentCount = _allStudents.Count(s => string.Equals(s.Classroom, c.Name, StringComparison.OrdinalIgnoreCase));
            var datesText = (!string.IsNullOrWhiteSpace(c.StartDate) || !string.IsNullOrWhiteSpace(c.EndDate))
                ? $"{c.StartDate ?? "-"} / {c.EndDate ?? "-"}"
                : "Belirtilmemiş";

            var daysText = c.HasInternship
                ? (string.IsNullOrWhiteSpace(c.InternshipDays) ? "Belirtilmemiş" : c.InternshipDays)
                : "İşletmeye Gitmiyor";

            var rowIndex = _grid.Rows.Add(
                idx++,
                c.Name,
                c.FieldName ?? "-",
                c.InstitutionType,
                c.EducationType,
                datesText,
                daysText,
                $"{studentCount} Öğrenci"
            );

            _grid.Rows[rowIndex].Tag = c;
        }
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not SchoolClass c) return;

        if (e.ColumnIndex == _grid.Columns["colStudentsBtn"].Index)
        {
            OpenClassStudentsDialog(c);
        }
        else if (_grid.Columns.Contains("colAction") && e.ColumnIndex == _grid.Columns["colAction"].Index)
        {
            if (MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
                OpenClassDialog(c);
        }
        else if (_grid.Columns.Contains("colDelete") && e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin) return;
            var res = MessageBox.Show(
                $"{c.Name} sınıfını silmek istediğinize emin misiniz?",
                "Sınıf Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                _ = DeleteClassAsync(c.Id);
            }
        }
    }

    private async void OpenClassStudentsDialog(SchoolClass c)
    {
        var classStudents = _allStudents.Where(s => string.Equals(s.Classroom, c.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        AppSettings? settings = null;
        if (_settingRepo != null)
        {
            try
            {
                settings = await _settingRepo.GetAppSettingsAsync();
            }
            catch { }
        }

        using var dlg = new ClassStudentsDialog(c, classStudents, settings);
        dlg.ShowDialog(this);
    }

    private async Task DeleteClassAsync(int id)
    {
        await _classRepo.DeleteAsync(id);
        await LoadClassesAsync();
    }

    private async void OpenClassDialog(SchoolClass? existing)
    {
        var oldName = existing?.Name;
        var oldInst = existing?.InstitutionType;
        var oldEdu = existing?.EducationType;
        var oldFieldName = existing?.FieldName;

        using var dlg = new ClassEditDialog(_classRepo, _fieldRepo, existing);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await SaveClassAsync(dlg.ClassResult, oldName, oldInst, oldEdu, oldFieldName);
        }
    }

    private async Task SaveClassAsync(SchoolClass c, string? oldName, string? oldInst, string? oldEdu, string? oldFieldName = null)
    {
        try
        {
            bool updateSgkCodes = false;
            bool toMesem = false;
            bool updateStudentFields = false;

            if (c.Id > 0 && (!string.IsNullOrEmpty(oldInst) || !string.IsNullOrEmpty(oldEdu)))
            {
                bool wasMesem = (oldInst?.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (oldEdu?.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0);
                bool isMesem = (c.InstitutionType?.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (c.EducationType?.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!wasMesem && isMesem)
                {
                    toMesem = true;
                    // 1. Onay
                    var res1 = MessageBox.Show(
                        $"[1. ONAY] [{c.Name}] sınıfının Kurum/Öğretim türü 'MESEM' olarak değiştirilecektir.\n\n" +
                        "Bu işlem onaylandığında:\n" +
                        "• Sınıftaki öğrencilerin Öğretim Türü 'MESEM' yapılacak\n" +
                        "• Sağlık Güvencesi VAR olanların SGK kodu: 7 (MESEM)\n" +
                        "• Sağlık Güvencesi YOK olanların SGK kodu: 42 (MESEM)\n" +
                        "• Öğrencilerin aylık devamsızlık kayıtları MESEM çalışma takvimine göre (haftaiçi 'X', haftasonu '-') otomatik güncellenecektir.\n\n" +
                        "Devam etmek istiyor musunuz?",
                        "Kurum/Öğretim Türü Değişikliği (1. Onay)",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (res1 != DialogResult.Yes) return;

                    // 2. Onay (İkinci güvenlik onayı)
                    var res2 = MessageBox.Show(
                        $"⚠️ [2. VE SON ONAY] Bu işlem geri alınamaz!\n\n" +
                        $"[{c.Name}] sınıfındaki tüm öğrencilerin SGK prim kodları ve aylık devamsızlık kayıtları MESEM statüsüne dönüştürülecektir.\n\n" +
                        "Değişiklikleri onaylıyor musunuz?",
                        "DİKKAT: Tür Değişikliğini Onayla (2. Onay)",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (res2 != DialogResult.Yes) return;

                    updateSgkCodes = true;
                }
                else if (wasMesem && !isMesem)
                {
                    toMesem = false;
                    // 1. Onay
                    var res1 = MessageBox.Show(
                        $"[1. ONAY] [{c.Name}] sınıfının Kurum/Öğretim türü 'Örgün Eğitim' olarak değiştirilecektir.\n\n" +
                        "Bu işlem onaylandığında:\n" +
                        "• Sınıftaki öğrencilerin Öğretim Türü 'Örgün Eğitim' yapılacak\n" +
                        "• Sağlık Güvencesi VAR olanların SGK kodu: 49 (Bakmakla Yükümlü)\n" +
                        "• Sağlık Güvencesi YOK olanların SGK kodu: 50 (Yükümlü Olmayan)\n" +
                        "• Öğrencilerin aylık devamsızlık kayıtları Örgün Eğitim takvimine göre güncellenecektir.\n\n" +
                        "Devam etmek istiyor musunuz?",
                        "Kurum/Öğretim Türü Değişikliği (1. Onay)",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (res1 != DialogResult.Yes) return;

                    // 2. Onay (İkinci güvenlik onayı)
                    var res2 = MessageBox.Show(
                        $"⚠️ [2. VE SON ONAY] Bu işlem geri alınamaz!\n\n" +
                        $"[{c.Name}] sınıfındaki tüm öğrencilerin SGK prim kodları ve aylık devamsızlık kayıtları Örgün Eğitim statüsüne dönüştürülecektir.\n\n" +
                        "Değişiklikleri onaylıyor musunuz?",
                        "DİKKAT: Tür Değişikliğini Onayla (2. Onay)",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (res2 != DialogResult.Yes) return;

                    updateSgkCodes = true;
                }
            }

            // Mesleki Alan / Dal değişikliği kontrolü
            if (c.Id > 0 && !string.Equals(c.FieldName?.Trim() ?? "", oldFieldName?.Trim() ?? "", StringComparison.CurrentCultureIgnoreCase))
            {
                var studentCount = _allStudents.Count(s =>
                    s.ClassId == c.Id ||
                    string.Equals(s.Classroom, c.Name, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(oldName) && string.Equals(s.Classroom, oldName, StringComparison.OrdinalIgnoreCase)));

                string countMsg = studentCount > 0 ? $"Bu sınıfa bağlı kayıtlı {studentCount} öğrencinin de" : "Bu sınıfa bağlı öğrencilerin de";
                var resField = MessageBox.Show(
                    $"[{c.Name}] sınıfının Meslek Alan/Dal bilgisi \"{oldFieldName ?? "Belirtilmemiş"}\" yerine \"{c.FieldName}\" olarak değiştirildi.\n\n" +
                    $"{countMsg} meslek alan/dal bilgisi güncellensin mi?",
                    "Öğrenci Alan/Dal Bilgilerini Güncelle",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                updateStudentFields = (resField == DialogResult.Yes);
            }

            if (c.Id == 0)
            {
                var newId = await _classRepo.AddAsync(c);
                c.Id = newId;
            }
            else
            {
                await _classRepo.UpdateAsync(c);
            }

            // Sync class info to students and update SGK codes / Field if confirmed
            await _studentRepo.SyncClassInfoAndSgkCodesToStudentsAsync(c, oldName, updateSgkCodes, toMesem, updateStudentFields);

            // Update monthly attendance entries for this class if SGK codes were updated
            if (updateSgkCodes && _attendanceRepo != null)
            {
                var allStudents = await _studentRepo.GetAllAsync();
                var classStudents = allStudents.Where(s =>
                    s.ClassId == c.Id ||
                    string.Equals(s.Classroom, c.Name, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(oldName) && string.Equals(s.Classroom, oldName, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                if (classStudents.Count > 0)
                {
                    var settings = _settingRepo != null ? await _settingRepo.GetAppSettingsAsync() : null;
                    await _attendanceRepo.SyncClassAttendanceEntriesAsync(c, classStudents, toMesem, settings);
                }
            }

            if (updateStudentFields)
            {
                MessageBox.Show(
                    $"[{c.Name}] sınıfı ve sınıfa bağlı öğrencilerin meslek alan/dal bilgisi \"{c.FieldName}\" olarak güncellendi.",
                    "Bilgi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            await LoadClassesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sınıf kaydedilirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
