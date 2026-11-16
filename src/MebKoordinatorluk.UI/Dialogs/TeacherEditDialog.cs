using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class TeacherEditDialog : Form
{
    private readonly Teacher? _existingTeacher;
    private readonly IFieldRepository _fieldRepo;
    public Teacher TeacherResult { get; private set; } = null!;

    private TextBox _txtTc = null!;
    private TextBox _txtFirstName = null!;
    private TextBox _txtLastName = null!;
    private ComboBox _cmbBranch = null!;
    private CheckBox[] _chkDays = null!;
    private NumericUpDown _numMaxHours = null!;
    private NumericUpDown _numCoordHours = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtEmail = null!;
    private CheckBox _chkIsActive = null!;

    public TeacherEditDialog(IFieldRepository fieldRepo, Teacher? teacher = null)
    {
        _fieldRepo = fieldRepo;
        _existingTeacher = teacher;
        InitializeComponent();
        LoadFieldsAndDataAsync();
    }

    private void InitializeComponent()
    {
        Text = _existingTeacher == null ? "Yeni Koordinatör Öğretmen Ekle" : "Öğretmen Bilgilerini Düzenle";
        Size = new Size(640, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        var lblHeader = new Label
        {
            Text = _existingTeacher == null ? "Yeni Koordinatör Öğretmen Tanımlama" : "Öğretmen Bilgilerini Güncelle",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(25, 20),
            AutoSize = true
        };

        // ── Ad Soyad ────────────────────────────────────────────────────────
        var lblFirstName = new Label { Text = "Öğretmen Adı *", Location = new Point(25, 65), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtFirstName = new TextBox { Location = new Point(25, 87), Width = 270 };

        var lblLastName = new Label { Text = "Öğretmen Soyadı *", Location = new Point(315, 65), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtLastName = new TextBox { Location = new Point(315, 87), Width = 270 };

        // ── Branş / Alan ─────────────────────────────────────────────────────
        var lblBranch = new Label { Text = "Alanı / Branşı *", Location = new Point(25, 123), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cmbBranch = new ComboBox { Location = new Point(25, 145), Width = 560, DropDownStyle = ComboBoxStyle.DropDown };

        // ── Koordinatörlük Günleri ───────────────────────────────────────────
        var lblDays = new Label { Text = "Koordinatörlük Görev Günleri * (Haftalık boş güne göre)", Location = new Point(25, 183), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var pnlDays = new FlowLayoutPanel { Location = new Point(25, 205), Width = 580, Height = 36 };

        string[] dayNames = new[] { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
        _chkDays = new CheckBox[dayNames.Length];
        var existingDays = _existingTeacher?.GetDaysList() ?? new List<string> { "Salı" };

        for (int i = 0; i < dayNames.Length; i++)
        {
            _chkDays[i] = new CheckBox
            {
                Text = dayNames[i],
                AutoSize = true,
                Checked = existingDays.Contains(dayNames[i], StringComparer.OrdinalIgnoreCase),
                Margin = new Padding(0, 4, 14, 4)
            };
            pnlDays.Controls.Add(_chkDays[i]);
        }

        // ── T.C. Kimlik No & Azami Saat ──────────────────────────────────────
        var lblTc = new Label { Text = "T.C. Kimlik No (İsteğe bağlı)", Location = new Point(25, 255), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtTc = new TextBox { Location = new Point(25, 277), Width = 270, MaxLength = 11 };

        var lblMaxHours = new Label { Text = "Azami Koordinatörlük Yükü (MEB: 16)", Location = new Point(315, 255), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _numMaxHours = new NumericUpDown { Location = new Point(315, 277), Width = 130, Minimum = 0, Maximum = 40, Value = 16 };

        // ── Aldığı Koordinatörlük Saati ──────────────────────────────────────
        var pnlCoordHours = new Panel
        {
            Location = new Point(25, 318),
            Size = new Size(560, 70),
            BackColor = Color.FromArgb(239, 246, 255),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblCoordHoursTitle = new Label
        {
            Text = "📅  Aldığı Koordinatörlük Saati",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 64, 175),
            Location = new Point(12, 10),
            AutoSize = true
        };

        var lblCoordHoursDesc = new Label
        {
            Text = "Öğretmenin ders programından aldığı koordinatörlük görevi saati (görev formunda görünür)",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(12, 32),
            Size = new Size(400, 30),   // Sabit genişlik — NumericUpDown'a taşmaz
            AutoSize = false
        };

        _numCoordHours = new NumericUpDown
        {
            Location = new Point(468, 14),
            Width = 78,
            Height = 28,
            Minimum = 0,
            Maximum = 60,
            Value = 16,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold)
        };
        _numCoordHours.ValueChanged += (s, e) =>
        {
            if (_numCoordHours.Value == 0)
            {
                lblCoordHoursDesc.Text = "0 Saat: Öğretmen koordinatörlük görevi almaz (Kültür dersi vb.). Görev formlarına dahil edilmez.";
                lblCoordHoursDesc.ForeColor = Color.FromArgb(185, 28, 28);
            }
            else
            {
                lblCoordHoursDesc.Text = "Öğretmenin ders programından aldığı koordinatörlük görevi saati (görev formunda görünür)";
                lblCoordHoursDesc.ForeColor = Color.FromArgb(71, 85, 105);
            }
        };

        var lblSaat = new Label
        {
            Text = "saat",
            Location = new Point(432, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(51, 65, 85)
        };

        pnlCoordHours.Controls.AddRange([lblCoordHoursTitle, lblCoordHoursDesc, lblSaat, _numCoordHours]);

        // ── Telefon & E-Posta ─────────────────────────────────────────────────
        var lblPhone = new Label { Text = "Telefon Numarası", Location = new Point(25, 406), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtPhone = new TextBox { Location = new Point(25, 428), Width = 270 };

        var lblEmail = new Label { Text = "E-Posta Adresi", Location = new Point(315, 406), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtEmail = new TextBox { Location = new Point(315, 428), Width = 270 };

        // ── Aktiflik ──────────────────────────────────────────────────────────
        _chkIsActive = new CheckBox
        {
            Text = "Öğretmen aktif (Koordinatörlük görevi alabilir)",
            Checked = true,
            Location = new Point(25, 472),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 185, 129)
        };

        // ── Butonlar ──────────────────────────────────────────────────────────
        var pnlButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet",
            Location = new Point(490, 12),
            Size = new Size(120, 36),
            BackColor = Color.FromArgb(37, 99, 235),
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
            Location = new Point(390, 12),
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        pnlButtons.Controls.AddRange([btnSave, btnCancel]);

        Controls.AddRange([
            lblHeader,
            lblFirstName, _txtFirstName,
            lblLastName, _txtLastName,
            lblBranch, _cmbBranch,
            lblDays, pnlDays,
            lblTc, _txtTc,
            lblMaxHours, _numMaxHours,
            pnlCoordHours,
            lblPhone, _txtPhone,
            lblEmail, _txtEmail,
            _chkIsActive,
            pnlButtons
        ]);
    }

    private List<Field> _fields = [];

    private async void LoadFieldsAndDataAsync()
    {
        _fields = (await _fieldRepo.GetAllAsync()).ToList();
        _cmbBranch.Items.Clear();
        foreach (var f in _fields)
        {
            _cmbBranch.Items.Add(f.Name);
        }

        if (_existingTeacher != null)
        {
            _txtTc.Text = _existingTeacher.TcKimlikNo ?? "";
            _txtFirstName.Text = _existingTeacher.FirstName;
            _txtLastName.Text = _existingTeacher.LastName;
            _cmbBranch.Text = _existingTeacher.Branch ?? _existingTeacher.DepartmentName ?? "";
            _numMaxHours.Value = _existingTeacher.MaxCoordinationHours > 0 ? _existingTeacher.MaxCoordinationHours : 16;
            _numCoordHours.Value = _existingTeacher.CoordinatorFeeHours >= 0 ? _existingTeacher.CoordinatorFeeHours : 16;
            _txtPhone.Text = _existingTeacher.Phone ?? "";
            _txtEmail.Text = _existingTeacher.Email ?? "";
            _chkIsActive.Checked = _existingTeacher.IsActive;
        }
        else if (_cmbBranch.Items.Count > 0)
        {
            _cmbBranch.SelectedIndex = 0;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var firstName = _txtFirstName.Text.Trim();
        var lastName = _txtLastName.Text.Trim();
        var branch = _cmbBranch.Text.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            MessageBox.Show("Lütfen öğretmen adını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtFirstName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            MessageBox.Show("Lütfen öğretmen soyadını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtLastName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(branch))
        {
            MessageBox.Show("Lütfen bir alan/branş belirtiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbBranch.Focus();
            return;
        }

        var tc = _txtTc.Text.Trim();
        if (!string.IsNullOrWhiteSpace(tc) && tc.Length != 11)
        {
            var confirmTc = MessageBox.Show(
                "Girilen T.C. Kimlik Numarası 11 haneli değil. Yine de kaydetmek istiyor musunuz?",
                "T.C. Kimlik Uyarısı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirmTc != DialogResult.Yes)
            {
                _txtTc.Focus();
                return;
            }
        }

        var selectedDays = _chkDays.Where(c => c.Checked).Select(c => c.Text).ToList();
        var matchedField = _fields.FirstOrDefault(f => string.Equals(f.Name, branch, StringComparison.OrdinalIgnoreCase));
        int deptId = matchedField?.Id ?? (_existingTeacher?.DepartmentId ?? 1);
        if (deptId <= 0) deptId = 1;

        TeacherResult = new Teacher
        {
            Id = _existingTeacher?.Id ?? 0,
            TcKimlikNo = string.IsNullOrWhiteSpace(tc) ? null : tc,
            FirstName = firstName,
            LastName = lastName,
            DepartmentId = deptId,
            Branch = branch,
            DepartmentName = branch,
            AvailableDays = string.Join(",", selectedDays),
            MaxCoordinationHours = (int)_numMaxHours.Value,
            TargetHours = (int)_numMaxHours.Value,
            CoordinatorFeeHours = (int)_numCoordHours.Value,
            Phone = string.IsNullOrWhiteSpace(_txtPhone.Text) ? null : _txtPhone.Text.Trim(),
            Email = string.IsNullOrWhiteSpace(_txtEmail.Text) ? null : _txtEmail.Text.Trim(),
            IsActive = _chkIsActive.Checked
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
