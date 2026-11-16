using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class AddressGroupsView : UserControl
{
    private readonly IAddressGroupRepository _groupRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;

    private List<AddressGroup> _allGroups = [];
    private List<Enterprise> _allEnterprises = [];

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private Label _lblCount = null!;

    public AddressGroupsView(IAddressGroupRepository groupRepo, IEnterpriseRepository enterpriseRepo)
    {
        _groupRepo = groupRepo;
        _enterpriseRepo = enterpriseRepo;

        InitializeComponent();
        LoadGroupsAsync();
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
            Text = "📍 Adres ve Bölge Grupları",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Birbirine yakın işletmeleri gruplandırarak öğretmenlerin koordinatörlük gün ve yol verimini artırın",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        var btnAddGroup = new Button
        {
            Text = "➕ Yeni Adres Grubu Ekle",
            Size = new Size(180, 36),
            Location = new Point(pnlHeader.Width - 204, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(29, 78, 216), // blue-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAddGroup.FlatAppearance.BorderSize = 0;
        btnAddGroup.Visible = MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin;
        btnAddGroup.Click += (s, e) => OpenGroupDialog(null);

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, btnAddGroup]);

        // Info Banner
        var pnlInfo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(239, 246, 255), // blue-50
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(24, 0, 24, 12)
        };
        pnlInfo.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(191, 219, 254), 1); // blue-200
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
            Text = "Adres Grupları Nasıl Çalışır?",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138), // blue-900
            Location = new Point(66, 12),
            AutoSize = true
        };

        var lblInfoText = new Label
        {
            Text = "Aynı sanayi sitesindeki veya birbirine yakın işletmeleri tek bir \"Adres Grubu\" altında toplayabilirsiniz. Akıllı Dağıtım algoritması, aynı gruptaki işletmeleri öncelikli olarak aynı koordinatör öğretmene atar. Böylece öğretmenler aynı gün içinde birbirine yakın işletmeleri ziyaret ederek zaman ve yol tasarrufu sağlar.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(30, 64, 175), // blue-800
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
            PlaceholderText = "🔍 Grup adı veya bölge ara...",
            Location = new Point(24, 12),
            Width = 260,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _lblCount = new Label
        {
            Text = "Toplam 0 grup tanımlı",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 9f),
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
            HeaderText = "Adres / Bölge Grubu Adı",
            FillWeight = 26,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colDistrict = new DataGridViewTextBoxColumn
        {
            Name = "colDistrict",
            HeaderText = "İlçe / Bölge",
            FillWeight = 18
        };

        var colDesc = new DataGridViewTextBoxColumn
        {
            Name = "colDesc",
            HeaderText = "Açıklama / Kapsam",
            FillWeight = 30
        };

        var colEnterprises = new DataGridViewTextBoxColumn
        {
            Name = "colEnterprises",
            HeaderText = "Bağlı İşletme",
            FillWeight = 14,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
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
            _grid.Columns.AddRange([colIdx, colName, colDistrict, colDesc, colEnterprises, colAction, colDelete]);
        }
        else
        {
            _grid.Columns.AddRange([colIdx, colName, colDistrict, colDesc, colEnterprises]);
        }
    }

    public async Task LoadGroupsAsync()
    {
        _allGroups = (await _groupRepo.GetAllAsync()).OrderBy(g => g.Name).ToList();
        _allEnterprises = (await _enterpriseRepo.GetAllAsync()).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        var filtered = _allGroups.Where(g =>
        {
            if (string.IsNullOrWhiteSpace(q)) return true;
            return g.Name.ToLowerInvariant().Contains(q) ||
                   (g.District ?? "").ToLowerInvariant().Contains(q) ||
                   (g.Description ?? "").ToLowerInvariant().Contains(q);
        }).ToList();

        _lblCount.Text = $"Toplam {filtered.Count} grup tanımlı";

        _grid.Rows.Clear();
        int idx = 1;
        foreach (var g in filtered)
        {
            var entCount = _allEnterprises.Count(e => e.AddressGroupId == g.Id);

            var rowIndex = _grid.Rows.Add(
                idx++,
                g.Name,
                g.District ?? "-",
                g.Description ?? "-",
                $"{entCount} İşletme"
            );

            _grid.Rows[rowIndex].Tag = g;
        }
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not AddressGroup g) return;

        if (_grid.Columns.Contains("colAction") && e.ColumnIndex == _grid.Columns["colAction"].Index)
        {
            OpenGroupDialog(g);
        }
        else if (_grid.Columns.Contains("colDelete") && e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            var res = MessageBox.Show(
                $"{g.Name} adres grubunu silmek istediğinize emin misiniz?",
                "Grup Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                _ = DeleteGroupAsync(g.Id);
            }
        }
    }

    private async Task DeleteGroupAsync(int id)
    {
        await _groupRepo.DeleteAsync(id);
        await LoadGroupsAsync();
    }

    private void OpenGroupDialog(AddressGroup? existing)
    {
        using var dlg = new AddressGroupEditDialog(existing);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _ = SaveGroupAsync(dlg.GroupResult);
        }
    }

    private async Task SaveGroupAsync(AddressGroup g)
    {
        if (g.Id == 0)
            await _groupRepo.AddAsync(g);
        else
            await _groupRepo.UpdateAsync(g);

        await LoadGroupsAsync();
    }
}
