using System;
using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.Keygen;

public class KeygenForm : Form
{
    private TextBox _txtHwid = null!;
    private TextBox _txtSchoolName = null!;
    private TextBox _txtCityDistrict = null!;
    private ComboBox _cmbDuration = null!;
    private DateTimePicker _dtpEndDate = null!;
    private TextBox _txtNote = null!;
    private Button _btnGenerate = null!;
    private TextBox _txtResult = null!;
    private Button _btnCopy = null!;
    private Label _lblStatus = null!;

    public KeygenForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "3308 Pro - Lisans Anahtarı Üretim Aracı (TRGY CYMZ)";
        try
        {
            if (System.IO.File.Exists("app.ico"))
                Icon = new Icon("app.ico");
            else if (!string.IsNullOrEmpty(Application.ExecutablePath))
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }
        Size = new Size(680, 680);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(30, 58, 138) // Navy / Blue-900
        };

        var lblTitle = new Label
        {
            Text = "🔑 3308 Pro - Lisans Üreteci",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 12),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Okul makine koduna özel süreli RSA dijital lisans anahtarı üretimi ve Google E-Tablo arşivi",
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
            Size = new Size(624, 530),
            BackColor = Color.White
        };
        pnlCard.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
        };

        int y = 16;
        int lblW = 180;
        int inputW = 390;

        // 1. Makine Kodu (HWID)
        var lblHwid = new Label { Text = "Makine Kodu (HWID) *:", Location = new Point(20, y + 3), Size = new Size(lblW, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtHwid = new TextBox { Location = new Point(210, y), Width = inputW, PlaceholderText = "Örn: 3308-A9F1-20BC-48ED-713A" };
        pnlCard.Controls.AddRange([lblHwid, _txtHwid]);
        y += 36;

        // 2. Okul Adı
        var lblSchool = new Label { Text = "Okul / Kurum Adı *:", Location = new Point(20, y + 3), Size = new Size(lblW, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtSchoolName = new TextBox { Location = new Point(210, y), Width = inputW, PlaceholderText = "Örn: Kırkkızlar Mesleki ve Teknik Anadolu Lisesi" };
        pnlCard.Controls.AddRange([lblSchool, _txtSchoolName]);
        y += 36;

        // 3. İl / İlçe
        var lblCity = new Label { Text = "İl / İlçe :", Location = new Point(20, y + 3), Size = new Size(lblW, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtCityDistrict = new TextBox { Location = new Point(210, y), Width = inputW, PlaceholderText = "Örn: Niksar / Tokat" };
        pnlCard.Controls.AddRange([lblCity, _txtCityDistrict]);
        y += 36;

        // 4. Lisans Süresi
        var lblDur = new Label { Text = "Lisans Süresi :", Location = new Point(20, y + 3), Size = new Size(lblW, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbDuration = new ComboBox { Location = new Point(210, y), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbDuration.Items.AddRange([
            "1 Yıl (365 Gün)",
            "Eğitim-Öğretim Yılı Sonu (30 Haziran 2027)",
            "6 Ay (180 Gün)",
            "3 Ay (90 Gün)",
            "1 Ay (30 Gün)",
            "Özel Tarih Seç"
        ]);
        _cmbDuration.SelectedIndex = 0;

        _dtpEndDate = new DateTimePicker
        {
            Location = new Point(440, y),
            Width = 160,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today.AddDays(365)
        };

        _cmbDuration.SelectedIndexChanged += (s, e) =>
        {
            _dtpEndDate.Value = _cmbDuration.SelectedIndex switch
            {
                0 => DateTime.Today.AddDays(365),
                1 => new DateTime(2027, 6, 30),
                2 => DateTime.Today.AddDays(180),
                3 => DateTime.Today.AddDays(90),
                4 => DateTime.Today.AddDays(30),
                _ => _dtpEndDate.Value
            };
        };

        pnlCard.Controls.AddRange([lblDur, _cmbDuration, _dtpEndDate]);
        y += 36;

        // 5. Not / Ücret
        var lblNote = new Label { Text = "Not / Ücret Durumu :", Location = new Point(20, y + 3), Size = new Size(lblW, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtNote = new TextBox { Location = new Point(210, y), Width = inputW, Text = "Ödendi - 2026/2027 Dönemi", PlaceholderText = "Örn: Ödendi, Havale yapıldı vb." };
        pnlCard.Controls.AddRange([lblNote, _txtNote]);
        y += 45;

        // 6. Üret Butonu
        _btnGenerate = new Button
        {
            Text = "🔑 Lisans Anahtarı Üret ve Google E-Tabloya Kaydet",
            Location = new Point(20, y),
            Size = new Size(580, 42),
            BackColor = Color.FromArgb(37, 99, 235), // Blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnGenerate.FlatAppearance.BorderSize = 0;
        _btnGenerate.Click += BtnGenerate_Click;
        pnlCard.Controls.Add(_btnGenerate);
        y += 52;

        // 7. Durum Bildirimi
        _lblStatus = new Label
        {
            Text = "Lisans üretildiğinde anahtar aşağıda görüntülenecek ve E-Tabloya arşivlenecektir.",
            Location = new Point(20, y),
            Size = new Size(580, 20),
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleCenter
        };
        pnlCard.Controls.Add(_lblStatus);
        y += 24;

        // 8. Sonuç Kutusu
        _txtResult = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(580, 115),
            Multiline = true,
            ReadOnly = true,
            Font = new Font("Consolas", 8.5f),
            BackColor = Color.FromArgb(241, 245, 249),
            ScrollBars = ScrollBars.Vertical
        };
        pnlCard.Controls.Add(_txtResult);
        y += 122;

        // 9. Kopyala Butonu
        _btnCopy = new Button
        {
            Text = "📋 Lisans Anahtarını Kopyala",
            Location = new Point(20, y),
            Size = new Size(580, 36),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _btnCopy.FlatAppearance.BorderSize = 0;
        _btnCopy.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtResult.Text))
            {
                Clipboard.SetText(_txtResult.Text.Trim());
                MessageBox.Show("Lisans anahtarı panoya kopyalandı! Okula iletebilirsiniz.", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        pnlCard.Controls.Add(_btnCopy);

        Controls.Add(pnlCard);
        Controls.Add(pnlHeader);
    }

    private async void BtnGenerate_Click(object? sender, EventArgs e)
    {
        var hwid = _txtHwid.Text.Trim();
        var school = _txtSchoolName.Text.Trim();
        var city = _txtCityDistrict.Text.Trim();
        var note = _txtNote.Text.Trim();
        var startDate = DateTime.Today;
        var endDate = _dtpEndDate.Value.Date;

        if (string.IsNullOrWhiteSpace(hwid))
        {
            MessageBox.Show("Lütfen okuldan aldığınız Makine Kodunu (HWID) giriniz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtHwid.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(school))
        {
            MessageBox.Show("Lütfen Okul / Kurum Adını giriniz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtSchoolName.Focus();
            return;
        }

        if (endDate <= startDate)
        {
            MessageBox.Show("Bitiş tarihi bugünden sonraki bir tarih olmalıdır.", "Tarih Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _btnGenerate.Enabled = false;
            _lblStatus.Text = "⏳ Lisans üretiliyor ve Google E-Tablo'ya gönderiliyor...";
            _lblStatus.ForeColor = Color.FromArgb(37, 99, 235);

            // 1. RSA İmzalı Lisans Anahtarını Üret
            string licenseKey = KeygenGenerator.GenerateLicenseKey(hwid, school, city, startDate, endDate, note);
            _txtResult.Text = licenseKey;
            _btnCopy.Enabled = true;

            // 2. Google E-Tablo'nun 'Uretilen_Lisanslar' Sayfasına Arşivle
            bool synced = await LicenseHeartbeatService.SendLicenseCreatedAsync(
                hwid, school, city, startDate, endDate, licenseKey, note);

            if (synced)
            {
                _lblStatus.Text = "✅ Lisans başarıyla üretildi ve Google E-Tablo'ya arşivlendi!";
                _lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            }
            else
            {
                _lblStatus.Text = "⚠️ Lisans üretildi ancak E-Tablo bağlantısı kurulamadı (İnternet kontrol ediniz).";
                _lblStatus.ForeColor = Color.FromArgb(217, 119, 6);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Lisans üretilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "❌ Hata oluştu!";
            _lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
        }
        finally
        {
            _btnGenerate.Enabled = true;
        }
    }
}
