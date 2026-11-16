using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class UsersView : UserControl
{
    private readonly IUserRepository _userRepo;
    private readonly ITeacherRepository _teacherRepo;
    private readonly ISettingRepository _settingRepo;

    private DataGridView _grid = null!;
    private Label _lblCount = null!;
    private CheckBox _chkTeacherCanEdit = null!;
    private bool _isUpdatingPermission = false;
    private List<User> _users = [];

    public UsersView(IUserRepository userRepo, ITeacherRepository teacherRepo, ISettingRepository settingRepo)
    {
        _userRepo = userRepo;
        _teacherRepo = teacherRepo;
        _settingRepo = settingRepo;

        InitializeComponent();
        _ = LoadUsersAsync();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.Transparent,
            Padding = new Padding(24, 6, 24, 0)
        };

        var lblTitle = new Label
        {
            Text = "👥 Kullanıcı ve Yetki Yönetimi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 6),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Sisteme giriş yapabilen kullanıcılar, roller (Yönetici / Öğretmen) ve şifre işlemleri",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 34),
            AutoSize = true
        };

        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 360,
            Height = 55,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnAddUser = new Button
        {
            Text = "➕ Yeni Kullanıcı Ekle",
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnAddUser.FlatAppearance.BorderSize = 0;
        btnAddUser.Click += (s, e) => OpenUserEditDialog(null);

        var btnRefresh = new Button
        {
            Text = "🔄 Yenile",
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnRefresh.Click += async (s, e) => await LoadUsersAsync();

        pnlTopButtons.Controls.AddRange([btnAddUser, btnRefresh]);
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Filter / Summary Bar
        var pnlInfo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlInfo.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlInfo.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlInfo.Height - 1, pnlInfo.Width, pnlInfo.Height - 1);
        };

        _lblCount = new Label
        {
            Text = "Toplam 0 kullanıcı",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(24, 12),
            AutoSize = true
        };
        pnlInfo.Controls.Add(_lblCount);

        _chkTeacherCanEdit = new CheckBox
        {
            Text = "🔒 Öğretmenler Salt Okunur Modda (Veri Değişikliği Kilitli)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 83, 9),
            Location = new Point(360, 11),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        _chkTeacherCanEdit.CheckedChanged += async (s, e) =>
        {
            if (_isUpdatingPermission) return;
            bool allowed = _chkTeacherCanEdit.Checked;
            CurrentUserSession.TeacherCanEditData = allowed;
            await _settingRepo.SetValueAsync("TeacherCanEditData", allowed.ToString().ToLowerInvariant());
            UpdateTeacherPermissionLabel(allowed);
        };
        pnlInfo.Controls.Add(_chkTeacherCanEdit);

        // Grid Panel
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 12, 24, 20)
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

        var colUsername = new DataGridViewTextBoxColumn
        {
            Name = "colUsername",
            HeaderText = "Kullanıcı Adı",
            FillWeight = 14,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colFullName = new DataGridViewTextBoxColumn
        {
            Name = "colFullName",
            HeaderText = "Adı Soyadı",
            FillWeight = 20
        };

        var colRole = new DataGridViewTextBoxColumn
        {
            Name = "colRole",
            HeaderText = "Yetki Rolü",
            FillWeight = 12,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
        };

        var colTeacher = new DataGridViewTextBoxColumn
        {
            Name = "colTeacher",
            HeaderText = "İlişkili Öğretmen",
            FillWeight = 18
        };

        var colStatus = new DataGridViewTextBoxColumn
        {
            Name = "colStatus",
            HeaderText = "Durum",
            FillWeight = 10,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
        };

        var colLastLogin = new DataGridViewTextBoxColumn
        {
            Name = "colLastLogin",
            HeaderText = "Son Giriş",
            FillWeight = 14,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colEdit = new DataGridViewButtonColumn
        {
            Name = "colEdit",
            HeaderText = "",
            Text = "✏️ Düzenle",
            UseColumnTextForButtonValue = true,
            FillWeight = 10
        };

        var colResetPass = new DataGridViewButtonColumn
        {
            Name = "colResetPass",
            HeaderText = "",
            Text = "🔑 Şifre Sıfırla",
            UseColumnTextForButtonValue = true,
            FillWeight = 11
        };

        var colDelete = new DataGridViewButtonColumn
        {
            Name = "colDelete",
            HeaderText = "",
            Text = "🗑️ Sil",
            UseColumnTextForButtonValue = true,
            FillWeight = 8
        };

        _grid.Columns.AddRange([colIdx, colUsername, colFullName, colRole, colTeacher, colStatus, colLastLogin, colEdit, colResetPass, colDelete]);
    }

    private void UpdateTeacherPermissionLabel(bool allowed)
    {
        if (allowed)
        {
            _chkTeacherCanEdit.Text = "🔓 Öğretmenler Veri Değişikliği Yapabilir (Düzenleme İzni Açık)";
            _chkTeacherCanEdit.ForeColor = Color.FromArgb(5, 150, 105);
        }
        else
        {
            _chkTeacherCanEdit.Text = "🔒 Öğretmenler Salt Okunur Modda (Veri Değişikliği Kilitli)";
            _chkTeacherCanEdit.ForeColor = Color.FromArgb(180, 83, 9);
        }
    }

    public async Task LoadUsersAsync()
    {
        try
        {
            var appSettings = await _settingRepo.GetAppSettingsAsync();
            _isUpdatingPermission = true;
            _chkTeacherCanEdit.Checked = appSettings.TeacherCanEditData;
            CurrentUserSession.TeacherCanEditData = appSettings.TeacherCanEditData;
            UpdateTeacherPermissionLabel(appSettings.TeacherCanEditData);
            _isUpdatingPermission = false;

            _users = (await _userRepo.GetAllAsync()).ToList();
            _lblCount.Text = $"Toplam {_users.Count} kullanıcı ({_users.Count(u => u.IsActive)} aktif, {_users.Count(u => !u.IsActive)} pasif)";

            _grid.Rows.Clear();
            int idx = 1;
            foreach (var u in _users)
            {
                var roleText = u.Role == "Admin" ? "⭐ Yönetici" : "👨‍🏫 Öğretmen";
                var statusText = u.IsActive ? "✓ Aktif" : "❌ Pasif";
                var lastLoginText = !string.IsNullOrWhiteSpace(u.LastLoginAt) ? u.LastLoginAt : "Giriş Yapılmadı";

                var rowIndex = _grid.Rows.Add(
                    idx++,
                    u.Username,
                    u.FullName,
                    roleText,
                    u.TeacherName ?? "-",
                    statusText,
                    lastLoginText
                );

                var row = _grid.Rows[rowIndex];
                row.Tag = u;

                // Status styling
                var cellStatus = row.Cells["colStatus"];
                cellStatus.Style.ForeColor = u.IsActive ? Color.FromArgb(22, 101, 52) : Color.FromArgb(185, 28, 28);

                // Role styling
                var cellRole = row.Cells["colRole"];
                cellRole.Style.ForeColor = u.Role == "Admin" ? Color.FromArgb(30, 58, 138) : Color.FromArgb(71, 85, 105);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kullanıcılar yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not User user) return;

        if (e.ColumnIndex == _grid.Columns["colEdit"].Index)
        {
            OpenUserEditDialog(user);
        }
        else if (e.ColumnIndex == _grid.Columns["colResetPass"].Index)
        {
            ResetPassword(user);
        }
        else if (e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            DeleteUser(user);
        }
    }

    private void OpenUserEditDialog(User? user)
    {
        using var dlg = new UserEditDialog(_userRepo, _teacherRepo, user);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadUsersAsync();
        }
    }

    private async void ResetPassword(User user)
    {
        // Simple input prompt for admin to set new password
        using var form = new Form
        {
            Text = $"Şifre Sıfırla: {user.FullName}",
            Size = new Size(360, 200),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9f)
        };

        var lbl = new Label
        {
            Text = $"{user.FullName} ({user.Username}) için yeni şifre belirleyin:",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        var txtPass = new TextBox
        {
            Location = new Point(20, 48),
            Width = 300,
            Font = new Font("Segoe UI", 10f),
            UseSystemPasswordChar = true
        };

        var btnOk = new Button
        {
            Text = "Şifreyi Değiştir",
            Location = new Point(190, 95),
            Size = new Size(130, 32),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnOk.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(95, 95),
            Size = new Size(85, 32),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        btnOk.Click += async (s, e) =>
        {
            var p = txtPass.Text.Trim();
            if (string.IsNullOrWhiteSpace(p) || p.Length < 4)
            {
                MessageBox.Show("Şifre en az 4 karakter olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await _userRepo.ChangePasswordAsync(user.Id, p);
                MessageBox.Show($"{user.Username} kullanıcısının şifresi başarıyla sıfırlandı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        btnCancel.Click += (s, e) => { form.DialogResult = DialogResult.Cancel; form.Close(); };

        form.Controls.AddRange([lbl, txtPass, btnOk, btnCancel]);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        form.ShowDialog(this);
    }

    private async void DeleteUser(User user)
    {
        if (CurrentUserSession.CurrentUser?.Id == user.Id)
        {
            MessageBox.Show("Şu anda oturum açmış olduğunuz kendi hesabınızı silemezsiniz!", "Engellendi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var activeAdmins = _users.Count(u => u.Role == "Admin" && u.IsActive);
        if (user.Role == "Admin" && activeAdmins <= 1)
        {
            MessageBox.Show("Sistemde kalan tek aktif yönetici hesabını silemezsiniz!", "Engellendi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"'{user.FullName} ({user.Username})' kullanıcısını silmek istediğinize emin misiniz?",
            "Kullanıcı Silme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            try
            {
                await _userRepo.DeleteAsync(user.Id);
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kullanıcı silinemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
