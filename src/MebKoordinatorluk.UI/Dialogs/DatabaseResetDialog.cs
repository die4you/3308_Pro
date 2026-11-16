using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class DatabaseResetDialog : Form
{
    private readonly IDatabaseBackupService _backupService;
    private readonly IUserRepository? _userRepo;

    private RadioButton _rbOperational = null!;
    private RadioButton _rbFull = null!;
    private TextBox _txtPassword = null!;
    private TextBox _txtConfirmWord = null!;
    private Label _lblError = null!;
    private Button _btnReset = null!;
    private Button _btnCancel = null!;
    private Button _btnTogglePassword = null!;
    private bool _isPasswordVisible = false;

    public string? BackupCreatedPath { get; private set; }

    public DatabaseResetDialog(IDatabaseBackupService backupService, IUserRepository? userRepo = null)
    {
        _backupService = backupService;
        _userRepo = userRepo;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "⚠️ Veritabanını Sıfırla ve Temizle";
        Size = new Size(580, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        // 1. Üst Kırmızı Başlık Paneli (Crimson Red #991B1B)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.FromArgb(153, 27, 27),
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblIcon = new Label
        {
            Text = "⚠️",
            Font = new Font("Segoe UI Emoji", 22f),
            ForeColor = Color.FromArgb(254, 202, 202),
            Location = new Point(16, 12),
            Size = new Size(44, 46),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblTitle = new Label
        {
            Text = "Veritabanını Sıfırla ve Temizle",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(68, 12),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Kritik Sistem İşlemi — İşlem öncesi otomatik güvenlik yedeği alınır",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(254, 202, 202),
            Location = new Point(70, 38),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange([lblIcon, lblTitle, lblSubtitle]);
        Controls.Add(pnlHeader);

        // 2. Gövde Paneli
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16)
        };

        int curY = 12;

        // Uyarı Kutusu
        var pnlWarning = new Panel
        {
            Location = new Point(24, curY),
            Size = new Size(516, 60),
            BackColor = Color.FromArgb(254, 242, 242)
        };
        pnlWarning.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(252, 165, 165), 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlWarning.Width - 1, pnlWarning.Height - 1);
        };

        var lblWarnText = new Label
        {
            Text = "🛡️ GÜVENLİK ÖNLEMİ:\nİşlem başlamadan önce tüm veritabanınız otomatik olarak yedeklenecektir. Yanlışlıkla silme durumunda dahi verileriniz Yedekler klasöründen geri yüklenebilir.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(153, 27, 27),
            Location = new Point(10, 8),
            Size = new Size(496, 44)
        };
        pnlWarning.Controls.Add(lblWarnText);
        pnlBody.Controls.Add(pnlWarning);
        curY += 72;

        // Sıfırlama Kapsamı Seçimi
        var lblScopeTitle = new Label
        {
            Text = "Sıfırlama Kapsamını Seçiniz:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, curY),
            AutoSize = true
        };
        pnlBody.Controls.Add(lblScopeTitle);
        curY += 24;

        _rbOperational = new RadioButton
        {
            Text = "Operasyonel Verileri Temizle (Önerilen)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(32, curY),
            Size = new Size(500, 22),
            Checked = true
        };
        pnlBody.Controls.Add(_rbOperational);
        curY += 22;

        var lblOpDesc = new Label
        {
            Text = "Öğrenciler, işletmeler, öğretmenler, sınıflar, alanlar, devamsızlıklar, ücret ve evrak kayıtları silinir. Kurum ayarları ve kullanıcı hesapları KORUNUR.",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(54, curY),
            Size = new Size(480, 28)
        };
        pnlBody.Controls.Add(lblOpDesc);
        curY += 34;

        _rbFull = new RadioButton
        {
            Text = "Tam Fabrika Sıfırlaması (Tüm Veritabanı)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 28, 28),
            Location = new Point(32, curY),
            Size = new Size(500, 22)
        };
        pnlBody.Controls.Add(_rbFull);
        curY += 22;

        var lblFullDesc = new Label
        {
            Text = "Kullanıcı hesapları ve kurum ayarları dahil veritabanındaki tüm veriler sıfırlanır (Yalnızca varsayılan 'admin / admin123' hesabı kalır).",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(185, 28, 28),
            Location = new Point(54, curY),
            Size = new Size(480, 28)
        };
        pnlBody.Controls.Add(lblFullDesc);
        curY += 38;

        // Yönetici Şifresi
        var lblPass = new Label
        {
            Text = "Yönetici (Admin) Şifrenizi Giriniz:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, curY),
            AutoSize = true
        };
        pnlBody.Controls.Add(lblPass);
        curY += 20;

        _txtPassword = new TextBox
        {
            Location = new Point(24, curY),
            Size = new Size(468, 26),
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtPassword.TextChanged += (s, e) => ValidateInputs();
        pnlBody.Controls.Add(_txtPassword);

        _btnTogglePassword = new Button
        {
            Text = "👁️",
            Location = new Point(498, curY - 1),
            Size = new Size(42, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            Cursor = Cursors.Hand
        };
        _btnTogglePassword.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnTogglePassword.Click += (s, e) =>
        {
            _isPasswordVisible = !_isPasswordVisible;
            _txtPassword.UseSystemPasswordChar = !_isPasswordVisible;
            _btnTogglePassword.Text = _isPasswordVisible ? "🔒" : "👁️";
        };
        pnlBody.Controls.Add(_btnTogglePassword);
        curY += 36;

        // Onay Kelimesi (SIFIRLA)
        var lblConfirm = new Label
        {
            Text = "Onaylamak için aşağıdaki kutucuğa SIFIRLA yazınız:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, curY),
            AutoSize = true
        };
        pnlBody.Controls.Add(lblConfirm);
        curY += 20;

        _txtConfirmWord = new TextBox
        {
            Location = new Point(24, curY),
            Size = new Size(516, 26),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 28, 28)
        };
        _txtConfirmWord.TextChanged += (s, e) => ValidateInputs();
        pnlBody.Controls.Add(_txtConfirmWord);
        curY += 32;

        // Hata Bildirim Etiketi
        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, curY),
            Size = new Size(516, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlBody.Controls.Add(_lblError);

        // 3. Alt Buton Paneli
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width - 1, 0);
        };

        _btnCancel = new Button
        {
            Text = "İptal",
            Size = new Size(110, 36),
            Location = new Point(280, 11),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        _btnReset = new Button
        {
            Text = "⚠️ Veritabanını Sıfırla",
            Size = new Size(170, 36),
            Location = new Point(400, 11),
            BackColor = Color.FromArgb(220, 38, 38), // red-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnReset.FlatAppearance.BorderSize = 0;
        _btnReset.Click += async (s, e) => await ExecuteResetAsync();

        pnlBottom.Controls.AddRange([_btnCancel, _btnReset]);
        Controls.Add(pnlBody);
        Controls.Add(pnlBottom);

        CancelButton = _btnCancel;
        Shown += (s, e) => _txtPassword.Focus();
    }

    private void ValidateInputs()
    {
        bool hasPassword = !string.IsNullOrWhiteSpace(_txtPassword.Text);
        bool isConfirmValid = string.Equals(_txtConfirmWord.Text.Trim(), "SIFIRLA", StringComparison.OrdinalIgnoreCase);

        _btnReset.Enabled = hasPassword && isConfirmValid;
    }

    private async Task ExecuteResetAsync()
    {
        _lblError.Text = "";
        string password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(password))
        {
            _lblError.Text = "❌ Lütfen yönetici şifresini giriniz.";
            _txtPassword.Focus();
            return;
        }

        if (!string.Equals(_txtConfirmWord.Text.Trim(), "SIFIRLA", StringComparison.OrdinalIgnoreCase))
        {
            _lblError.Text = "❌ Onay kutucuğuna 'SIFIRLA' yazmanız gerekmektedir.";
            _txtConfirmWord.Focus();
            return;
        }

        // Yönetici Şifresi Doğrulama
        bool isVerified = false;
        if (_userRepo != null)
        {
            try
            {
                if (CurrentUserSession.CurrentUser != null && CurrentUserSession.IsAdmin)
                {
                    var fresh = await _userRepo.GetByIdAsync(CurrentUserSession.CurrentUser.Id);
                    if (fresh != null && PasswordSecurity.VerifyPassword(password, fresh.PasswordHash, fresh.PasswordSalt))
                    {
                        isVerified = true;
                    }
                }

                if (!isVerified)
                {
                    var allUsers = await _userRepo.GetAllAsync();
                    var admins = allUsers.Where(u => u.IsAdmin && u.IsActive).ToList();
                    foreach (var admin in admins)
                    {
                        if (PasswordSecurity.VerifyPassword(password, admin.PasswordHash, admin.PasswordSalt))
                        {
                            isVerified = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                isVerified = false;
            }
        }
        else
        {
            // Kullanıcı reposu enjekte edilmediyse varsayılan admin şifresini kontrol et
            isVerified = (password == "admin123" || password == "admin");
        }

        if (!isVerified)
        {
            _lblError.Text = "❌ Yönetici şifresi hatalı! İşlem iptal edildi.";
            _txtPassword.SelectAll();
            _txtPassword.Focus();
            return;
        }

        // Son kullanıcı onayı
        var confirmResult = MessageBox.Show(
            "Veritabanı sıfırlama işlemi başlamak üzere!\n\n" +
            "Tüm seçili kayıtlar silinecek ve işlemden önce otomatik güvenlik yedeği alınacaktır.\n\n" +
            "Devam etmek istediğinize kesinlikle emin misiniz?",
            "Son Onay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmResult != DialogResult.Yes)
        {
            return;
        }

        // Sıfırlama işlemi
        try
        {
            _btnReset.Enabled = false;
            _btnCancel.Enabled = false;
            _txtPassword.Enabled = false;
            _txtConfirmWord.Enabled = false;
            _rbOperational.Enabled = false;
            _rbFull.Enabled = false;
            _btnReset.Text = "Yedek Alınıyor ve Sıfırlanıyor...";

            bool keepOperational = _rbOperational.Checked;
            string backupPath = await _backupService.ResetDatabaseAsync(keepUsersAndSettings: keepOperational);
            BackupCreatedPath = backupPath;

            MessageBox.Show(
                "Veritabanı başarıyla sıfırlandı!\n\n" +
                $"🛡️ Güvenlik yedeğiniz şu konuma kaydedildi:\n{backupPath}\n\n" +
                (keepOperational
                    ? "Öğrenci, işletme, öğretmen, devamsızlık ve evrak kayıtları temizlendi. Kurum bilgileriniz ve kullanıcı hesaplarınız korundu."
                    : "Tüm veritabanı fabrika ayarlarına döndürüldü. Varsayılan kullanıcı: admin / admin123") +
                "\n\nDeğişikliklerin ekrana yansıması için veriler güncellenecektir.",
                "Sıfırlama Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Veritabanı sıfırlanırken bir hata oluştu:\n{ex.Message}",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _btnReset.Enabled = true;
            _btnCancel.Enabled = true;
            _txtPassword.Enabled = true;
            _txtConfirmWord.Enabled = true;
            _rbOperational.Enabled = true;
            _rbFull.Enabled = true;
            _btnReset.Text = "⚠️ Veritabanını Sıfırla";
        }
    }
}
