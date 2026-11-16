using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class AdminPasswordPromptDialog : Form
{
    private readonly IUserRepository _userRepo;
    private TextBox _txtPassword = null!;
    private Label _lblError = null!;
    private Button _btnConfirm = null!;
    private Button _btnCancel = null!;
    private Button _btnTogglePassword = null!;
    private bool _isPasswordVisible = false;

    public AdminPasswordPromptDialog(IUserRepository userRepo)
    {
        _userRepo = userRepo;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "🔐 Yönetici Doğrulaması - Veri Dışa Aktarımı";
        Size = new Size(490, 370);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        // Header Panel (MEB Navy Blue #1E3A8A)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = Color.FromArgb(30, 58, 138), // #1E3A8A
            Padding = new Padding(20, 14, 20, 14)
        };

        var lblIcon = new Label
        {
            Text = "🛡️",
            Font = new Font("Segoe UI", 22f),
            ForeColor = Color.FromArgb(245, 158, 11), // amber-500
            Location = new Point(16, 12),
            Size = new Size(44, 48)
        };

        var lblTitle = new Label
        {
            Text = "Yönetici Doğrulaması Gerekli",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(66, 13),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Hassas Kişisel Veri ve Veritabanı Dışa Aktarımı",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(68, 40),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange([lblIcon, lblTitle, lblSubtitle]);
        Controls.Add(pnlHeader);

        // Body Panel
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 18)
        };

        var lblWarning = new Label
        {
            Text = "⚠️ Bu işlem tüm öğrenci T.C. kimlik numaraları, banka IBAN bilgileri, veli iletişimleri ve öğretmen verilerini Excel'e aktaracaktır.\n\nKVKK ve veri güvenliği gereği lütfen yönetici şifrenizi giriniz:",
            Font = new Font("Segoe UI", 9.2f),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(24, 20),
            Size = new Size(426, 68)
        };

        var lblPassTitle = new Label
        {
            Text = "Yönetici Şifresi:",
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 98),
            AutoSize = true
        };

        _txtPassword = new TextBox
        {
            Location = new Point(24, 124),
            Width = 345,
            Height = 30,
            Font = new Font("Segoe UI", 11f),
            UseSystemPasswordChar = true
        };
        _txtPassword.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = VerifyAndProceedAsync();
            }
        };

        _btnTogglePassword = new Button
        {
            Text = "Göster",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Size = new Size(72, 30),
            Location = new Point(377, 123),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            Cursor = Cursors.Hand
        };
        _btnTogglePassword.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnTogglePassword.Click += (s, e) =>
        {
            _isPasswordVisible = !_isPasswordVisible;
            _txtPassword.UseSystemPasswordChar = !_isPasswordVisible;
            _btnTogglePassword.Text = _isPasswordVisible ? "Gizle" : "Göster";
        };

        _lblError = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38), // red-600
            Location = new Point(24, 162),
            Size = new Size(426, 22),
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Bottom Button Bar
        var pnlButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(24, 192),
            Size = new Size(426, 42),
            BackColor = Color.Transparent
        };

        _btnConfirm = new Button
        {
            Text = "🔓 Doğrula ve İndir",
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            BackColor = Color.FromArgb(5, 150, 105), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(160, 36),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 0)
        };
        _btnConfirm.FlatAppearance.BorderSize = 0;
        _btnConfirm.Click += (s, e) => _ = VerifyAndProceedAsync();

        _btnCancel = new Button
        {
            Text = "İptal",
            Font = new Font("Segoe UI", 9.2f),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(84, 36),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        pnlButtons.Controls.AddRange([_btnConfirm, _btnCancel]);

        pnlBody.Controls.AddRange([lblWarning, lblPassTitle, _txtPassword, _btnTogglePassword, _lblError, pnlButtons]);
        Controls.Add(pnlBody);

        AcceptButton = _btnConfirm;
        CancelButton = _btnCancel;

        Shown += (s, e) => _txtPassword.Focus();
    }

    private async Task VerifyAndProceedAsync()
    {
        _lblError.Text = "";
        string entered = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(entered))
        {
            _lblError.Text = "❌ Lütfen yönetici şifresini giriniz.";
            _txtPassword.Focus();
            return;
        }

        _btnConfirm.Enabled = false;
        _btnConfirm.Text = "Doğrulanıyor...";

        try
        {
            bool isVerified = false;

            // 1. Eğer oturumdaki kullanıcı admin ise öncelikle onun şifresini kontrol et
            if (CurrentUserSession.CurrentUser != null && CurrentUserSession.IsAdmin)
            {
                var fresh = await _userRepo.GetByIdAsync(CurrentUserSession.CurrentUser.Id);
                if (fresh != null && PasswordSecurity.VerifyPassword(entered, fresh.PasswordHash, fresh.PasswordSalt))
                {
                    isVerified = true;
                }
            }

            // 2. Eğer henüz doğrulanmadıysa, veritabanındaki aktif yöneticilerden herhangi birinin şifresiyle eşleşiyor mu kontrol et
            if (!isVerified)
            {
                var allUsers = await _userRepo.GetAllAsync();
                var admins = allUsers.Where(u => u.IsAdmin && u.IsActive).ToList();
                foreach (var admin in admins)
                {
                    if (PasswordSecurity.VerifyPassword(entered, admin.PasswordHash, admin.PasswordSalt))
                    {
                        isVerified = true;
                        break;
                    }
                }
            }

            if (isVerified)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _lblError.Text = "❌ Hatalı yönetici şifresi girdiniz!";
                _txtPassword.SelectAll();
                _txtPassword.Focus();
            }
        }
        catch (Exception ex)
        {
            _lblError.Text = $"Doğrulama hatası: {ex.Message}";
        }
        finally
        {
            _btnConfirm.Enabled = true;
            _btnConfirm.Text = "🔓 Doğrula ve İndir";
        }
    }
}
