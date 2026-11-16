using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class UserEditDialog : Form
{
    private readonly IUserRepository _userRepo;
    private readonly ITeacherRepository _teacherRepo;
    private readonly User? _editingUser;

    private TextBox _txtUsername = null!;
    private TextBox _txtFullName = null!;
    private ComboBox _cmbRole = null!;
    private ComboBox _cmbTeacher = null!;
    private TextBox _txtPassword = null!;
    private CheckBox _chkIsActive = null!;
    private Label _lblError = null!;
    private Button _btnSave = null!;

    private List<Teacher> _teachers = [];

    public UserEditDialog(IUserRepository userRepo, ITeacherRepository teacherRepo, User? editingUser = null)
    {
        _userRepo = userRepo;
        _teacherRepo = teacherRepo;
        _editingUser = editingUser;

        InitializeComponent();
        LoadTeachersAsync();
    }

    private void InitializeComponent()
    {
        bool isEdit = _editingUser != null;
        Text = isEdit ? $"Kullanıcı Düzenle: {_editingUser!.Username}" : "Yeni Kullanıcı Ekle";
        Size = new Size(460, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(30, 58, 138),
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = isEdit ? $"✏️ Kullanıcı Bilgilerini Düzenle: {_editingUser!.Username}" : "➕ Yeni Kullanıcı Tanımla",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblTitle);

        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16),
            BackColor = Color.White
        };

        int y = 14;

        // Kullanıcı Adı
        var lblUser = new Label { Text = "👤  Kullanıcı Adı * (Giriş için)", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
        y += 22;
        _txtUsername = new TextBox
        {
            Location = new Point(24, y),
            Width = 396,
            Font = new Font("Segoe UI", 10f),
            Text = _editingUser?.Username ?? "",
            Enabled = !isEdit || _editingUser?.Id != CurrentUserSession.CurrentUser?.Id,
            PlaceholderText = "Örn: ogretmen1 veya ad.soyad"
        };
        y += 36;

        // Ad Soyad
        var lblName = new Label { Text = "🏷️  Adı Soyadı *", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
        y += 22;
        _txtFullName = new TextBox
        {
            Location = new Point(24, y),
            Width = 396,
            Font = new Font("Segoe UI", 10f),
            Text = _editingUser?.FullName ?? "",
            PlaceholderText = "Örn: Ahmet YILMAZ"
        };
        y += 36;

        // Rol ve İlişkili Öğretmen (Yan yana)
        var lblRole = new Label { Text = "⭐  Kullanıcı Rolü *", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
        var lblTeacher = new Label { Text = "👨‍🏫  İlişkili Öğretmen (İsteğe Bağlı)", Location = new Point(220, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) };
        y += 22;

        _cmbRole = new ComboBox
        {
            Location = new Point(24, y),
            Width = 186,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cmbRole.Items.AddRange(["Admin", "Öğretmen"]);
        _cmbRole.SelectedItem = _editingUser?.Role ?? "Öğretmen";

        _cmbTeacher = new ComboBox
        {
            Location = new Point(220, y),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cmbTeacher.Items.Add("-- İlişkili Öğretmen Yok --");
        _cmbTeacher.SelectedIndex = 0;
        y += 38;

        // Şifre
        var lblPass = new Label
        {
            Text = isEdit ? "🔑  Şifre (Değiştirmek istemiyorsanız boş bırakın)" : "🔑  Giriş Şifresi * (En az 4 karakter)",
            Location = new Point(24, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85)
        };
        y += 22;
        _txtPassword = new TextBox
        {
            Location = new Point(24, y),
            Width = 396,
            Font = new Font("Segoe UI", 10f),
            UseSystemPasswordChar = true,
            PlaceholderText = isEdit ? "Mevcut şifreyi korumak için boş bırakın..." : "En az 4 karakterli şifre..."
        };
        y += 36;

        // Aktiflik
        _chkIsActive = new CheckBox
        {
            Text = "Hesap Aktif (Kullanıcı sisteme giriş yapabilir)",
            Location = new Point(24, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            Checked = _editingUser?.IsActive ?? true
        };
        y += 30;

        // Hata Mesajı
        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, y),
            Size = new Size(396, 20)
        };
        y += 22;

        // Butonlar
        var pnlButtons = new FlowLayoutPanel
        {
            Location = new Point(24, y),
            Size = new Size(396, 40),
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnSave = new Button
        {
            Text = isEdit ? "💾 Değişiklikleri Kaydet" : "➕ Kullanıcıyı Kaydet",
            Size = new Size(170, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SaveUserAsync();

        var btnCancel = new Button
        {
            Text = "Vazgeç",
            Size = new Size(90, 34),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0)
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlButtons.Controls.AddRange([_btnSave, btnCancel]);

        pnlBody.Controls.AddRange([
            lblUser, _txtUsername,
            lblName, _txtFullName,
            lblRole, lblTeacher, _cmbRole, _cmbTeacher,
            lblPass, _txtPassword,
            _chkIsActive, _lblError, pnlButtons
        ]);

        Controls.Add(pnlBody);
        Controls.Add(pnlHeader);
        pnlHeader.SendToBack();
    }

    private async void LoadTeachersAsync()
    {
        try
        {
            _teachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();
            _cmbTeacher.BeginUpdate();
            _cmbTeacher.Items.Clear();
            _cmbTeacher.Items.Add("-- İlişkili Öğretmen Yok --");

            int selectedIdx = 0;
            int idx = 1;
            foreach (var t in _teachers)
            {
                _cmbTeacher.Items.Add($"{t.FullName} ({t.Branch})");
                if (_editingUser?.TeacherId == t.Id)
                {
                    selectedIdx = idx;
                }
                idx++;
            }
            _cmbTeacher.SelectedIndex = selectedIdx;
            _cmbTeacher.EndUpdate();
        }
        catch { }
    }

    private async Task SaveUserAsync()
    {
        _lblError.Text = "";
        var username = _txtUsername.Text.Trim();
        var fullName = _txtFullName.Text.Trim();
        var role = _cmbRole.SelectedItem?.ToString() ?? "Öğretmen";
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            _lblError.Text = "⚠️ Kullanıcı adı zorunludur.";
            _txtUsername.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            _lblError.Text = "⚠️ Ad Soyad alanı zorunludur.";
            _txtFullName.Focus();
            return;
        }

        int? teacherId = null;
        if (_cmbTeacher.SelectedIndex > 0 && _cmbTeacher.SelectedIndex - 1 < _teachers.Count)
        {
            teacherId = _teachers[_cmbTeacher.SelectedIndex - 1].Id;
        }

        // Check if username is already taken by another user
        var existing = await _userRepo.GetByUsernameAsync(username);
        if (existing != null && (_editingUser == null || existing.Id != _editingUser.Id))
        {
            _lblError.Text = "❌ Bu kullanıcı adı zaten başka bir kullanıcıda kayıtlı!";
            _txtUsername.Focus();
            return;
        }

        if (_editingUser == null)
        {
            // Yeni kullanıcı - şifre zorunlu
            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                _lblError.Text = "⚠️ Yeni kullanıcı için en az 4 karakterli şifre giriniz.";
                _txtPassword.Focus();
                return;
            }

            var (hash, salt) = PasswordSecurity.HashPassword(password);
            var newUser = new User
            {
                Username = username,
                FullName = fullName,
                Role = role,
                TeacherId = teacherId,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = _chkIsActive.Checked,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await _userRepo.AddAsync(newUser);
            MessageBox.Show("Yeni kullanıcı başarıyla oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            // Mevcut kullanıcı güncelleme
            _editingUser.Username = username;
            _editingUser.FullName = fullName;
            _editingUser.Role = role;
            _editingUser.TeacherId = teacherId;
            _editingUser.IsActive = _chkIsActive.Checked;

            await _userRepo.UpdateAsync(_editingUser);

            if (!string.IsNullOrWhiteSpace(password))
            {
                if (password.Length < 4)
                {
                    _lblError.Text = "⚠️ Şifre en az 4 karakter olmalıdır.";
                    _txtPassword.Focus();
                    return;
                }
                await _userRepo.ChangePasswordAsync(_editingUser.Id, password);
            }

            MessageBox.Show("Kullanıcı bilgileri başarıyla güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
