using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class CalendarTasksManagementView : UserControl
{
    private readonly ICalendarTaskRepository _taskRepo;
    private readonly Action<string>? _onNavigate;

    private DataGridView _dgvTasks = null!;
    private ComboBox _cmbFilter = null!;
    private TextBox _txtSearch = null!;
    private Label _lblSummary = null!;
    private Button _btnToggleActive = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnAdd = null!;
    private Button _btnReset = null!;

    private List<CalendarTask> _allTasks = new();

    public CalendarTasksManagementView(ICalendarTaskRepository taskRepo, Action<string>? onNavigate = null)
    {
        _taskRepo = taskRepo;
        _onNavigate = onNavigate;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 250, 252); // slate-50
        Font = new Font("Segoe UI", 9f);

        // Ana Kapsayıcı (TableLayoutPanel: 3 Satır -> 0: Başlık, 1: Araç Çubuğu, 2: Tablo)
        // Bu yapı sayesinde hiçbir buton veya panel birbirinin altına girip kaybolamaz!
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f)); // Satır 0: Başlık
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f)); // Satır 1: Araç Çubuğu
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Satır 2: Tablo

        // ==========================================
        // 1. ÜST BAŞLIK PANELİ (Row 0)
        // ==========================================
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 12, 24, 10),
            Margin = Padding.Empty
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "📅  İdari İşlem ve Görev Takvimi Yönetimi",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42), // slate-900
            Location = new Point(24, 10),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Dashboard takviminde gösterilen aylık rutinler, dönemsel MEB işlemleri ve okula özel görevler buradan yönetilir; dilediğinizi tek tıkla gizleyebilir veya düzenleyebilirsiniz.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            Location = new Point(24, 38),
            AutoSize = true
        };

        _lblSummary = new Label
        {
            Text = "Yükleniyor...",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(67, 56, 202), // indigo-700
            BackColor = Color.FromArgb(238, 242, 255), // indigo-50
            Padding = new Padding(12, 6, 12, 6),
            AutoSize = true
        };

        pnlHeader.Resize += (s, e) =>
        {
            _lblSummary.Location = new Point(pnlHeader.Width - _lblSummary.PreferredWidth - 24, 18);
        };

        pnlHeader.Controls.AddRange([lblTitle, lblSub, _lblSummary]);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // ==========================================
        // 2. AKSİYON VE ARAÇ ÇUBUĞU (Row 1)
        // ==========================================
        var pnlToolbar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 10, 24, 10),
            Margin = Padding.Empty
        };
        pnlToolbar.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1);
        };

        var lblFilter = new Label
        {
            Text = "Filtre:",
            Location = new Point(24, 18),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        _cmbFilter = new ComboBox
        {
            Location = new Point(68, 13),
            Width = 175,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilter.Items.Add("Tüm Görevler");
        _cmbFilter.Items.Add("🟢 Sadece Aktifler");
        _cmbFilter.Items.Add("🚫 Sadece Pasifler (Gizli)");
        _cmbFilter.Items.Add("🔁 Aylık Yenileme");
        _cmbFilter.Items.Add("📅 Yıllık Yenileme");
        _cmbFilter.Items.Add("📌 Belirlenen Tarihte");
        _cmbFilter.SelectedIndex = 0;
        _cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        _txtSearch = new TextBox
        {
            Location = new Point(255, 13),
            Width = 180,
            PlaceholderText = "🔍 Görevlerde ara...",
            Font = new Font("Segoe UI", 9f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _btnAdd = new Button
        {
            Text = "➕ Yeni Görev Ekle",
            Location = new Point(450, 11),
            Size = new Size(135, 34),
            BackColor = Color.FromArgb(79, 70, 229), // Indigo-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnAdd.FlatAppearance.BorderSize = 0;
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button
        {
            Text = "✏️ Düzenle",
            Location = new Point(593, 11),
            Size = new Size(95, 34),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnEdit.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnEdit.Click += BtnEdit_Click;

        _btnToggleActive = new Button
        {
            Text = "👁️ Aktif / Pasif (Gizle)",
            Location = new Point(696, 11),
            Size = new Size(150, 34),
            BackColor = Color.FromArgb(254, 243, 199), // amber-100
            ForeColor = Color.FromArgb(146, 64, 14),   // amber-800
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnToggleActive.FlatAppearance.BorderColor = Color.FromArgb(245, 158, 11);
        _btnToggleActive.Click += BtnToggleActive_Click;

        _btnDelete = new Button
        {
            Text = "🗑️ Sil",
            Location = new Point(854, 11),
            Size = new Size(75, 34),
            BackColor = Color.FromArgb(254, 242, 242), // red-100
            ForeColor = Color.FromArgb(185, 28, 28),   // red-700
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnDelete.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
        _btnDelete.Click += BtnDelete_Click;

        _btnReset = new Button
        {
            Text = "🔄 Standart MEB Görevlerini Geri Yükle",
            Size = new Size(250, 34),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnReset.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnReset.Click += BtnReset_Click;

        pnlToolbar.Resize += (s, e) =>
        {
            _btnReset.Location = new Point(pnlToolbar.Width - _btnReset.Width - 24, 11);
        };

        pnlToolbar.Controls.AddRange([lblFilter, _cmbFilter, _txtSearch, _btnAdd, _btnEdit, _btnToggleActive, _btnDelete, _btnReset]);
        mainLayout.Controls.Add(pnlToolbar, 0, 1);

        // ==========================================
        // 3. TABLO KART ALANI (Row 2)
        // ==========================================
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 24),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var pnlCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1)
        };
        pnlCard.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
        };

        _dgvTasks = new DataGridView
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
            ReadOnly = true,
            AutoGenerateColumns = false,
            RowTemplate = { Height = 44 }, // Ferah ve modern satır yüksekliği
            ColumnHeadersHeight = 40,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(241, 245, 249)
        };

        _dgvTasks.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        _dgvTasks.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
        _dgvTasks.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _dgvTasks.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

        _dgvTasks.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255); // Indigo-50
        _dgvTasks.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 27, 75);    // Indigo-950
        _dgvTasks.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250); // Zebra deseni

        // Sütunlar
        _dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "Durum",
            Width = 115,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
        });

        _dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colFrequency",
            HeaderText = "Tekrarlama Sıklığı",
            Width = 150,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
        });

        _dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colRange",
            HeaderText = "Gün Aralığı",
            Width = 120,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
        });

        _dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colTitle",
            HeaderText = "Görev Başlığı",
            Width = 360,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        });

        _dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colSubtitle",
            HeaderText = "Açıklama / İş Akışı",
            Width = 320,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(100, 116, 139) }
        });

        _dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colNav",
            HeaderText = "Hızlı Buton",
            Width = 150,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
        });

        _dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colType",
            HeaderText = "Tür",
            Width = 130,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
        });

        _dgvTasks.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0) BtnEdit_Click(s, e);
        };

        // Hızlı Butona Tıklama (Doğrudan İlgili Sayfaya Gitme Desteği)
        _dgvTasks.CellContentClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _dgvTasks.Columns["colNav"].Index)
            {
                if (_dgvTasks.Rows[e.RowIndex].Tag is CalendarTask task && !string.IsNullOrEmpty(task.NavTarget))
                {
                    _onNavigate?.Invoke(task.NavTarget);
                }
            }
        };

        // Hücre Renklendirme ve Şık Rozet (Badge) Tasarımı
        _dgvTasks.CellFormatting += DgvTasks_CellFormatting;

        // Sağ Tık Menüsü
        var ctxMenu = new ContextMenuStrip();
        var miEdit = ctxMenu.Items.Add("✏️ Düzenle");
        miEdit.Click += (s, e) => BtnEdit_Click(s, e);

        var miToggle = ctxMenu.Items.Add("👁️ Aktif / Pasif Yap (Gizle/Göster)");
        miToggle.Click += (s, e) => BtnToggleActive_Click(s, e);

        var miDelete = ctxMenu.Items.Add("🗑️ Sil");
        miDelete.Click += (s, e) => BtnDelete_Click(s, e);

        ctxMenu.Items.Add(new ToolStripSeparator());
        var miNav = ctxMenu.Items.Add("🚀 İlgili Modüle Git");
        miNav.Click += (s, e) =>
        {
            var sel = GetSelectedTask();
            if (sel != null && !string.IsNullOrEmpty(sel.NavTarget))
            {
                _onNavigate?.Invoke(sel.NavTarget);
            }
        };

        ctxMenu.Opening += (s, e) =>
        {
            var sel = GetSelectedTask();
            miNav.Enabled = sel != null && !string.IsNullOrEmpty(sel.NavTarget);
        };

        _dgvTasks.ContextMenuStrip = ctxMenu;

        pnlCard.Controls.Add(_dgvTasks);
        pnlGridContainer.Controls.Add(pnlCard);
        mainLayout.Controls.Add(pnlGridContainer, 0, 2);

        Controls.Add(mainLayout);
    }

    private void DgvTasks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _dgvTasks.Rows.Count) return;

        var row = _dgvTasks.Rows[e.RowIndex];
        if (row.Tag is not CalendarTask task) return;

        string colName = _dgvTasks.Columns[e.ColumnIndex].Name;

        // 1. Durum Sütunu (Renkli Rozet)
        if (colName == "colStatus")
        {
            if (task.IsActive)
            {
                e.CellStyle!.BackColor = Color.FromArgb(236, 253, 245); // emerald-50
                e.CellStyle.ForeColor = Color.FromArgb(5, 150, 105);   // emerald-600
                e.CellStyle.SelectionBackColor = Color.FromArgb(209, 250, 229);
                e.CellStyle.SelectionForeColor = Color.FromArgb(4, 120, 87);
            }
            else
            {
                e.CellStyle!.BackColor = Color.FromArgb(254, 242, 242); // red-50
                e.CellStyle.ForeColor = Color.FromArgb(220, 38, 38);   // red-600
                e.CellStyle.SelectionBackColor = Color.FromArgb(254, 226, 226);
                e.CellStyle.SelectionForeColor = Color.FromArgb(185, 28, 28);
            }
        }
        // 2. Tekrarlama Sıklığı Sütunu
        else if (colName == "colFrequency")
        {
            if (task.FrequencyType == TaskFrequencyType.RecurringMonthly)
            {
                e.CellStyle!.BackColor = Color.FromArgb(239, 246, 255); // blue-50
                e.CellStyle.ForeColor = Color.FromArgb(29, 78, 216);   // blue-700
            }
            else if (task.FrequencyType == TaskFrequencyType.SpecificMonthEveryYear)
            {
                e.CellStyle!.BackColor = Color.FromArgb(245, 243, 255); // purple-50
                e.CellStyle.ForeColor = Color.FromArgb(109, 40, 217);  // purple-700
            }
            else
            {
                e.CellStyle!.BackColor = Color.FromArgb(254, 243, 199); // amber-50
                e.CellStyle.ForeColor = Color.FromArgb(180, 83, 9);    // amber-700
            }
        }
        // 3. Gün Aralığı Sütunu
        else if (colName == "colRange")
        {
            e.CellStyle!.BackColor = Color.FromArgb(241, 245, 249); // slate-100
            e.CellStyle.ForeColor = Color.FromArgb(51, 65, 85);    // slate-700
        }
        // 4. Hızlı Buton Sütunu
        else if (colName == "colNav")
        {
            if (!string.IsNullOrEmpty(task.NavBtnText) && task.NavBtnText != "-")
            {
                e.CellStyle!.BackColor = Color.FromArgb(238, 242, 255); // indigo-50
                e.CellStyle.ForeColor = Color.FromArgb(67, 56, 202);   // indigo-700
                e.CellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                e.CellStyle!.ForeColor = Color.FromArgb(148, 163, 184);
            }
        }
        // 5. Tür Sütunu
        else if (colName == "colType")
        {
            if (task.IsSystemDefault)
            {
                e.CellStyle!.BackColor = Color.FromArgb(240, 253, 244); // green-50
                e.CellStyle.ForeColor = Color.FromArgb(22, 101, 52);    // green-800
            }
            else
            {
                e.CellStyle!.BackColor = Color.FromArgb(240, 249, 255); // sky-50
                e.CellStyle.ForeColor = Color.FromArgb(3, 105, 161);   // sky-700
            }
        }

        // Eğer görev pasif (gizli) ise başlık ve açıklama silik görünsün
        if (!task.IsActive && (colName == "colTitle" || colName == "colSubtitle"))
        {
            e.CellStyle!.ForeColor = Color.FromArgb(148, 163, 184); // muted slate
        }
    }

    public async Task LoadTasksAsync()
    {
        try
        {
            var tasks = await _taskRepo.GetAllAsync(includeInactive: true);
            _allTasks = tasks.ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Görevler yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        if (_dgvTasks == null) return;

        var query = _allTasks.AsEnumerable();

        // 1. Kategori Filtresi
        query = _cmbFilter.SelectedIndex switch
        {
            1 => query.Where(t => t.IsActive),
            2 => query.Where(t => !t.IsActive),
            3 => query.Where(t => t.FrequencyType == TaskFrequencyType.RecurringMonthly),
            4 => query.Where(t => t.FrequencyType == TaskFrequencyType.SpecificMonthEveryYear),
            5 => query.Where(t => t.FrequencyType == TaskFrequencyType.OneTimeSpecificMonth),
            _ => query
        };

        // 2. Metin Arama
        var search = _txtSearch.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t =>
                t.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(t.Subtitle) && t.Subtitle.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(t.RangeText) && t.RangeText.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.OrderBy(t => t.OrderIndex).ThenBy(t => t.StartDay).ThenBy(t => t.Id).ToList();

        _dgvTasks.Rows.Clear();
        foreach (var task in filtered)
        {
            string statusStr = task.IsActive ? "🟢 Aktif" : "🚫 Gizli (Pasif)";
            string navStr = !string.IsNullOrWhiteSpace(task.NavBtnText) ? task.NavBtnText : "-";
            string typeStr = task.IsSystemDefault ? "🏛️ Standart MEB" : "👤 Okula Özel";

            int rowIdx = _dgvTasks.Rows.Add(
                statusStr,
                task.FrequencyDisplayText,
                task.DisplayRange,
                task.Title,
                task.Subtitle ?? "-",
                navStr,
                typeStr
            );

            var row = _dgvTasks.Rows[rowIdx];
            row.Tag = task;
        }

        int activeCount = _allTasks.Count(t => t.IsActive);
        int passiveCount = _allTasks.Count(t => !t.IsActive);
        _lblSummary.Text = $"Toplam: {_allTasks.Count} Görev  |  🟢 {activeCount} Aktif  |  🚫 {passiveCount} Gizli";
    }

    private CalendarTask? GetSelectedTask()
    {
        if (_dgvTasks.SelectedRows.Count == 0) return null;
        return _dgvTasks.SelectedRows[0].Tag as CalendarTask;
    }

    private async void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new CalendarTaskEditDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            dlg.TaskItem.IsSystemDefault = false;
            dlg.TaskItem.OrderIndex = 50; // Okula özel eklenenler standartlardan sonra sıralanır
            await _taskRepo.AddAsync(dlg.TaskItem);
            await LoadTasksAsync();
            MessageBox.Show("Yeni idari görev başarıyla eklendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void BtnEdit_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedTask();
        if (selected == null)
        {
            MessageBox.Show("Lütfen düzenlemek istediğiniz görevi seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new CalendarTaskEditDialog(selected);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await _taskRepo.UpdateAsync(dlg.TaskItem);
            await LoadTasksAsync();
            MessageBox.Show("Görev bilgileri güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void BtnToggleActive_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedTask();
        if (selected == null)
        {
            MessageBox.Show("Lütfen durumunu değiştirmek istediğiniz görevi seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        selected.IsActive = !selected.IsActive;
        await _taskRepo.UpdateAsync(selected);
        await LoadTasksAsync();

        string action = selected.IsActive ? "Aktif yapıldı (Dashboard takviminde gösterilecek)" : "Pasife alındı (Dashboard takviminde gizlendi)";
        MessageBox.Show($"Görev durumu güncellendi:\n{action}.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedTask();
        if (selected == null)
        {
            MessageBox.Show("Lütfen silmek istediğiniz görevi seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (selected.IsSystemDefault)
        {
            var res = MessageBox.Show(
                $"'{selected.Title}' görevi bir standart MEB işlemidir.\n\n" +
                $"Öneri: Görevi tamamen silmek yerine 'Pasife Alıp Gizlemek' takvimden kaldırır ancak gerektiğinde tek tıkla geri getirmeyi kolaylaştırır.\n\n" +
                $"• 'Evet' : Görevi PASİFE ALIP GİZLE (Önerilen)\n" +
                $"• 'Hayır': Görevi TAMAMEN SİL\n" +
                $"• 'İptal': Vazgeç",
                "Standart MEB Görevi",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                selected.IsActive = false;
                await _taskRepo.UpdateAsync(selected);
                await LoadTasksAsync();
                MessageBox.Show("Görev pasife alındı ve takvimden gizlendi.", "Gizlendi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (res == DialogResult.No)
            {
                await _taskRepo.DeleteAsync(selected.Id);
                await LoadTasksAsync();
                MessageBox.Show("Görev tamamen silindi.", "Silindi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        else
        {
            var res = MessageBox.Show(
                $"'{selected.Title}' görevini silmek istediğinize emin misiniz?",
                "Görevi Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                await _taskRepo.DeleteAsync(selected.Id);
                await LoadTasksAsync();
                MessageBox.Show("Görev silindi.", "Silindi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private async void BtnReset_Click(object? sender, EventArgs e)
    {
        var res = MessageBox.Show(
            "Standart MEB görevleri orijinal haline döndürülecektir.\n" +
            "(Silinen veya değiştirilen standart MEB görevleri yeniden oluşturulacak, okula özel eklediğiniz görevler korunacaktır.)\n\n" +
            "Devam etmek istiyor musunuz?",
            "Standart Görevleri Geri Yükle",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res == DialogResult.Yes)
        {
            await _taskRepo.ResetToDefaultsAsync();
            await LoadTasksAsync();
            MessageBox.Show("Standart MEB görevleri başarıyla geri yüklendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
