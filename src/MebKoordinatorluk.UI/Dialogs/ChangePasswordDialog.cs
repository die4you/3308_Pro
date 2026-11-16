using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class ChangePasswordDialog : Form
{
    private readonly IUserRepository _userRepo;
    private readonly User _user;

    private TextBox _txtCurrentPassword = null!;
    private TextBox _txtNewPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Label _lblError = null!;
    private Button _btnSave = null!;

    public ChangePasswordDialog(IUserRepository userRepo, User user)
    {
        _userRepo = userRepo;
        _user = user;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Şifre Değiştir";
        Size = new Size(420, 360);
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
            Height = 60,
            BackColor = Color.FromArgb(30, 58, 138),
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = $"🔑 Şifre Değiştir: {_user.FullName} ({_user.Username})",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblTitle);
        Controls.Add(pnlHeader);

        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16)
        };

        int y = 14;

        var lblCur = new Label { Text = "Mevcut Şifre *", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;
        _txtCurrentPassword = new TextBox { Location = new Point(24, y), Width = 356, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10f) };
        y += 34;

        var lblNew = new Label { Text = "Yeni Şifre * (En az 4 karakter)", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;
        _txtNewPassword = new TextBox { Location = new Point(24, y), Width = 356, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10f) };
        y += 34;

        var lblConf = new Label { Text = "Yeni Şifre (Tekrar) *", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;
        _txtConfirmPassword = new TextBox { Location = new Point(24, y), Width = 356, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10f) };
        y += 34;

        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, y),
            Size = new Size(356, 20)
        };
        y += 24;

        var pnlButtons = new FlowLayoutPanel
        {
            Location = new Point(24, y),
            Size = new Size(356, 40),
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnSave = new Button
        {
            Text = "💾 Şifreyi Güncelle",
            Size = new Size(150, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SavePasswordAsync();

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
            lblCur, _txtCurrentPassword,
            lblNew, _txtNewPassword,
            lblConf, _txtConfirmPassword,
            _lblError, pnlButtons
        ]);

        Controls.Add(pnlBody);
    }

    private async Task SavePasswordAsync()
    {
        _lblError.Text = "";
        var curPass = _txtCurrentPassword.Text;
        var newPass = _txtNewPassword.Text;
        var confPass = _txtConfirmPassword.Text;

        // Verify current password
        var freshUser = await _userRepo.GetByIdAsync(_user.Id);
        if (freshUser == null)
        {
            _lblError.Text = "Kullanıcı bilgisi bulunamadı.";
            return;
        }

        if (!PasswordSecurity.VerifyPassword(curPass, freshUser.PasswordHash, freshUser.PasswordSalt))
        {
            _lblError.Text = "❌ Mevcut şifreniz hatalı!";
            _txtCurrentPassword.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < 4)
        {
            _lblError.Text = "⚠️ Yeni şifre en az 4 karakter olmalıdır.";
            _txtNewPassword.Focus();
            return;
        }

        if (newPass != confPass)
        {
            _lblError.Text = "⚠️ Yeni şifreler birbiriyle eşleşmiyor!";
            _txtConfirmPassword.Focus();
            return;
        }

        try
        {
            await _userRepo.ChangePasswordAsync(_user.Id, newPass);
            MessageBox.Show("Şifreniz başarıyla güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblError.Text = $"Hata: {ex.Message}";
        }
    }
}
