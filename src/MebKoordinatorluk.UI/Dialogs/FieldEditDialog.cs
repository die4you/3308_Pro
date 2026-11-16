using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;

namespace MebKoordinatorluk.UI.Dialogs;

public class FieldEditDialog : Form
{
    private readonly Field? _existingField;
    private TextBox _txtName = null!;
    private TextBox _txtBranches = null!;

    public Field FieldResult { get; private set; } = null!;

    public FieldEditDialog(Field? field = null)
    {
        _existingField = field;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = _existingField == null ? "Yeni Mesleki Alan / Dal Ekle" : "Alan / Dal Bilgilerini Düzenle";
        this.Size = new Size(500, 360);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.Font = new Font("Segoe UI", 9f);

        var lblTitle = new Label
        {
            Text = _existingField == null ? "Yeni Alan / Dal Tanımlama" : "Alan / Dal Bilgilerini Güncelle",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lblName = new Label { Text = "Alan / Dal Adı *", Location = new Point(24, 65), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtName = new TextBox
        {
            Location = new Point(24, 88),
            Width = 435,
            Text = _existingField?.Name ?? ""
        };

        var lblBranches = new Label { Text = "Dallar / Açıklama (Virgülle veya satırla ayırın)", Location = new Point(24, 125), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtBranches = new TextBox
        {
            Location = new Point(24, 148),
            Width = 435,
            Height = 80,
            Multiline = true,
            Text = _existingField?.Branches ?? _existingField?.Description ?? ""
        };

        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet",
            Location = new Point(350, 10),
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(79, 70, 229),
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
            Location = new Point(255, 10),
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
        this.Controls.Add(lblBranches);
        this.Controls.Add(_txtBranches);
        this.Controls.Add(pnlBottom);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen alan adı giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        FieldResult = new Field
        {
            Id = _existingField?.Id ?? 0,
            Name = name,
            Description = _txtBranches.Text.Trim(),
            Branches = _txtBranches.Text.Trim(),
            IsActive = true
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
