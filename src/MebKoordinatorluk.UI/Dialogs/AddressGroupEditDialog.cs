using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.UI.Dialogs;

public class AddressGroupEditDialog : Form
{
    private readonly AddressGroup? _existingGroup;
    private TextBox _txtName = null!;
    private TextBox _txtRegion = null!;
    private TextBox _txtDescription = null!;
    private ComboBox _cmbColor = null!;

    public AddressGroup GroupResult { get; private set; } = null!;

    public AddressGroupEditDialog(AddressGroup? group = null)
    {
        _existingGroup = group;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = _existingGroup == null ? "Yeni Adres / Bölge Grubu Ekle" : "Adres Grubu Bilgilerini Düzenle";
        this.Size = new Size(520, 390);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.Font = new Font("Segoe UI", 9f);

        var lblTitle = new Label
        {
            Text = _existingGroup == null ? "Yeni Adres Grubu Tanımlama" : "Adres Grubu Bilgilerini Güncelle",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lblName = new Label { Text = "Grup Adı (Örn: İkitelli OSB Sanayi Bölgesi) *", Location = new Point(24, 60), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtName = new TextBox { Location = new Point(24, 82), Width = 455, Text = _existingGroup?.Name ?? "" };

        var lblRegion = new Label { Text = "Bölge / İlçe / Semt (Örn: Başakşehir / İkitelli)", Location = new Point(24, 118), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtRegion = new TextBox { Location = new Point(24, 140), Width = 455, Text = _existingGroup?.Region ?? "" };

        var lblDesc = new Label { Text = "Açıklama / Kapsanan Sanayi Siteleri", Location = new Point(24, 176), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtDescription = new TextBox { Location = new Point(24, 198), Width = 455, Height = 60, Multiline = true, Text = _existingGroup?.Description ?? "" };

        var lblColor = new Label { Text = "Grup Rozet Rengi", Location = new Point(24, 268), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cmbColor = new ComboBox { Location = new Point(160, 265), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbColor.Items.AddRange(new[] { "blue", "emerald", "purple", "amber", "rose", "indigo" });
        _cmbColor.SelectedItem = _existingGroup?.Color ?? "blue";

        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet",
            Location = new Point(370, 10),
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(29, 78, 216),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;

        var btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(275, 10),
            Size = new Size(85, 34),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => this.Close();

        pnlBottom.Controls.Add(btnSave);
        pnlBottom.Controls.Add(btnCancel);

        this.Controls.Add(lblTitle);
        this.Controls.Add(lblName);
        this.Controls.Add(_txtName);
        this.Controls.Add(lblRegion);
        this.Controls.Add(_txtRegion);
        this.Controls.Add(lblDesc);
        this.Controls.Add(_txtDescription);
        this.Controls.Add(lblColor);
        this.Controls.Add(_cmbColor);
        this.Controls.Add(pnlBottom);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen grup adı giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        GroupResult = new AddressGroup
        {
            Id = _existingGroup?.Id ?? 0,
            Name = name,
            Region = _txtRegion.Text.Trim(),
            Description = _txtDescription.Text.Trim(),
            Color = _cmbColor.SelectedItem?.ToString() ?? "blue",
            IsActive = true
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
