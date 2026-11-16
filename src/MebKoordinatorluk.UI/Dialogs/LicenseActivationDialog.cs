using System;
using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;

namespace MebKoordinatorluk.UI.Dialogs;

public class LicenseActivationDialog : Form
{
    private readonly ILicenseService _licenseService;
    private readonly Action? _onActivated;

    private Label _lblStatusBadge = null!;
    private Label _lblDetails = null!;
    private TextBox _txtHwid = null!;
    private TextBox _txtLicenseKey = null!;
    private Button _btnActivate = null!;

    public LicenseActivationDialog(ILicenseService licenseService, Action? onActivated = null)
    {
        _licenseService = licenseService;
        _onActivated = onActivated;
        InitializeComponent();
        RefreshStatus();
    }

    private void InitializeComponent()
    {
        Text = "3308 Pro - Lisans ve Aktivasyon";
        Size = new Size(580, 550);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(30, 58, 138)
        };

        var lblTitle = new Label
        {
            Text = "🔑 Yazılım Lisansı ve Aktivasyon",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 14),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Programı sınırsız kullanmak için Makine Kodunu ileterek lisans anahtarınızı giriniz.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(191, 219, 254),
            Location = new Point(22, 40),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblSub]);

        // Card Container
        var pnlCard = new Panel
        {
            Location = new Point(20, 85),
            Size = new Size(524, 410),
            BackColor = Color.White
        };
        pnlCard.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
        };

        int y = 14;

        // 1. Mevcut Lisans Durumu Rozeti
        var lblStatusTitle = new Label
        {
            Text = "Mevcut Lisans Durumu:",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _lblStatusBadge = new Label
        {
            Location = new Point(180, y - 2),
            Size = new Size(320, 24),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlCard.Controls.AddRange([lblStatusTitle, _lblStatusBadge]);
        y += 28;

        _lblDetails = new Label
        {
            Location = new Point(20, y),
            Size = new Size(480, 36),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f)
        };
        pnlCard.Controls.Add(_lblDetails);
        y += 42;

        // Çizgi
        var pnlSep = new Panel { Location = new Point(20, y), Size = new Size(484, 1), BackColor = Color.FromArgb(226, 232, 240) };
        pnlCard.Controls.Add(pnlSep);
        y += 12;

        // 2. Makine Kodu (HWID)
        var lblHwid = new Label
        {
            Text = "1. Bu Bilgisayarın Makine Kodu (Geliştiriciye İletiniz):",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        pnlCard.Controls.Add(lblHwid);
        y += 22;

        _txtHwid = new TextBox
        {
            Text = _licenseService.MachineCode,
            Location = new Point(20, y),
            Width = 350,
            ReadOnly = true,
            Font = new Font("Consolas", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249)
        };

        var btnCopyHwid = new Button
        {
            Text = "📋 Kodu Kopyala",
            Location = new Point(380, y - 1),
            Size = new Size(124, 28),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnCopyHwid.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCopyHwid.Click += (s, e) =>
        {
            Clipboard.SetText(_txtHwid.Text);
            MessageBox.Show("Makine Kodu panoya kopyalandı! Bu kodu yazılım geliştiricinize iletebilirsiniz.", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        pnlCard.Controls.AddRange([_txtHwid, btnCopyHwid]);
        y += 34;

        var lblContact = new Label
        {
            Text = "📧 Lisans Talebi ve İletişim: 3308pro@gmail.com",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(37, 99, 235)
        };
        pnlCard.Controls.Add(lblContact);
        y += 26;

        // 3. Lisans Anahtarı Girişi
        var lblLicKey = new Label
        {
            Text = "2. Size Verilen Lisans Anahtarını Buraya Yapıştırınız:",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        pnlCard.Controls.Add(lblLicKey);
        y += 22;

        _txtLicenseKey = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(484, 80),
            Multiline = true,
            Font = new Font("Consolas", 8.5f),
            PlaceholderText = "LIC-..."
        };
        pnlCard.Controls.Add(_txtLicenseKey);
        y += 88;

        // 4. Etkinleştir Butonu
        _btnActivate = new Button
        {
            Text = "🚀 Lisansı Doğrula ve Etkinleştir",
            Location = new Point(20, y),
            Size = new Size(484, 40),
            BackColor = Color.FromArgb(37, 99, 235), // Blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnActivate.FlatAppearance.BorderSize = 0;
        _btnActivate.Click += BtnActivate_Click;
        pnlCard.Controls.Add(_btnActivate);

        Controls.Add(pnlCard);
        Controls.Add(pnlHeader);
    }

    private void RefreshStatus()
    {
        var lic = _licenseService.CurrentLicense;

        if (lic.IsActive)
        {
            _lblStatusBadge.Text = $"✅ AKTİF LİSANS ({lic.RemainingDays} Gün Kaldı)";
            _lblStatusBadge.ForeColor = Color.FromArgb(5, 150, 105); // Green
            _lblDetails.Text = $"Lisanslanan Kurum: {lic.SchoolName}\nGeçerlilik Bitiş Tarihi: {lic.EndDate:dd.MM.yyyy}";
        }
        else if (lic.Status == LicenseStatus.Expired)
        {
            _lblStatusBadge.Text = "⚠️ LİSANS SÜRESİ DOLDU";
            _lblStatusBadge.ForeColor = Color.FromArgb(220, 38, 38); // Red
            _lblDetails.Text = $"Lisansınızın süresi {lic.EndDate:dd.MM.yyyy} tarihinde sona erdi. Program Demo Moduna geçmiştir (Maksimum 5 öğrenci / 3 işletme).";
        }
        else if (lic.Status == LicenseStatus.TamperedClock)
        {
            _lblStatusBadge.Text = "⛔ TARİH HİLESİ TESPİT EDİLDİ";
            _lblStatusBadge.ForeColor = Color.FromArgb(185, 28, 28);
            _lblDetails.Text = "Bilgisayar sistem saatinin geriye alındığı tespit edildi. Lütfen sistem saatinizi güncelleyiniz.";
        }
        else
        {
            _lblStatusBadge.Text = "ℹ️ DEMO SÜRÜM (LİSANSSIZ)";
            _lblStatusBadge.ForeColor = Color.FromArgb(217, 119, 6); // Amber
            _lblDetails.Text = "Program demo modundadır (Maksimum 5 öğrenci ve 3 işletme kaydı yapılabilir). Sınırsız tam sürüm için lisans anahtarı giriniz.";
        }
    }

    private async void BtnActivate_Click(object? sender, EventArgs e)
    {
        var key = _txtLicenseKey.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Lütfen lisans anahtarınızı yapıştırınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _btnActivate.Enabled = false;
            _btnActivate.Text = "Doğrulanıyor...";

            var result = await _licenseService.ApplyLicenseKeyAsync(key);

            if (result.Success)
            {
                MessageBox.Show(result.Message, "Lisans Etkinleştirildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
                _onActivated?.Invoke();
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(result.Message, "Lisans Doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            _btnActivate.Enabled = true;
            _btnActivate.Text = "🚀 Lisansı Doğrula ve Etkinleştir";
        }
    }
}
