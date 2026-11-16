using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Views;

public class InsuranceSettingsView : UserControl
{
    private readonly ISettingRepository _settingRepo;
    private AppSettings _settings = new();

    // Controls
    private TextBox _txtSgkSicilNo = null!;
    private TextBox _txtSgkKontrolNo = null!;
    private TextBox _txtSgkAraciNo = null!;
    private TextBox _txtVergiNo = null!;
    private ComboBox _cmbSonSinifBildirge = null!;
    private TextBox _txtSgkNaceKodu = null!;

    private TextBox _txtSgkYetkiliAdSoyad = null!;
    private TextBox _txtSgkYetkiliUnvan = null!;
    private TextBox _txtSgkYetkiliEposta = null!;
    private TextBox _txtSgkYetkiliTelefon = null!;
    private TextBox _txtSgkBildirimNotu = null!;

    // Giriş / Çıkış Parametreleri Kontrolleri
    private TextBox _txtOrgunSigortaKolu = null!;
    private TextBox _txtOrgunOgrenimKodu = null!;
    private TextBox _txtOrgunMeslekKodu = null!;
    private TextBox _txtOrgunGorevKodu = null!;

    private TextBox _txtMesemSigortaKolu = null!;
    private TextBox _txtMesemOgrenimKodu = null!;
    private TextBox _txtMesemMeslekKodu = null!;
    private TextBox _txtMesemGorevKodu = null!;

    private Panel _pnlHeaderContent = null!;
    private Panel _pnlBody = null!;
    private Panel _pnlContainer = null!;

    public InsuranceSettingsView(ISettingRepository settingRepo)
    {
        _settingRepo = settingRepo;
        InitializeComponent();
        LoadSettingsAsync();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Section
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.Transparent
        };

        _pnlHeaderContent = new Panel
        {
            Location = new Point(24, 4),
            Size = new Size(840, 56),
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = "🛡️ Sigorta ve SGK Bilgileri Ayarları",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(0, 0),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Öğrenci SGK bildirimleri, işyeri sicil numarası, aracı ve yetkili bilgileri",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(2, 28),
            AutoSize = true
        };

        _pnlHeaderContent.Controls.AddRange([lblTitle, lblSubtitle]);
        pnlHeader.Controls.Add(_pnlHeaderContent);

        // Main Scrollable Body
        _pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 0, 0, 40)
        };

        _pnlContainer = new Panel
        {
            Location = new Point(24, 8),
            Size = new Size(840, 1110),
            BackColor = Color.Transparent
        };

        // ==========================================
        // 1. SGK BİLGİLERİ KARTI (Turuncu Başlık Bannerlı)
        // ==========================================
        var cardSgkInfo = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(840, 325),
            BackColor = Color.White
        };
        cardSgkInfo.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(245, 158, 11), 1.5f); // Turuncu kenarlık #F59E0B
            e.Graphics.DrawRectangle(pen, 0, 0, cardSgkInfo.Width - 1, cardSgkInfo.Height - 1);
        };

        var pnlOrangeBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(245, 158, 11) // Amber / Orange #F59E0B
        };
        var lblOrangeTitle = new Label
        {
            Text = "✔  SGK Bilgileri",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 7),
            AutoSize = true
        };
        pnlOrangeBanner.Controls.Add(lblOrangeTitle);

        // Rows
        var (row1Panel, txt1) = CreateSgkRow("İşyeri SGK Sicil No", 48);
        _txtSgkSicilNo = txt1;

        var (row2Panel, txt2) = CreateSgkRow("İşyeri SGK Kontrol No", 94);
        _txtSgkKontrolNo = txt2;

        var (row3Panel, txt3) = CreateSgkRow("İşyeri SGK Aracı No", 140);
        _txtSgkAraciNo = txt3;

        var (row4Panel, txt4) = CreateSgkRow("Vergi No", 186);
        _txtVergiNo = txt4;

        var (row5Panel, cmb5) = CreateSgkDropdownRow("Son Sınıf Bildirge Kodu", 232, new[]
        {
            "Son Sınıflar 49/50 Kodunu Kullansın.",
            "Son Sınıflar 07/42 Kodunu Kullansın."
        });
        _cmbSonSinifBildirge = cmb5;

        var (row6Panel, txt6) = CreateSgkRow("NACE Kodu / Faaliyet Alanı", 278);
        _txtSgkNaceKodu = txt6;

        cardSgkInfo.Controls.AddRange([pnlOrangeBanner, row1Panel, row2Panel, row3Panel, row4Panel, row5Panel, row6Panel]);


        // ==========================================
        // 2. SGK İŞE GİRİŞ / ÇIKIŞ PARAMETRELERİ TABLOSU (İndigo Başlık Bannerlı)
        // ==========================================
        var cardEntryExitParams = new Panel
        {
            Location = new Point(0, 340),
            Size = new Size(840, 250),
            BackColor = Color.White
        };
        cardEntryExitParams.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(99, 102, 241), 1.5f); // Indigo kenarlık #6366F1
            e.Graphics.DrawRectangle(pen, 0, 0, cardEntryExitParams.Width - 1, cardEntryExitParams.Height - 1);
        };

        var pnlIndigoBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(79, 70, 229) // Indigo #4F46E5
        };
        var lblIndigoTitle = new Label
        {
            Text = "📋  Giriş / Çıkış Parametreleri",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 7),
            AutoSize = true
        };
        pnlIndigoBanner.Controls.Add(lblIndigoTitle);

        // Header Row
        var pnlTableHeader = new Panel
        {
            Location = new Point(16, 46),
            Size = new Size(808, 34),
            BackColor = Color.FromArgb(241, 245, 249)
        };
        pnlTableHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlTableHeader.Width - 1, pnlTableHeader.Height - 1);
        };

        var lblThParamName = new Label
        {
            Text = "Parametre Tanımı",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(12, 8),
            Width = 230
        };
        var lblThOrgun = new Label
        {
            Text = "🏛️ Örgün Eğitim İşe Giriş Parametresi",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 64, 175),
            Location = new Point(255, 8),
            Width = 265
        };
        var lblThMesem = new Label
        {
            Text = "🏛️ MESEM İşe Giriş Parametresi",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(109, 40, 217),
            Location = new Point(530, 8),
            Width = 265
        };
        pnlTableHeader.Controls.AddRange([lblThParamName, lblThOrgun, lblThMesem]);

        var (rowP1, txtO1, txtM1) = CreateParamTableRow("Sigorta Kolu", 88);
        _txtOrgunSigortaKolu = txtO1;
        _txtMesemSigortaKolu = txtM1;

        var (rowP2, txtO2, txtM2) = CreateParamTableRow("Öğrenim Kodu", 126);
        _txtOrgunOgrenimKodu = txtO2;
        _txtMesemOgrenimKodu = txtM2;

        var (rowP3, txtO3, txtM3) = CreateParamTableRow("Meslek Kodu", 164);
        _txtOrgunMeslekKodu = txtO3;
        _txtMesemMeslekKodu = txtM3;

        var (rowP4, txtO4, txtM4) = CreateParamTableRow("Görev Kodu", 202);
        _txtOrgunGorevKodu = txtO4;
        _txtMesemGorevKodu = txtM4;

        cardEntryExitParams.Controls.AddRange([pnlIndigoBanner, pnlTableHeader, rowP1, rowP2, rowP3, rowP4]);


        // ==========================================
        // 3. SGK YETKİLİ İLETİŞİM KARTI (Mavi Başlık Bannerlı)
        // ==========================================
        var cardAuthor = new Panel
        {
            Location = new Point(0, 605),
            Size = new Size(840, 230),
            BackColor = Color.White
        };
        cardAuthor.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(37, 99, 235), 1.5f); // Mavi kenarlık #2563EB
            e.Graphics.DrawRectangle(pen, 0, 0, cardAuthor.Width - 1, cardAuthor.Height - 1);
        };

        var pnlBlueBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(37, 99, 235) // Blue #2563EB
        };
        var lblBlueTitle = new Label
        {
            Text = "👤  SGK Bildirim Yetkilisi Bilgileri",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 7),
            AutoSize = true
        };
        pnlBlueBanner.Controls.Add(lblBlueTitle);

        var lblAuthorName = CreateFieldLabel("Yetkili Adı Soyadı", 16, 48);
        _txtSgkYetkiliAdSoyad = CreateTextBox(16, 68, 380, "SGK Bildiriminden Sorumlu Müdür Yrd / Personel");

        var lblAuthorTitle = CreateFieldLabel("Yetkili Görevi / Ünvanı", 420, 48);
        _txtSgkYetkiliUnvan = CreateTextBox(420, 68, 380, "Örn: Koordinatör Müdür Yardımcısı");

        var lblAuthorEmail = CreateFieldLabel("İletişim E-Posta Adresi", 16, 116);
        _txtSgkYetkiliEposta = CreateTextBox(16, 136, 380, "ornek@meb.k12.tr");

        var lblAuthorPhone = CreateFieldLabel("İletişim Telefon Numarası", 420, 116);
        _txtSgkYetkiliTelefon = CreateTextBox(420, 136, 380, "0 (212) 000 00 00");

        cardAuthor.Controls.AddRange([pnlBlueBanner, lblAuthorName, _txtSgkYetkiliAdSoyad, lblAuthorTitle, _txtSgkYetkiliUnvan, lblAuthorEmail, _txtSgkYetkiliEposta, lblAuthorPhone, _txtSgkYetkiliTelefon]);


        // ==========================================
        // 4. SİGORTA BİLDİRİM VE MEVZUAT METNİ KARTI (Yeşil Başlık Bannerlı)
        // ==========================================
        var cardNote = new Panel
        {
            Location = new Point(0, 850),
            Size = new Size(840, 180),
            BackColor = Color.White
        };
        cardNote.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(16, 185, 129), 1.5f); // Yeşil kenarlık #10B981
            e.Graphics.DrawRectangle(pen, 0, 0, cardNote.Width - 1, cardNote.Height - 1);
        };

        var pnlGreenBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(16, 185, 129) // Emerald Green #10B981
        };
        var lblGreenTitle = new Label
        {
            Text = "📑  SGK Bildirim ve Mevzuat Metni",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 7),
            AutoSize = true
        };
        pnlGreenBanner.Controls.Add(lblGreenTitle);

        var lblNote = CreateFieldLabel("Evrak & Bildirimlerde Yer Alacak Sigorta Paragrafı", 16, 44);
        _txtSgkBildirimNotu = new TextBox
        {
            Location = new Point(16, 64),
            Width = 784,
            Height = 85,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 9.5f)
        };

        cardNote.Controls.AddRange([pnlGreenBanner, lblNote, _txtSgkBildirimNotu]);


        // ==========================================
        // 5. KAYDET BUTONU PANELİ
        // ==========================================
        var pnlActions = new Panel
        {
            Location = new Point(0, 1045),
            Size = new Size(840, 50),
            BackColor = Color.Transparent
        };

        var btnSave = new Button
        {
            Text = "💾 Sigorta Bilgilerini Kaydet",
            Size = new Size(240, 42),
            Location = new Point(600, 0),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;

        pnlActions.Controls.Add(btnSave);

        _pnlContainer.Controls.AddRange([cardSgkInfo, cardEntryExitParams, cardAuthor, cardNote, pnlActions]);
        _pnlBody.Controls.Add(_pnlContainer);

        Controls.Add(_pnlBody);
        Controls.Add(pnlHeader);

        // Center on Resize
        Resize += (s, e) => AlignCenter();
        _pnlBody.Resize += (s, e) => AlignCenter();
        AlignCenter();
    }

    private static (Panel RowContainer, TextBox InputBox) CreateSgkRow(string labelText, int topY)
    {
        var rowPanel = new Panel
        {
            Location = new Point(16, topY),
            Size = new Size(808, 38),
            BackColor = Color.Transparent
        };

        // Left Gray Label Box
        var lblBox = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(210, 36),
            BackColor = Color.FromArgb(239, 241, 245) // Light gray #EFF1F5
        };
        lblBox.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, lblBox.Width - 1, lblBox.Height - 1);
        };

        var lblText = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        lblBox.Controls.Add(lblText);

        // Right Input TextBox
        var txtInput = new TextBox
        {
            Location = new Point(215, 4),
            Width = 593,
            Font = new Font("Segoe UI", 9.5f)
        };

        rowPanel.Controls.Add(lblBox);
        rowPanel.Controls.Add(txtInput);

        return (rowPanel, txtInput);
    }

    private static (Panel RowContainer, ComboBox Dropdown) CreateSgkDropdownRow(string labelText, int topY, string[] items)
    {
        var rowPanel = new Panel
        {
            Location = new Point(16, topY),
            Size = new Size(808, 38),
            BackColor = Color.Transparent
        };

        // Left Gray Label Box
        var lblBox = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(210, 36),
            BackColor = Color.FromArgb(239, 241, 245) // Light gray #EFF1F5
        };
        lblBox.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, lblBox.Width - 1, lblBox.Height - 1);
        };

        var lblText = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        lblBox.Controls.Add(lblText);

        // Right Dropdown ComboBox
        var cmbInput = new ComboBox
        {
            Location = new Point(215, 4),
            Width = 593,
            Font = new Font("Segoe UI", 9.5f),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbInput.Items.AddRange(items);

        rowPanel.Controls.Add(lblBox);
        rowPanel.Controls.Add(cmbInput);

        return (rowPanel, cmbInput);
    }

    private static (Panel RowPanel, TextBox TxtOrgun, TextBox TxtMesem) CreateParamTableRow(string labelText, int topY)
    {
        var rowPanel = new Panel
        {
            Location = new Point(16, topY),
            Size = new Size(808, 34),
            BackColor = Color.Transparent
        };

        var lblBox = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(245, 32),
            BackColor = Color.FromArgb(248, 250, 252)
        };
        lblBox.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, lblBox.Width - 1, lblBox.Height - 1);
        };
        var lbl = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        lblBox.Controls.Add(lbl);

        var txtOrgun = new TextBox
        {
            Location = new Point(255, 3),
            Width = 265,
            Font = new Font("Segoe UI", 9.5f)
        };

        var txtMesem = new TextBox
        {
            Location = new Point(530, 3),
            Width = 265,
            Font = new Font("Segoe UI", 9.5f)
        };

        rowPanel.Controls.AddRange([lblBox, txtOrgun, txtMesem]);
        return (rowPanel, txtOrgun, txtMesem);
    }

    private void AlignCenter()
    {
        if (_pnlBody == null || _pnlHeaderContent == null || _pnlContainer == null) return;
        int x = Math.Max(24, (_pnlBody.ClientSize.Width - 840) / 2);
        _pnlHeaderContent.Location = new Point(x, 4);
        _pnlContainer.Location = new Point(x, 8);
    }

    private static Label CreateFieldLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(x, y),
            AutoSize = true
        };
    }

    private static TextBox CreateTextBox(int x, int y, int width, string placeholder = "")
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Width = width,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = placeholder
        };
    }

    private async void LoadSettingsAsync()
    {
        try
        {
            _settings = await _settingRepo.GetAppSettingsAsync();

            _txtSgkSicilNo.Text = _settings.SgkSicilNo ?? "";
            _txtSgkKontrolNo.Text = _settings.SgkKontrolNo ?? "90";
            _txtSgkAraciNo.Text = _settings.SgkAraciNo ?? "000";
            _txtVergiNo.Text = _settings.VergiNo ?? "";
            _txtSgkNaceKodu.Text = _settings.SgkNaceKodu ?? "";

            if (!string.IsNullOrWhiteSpace(_settings.SgkSonSinifBildirgeKodu))
            {
                int idx = _cmbSonSinifBildirge.FindString(_settings.SgkSonSinifBildirgeKodu);
                if (idx >= 0) _cmbSonSinifBildirge.SelectedIndex = idx;
                else _cmbSonSinifBildirge.SelectedIndex = 0;
            }
            else
            {
                _cmbSonSinifBildirge.SelectedIndex = 0;
            }

            _txtSgkYetkiliAdSoyad.Text = _settings.SgkYetkiliAdSoyad ?? "";
            _txtSgkYetkiliUnvan.Text = _settings.SgkYetkiliUnvan ?? "";
            _txtSgkYetkiliEposta.Text = _settings.SgkYetkiliEposta ?? "";
            _txtSgkYetkiliTelefon.Text = _settings.SgkYetkiliTelefon ?? "";
            _txtSgkBildirimNotu.Text = _settings.SgkBildirimNotu ?? "";

            // Giriş / Çıkış Parametreleri
            _txtOrgunSigortaKolu.Text = _settings.OrgunSigortaKolu ?? "37";
            _txtOrgunOgrenimKodu.Text = _settings.OrgunOgrenimKodu ?? "3";
            _txtOrgunMeslekKodu.Text = _settings.OrgunMeslekKodu ?? "9901.02";
            _txtOrgunGorevKodu.Text = _settings.OrgunGorevKodu ?? "5";

            _txtMesemSigortaKolu.Text = _settings.MesemSigortaKolu ?? "7";
            _txtMesemOgrenimKodu.Text = _settings.MesemOgrenimKodu ?? "3";
            _txtMesemMeslekKodu.Text = _settings.MesemMeslekKodu ?? "9901.03";
            _txtMesemGorevKodu.Text = _settings.MesemGorevKodu ?? "5";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sigorta ayarları yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            _settings.SgkSicilNo = _txtSgkSicilNo.Text.Trim();
            _settings.SgkKontrolNo = _txtSgkKontrolNo.Text.Trim();
            _settings.SgkAraciNo = _txtSgkAraciNo.Text.Trim();
            _settings.VergiNo = _txtVergiNo.Text.Trim();
            _settings.SgkNaceKodu = _txtSgkNaceKodu.Text.Trim();
            _settings.SgkSonSinifBildirgeKodu = _cmbSonSinifBildirge.SelectedItem?.ToString() ?? _cmbSonSinifBildirge.Text.Trim();

            _settings.SgkYetkiliAdSoyad = _txtSgkYetkiliAdSoyad.Text.Trim();
            _settings.SgkYetkiliUnvan = _txtSgkYetkiliUnvan.Text.Trim();
            _settings.SgkYetkiliEposta = _txtSgkYetkiliEposta.Text.Trim();
            _settings.SgkYetkiliTelefon = _txtSgkYetkiliTelefon.Text.Trim();
            _settings.SgkBildirimNotu = _txtSgkBildirimNotu.Text.Trim();

            // Giriş / Çıkış Parametreleri
            _settings.OrgunSigortaKolu = _txtOrgunSigortaKolu.Text.Trim();
            _settings.OrgunOgrenimKodu = _txtOrgunOgrenimKodu.Text.Trim();
            _settings.OrgunMeslekKodu = _txtOrgunMeslekKodu.Text.Trim();
            _settings.OrgunGorevKodu = _txtOrgunGorevKodu.Text.Trim();

            _settings.MesemSigortaKolu = _txtMesemSigortaKolu.Text.Trim();
            _settings.MesemOgrenimKodu = _txtMesemOgrenimKodu.Text.Trim();
            _settings.MesemMeslekKodu = _txtMesemMeslekKodu.Text.Trim();
            _settings.MesemGorevKodu = _txtMesemGorevKodu.Text.Trim();

            await _settingRepo.SaveAppSettingsAsync(_settings);

            MessageBox.Show("Sigorta ve SGK bilgileri başarıyla güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ayarlar kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
