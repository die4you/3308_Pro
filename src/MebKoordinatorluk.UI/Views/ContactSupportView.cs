using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class ContactSupportView : UserControl
{
    private readonly ISettingRepository _settingRepo;
    private readonly ILicenseService _licenseService;
    private readonly Action? _onLicenseUpdated;

    private AppSettings _settings = new();
    private LicenseInfo _licenseInfo = new();

    // UI Controls - Sol Kart (Sistem & Lisans)
    private Label _lblSchoolVal = null!;
    private Label _lblCityVal = null!;
    private Label _lblStatusBadge = null!;
    private Label _lblLicenseDetails = null!;
    private Button _btnActivateLicense = null!;
    private Button _btnCheckUpdates = null!;
    private Button _btnCopyAllInfo = null!;
    private Label _lblUpdateStatus = null!;

    // UI Controls - Sağ Kart (Destek Formu)
    private TextBox _txtName = null!;
    private TextBox _txtContact = null!;
    private ComboBox _cmbSubject = null!;
    private TextBox _txtMessage = null!;
    private Button _btnSendMessage = null!;
    private Button _btnOpenMailClient = null!;
    private Label _lblSendStatus = null!;

    public ContactSupportView(
        ISettingRepository settingRepo,
        ILicenseService licenseService,
        Action? onLicenseUpdated = null)
    {
        _settingRepo = settingRepo;
        _licenseService = licenseService;
        _onLicenseUpdated = onLicenseUpdated;

        InitializeComponent();
        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;
        AutoScroll = true;

        // Ana Konteyner
        var pnlMain = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 20, 24, 24)
        };
        pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 1. Üst Başlık Kartı
        var pnlHeader = CreateHeaderCard();
        pnlMain.Controls.Add(pnlHeader, 0, 0);

        // 2. Orta İki Kolonlu Panel (Sol: Lisans & Sistem, Sağ: Destek & İletişim Formu)
        var pnlColumns = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 16, 0, 16)
        };
        pnlColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
        pnlColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16f)); // Kartlar arası boşluk
        pnlColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        pnlColumns.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var pnlLeft = CreateSystemInfoCard();
        var pnlRight = CreateContactFormCard();

        pnlColumns.Controls.Add(pnlLeft, 0, 0);
        pnlColumns.Controls.Add(pnlRight, 2, 0);
        pnlMain.Controls.Add(pnlColumns, 0, 1);

        // 3. Alt Bilgi / Resmi İletişim Kartı
        var pnlFooter = CreateFooterCard();
        pnlMain.Controls.Add(pnlFooter, 0, 2);

        Controls.Add(pnlMain);
    }

    private Panel CreateHeaderCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = Color.FromArgb(30, 58, 138), // Deep Navy (#1E3A8A)
            Padding = new Padding(20, 16, 20, 16)
        };

        var lblIcon = new Label
        {
            Text = "📞",
            Font = new Font("Segoe UI", 26f),
            ForeColor = Color.White,
            Size = new Size(54, 54),
            Location = new Point(20, 16),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblTitle = new Label
        {
            Text = "İletişim, Lisans & Teknik Destek Merkezi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(84, 18)
        };

        var lblSub = new Label
        {
            Text = "3308 Pro ile ilgili her türlü soru, lisans edinimi, teknik destek ve önerileriniz için doğrudan bize ulaşabilirsiniz.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(224, 231, 255), // indigo-100
            AutoSize = true,
            UseMnemonic = false,
            Location = new Point(86, 48)
        };

        card.Controls.AddRange([lblIcon, lblTitle, lblSub]);
        return card;
    }

    private Panel CreateSystemInfoCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(360, 540),
            BackColor = Color.White,
            Padding = new Padding(24, 20, 24, 20),
            Margin = Padding.Empty
        };
        card.Paint += (s, e) => DrawCardBorder(e.Graphics, card.ClientRectangle);

        var lblTitle = new Label
        {
            Text = "💻 Kurum & Lisans Bilgileri",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 36,
            UseMnemonic = false
        };

        var flp = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        // Okul Adı
        flp.Controls.Add(CreateFieldHeader("Okul / Kurum Adı:"));
        _lblSchoolVal = new Label
        {
            Text = "Yükleniyor...",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 10)
        };
        flp.Controls.Add(_lblSchoolVal);

        // İl / İlçe
        flp.Controls.Add(CreateFieldHeader("İl / İlçe:"));
        _lblCityVal = new Label
        {
            Text = "-",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = UITheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 12)
        };
        flp.Controls.Add(_lblCityVal);

        // Lisans Durumu
        flp.Controls.Add(CreateFieldHeader("Lisans Durumu:"));
        _lblStatusBadge = new Label
        {
            Text = "Kontrol ediliyor...",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(100, 116, 139),
            Padding = new Padding(8, 4, 8, 4),
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 6)
        };
        flp.Controls.Add(_lblStatusBadge);

        _lblLicenseDetails = new Label
        {
            Text = "-",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        flp.Controls.Add(_lblLicenseDetails);

        // Lisans Aktivasyon Butonu (Sadece Demo veya süresi bitmiş sistemlerde görünür)
        _btnActivateLicense = new Button
        {
            Text = "🔑 Tam Sürüm Lisansı Gir",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(240, 32),
            Cursor = Cursors.Hand,
            Visible = false,
            Margin = new Padding(0, 0, 0, 16)
        };
        _btnActivateLicense.FlatAppearance.BorderSize = 0;
        _btnActivateLicense.Click += BtnActivateLicense_Click;
        flp.Controls.Add(_btnActivateLicense);

        // Ayırıcı Çizgi
        var sep = new Panel { Height = 1, Width = 410, BackColor = UITheme.Border, Margin = new Padding(0, 4, 0, 14) };
        flp.Controls.Add(sep);

        // Sürüm ve Güncelleme
        flp.Controls.Add(CreateFieldHeader("Program Sürümü:"));
        var lblVersion = new Label
        {
            Text = "3308 Pro v2.9 (Bağımsız x64 Sürüm)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 8)
        };
        flp.Controls.Add(lblVersion);

        var pnlUpdateBar = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10)
        };
        _btnCheckUpdates = new Button
        {
            Text = "🔄 Güncellemeleri Denetle",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 58, 138),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 30),
            Cursor = Cursors.Hand
        };
        _btnCheckUpdates.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnCheckUpdates.Click += BtnCheckUpdates_Click;

        _btnCopyAllInfo = new Button
        {
            Text = "📋 Tüm Bilgileri Kopyala",
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = UITheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(170, 30),
            Margin = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        _btnCopyAllInfo.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnCopyAllInfo.Click += (s, e) => CopyAllSystemInfo();

        pnlUpdateBar.Controls.AddRange([_btnCheckUpdates, _btnCopyAllInfo]);
        flp.Controls.Add(pnlUpdateBar);

        _lblUpdateStatus = new Label
        {
            Text = "Son kontrol: Bugün (Yazılımınız en güncel sürümdedir)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true
        };
        flp.Controls.Add(_lblUpdateStatus);

        card.Controls.Add(flp);
        card.Controls.Add(lblTitle);
        lblTitle.BringToFront();
        return card;
    }

    private Panel CreateContactFormCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(420, 540),
            BackColor = Color.White,
            Padding = new Padding(24, 20, 24, 20),
            Margin = Padding.Empty
        };
        card.Paint += (s, e) => DrawCardBorder(e.Graphics, card.ClientRectangle);

        var lblTitle = new Label
        {
            Text = "✉️ Doğrudan Destek & Lisans Talebi",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 36,
            UseMnemonic = false
        };

        var pnlForm = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(0, 6, 0, 0)
        };
        pnlForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155f));
        pnlForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // 1. Ad Soyad / Görev
        pnlForm.Controls.Add(CreateFormLabel("Adınız Soyadınız / Görev:"), 0, 0);
        _txtName = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 6, 0, 6) };
        pnlForm.Controls.Add(_txtName, 1, 0);

        // 2. İletişim (Telefon / E-posta)
        pnlForm.Controls.Add(CreateFormLabel("İletişim (Tel / E-posta):"), 0, 1);
        _txtContact = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 6, 0, 6) };
        pnlForm.Controls.Add(_txtContact, 1, 1);

        // 3. Konu
        pnlForm.Controls.Add(CreateFormLabel("Talep / Konu Türü:"), 0, 2);
        _cmbSubject = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f),
            Margin = new Padding(0, 6, 0, 6)
        };
        _cmbSubject.Items.AddRange([
            "🔑 Lisans Satın Alma / Yenileme Talebi",
            "🛠️ Teknik Destek & Hata Bildirimi",
            "📋 Mevzuat & SGK İşlemleri Hakkında Soru",
            "💡 Öneri & Yeni Özellik Talebi",
            "ℹ️ Diğer Konular"
        ]);
        _cmbSubject.SelectedIndex = 0;
        pnlForm.Controls.Add(_cmbSubject, 1, 2);

        // 4. Mesaj
        pnlForm.Controls.Add(CreateFormLabel("Mesajınız / Notunuz:"), 0, 3);
        _txtMessage = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Height = 110,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 9.5f),
            Margin = new Padding(0, 6, 0, 8)
        };
        pnlForm.Controls.Add(_txtMessage, 1, 3);

        // 5. Bilgilendirme Notu (Makine Kodu Arka Planda Ekleniyor)
        var pnlInfo = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 48,
            BackColor = Color.FromArgb(240, 249, 255), // sky-50
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0, 4, 0, 12)
        };
        pnlInfo.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(186, 230, 253), 1); // sky-200
            e.Graphics.DrawRectangle(pen, 0, 0, pnlInfo.Width - 1, pnlInfo.Height - 1);
        };
        var lblAutoHwidNotice = new Label
        {
            Text = "🔒 Güvenli Entegrasyon: Makine Kodunuz (HWID), okul adınız ve sürüm bilgileriniz bu talebe arka planda otomatik olarak eklenecektir.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(3, 105, 161), // sky-700
            Dock = DockStyle.Fill,
            UseMnemonic = false
        };
        pnlInfo.Controls.Add(lblAutoHwidNotice);
        pnlForm.Controls.Add(pnlInfo, 0, 4);
        pnlForm.SetColumnSpan(pnlInfo, 2);

        // 6. Butonlar
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 0)
        };

        _btnSendMessage = new Button
        {
            Text = "🚀 Talebi İlet (Anında Gönder)",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 58, 138),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(200, 36),
            Cursor = Cursors.Hand
        };
        _btnSendMessage.FlatAppearance.BorderSize = 0;
        _btnSendMessage.Click += async (s, e) => await SendSupportMessageAsync();

        _btnOpenMailClient = new Button
        {
            Text = "📧 E-posta Uygulamasıyla Gönder",
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = UITheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(210, 36),
            Margin = new Padding(10, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        _btnOpenMailClient.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnOpenMailClient.Click += (s, e) => OpenDefaultEmailClient();

        pnlButtons.Controls.AddRange([_btnSendMessage, _btnOpenMailClient]);
        pnlForm.Controls.Add(pnlButtons, 0, 5);
        pnlForm.SetColumnSpan(pnlButtons, 2);

        // 7. Durum Bildirimi
        _lblSendStatus = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UITheme.Success,
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        pnlForm.Controls.Add(_lblSendStatus, 0, 6);
        pnlForm.SetColumnSpan(_lblSendStatus, 2);

        card.Controls.Add(pnlForm);
        card.Controls.Add(lblTitle);
        lblTitle.BringToFront();
        return card;
    }

    private Panel CreateFooterCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(20, 14, 20, 14),
            Margin = new Padding(0, 8, 0, 0)
        };
        card.Paint += (s, e) => DrawCardBorder(e.Graphics, card.ClientRectangle);

        var lblFooterText = new Label
        {
            Text = "📧 Resmi Destek & Lisans E-Posta: 3308pro@gmail.com    |    ⏰ Destek Saatleri: Hafta İçi 08:30 – 18:00\n" +
                   "⚖️ 3308 Sayılı Mesleki Eğitim Kanunu ve Millî Eğitim Bakanlığı Ortaöğretim Kurumları Yönetmeliği Uyumlu Resmi Koordinatörlük Sistemi",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleCenter
        };

        card.Controls.Add(lblFooterText);
        return card;
    }

    private static Label CreateFieldHeader(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139), // slate-500
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(0, 0, 0, 2)
        };
    }

    private static Label CreateFormLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            UseMnemonic = false,
            Margin = new Padding(0, 8, 8, 0)
        };
    }

    private static void DrawCardBorder(Graphics g, Rectangle rect)
    {
        using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
        g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
    }

    public async Task LoadDataAsync()
    {
        try
        {
            _settings = await _settingRepo.GetAppSettingsAsync();
            _licenseInfo = await _licenseService.RefreshLicenseStatusAsync();

            _lblSchoolVal.Text = !string.IsNullOrWhiteSpace(_settings.SchoolName)
                ? _settings.SchoolName
                : (!string.IsNullOrWhiteSpace(_licenseInfo.SchoolName) ? _licenseInfo.SchoolName : "Kurum Adı Girilmemiş");

            _lblCityVal.Text = !string.IsNullOrWhiteSpace(_settings.DistrictAndCity)
                ? _settings.DistrictAndCity
                : (!string.IsNullOrWhiteSpace(_licenseInfo.CityDistrict) ? _licenseInfo.CityDistrict : "-");

            if (_licenseInfo.IsActive)
            {
                _lblStatusBadge.Text = " ✔️ TAM SÜRÜM (LİSANSLI) ";
                _lblStatusBadge.BackColor = Color.FromArgb(16, 185, 129); // emerald-500
                _lblLicenseDetails.Text = $"Bitiş Tarihi: {_licenseInfo.EndDate:dd.MM.yyyy} ({_licenseInfo.RemainingDays} gün kaldı)";
                _btnActivateLicense.Visible = false; // Lisanslı sistemde bu butona kesinlikle gerek yok, gizlendi.
            }
            else
            {
                _lblStatusBadge.Text = " ⏳ DENEME SÜRÜMÜ (DEMO) ";
                _lblStatusBadge.BackColor = Color.FromArgb(245, 158, 11); // amber-500
                _lblLicenseDetails.Text = "Kısıtlama: En fazla 5 öğrenci / 3 işletme kaydı yapılabilir.";
                _btnActivateLicense.Visible = true;
                _btnActivateLicense.Text = "🔑 Tam Sürüm Lisansı Gir";
                _btnActivateLicense.BackColor = Color.FromArgb(16, 185, 129);
            }
        }
        catch (Exception ex)
        {
            _lblSchoolVal.Text = "Bilgiler alınamadı: " + ex.Message;
        }
    }

    private async Task SendSupportMessageAsync()
    {
        string name = _txtName.Text.Trim();
        string contact = _txtContact.Text.Trim();
        string subject = _cmbSubject.SelectedItem?.ToString() ?? "Destek Talebi";
        string message = _txtMessage.Text.Trim();

        if (string.IsNullOrWhiteSpace(contact))
        {
            MessageBox.Show(
                "Lütfen size ulaşabilmemiz için telefon numaranızı veya e-posta adresinizi giriniz.",
                "İletişim Bilgisi Eksik",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _txtContact.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            MessageBox.Show(
                "Lütfen iletmek istediğiniz mesajınızı yazınız.",
                "Mesaj Eksik",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _txtMessage.Focus();
            return;
        }

        _btnSendMessage.Enabled = false;
        _btnSendMessage.Text = "⏳ Gönderiliyor...";
        _lblSendStatus.Text = string.Empty;

        try
        {
            var (success, resultMsg) = await LicenseHeartbeatService.SendSupportMessageAsync(
                name,
                contact,
                subject,
                message,
                _licenseService.MachineCode,
                _lblSchoolVal.Text,
                _lblCityVal.Text,
                _licenseInfo.IsActive ? "Lisanslı" : "Demo");

            if (success)
            {
                _lblSendStatus.ForeColor = Color.FromArgb(16, 185, 129);
                _lblSendStatus.Text = "✔️ Talebiniz başarıyla iletildi. En kısa sürede sizinle irtibata geçilecektir.";
                MessageBox.Show(
                    "Talebiniz başarıyla iletildi!\n\nMakine Kodunuz (HWID) ve kurum bilgileriniz mesajınıza otomatik olarak eklenmiştir.",
                    "Talep İletildi — 3308 Pro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _txtMessage.Clear();
            }
            else
            {
                _lblSendStatus.ForeColor = Color.FromArgb(220, 38, 38);
                _lblSendStatus.Text = "⚠️ Mesaj iletilemedi. Lütfen doğrudan e-posta (3308pro@gmail.com) ile iletişime geçiniz.";
            }
        }
        catch (Exception ex)
        {
            _lblSendStatus.ForeColor = Color.FromArgb(220, 38, 38);
            _lblSendStatus.Text = "Hata: " + ex.Message;
        }
        finally
        {
            _btnSendMessage.Enabled = true;
            _btnSendMessage.Text = "🚀 Talebi İlet (Anında Gönder)";
        }
    }

    private void OpenDefaultEmailClient()
    {
        try
        {
            string hwid = _licenseService.MachineCode;
            string school = _lblSchoolVal.Text;
            string city = _lblCityVal.Text;
            string subjectText = _cmbSubject.SelectedItem?.ToString() ?? "3308 Pro Destek Talebi";
            string userMsg = _txtMessage.Text.Trim();

            string email = "3308pro@gmail.com";
            string subject = Uri.EscapeDataString($"[3308 Pro Destek] {school} - {hwid}");
            string body = Uri.EscapeDataString(
                $"Merhaba,\n\n" +
                $"Okul / Kurum: {school}\n" +
                $"İl / İlçe: {city}\n" +
                $"Makine Kodu (HWID): {hwid}\n" +
                $"Sürüm: 3308 Pro v2.9\n" +
                $"Lisans Durumu: {(_licenseInfo.IsActive ? "Tam Sürüm (Lisanslı)" : "Deneme Sürümü (Demo)")}\n" +
                $"Konu: {subjectText}\n\n" +
                $"Mesajım:\n{userMsg}\n\n" +
                $"İletişim Bilgilerim: {_txtName.Text.Trim()} / {_txtContact.Text.Trim()}\n");

            var psi = new ProcessStartInfo
            {
                FileName = $"mailto:{email}?subject={subject}&body={body}",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            MessageBox.Show(
                $"Varsayılan e-posta istemcisi açılamadı.\nLütfen doğrudan '3308pro@gmail.com' adresine e-posta gönderiniz.\n\nMakine Kodunuz: {_licenseService.MachineCode}",
                "E-posta Uygulaması Açılamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void BtnActivateLicense_Click(object? sender, EventArgs e)
    {
        if (_licenseInfo.IsActive && _licenseInfo.RemainingDays > 30)
        {
            MessageBox.Show(
                $"Yazılımınız lisanslıdır ve {_licenseInfo.EndDate:dd.MM.yyyy} tarihine kadar ({_licenseInfo.RemainingDays} gün) geçerlidir.\n\nLisans yenileme ve yeni anahtar girişi son 1 ay (30 gün) kala kullanıma açılacaktır.",
                "Lisans Aktif ve Geçerli — 3308 Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dlg = new LicenseActivationDialog(_licenseService, async () =>
        {
            await LoadDataAsync();
            _onLicenseUpdated?.Invoke();
        });
        dlg.ShowDialog(this);
    }

    private async void BtnCheckUpdates_Click(object? sender, EventArgs e)
    {
        // 🔒 Güncelleme Lisans Kısıtlaması: Yalnızca geçerli ve aktif tam sürüm lisansı olanlar güncelleme alabilir!
        if (!_licenseInfo.IsActive)
        {
            MessageBox.Show(
                "3308 Pro Güncelleme Kısıtlaması:\n\n" +
                "Yazılım güncellemeleri, mevzuat değişiklikleri ve yeni sürüm özellikleri yalnızca aktif lisansı bulunan kurumlara sunulmaktadır.\n\n" +
                "En yeni sürüme geçmek ve güncellemelerden yararlanabilmek için lütfen yazılım lisansınızı etkinleştiriniz veya geliştirici ile iletişime geçiniz.",
                "Lisans Gerekli — 3308 Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _btnCheckUpdates.Enabled = false;
        _btnCheckUpdates.Text = "⏳ Denetleniyor...";

        var updateService = new MebKoordinatorluk.Core.Services.GitHubUpdateService();

        try
        {
            var result = await updateService.CheckForUpdatesAsync();

            _btnCheckUpdates.Enabled = true;
            _btnCheckUpdates.Text = "🔄 Güncellemeleri Denetle";

            if (result.Success)
            {
                if (result.IsUpdateAvailable)
                {
                    _lblUpdateStatus.Text = $"Son kontrol: {DateTime.Now:HH:mm} (Yeni sürüm mevcut: v{result.LatestVersion})";
                    using var dlg = new MebKoordinatorluk.UI.Dialogs.UpdateAvailableDialog(result, updateService, _licenseInfo.IsActive);
                    dlg.ShowDialog(this);
                }
                else
                {
                    _lblUpdateStatus.Text = $"Son kontrol: {DateTime.Now:HH:mm} (Yazılımınız en güncel sürümdedir: v{result.CurrentVersion})";
                    MessageBox.Show(
                        $"3308 Pro şu anda en güncel sürümündedir (v{result.CurrentVersion}).\n\nYeni bir güncelleme yayınlandığında sistem sizi otomatik olarak bilgilendirecektir.",
                        "Yazılım Güncel — 3308 Pro",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            else
            {
                _lblUpdateStatus.Text = $"Son kontrol: {DateTime.Now:HH:mm} (Sunucuya bağlanılamadı)";
                MessageBox.Show(
                    $"Güncelleme sunucusuna bağlanılamadı:\n{result.ErrorMessage}\n\nİnternet bağlantınızı kontrol ediniz.",
                    "Güncelleme Kontrolü",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _btnCheckUpdates.Enabled = true;
            _btnCheckUpdates.Text = "🔄 Güncellemeleri Denetle";
            _lblUpdateStatus.Text = $"Son kontrol: {DateTime.Now:HH:mm} (Hata oluştu)";
            MessageBox.Show($"Güncelleme kontrolü sırasında beklenmeyen hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyToClipboard(string text, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            Clipboard.SetText(text);
            MessageBox.Show(successMessage, "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { }
    }

    private void CopyAllSystemInfo()
    {
        string info = $"--- 3308 PRO SİSTEM & LİSANS BİLGİSİ ---\n" +
                      $"Okul / Kurum: {_lblSchoolVal.Text}\n" +
                      $"İl / İlçe: {_lblCityVal.Text}\n" +
                      $"Makine Kodu (HWID): {_licenseService.MachineCode}\n" +
                      $"Lisans Durumu: {(_licenseInfo.IsActive ? $"Lisanslı (Bitiş: {_licenseInfo.EndDate:dd.MM.yyyy})" : "Deneme Sürümü (Demo)")}\n" +
                      $"Sürüm: 3308 Pro v2.9\n" +
                      $"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}";

        CopyToClipboard(info, "Tüm sistem ve lisans bilgileri panoya kopyalandı!\nE-posta veya WhatsApp üzerinden kolayca paylaşabilirsiniz.");
    }
}
