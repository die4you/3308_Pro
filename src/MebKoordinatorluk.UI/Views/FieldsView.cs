using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class FieldsView : UserControl
{
    private readonly IFieldRepository _fieldRepo;
    private readonly ITeacherRepository _teacherRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly IStudentRepository _studentRepo;

    private List<Field> _allFields = [];
    private List<Teacher> _allTeachers = [];
    private List<Enterprise> _allEnterprises = [];
    private List<Student> _allStudents = [];

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private Label _lblCount = null!;

    public FieldsView(
        IFieldRepository fieldRepo,
        ITeacherRepository teacherRepo,
        IEnterpriseRepository enterpriseRepo,
        IStudentRepository studentRepo)
    {
        _fieldRepo = fieldRepo;
        _teacherRepo = teacherRepo;
        _enterpriseRepo = enterpriseRepo;
        _studentRepo = studentRepo;

        InitializeComponent();
        LoadFieldsAsync();
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
            Text = "📚 Mesleki Alan ve Dal Yönetimi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Mesleki eğitim alanları; öğretmen, işletme ve öğrenci kayıtlarında açılır liste olarak ortak kullanılır",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        var btnAddField = new Button
        {
            Text = "➕ Yeni Alan Ekle",
            Size = new Size(150, 36),
            Location = new Point(pnlHeader.Width - 174, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(79, 70, 229), // indigo-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAddField.FlatAppearance.BorderSize = 0;
        btnAddField.Visible = MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin;
        btnAddField.Click += (s, e) => OpenFieldDialog(null);

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, btnAddField]);

        // Info Banner
        var pnlInfo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(238, 242, 255), // indigo-50
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(24, 0, 24, 12)
        };
        pnlInfo.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(199, 210, 254), 1); // indigo-200
            e.Graphics.DrawRectangle(pen, 24, 0, pnlInfo.Width - 49, pnlInfo.Height - 1);
        };

        var lblInfoIcon = new Label
        {
            Text = "💡",
            Font = new Font("Segoe UI", 14f),
            Location = new Point(34, 12),
            Size = new Size(28, 28)
        };

        var lblInfoTitle = new Label
        {
            Text = "Ortak Alan / Dal Havuzu",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(49, 46, 129), // indigo-900
            Location = new Point(66, 12),
            AutoSize = true
        };

        var lblInfoText = new Label
        {
            Text = "Burada tanımlanan alanlar öğretmenlerin branşı, işletmelerin faaliyet alanı ve öğrencilerin alan seçimlerinde açılır liste olarak sunulur. Böylece yazım farklılıkları engellenerek Akıllı Dağıtım algoritmasının branş eşleştirmesini %100 doğrulukla yapması sağlanır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(55, 48, 163), // indigo-800
            Location = new Point(66, 32),
            Size = new Size(950, 32)
        };

        pnlInfo.Controls.AddRange([lblInfoIcon, lblInfoTitle, lblInfoText]);

        // Search Bar Panel
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
            PlaceholderText = "🔍 Alan veya açıklama ara...",
            Location = new Point(24, 12),
            Width = 260,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _lblCount = new Label
        {
            Text = "Toplam 0 alan tanımlı",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(300, 16)
        };

        pnlSearch.Controls.AddRange([_txtSearch, _lblCount]);

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
        Controls.Add(pnlInfo);
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
            HeaderText = "Alan / Dal Adı",
            FillWeight = 26,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colDesc = new DataGridViewTextBoxColumn
        {
            Name = "colDesc",
            HeaderText = "Açıklama / Dallar",
            FillWeight = 34
        };

        var colTeachers = new DataGridViewTextBoxColumn
        {
            Name = "colTeachers",
            HeaderText = "Öğretmen Sayısı",
            FillWeight = 12,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colEnterprises = new DataGridViewTextBoxColumn
        {
            Name = "colEnterprises",
            HeaderText = "İşletme Sayısı",
            FillWeight = 12,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colStudents = new DataGridViewTextBoxColumn
        {
            Name = "colStudents",
            HeaderText = "Öğrenci Sayısı",
            FillWeight = 12,
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
            _grid.Columns.AddRange([colIdx, colName, colDesc, colTeachers, colEnterprises, colStudents, colAction, colDelete]);
        }
        else
        {
            _grid.Columns.AddRange([colIdx, colName, colDesc, colTeachers, colEnterprises, colStudents]);
        }
    }

    public async Task LoadFieldsAsync()
    {
        _allFields = (await _fieldRepo.GetAllAsync()).OrderBy(f => f.Name).ToList();
        _allTeachers = (await _teacherRepo.GetAllAsync()).ToList();
        _allEnterprises = (await _enterpriseRepo.GetAllAsync()).ToList();
        _allStudents = (await _studentRepo.GetAllAsync()).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        var filtered = _allFields.Where(f =>
        {
            if (string.IsNullOrWhiteSpace(q)) return true;
            return f.Name.ToLowerInvariant().Contains(q) ||
                   (f.Description ?? "").ToLowerInvariant().Contains(q) ||
                   (f.Branches ?? "").ToLowerInvariant().Contains(q);
        }).ToList();

        _lblCount.Text = $"Toplam {filtered.Count} alan tanımlı";

        _grid.Rows.Clear();
        int idx = 1;
        foreach (var f in filtered)
        {
            var tCount = _allTeachers.Count(t => (t.Branch ?? "").Contains(f.Name, StringComparison.OrdinalIgnoreCase));
            var eCount = _allEnterprises.Count(e => (e.Sector ?? "").Contains(f.Name, StringComparison.OrdinalIgnoreCase));
            var sCount = _allStudents.Count(s => (s.FieldName ?? "").Contains(f.Name, StringComparison.OrdinalIgnoreCase));

            var descText = !string.IsNullOrWhiteSpace(f.Branches)
                ? $"Dallar: {f.Branches}"
                : (f.Description ?? "-");

            var rowIndex = _grid.Rows.Add(
                idx++,
                f.Name,
                descText,
                $"{tCount} Öğretmen",
                $"{eCount} İşletme",
                $"{sCount} Öğrenci"
            );

            _grid.Rows[rowIndex].Tag = f;
        }
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not Field f) return;

        if (_grid.Columns.Contains("colAction") && e.ColumnIndex == _grid.Columns["colAction"].Index)
        {
            OpenFieldDialog(f);
        }
        else if (_grid.Columns.Contains("colDelete") && e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            var res = MessageBox.Show(
                $"{f.Name} alanını silmek istediğinize emin misiniz?",
                "Alan Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                _ = DeleteFieldAsync(f.Id);
            }
        }
    }

    private async Task DeleteFieldAsync(int id)
    {
        await _fieldRepo.DeleteAsync(id);
        await LoadFieldsAsync();
    }

    private void OpenFieldDialog(Field? existing)
    {
        using var dlg = new FieldEditDialog(existing);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _ = SaveFieldAsync(dlg.FieldResult);
        }
    }

    private async Task SaveFieldAsync(Field f)
    {
        if (f.Id == 0)
            await _fieldRepo.AddAsync(f);
        else
            await _fieldRepo.UpdateAsync(f);

        await LoadFieldsAsync();
    }
}
