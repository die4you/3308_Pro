using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class LoginDialog : Form
{
    private readonly IUserRepository _userRepo;
    private readonly ISettingRepository _settingRepo;

    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private Button _btnTogglePassword = null!;
    private CheckBox _chkRemember = null!;
    private Button _btnLogin = null!;
    private Label _lblError = null!;
    private Label _lblCapsLock = null!;
    private bool _isPasswordVisible = false;

    // Window Dragging Support
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    public LoginDialog(IUserRepository userRepo, ISettingRepository settingRepo)
    {
        _userRepo = userRepo;
        _settingRepo = settingRepo;

        InitializeComponent();
        LoadRememberedUsernameAsync();
    }

    private void InitializeComponent()
    {
        Text = "Kullanıcı Girişi - 3308 Pro";
        Size = new Size(440, 430);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(248, 250, 252); // slate-50
        Font = new Font("Segoe UI", 9f);

        // 1. Header Panel (Deep Navy Bar #1E3A8A)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = Color.FromArgb(30, 58, 138)
        };
        pnlHeader.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        };

        // Close button
        var btnClose = new Button
        {
            Text = "✕",
            Size = new Size(34, 30),
            Location = new Point(pnlHeader.Width - 40, 8),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(203, 213, 225),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
        btnClose.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var lblIcon = new Label
        {
            Text = "🎓",
            Font = new Font("Segoe UI", 26f),
            ForeColor = Color.White,
            Location = new Point(18, 14),
            Size = new Size(48, 52),
            BackColor = Color.Transparent
        };
        lblIcon.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        };

        var lblAppTitle = new Label
        {
            Text = "3308 PRO",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(70, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblAppTitle.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        };

        var lblSubTitle = new Label
        {
            Text = "Koordinatörlük & Beceri Eğitimi Sistemi",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(191, 219, 254),
            Location = new Point(71, 38),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        var lblBadge = new Label
        {
            Text = "🔐 Personel ve Yönetici Girişi",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(254, 240, 138),
            Location = new Point(71, 58),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        pnlHeader.Controls.AddRange([btnClose, lblIcon, lblAppTitle, lblSubTitle, lblBadge]);

        // 2. Form Body Panel (Option A: Soft Slate-50 background)
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252), // #F8FAFC
            Padding = new Padding(36, 16, 36, 16)
        };

        int y = 14;

        // Kullanıcı Adı
        var lblUser = new Label
        {
            Text = "👤  Kullanıcı Adı",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(36, y),
            AutoSize = true
        };
        y += 20;

        var pnlUserBorder = new Panel
        {
            Location = new Point(36, y),
            Size = new Size(368, 36),
            BackColor = Color.White
        };
        bool userFocused = false;
        pnlUserBorder.Paint += (s, e) =>
        {
            using var pen = new Pen(userFocused ? Color.FromArgb(37, 99, 235) : Color.FromArgb(203, 213, 225), userFocused ? 1.8f : 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlUserBorder.Width - 1, pnlUserBorder.Height - 1);
        };

        _txtUsername = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Location = new Point(10, 8),
            Width = 348,
            Font = new Font("Segoe UI", 10.5f),
            PlaceholderText = "Kullanıcı adınızı girin..."
        };
        _txtUsername.Enter += (s, e) => { userFocused = true; pnlUserBorder.Invalidate(); };
        _txtUsername.Leave += (s, e) => { userFocused = false; pnlUserBorder.Invalidate(); };
        _txtUsername.KeyDown += OnInputKeyDown;
        pnlUserBorder.Click += (s, e) => _txtUsername.Focus();
        pnlUserBorder.Controls.Add(_txtUsername);
        y += 46;

        // Şifre
        var lblPass = new Label
        {
            Text = "🔑  Şifre",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(36, y),
            AutoSize = true
        };
        y += 20;

        var pnlPassBorder = new Panel
        {
            Location = new Point(36, y),
            Size = new Size(368, 36),
            BackColor = Color.White
        };
        bool passFocused = false;
        pnlPassBorder.Paint += (s, e) =>
        {
            using var pen = new Pen(passFocused ? Color.FromArgb(37, 99, 235) : Color.FromArgb(203, 213, 225), passFocused ? 1.8f : 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlPassBorder.Width - 1, pnlPassBorder.Height - 1);
        };

        _txtPassword = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Location = new Point(10, 8),
            Width = 318,
            Font = new Font("Segoe UI", 10.5f),
            UseSystemPasswordChar = true,
            PlaceholderText = "Şifrenizi girin..."
        };
        _txtPassword.Enter += (s, e) => { passFocused = true; pnlPassBorder.Invalidate(); };
        _txtPassword.Leave += (s, e) => { passFocused = false; pnlPassBorder.Invalidate(); };
        _txtPassword.KeyDown += OnInputKeyDown;
        _txtPassword.KeyUp += (s, e) => UpdateCapsLockState();

        _btnTogglePassword = new Button
        {
            Text = "👁️",
            Location = new Point(330, 3),
            Size = new Size(34, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10f)
        };
        _btnTogglePassword.FlatAppearance.BorderSize = 0;
        _btnTogglePassword.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        _btnTogglePassword.Click += (s, e) =>
        {
            _isPasswordVisible = !_isPasswordVisible;
            _txtPassword.UseSystemPasswordChar = !_isPasswordVisible;
            _btnTogglePassword.Text = _isPasswordVisible ? "🙈" : "👁️";
        };

        pnlPassBorder.Click += (s, e) => _txtPassword.Focus();
        pnlPassBorder.Controls.AddRange([_txtPassword, _btnTogglePassword]);
        y += 44;

        // Remember Me & CapsLock Warning
        _chkRemember = new CheckBox
        {
            Text = "Beni Hatırla",
            Location = new Point(36, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Checked = true
        };

        _lblCapsLock = new Label
        {
            Text = "⚠️ Caps Lock Açık",
            ForeColor = Color.FromArgb(217, 119, 6),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(275, y),
            AutoSize = true,
            Visible = false
        };
        y += 28;

        // Error Message
        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(36, y),
            Size = new Size(368, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };
        y += 24;

        // Giriş Yap Button
        _btnLogin = new Button
        {
            Text = "🚀 Sisteme Giriş Yap",
            Location = new Point(36, y),
            Size = new Size(368, 42),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
        _btnLogin.Click += async (s, e) => await AttemptLoginAsync();
        y += 50;

        // Footer / Version note
        var lblFooter = new Label
        {
            Text = "3308 Pro  •  Mesleki Eğitim & Koordinatörlük Yönetimi",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f),
            Location = new Point(36, y),
            Size = new Size(368, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };

        pnlBody.Controls.AddRange([
            lblUser, pnlUserBorder,
            lblPass, pnlPassBorder,
            _chkRemember, _lblCapsLock,
            _lblError, _btnLogin,
            lblFooter
        ]);

        // Docking setup: Body is added, Header is added and sent to back to guarantee correct top-docking without overlap
        Controls.Add(pnlBody);
        Controls.Add(pnlHeader);
        pnlHeader.SendToBack();

        // Draw outer accent border around dialog (Deep Navy #1E3A8A)
        Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(30, 58, 138), 2f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        Shown += (s, e) =>
        {
            UpdateCapsLockState();
            if (string.IsNullOrWhiteSpace(_txtUsername.Text))
                _txtUsername.Focus();
            else
                _txtPassword.Focus();
        };
    }

    private void UpdateCapsLockState()
    {
        _lblCapsLock.Visible = Control.IsKeyLocked(Keys.CapsLock);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        UpdateCapsLockState();
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            _ = AttemptLoginAsync();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private async void LoadRememberedUsernameAsync()
    {
        try
        {
            var remembered = await _settingRepo.GetValueAsync("LastLoginUsername");
            if (!string.IsNullOrWhiteSpace(remembered))
            {
                _txtUsername.Text = remembered.Trim();
                _chkRemember.Checked = true;
                _txtPassword.Focus();
            }
        }
        catch { }
    }

    private async Task AttemptLoginAsync()
    {
        _lblError.Text = "";
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            _lblError.Text = "⚠️ Lütfen kullanıcı adınızı giriniz.";
            _txtUsername.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            _lblError.Text = "⚠️ Lütfen şifrenizi giriniz.";
            _txtPassword.Focus();
            return;
        }

        _btnLogin.Enabled = false;
        _btnLogin.Text = "⏳ Giriş yapılıyor...";

        try
        {
            var user = await _userRepo.AuthenticateAsync(username, password);
            if (user == null)
            {
                _lblError.Text = "❌ Kullanıcı adı veya şifre hatalı!";
                _txtPassword.SelectAll();
                _txtPassword.Focus();
                return;
            }

            if (!user.IsActive)
            {
                _lblError.Text = "⛔ Bu kullanıcı hesabı pasife alınmıştır!";
                return;
            }

            // Başarılı giriş
            CurrentUserSession.CurrentUser = user;
            var appSettings = await _settingRepo.GetAppSettingsAsync();
            CurrentUserSession.TeacherCanEditData = appSettings.TeacherCanEditData;
            await _userRepo.UpdateLastLoginAsync(user.Id);

            // Beni Hatırla kaydı
            if (_chkRemember.Checked)
            {
                await _settingRepo.SetValueAsync("LastLoginUsername", username, "Son giriş yapan kullanıcı adı");
            }
            else
            {
                await _settingRepo.SetValueAsync("LastLoginUsername", "", "Son giriş yapan kullanıcı adı");
            }

            DialogResult = DialogResult.OK;
            Hide();
            Close();
        }
        catch (Exception ex)
        {
            _lblError.Text = $"Hata: {ex.Message}";
        }
        finally
        {
            _btnLogin.Enabled = true;
            _btnLogin.Text = "🚀 Sisteme Giriş Yap";
        }
    }
}
