using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class EnterpriseEditDialog : Form
{
    private readonly Enterprise? _existingEnterprise;
    private readonly IAddressGroupRepository _addressGroupRepo;
    private readonly IFieldRepository _fieldRepo;

    public Enterprise EnterpriseResult { get; private set; } = null!;

    private TextBox _txtName = null!;
    private ComboBox _cmbSector = null!;
    private NumericUpDown _numCapacity = null!;
    private TextBox _txtDistance = null!;
    private TextBox _txtContact = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtDistrict = null!;
    private TextBox _txtAddress = null!;
    private TextBox _txtIban = null!;
    private TextBox _txtSgkSicilNo = null!;
    private NumericUpDown _numEmployeeCount = null!;
    private CheckBox _chkOptOutStateContribution = null!;
    private ComboBox _cmbAddressGroup = null!;
    private readonly Dictionary<string, CheckBox> _dayCheckBoxes = new();

    private List<AddressGroup> _addressGroups = [];
    private List<Field> _fields = [];

    public EnterpriseEditDialog(
        IAddressGroupRepository addressGroupRepo,
        IFieldRepository fieldRepo,
        Enterprise? enterprise = null)
    {
        _addressGroupRepo = addressGroupRepo;
        _fieldRepo = fieldRepo;
        _existingEnterprise = enterprise;

        InitializeComponent();
        LoadLookupsAndDataAsync();
    }

    private void InitializeComponent()
    {
        Text = _existingEnterprise == null ? "Yeni İşletme Ekle" : "İşletme Bilgilerini Düzenle";
        Size = new Size(590, 740);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Surface;
        Font = new Font("Segoe UI", 9f);

        // Header Panel with Purple Accent
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(88, 28, 135), // purple-900
            Padding = new Padding(20, 0, 20, 0)
        };

        var lblHeader = new Label
        {
            Text = _existingEnterprise == null ? "🏢 Yeni İşletme Tanımla" : "🏢 İşletme Bilgilerini Düzenle",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblHeader);

        // Form Body Panel
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24, 16, 24, 16)
        };

        int y = 10;

        // İşletme Adı
        var lblName = new Label { Text = "İşletme Adı / Ticaret Unvanı *", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;
        _txtName = new TextBox { Location = new Point(24, y), Width = 512, Font = new Font("Segoe UI", 9.5f) };
        y += 36;

        // 3-Col: Sektör | Kontenjan | Mesafe
        var lblSector = new Label { Text = "Faaliyet Alanı *", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblCap = new Label { Text = "Öğrenci Kontenjanı", Location = new Point(270, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblDist = new Label { Text = "🚗 Okula Uzaklık (km)", Location = new Point(410, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;

        _cmbSector = new ComboBox { Location = new Point(24, y), Width = 230, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        _numCapacity = new NumericUpDown { Location = new Point(270, y), Width = 120, Minimum = 1, Maximum = 100, Value = 4, Font = new Font("Segoe UI", 9.5f) };
        _txtDistance = new TextBox { Location = new Point(410, y), Width = 126, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "Örn: 8.5" };
        y += 36;

        // 2-Col: Yetkili | Telefon
        var lblContact = new Label { Text = "İşletme Yetkilisi", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblPhone = new Label { Text = "Telefon Numarası", Location = new Point(280, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;

        _txtContact = new TextBox { Location = new Point(24, y), Width = 240, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "Yetkili Adı Soyadı" };
        _txtPhone = new TextBox { Location = new Point(280, y), Width = 256, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "0212 XXX XX XX" };
        y += 36;

        // 2-Col: İlçe / Bölge | Açık Adres
        var lblDistrict = new Label { Text = "İlçe / Bölge", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblAddress = new Label { Text = "Açık Adres", Location = new Point(190, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;

        _txtDistrict = new TextBox { Location = new Point(24, y), Width = 150, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "Kadıköy" };
        _txtAddress = new TextBox { Location = new Point(190, y), Width = 346, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "Mahalle, Cadde, No" };
        y += 36;

        // 2-Col: İşletme IBAN | SGK Sicil No
        var lblIban = new Label { Text = "🏦 İşletme IBAN (Banka Hesap Numarası)", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblSgkSicil = new Label { Text = "🛡️ SGK İşyeri Sicil No", Location = new Point(344, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;

        _txtIban = new TextBox { Location = new Point(24, y), Width = 310, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "TR00 0000 0000 0000 0000 0000 00", MaxLength = 34, CharacterCasing = CharacterCasing.Upper };
        _txtSgkSicilNo = new TextBox { Location = new Point(344, y), Width = 192, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "SGK Sicil Numarası", MaxLength = 30 };
        y += 36;

        // 2-Col: Çalışan (Personel) Sayısı | Devlet Katkısı Durumu
        var lblEmpCount = new Label { Text = "👥 Çalışan (Personel) Sayısı *", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblStateOpt = new Label { Text = "🏛️ Devlet Katkısı Tercihi", Location = new Point(190, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;

        _numEmployeeCount = new NumericUpDown { Location = new Point(24, y), Width = 150, Minimum = 1, Maximum = 10000, Value = 1, Font = new Font("Segoe UI", 9.5f) };
        _chkOptOutStateContribution = new CheckBox
        {
            Text = "Devlet Katkısı İstemiyor (Ödemelerin tamamı İşletme Maliyetidir)",
            Location = new Point(190, y + 2),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 18, 60), // rose-700
            Cursor = Cursors.Hand
        };
        y += 36;

        // Adres / Bölge Grubu
        var lblGroup = new Label { Text = "📍 Adres / Bölge Grubu (Yakın işletmeleri gruplamak için)", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 22;
        _cmbAddressGroup = new ComboBox { Location = new Point(24, y), Width = 512, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        y += 36;

        // Öğrencinin Eğitime Gittiği Günler
        var lblDays = new Label { Text = "📅 Öğrencinin Eğitime Gittiği Günler (Tıklayarak seçin/kaldırın)", Location = new Point(24, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 24;

        var pnlDays = new FlowLayoutPanel
        {
            Location = new Point(24, y),
            Size = new Size(512, 36),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        string[] days = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma"];
        foreach (var day in days)
        {
            var chk = new CheckBox
            {
                Text = day,
                AutoSize = true,
                Margin = new Padding(0, 4, 16, 0),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Checked = day is "Pazartesi" or "Salı" or "Çarşamba"
            };
            _dayCheckBoxes[day] = chk;
            pnlDays.Controls.Add(chk);
        }

        pnlBody.Controls.AddRange([
            lblName, _txtName,
            lblSector, lblCap, lblDist,
            _cmbSector, _numCapacity, _txtDistance,
            lblContact, lblPhone,
            _txtContact, _txtPhone,
            lblDistrict, lblAddress,
            _txtDistrict, _txtAddress,
            lblIban, lblSgkSicil,
            _txtIban, _txtSgkSicilNo,
            lblEmpCount, lblStateOpt,
            _numEmployeeCount, _chkOptOutStateContribution,
            lblGroup, _cmbAddressGroup,
            lblDays, pnlDays
        ]);

        // Bottom Actions Panel
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 12, 24, 12)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var btnSave = new Button
        {
            Text = "Kaydet",
            Size = new Size(110, 36),
            Location = new Point(pnlBottom.Width - 134, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(147, 51, 234), // purple-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;

        var btnCancel = new Button
        {
            Text = "İptal",
            Size = new Size(90, 36),
            Location = new Point(pnlBottom.Width - 234, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlBottom.Controls.AddRange([btnCancel, btnSave]);

        Controls.Add(pnlBody);
        Controls.Add(pnlBottom);
        Controls.Add(pnlHeader);
    }

    private async void LoadLookupsAndDataAsync()
    {
        try
        {
            _addressGroups = (await _addressGroupRepo.GetAllAsync()).OrderBy(g => g.Name).ToList();
            _fields = (await _fieldRepo.GetAllAsync()).OrderBy(f => f.Name).ToList();

            // Faaliyet Alanı listesini doldur (yalnızca alan adları)
            _cmbSector.Items.Clear();
            foreach (var f in _fields)
            {
                if (!string.IsNullOrWhiteSpace(f.Name) && !_cmbSector.Items.Contains(f.Name))
                {
                    _cmbSector.Items.Add(f.Name);
                }
            }

            // Adres Grupları listesini doldur
            _cmbAddressGroup.Items.Clear();
            _cmbAddressGroup.Items.Add("-- Grup Seçilmedi (Bağımsız İşletme) --");
            foreach (var g in _addressGroups)
            {
                _cmbAddressGroup.Items.Add(g.Name);
            }
            _cmbAddressGroup.SelectedIndex = 0;

            if (_existingEnterprise != null)
            {
                _txtName.Text = _existingEnterprise.Name;

                if (!string.IsNullOrWhiteSpace(_existingEnterprise.Sector))
                {
                    var existingSector = _existingEnterprise.Sector.Trim();
                    if (_cmbSector.Items.Contains(existingSector))
                    {
                        _cmbSector.SelectedItem = existingSector;
                    }
                    else
                    {
                        // "Alan - Dal" formatında ise sadece alan adını eşleştirmeye çalış
                        var prefix = existingSector.Split('-')[0].Trim();
                        var matched = _fields.FirstOrDefault(f => string.Equals(f.Name?.Trim(), prefix, StringComparison.CurrentCultureIgnoreCase));
                        if (matched != null && _cmbSector.Items.Contains(matched.Name))
                        {
                            _cmbSector.SelectedItem = matched.Name;
                        }
                        else
                        {
                            // Sistemde kayıtlı olmayan bir alan girilmişse listeye ekleyip seç
                            _cmbSector.Items.Add(existingSector);
                            _cmbSector.SelectedItem = existingSector;
                        }
                    }
                }
                else if (_cmbSector.Items.Count > 0)
                {
                    _cmbSector.SelectedIndex = 0;
                }

                _numCapacity.Value = Math.Max(1, _existingEnterprise.Capacity);
                _txtDistance.Text = _existingEnterprise.DistanceText ?? "";
                _txtContact.Text = _existingEnterprise.ContactPerson ?? "";
                _txtPhone.Text = _existingEnterprise.Phone ?? "";
                _txtDistrict.Text = _existingEnterprise.District ?? "";
                _txtAddress.Text = _existingEnterprise.Address ?? "";
                _txtIban.Text = _existingEnterprise.Iban ?? "";
                _txtSgkSicilNo.Text = _existingEnterprise.SgkSicilNo ?? "";
                _numEmployeeCount.Value = Math.Max(1, _existingEnterprise.EmployeeCount);
                _chkOptOutStateContribution.Checked = _existingEnterprise.OptOutStateContribution;

                if (_existingEnterprise.AddressGroupId.HasValue)
                {
                    var idx = _addressGroups.FindIndex(g => g.Id == _existingEnterprise.AddressGroupId.Value);
                    if (idx >= 0)
                    {
                        _cmbAddressGroup.SelectedIndex = idx + 1; // +1 for placeholder
                    }
                }

                var activeDaysList = _existingEnterprise.GetDaysList();
                if (activeDaysList.Count > 0)
                {
                    foreach (var kv in _dayCheckBoxes)
                    {
                        kv.Value.Checked = activeDaysList.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
        }
        catch
        {
            // Silently ignore lookup load errors or keep defaults
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen işletme adını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        var sector = _cmbSector.SelectedItem?.ToString()?.Trim() ?? _cmbSector.Text.Trim();
        if (string.IsNullOrWhiteSpace(sector))
        {
            MessageBox.Show("Lütfen faaliyet alanını seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbSector.Focus();
            return;
        }

        int? selectedGroupId = null;
        string? selectedGroupName = null;
        if (_cmbAddressGroup.SelectedIndex > 0 && _cmbAddressGroup.SelectedIndex - 1 < _addressGroups.Count)
        {
            var grp = _addressGroups[_cmbAddressGroup.SelectedIndex - 1];
            selectedGroupId = grp.Id;
            selectedGroupName = grp.Name;
        }

        var selectedDays = _dayCheckBoxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        var daysStr = string.Join(",", selectedDays);

        var distStr = _txtDistance.Text.Trim();
        var distType = DistanceType.MucavirAlanIci;
        if (double.TryParse(distStr.Replace("km", "").Trim(), out var distKm) && distKm > 30)
        {
            distType = DistanceType.MucavirAlanDisi;
        }

        EnterpriseResult = new Enterprise
        {
            Id = _existingEnterprise?.Id ?? 0,
            Name = name,
            Sector = sector,
            Capacity = (int)_numCapacity.Value,
            EmployeeCount = (int)_numEmployeeCount.Value,
            OptOutStateContribution = _chkOptOutStateContribution.Checked,
            DistanceText = string.IsNullOrWhiteSpace(distStr) ? null : distStr,
            DistanceType = distType,
            ContactPerson = string.IsNullOrWhiteSpace(_txtContact.Text) ? null : _txtContact.Text.Trim(),
            Phone = string.IsNullOrWhiteSpace(_txtPhone.Text) ? null : _txtPhone.Text.Trim(),
            District = string.IsNullOrWhiteSpace(_txtDistrict.Text) ? null : _txtDistrict.Text.Trim(),
            Address = string.IsNullOrWhiteSpace(_txtAddress.Text) ? null : _txtAddress.Text.Trim(),
            Iban = string.IsNullOrWhiteSpace(_txtIban.Text) ? null : _txtIban.Text.Trim().ToUpperInvariant(),
            SgkSicilNo = string.IsNullOrWhiteSpace(_txtSgkSicilNo.Text) ? null : _txtSgkSicilNo.Text.Trim(),
            AddressGroupId = selectedGroupId,
            AddressGroupName = selectedGroupName,
            ActiveDays = daysStr,
            IsActive = _existingEnterprise?.IsActive ?? true
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
