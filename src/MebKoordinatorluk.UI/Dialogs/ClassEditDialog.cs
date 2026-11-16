using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;

namespace MebKoordinatorluk.UI.Dialogs;

public class ClassEditDialog : Form
{
    private readonly SchoolClass? _existingClass;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private List<Field> _fields = [];

    private TextBox _txtName = null!;
    private ComboBox _cmbField = null!;
    private ComboBox _cmbInstitution = null!;
    private ComboBox _cmbEducation = null!;
    private CheckBox _chkHasInternship = null!;
    private CheckBox[] _chkDays = null!;
    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private CheckBox _chkIgnoreHalfDayDeduction = null!;

    public SchoolClass ClassResult { get; private set; } = null!;

    public ClassEditDialog(ISchoolClassRepository classRepo, IFieldRepository fieldRepo, SchoolClass? schoolClass = null)
    {
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _existingClass = schoolClass;
        InitializeComponent();
        LoadFieldsAsync();
    }

    private void InitializeComponent()
    {
        this.Text = _existingClass == null ? "Yeni Sınıf / Şube Ekle" : "Sınıf Bilgilerini Düzenle";
        this.Size = new Size(680, 645);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.Font = new Font("Segoe UI", 9f);

        var lblTitle = new Label
        {
            Text = _existingClass == null ? "Yeni Sınıf Tanımlama" : "Sınıf Bilgilerini Güncelle",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(30, 20),
            AutoSize = true
        };

        var lblName = new Label { Text = "Sınıf / Şube Adı (Örn: 12-A) *", Location = new Point(30, 62), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtName = new TextBox { Location = new Point(30, 84), Width = 605, Font = new Font("Segoe UI", 9.5f), Text = _existingClass?.Name ?? "" };

        var lblField = new Label { Text = "Mesleki Alan / Dal *", Location = new Point(30, 124), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cmbField = new ComboBox { Location = new Point(30, 146), Width = 605, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };

        var lblInst = new Label { Text = "Kurum Türü", Location = new Point(30, 186), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cmbInstitution = new ComboBox { Location = new Point(30, 208), Width = 295, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbInstitution.Items.AddRange(new[] { "Anadolu Meslek Programı", "MESEM" });

        var lblEdu = new Label { Text = "Öğretim Türü", Location = new Point(340, 186), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cmbEducation = new ComboBox { Location = new Point(340, 208), Width = 295, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbEducation.Items.AddRange(new[] { "Örgün Eğitim", "Özel Eğitim", "MESEM" });

        // Kurum türü değiştiğinde Öğretim türünü otomatik eşle
        _cmbInstitution.SelectedIndexChanged += (s, e) =>
        {
            var selected = _cmbInstitution.SelectedItem?.ToString() ?? "";
            if (selected.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _cmbEducation.SelectedItem = "MESEM";
            }
            else
            {
                if (_cmbEducation.SelectedItem?.ToString() == "MESEM")
                    _cmbEducation.SelectedItem = "Örgün Eğitim";
            }
        };

        if (_existingClass != null)
        {
            _cmbInstitution.SelectedItem = _existingClass.InstitutionType == "MESEM" ? "MESEM" : "Anadolu Meslek Programı";
            // Öğretim türünü tam eşleştir
            var existingEdu = _existingClass.EducationType ?? "Örgün Eğitim";
            if (_cmbEducation.Items.Contains(existingEdu))
                _cmbEducation.SelectedItem = existingEdu;
            else
                _cmbEducation.SelectedItem = "Örgün Eğitim";
        }
        else
        {
            _cmbInstitution.SelectedIndex = 0; // Varsayılan: Anadolu Meslek Programı -> Örgün Eğitim
        }

        _chkHasInternship = new CheckBox
        {
            Text = "İşletmeye Gidiyor",
            Location = new Point(30, 248),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Checked = _existingClass?.HasInternship ?? false
        };

        var lblDays = new Label { Text = "İşletme / Staj Günleri", Location = new Point(30, 278), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        var pnlDays = new FlowLayoutPanel { Location = new Point(30, 300), Width = 605, Height = 36 };

        string[] dayNames = new[] { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi" };
        _chkDays = new CheckBox[dayNames.Length];
        var existingDays = _existingClass?.GetDaysList() ?? new List<string> { "Pazartesi", "Salı", "Çarşamba" };

        for (int i = 0; i < dayNames.Length; i++)
        {
            _chkDays[i] = new CheckBox
            {
                Text = dayNames[i],
                AutoSize = true,
                Checked = existingDays.Contains(dayNames[i], StringComparer.OrdinalIgnoreCase),
                Margin = new Padding(0, 4, 16, 4)
            };
            pnlDays.Controls.Add(_chkDays[i]);
        }

        var lblStart = new Label { Text = "İşe Başlama Tarihi (İsteğe Bağlı)", Location = new Point(30, 348), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _dtpStart = new DateTimePicker
        {
            Location = new Point(30, 370),
            Width = 295,
            Font = new Font("Segoe UI", 9.5f),
            ShowCheckBox = true
        };

        if (_existingClass != null && !string.IsNullOrWhiteSpace(_existingClass.StartDate) && DateTime.TryParse(_existingClass.StartDate, out var sDate))
        {
            _dtpStart.Value = sDate;
            _dtpStart.Checked = true;
            _dtpStart.Format = DateTimePickerFormat.Short;
        }
        else
        {
            _dtpStart.Checked = false;
            _dtpStart.Format = DateTimePickerFormat.Custom;
            _dtpStart.CustomFormat = " ";
        }

        _dtpStart.ValueChanged += (s, e) =>
        {
            if (_dtpStart.Checked)
            {
                _dtpStart.Format = DateTimePickerFormat.Short;
            }
            else
            {
                _dtpStart.Format = DateTimePickerFormat.Custom;
                _dtpStart.CustomFormat = " ";
            }
        };

        var lblEnd = new Label { Text = "İş Bitiş Tarihi (İsteğe Bağlı)", Location = new Point(340, 348), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _dtpEnd = new DateTimePicker
        {
            Location = new Point(340, 370),
            Width = 295,
            Font = new Font("Segoe UI", 9.5f),
            ShowCheckBox = true
        };

        if (_existingClass != null && !string.IsNullOrWhiteSpace(_existingClass.EndDate) && DateTime.TryParse(_existingClass.EndDate, out var eDate))
        {
            _dtpEnd.Value = eDate;
            _dtpEnd.Checked = true;
            _dtpEnd.Format = DateTimePickerFormat.Short;
        }
        else
        {
            _dtpEnd.Checked = false;
            _dtpEnd.Format = DateTimePickerFormat.Custom;
            _dtpEnd.CustomFormat = " ";
        }

        _dtpEnd.ValueChanged += (s, e) =>
        {
            if (_dtpEnd.Checked)
            {
                _dtpEnd.Format = DateTimePickerFormat.Short;
            }
            else
            {
                _dtpEnd.Format = DateTimePickerFormat.Custom;
                _dtpEnd.CustomFormat = " ";
            }
        };

        _chkIgnoreHalfDayDeduction = new CheckBox
        {
            Text = "Yarım gün devamsızlıkları ücretten kesme (0.5 gün kesintilerini 0 say)",
            Location = new Point(30, 420),
            Width = 605,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            Checked = _existingClass?.IgnoreHalfDayDeduction ?? false
        };

        void UpdateInternshipControls()
        {
            bool isIntern = _chkHasInternship.Checked;
            lblDays.Enabled = isIntern;
            pnlDays.Enabled = isIntern;
            lblStart.Enabled = isIntern;
            _dtpStart.Enabled = isIntern;
            lblEnd.Enabled = isIntern;
            _dtpEnd.Enabled = isIntern;
            _chkIgnoreHalfDayDeduction.Enabled = isIntern;
        }

        _chkHasInternship.CheckedChanged += (s, e) => UpdateInternshipControls();
        UpdateInternshipControls();

        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet",
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
            Size = new Size(95, 36),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => this.Close();

        void PositionBottomButtons()
        {
            btnSave.Location = new Point(pnlBottom.ClientSize.Width - 120 - 30, 12);
            btnCancel.Location = new Point(btnSave.Left - 95 - 10, 12);
        }
        pnlBottom.Resize += (s, e) => PositionBottomButtons();
        PositionBottomButtons();

        pnlBottom.Controls.Add(btnSave);
        pnlBottom.Controls.Add(btnCancel);

        this.Controls.Add(lblTitle);
        this.Controls.Add(lblName);
        this.Controls.Add(_txtName);
        this.Controls.Add(lblField);
        this.Controls.Add(_cmbField);
        this.Controls.Add(lblInst);
        this.Controls.Add(_cmbInstitution);
        this.Controls.Add(lblEdu);
        this.Controls.Add(_cmbEducation);
        this.Controls.Add(_chkHasInternship);
        this.Controls.Add(lblDays);
        this.Controls.Add(pnlDays);
        this.Controls.Add(lblStart);
        this.Controls.Add(_dtpStart);
        this.Controls.Add(lblEnd);
        this.Controls.Add(_dtpEnd);
        this.Controls.Add(_chkIgnoreHalfDayDeduction);
        this.Controls.Add(pnlBottom);
    }

    private async void LoadFieldsAsync()
    {
        _fields = (await _fieldRepo.GetAllAsync()).ToList();
        _cmbField.Items.Clear();
        foreach (var f in _fields)
        {
            _cmbField.Items.Add(f.Name);
        }

        if (_existingClass != null && !string.IsNullOrWhiteSpace(_existingClass.FieldName))
        {
            _cmbField.SelectedItem = _existingClass.FieldName;
        }
        else if (_cmbField.Items.Count > 0)
        {
            _cmbField.SelectedIndex = 0;
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen sınıf adını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        var fieldName = _cmbField.SelectedItem?.ToString()?.Trim() ?? string.Empty;

        // Sınıf adı ve Alan adı aynı şekilde kaydedilirse uyarı ver
        bool exists = await _classRepo.ExistsAsync(name, fieldName, _existingClass?.Id);
        if (exists)
        {
            MessageBox.Show("Bu sınıfta bir adet kayıt vardır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        bool hasInternship = _chkHasInternship.Checked;
        var selectedDays = hasInternship ? _chkDays.Where(c => c.Checked).Select(c => c.Text).ToList() : new List<string>();
        string internshipDays = selectedDays.Count > 0 ? string.Join(",", selectedDays) : string.Empty;

        string? startDate = (hasInternship && _dtpStart.Checked) ? _dtpStart.Value.ToString("yyyy-MM-dd") : null;
        string? endDate = (hasInternship && _dtpEnd.Checked) ? _dtpEnd.Value.ToString("yyyy-MM-dd") : null;

        var matchedField = _fields.FirstOrDefault(f => string.Equals(f.Name?.Trim(), fieldName, StringComparison.CurrentCultureIgnoreCase));

        ClassResult = new SchoolClass
        {
            Id = _existingClass?.Id ?? 0,
            Name = name,
            FieldId = matchedField?.Id,
            FieldName = fieldName,
            InstitutionType = _cmbInstitution.SelectedItem?.ToString() ?? "Anadolu Meslek Programı",
            EducationType = _cmbEducation.SelectedItem?.ToString() ?? "Örgün Eğitim",
            HasInternship = hasInternship,
            InternshipDays = internshipDays,
            StartDate = startDate,
            EndDate = endDate,
            IgnoreHalfDayDeduction = hasInternship && _chkIgnoreHalfDayDeduction.Checked,
            IsActive = true
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
