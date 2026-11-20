using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class StudentEditDialog : Form
{
    private readonly Student? _existingStudent;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly ITeacherRepository _teacherRepo;
    private readonly IStudentRepository _studentRepo;

    public Student StudentResult { get; private set; } = null!;

    private TextBox _txtNumber = null!;
    private TextBox _txtName = null!;
    private TextBox _txtTcNo = null!;
    private TextBox _txtParentName = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtParentPhone = null!;
    private TextBox _txtIban = null!;
    private ComboBox _cmbGender = null!;
    private DateTimePicker _dtpBirthDate = null!;
    private Label _lblAgeDisplay = null!;
    private DateTimePicker _dtpSgkEntry = null!;
    private DateTimePicker _dtpSgkExit = null!;
    private CheckBox _chkFamilyInsurance = null!;
    private ComboBox _cmbEduPlace = null!;
    private ComboBox _cmbSgkPrimKodu = null!;
    private Panel _pnlSgk = null!;
    private bool _isInitializing = false;
    private int _previousClassIndex = -1;
    private bool _isRevertingClass = false;
    private bool _isDirty = false;

    private void MarkAsDirty()
    {
        if (!_isInitializing)
        {
            _isDirty = true;
        }
    }

    private static readonly string SgkPrim7 = "7 Nolu Belge Türü - 3308 / Çırak ve Mesleki Eğitim (Sağlık Güvencesi Var)";
    private static readonly string SgkPrim42 = "42 Nolu Belge Türü - 3308 / Çırak ve Mesleki Eğitim (GSS / Güvence Yok / Erkek 20+)";
    private static readonly string SgkPrim49 = "49 Nolu Belge Türü - Alan Eğitimi / Staj (Sağlık Güvencesi Var)";
    private static readonly string SgkPrim50 = "50 Nolu Belge Türü - Alan Eğitimi / Staj (Sağlık Güvencesi Yok - GSS)";

    private ComboBox _cmbClass = null!;
    private ComboBox _cmbEducationType = null!;
    private ComboBox _cmbField = null!;
    private readonly Dictionary<string, CheckBox> _internshipDayCheckBoxes = new();
    private Label _lblInternshipDays = null!;
    private FlowLayoutPanel _pnlInternshipDays = null!;
    private Label _lblEnt = null!;
    private ComboBox _cmbEnterprise = null!;
    private Label _lblEnterpriseInfo = null!;
    private Label _lblTeacher = null!;
    private ComboBox _cmbTeacher = null!;
    private Label _lblTeacherInfo = null!;
    private Label _lblDutyDays = null!;
    private FlowLayoutPanel _pnlDutyDays = null!;
    private readonly Dictionary<string, CheckBox> _dutyDayCheckBoxes = new();

    private List<SchoolClass> _classes = [];
    private List<Field> _fields = [];
    private List<Enterprise> _enterprises = [];
    private List<Teacher> _teachers = [];

    public StudentEditDialog(
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo,
        IEnterpriseRepository enterpriseRepo,
        ITeacherRepository teacherRepo,
        IStudentRepository studentRepo,
        Student? student = null)
    {
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;
        _enterpriseRepo = enterpriseRepo;
        _teacherRepo = teacherRepo;
        _studentRepo = studentRepo;
        _existingStudent = student;

        InitializeComponent();
        LoadLookupsAndDataAsync();
    }

    private void InitializeComponent()
    {
        Text = _existingStudent == null ? "Yeni Öğrenci Ekle" : "Öğrenci Bilgilerini Düzenle";
        Size = new Size(740, 950);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Surface;
        Font = new Font("Segoe UI", 9f);

        // Header Panel (Slate-800)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(30, 41, 59), // slate-800
            Padding = new Padding(24, 0, 24, 0)
        };

        var lblHeader = new Label
        {
            Text = _existingStudent == null ? "🎓 Yeni Öğrenci Kaydı" : "🎓 Öğrenci Bilgilerini Düzenle",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlHeader.Controls.Add(lblHeader);

        // Body Panel
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(28, 16, 28, 16)
        };

        int y = 12;

        // Okul No & Ad Soyad
        var lblNum = new Label { Text = "Okul No", Location = new Point(28, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblName = new Label { Text = "Adı Soyadı *", Location = new Point(190, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 24;

        _txtNumber = new TextBox { Location = new Point(28, y), Width = 150, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "1042" };
        _txtNumber.TextChanged += (s, e) => MarkAsDirty();
        _txtName = new TextBox { Location = new Point(190, y), Width = 506, Font = new Font("Segoe UI", 9.5f), PlaceholderText = "Öğrenci Adı Soyadı" };
        _txtName.TextChanged += (s, e) => MarkAsDirty();
        y += 38;

        // TC No, Cinsiyet & Doğum Tarihi
        var lblTcNo = new Label { Text = "T.C. Kimlik No", Location = new Point(28, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblGender = new Label { Text = "Cinsiyet", Location = new Point(244, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblBirthDate = new Label { Text = "Doğum Tarihi", Location = new Point(400, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _lblAgeDisplay = new Label { Location = new Point(500, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(37, 99, 235) };
        pnlBody.Controls.Add(_lblAgeDisplay);
        y += 24;

        _txtTcNo = new TextBox { Location = new Point(28, y), Width = 200, Font = new Font("Segoe UI", 9.5f), MaxLength = 11, PlaceholderText = "11 Haneli T.C." };
        _txtTcNo.TextChanged += (s, e) => MarkAsDirty();

        _cmbGender = new ComboBox { Location = new Point(244, y), Width = 140, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbGender.Items.AddRange(["Erkek", "Kız"]);
        _cmbGender.SelectedIndex = 0;
        _cmbGender.SelectedIndexChanged += (s, e) => { EvaluateSgkPrimFormula(); MarkAsDirty(); };

        _dtpBirthDate = new DateTimePicker { Location = new Point(400, y), Width = 258, Font = new Font("Segoe UI", 9.5f) };
        SetupNullableDatePicker(_dtpBirthDate);
        _dtpBirthDate.ValueChanged += (s, e) => { UpdateAgeDisplay(); EvaluateSgkPrimFormula(); MarkAsDirty(); };

        var btnCleanBirth = new Button
        {
            Text = "✕",
            Location = new Point(664, y),
            Size = new Size(32, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnCleanBirth.FlatAppearance.BorderSize = 0;
        btnCleanBirth.Click += (s, e) => { _dtpBirthDate.CustomFormat = " "; UpdateAgeDisplay(); EvaluateSgkPrimFormula(); MarkAsDirty(); };
        y += 38;

        // Veli ve İletişim Bilgileri (Telefon 1 / Telefon 2)
        var lblParentName = new Label { Text = "👨‍👩‍👧 Veli Adı Soyadı", Location = new Point(28, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblPhone = new Label { Text = "📱 Telefon 1 (Öğrenci)", Location = new Point(320, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblParentPhone = new Label { Text = "📞 Telefon 2 (Veli)", Location = new Point(512, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 24;

        _txtParentName = new TextBox
        {
            Location = new Point(28, y),
            Width = 280,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "Veli Adı Soyadı"
        };
        _txtParentName.TextChanged += (s, e) => MarkAsDirty();

        _txtPhone = new TextBox
        {
            Location = new Point(320, y),
            Width = 180,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "05xx xxx xx xx",
            MaxLength = 20
        };
        _txtPhone.TextChanged += (s, e) => MarkAsDirty();

        _txtParentPhone = new TextBox
        {
            Location = new Point(512, y),
            Width = 184,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "05xx xxx xx xx",
            MaxLength = 20
        };
        _txtParentPhone.TextChanged += (s, e) => MarkAsDirty();
        y += 38;

        // Öğrenci IBAN Numarası
        var lblIban = new Label { Text = "🏦 Öğrenci IBAN (Banka Hesap Numarası)", Location = new Point(28, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 24;

        _txtIban = new TextBox
        {
            Location = new Point(28, y),
            Width = 668,
            Font = new Font("Segoe UI", 9.5f),
            PlaceholderText = "TR00 0000 0000 0000 0000 0000 00",
            MaxLength = 34,
            CharacterCasing = CharacterCasing.Upper
        };
        _txtIban.TextChanged += (s, e) => MarkAsDirty();
        y += 38;

        // ── Sınıfı, Öğretim Türü & Alanı/Dalı ─────────────
        var lblClass = new Label { Text = "Sınıfı *", Location = new Point(28, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblEduType = new Label { Text = "Öğretim Türü (Sınıftan Gelir)", Location = new Point(200, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var lblField = new Label { Text = "Alanı / Dalı * (Sınıftan Gelir)", Location = new Point(372, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 24;

        _cmbClass = new ComboBox { Location = new Point(28, y), Width = 160, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbClass.SelectedIndexChanged += CmbClass_SelectedIndexChanged;

        _cmbEducationType = new ComboBox { Location = new Point(200, y), Width = 160, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
        _cmbEducationType.Items.AddRange(["Örgün Eğitim", "Özel Eğitim", "MESEM"]);

        _cmbField = new ComboBox { Location = new Point(372, y), Width = 324, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
        y += 38;

        // ── Staj Günleri (Sınıfının Altına Çekildi) ─────────────
        _lblInternshipDays = new Label
        {
            Text = "📅 İşletmeye Gittiği Staj Günleri (Sınıf tanımından otomatik gelir)",
            Location = new Point(28, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        y += 24;

        _pnlInternshipDays = new FlowLayoutPanel
        {
            Location = new Point(28, y),
            Size = new Size(668, 36),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8, 6, 8, 6)
        };

        string[] days = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma"];
        foreach (var day in days)
        {
            var chk = new CheckBox
            {
                Text = day,
                AutoSize = true,
                Margin = new Padding(0, 2, 28, 0),
                Font = new Font("Segoe UI", 9f),
                Enabled = false
            };
            _internshipDayCheckBoxes[day] = chk;
            _pnlInternshipDays.Controls.Add(chk);
        }
        y += 44;

        // ── SGK Bilgileri Paneli (Sınıf seçilince aktif olur) ───────────
        _pnlSgk = new Panel
        {
            Location = new Point(28, y),
            Size = new Size(668, 186),
            BackColor = Color.FromArgb(248, 250, 252),
            Enabled = false
        };
        _pnlSgk.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, _pnlSgk.Width - 1, _pnlSgk.Height - 1);
        };

        var lblSgkHeader = new Label
        {
            Text = "🛡️ SGK Bilgileri (Sınıf seçildiğinde aktif olur)",
            Location = new Point(14, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        _chkFamilyInsurance = new CheckBox
        {
            Text = "Aile Sağlık Güvencesi Var (Bakmakla Yükümlü)",
            Location = new Point(14, 38),
            AutoSize = true,
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };
        _chkFamilyInsurance.CheckedChanged += (s, e) => { EvaluateSgkPrimFormula(); MarkAsDirty(); };

        var lblEduPlace = new Label
        {
            Text = "Eğitim Yeri:",
            Location = new Point(430, 38),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _cmbEduPlace = new ComboBox
        {
            Location = new Point(510, 34),
            Width = 142,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbEduPlace.Items.AddRange(["İşletmede (Staj)", "Okulda"]);
        _cmbEduPlace.SelectedIndex = 0;
        _cmbEduPlace.SelectedIndexChanged += (s, e) => { EvaluateSgkPrimFormula(); MarkAsDirty(); };

        var lblSgkPrim = new Label
        {
            Text = "SGK Prim Kodu (Belge Türü) *",
            Location = new Point(14, 72),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138)
        };
        _cmbSgkPrimKodu = new ComboBox
        {
            Location = new Point(14, 94),
            Width = 638,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cmbSgkPrimKodu.Items.AddRange([SgkPrim7, SgkPrim42, SgkPrim49, SgkPrim50]);
        _cmbSgkPrimKodu.SelectedIndex = 0;
        _cmbSgkPrimKodu.SelectedIndexChanged += (s, e) => MarkAsDirty();

        var lblSgkEntry = new Label { Text = "İşe Giriş Tarihi", Location = new Point(14, 128), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _dtpSgkEntry = new DateTimePicker { Location = new Point(14, 148), Width = 265, Font = new Font("Segoe UI", 9.5f) };
        SetupNullableDatePicker(_dtpSgkEntry);

        var btnCleanSgkEntry = new Button
        {
            Text = "✕",
            Location = new Point(285, 148),
            Size = new Size(30, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnCleanSgkEntry.FlatAppearance.BorderSize = 0;
        btnCleanSgkEntry.Click += (s, e) => { _dtpSgkEntry.CustomFormat = " "; MarkAsDirty(); };

        var lblSgkExit = new Label { Text = "İşten Çıkış Tarihi", Location = new Point(348, 128), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _dtpSgkExit = new DateTimePicker { Location = new Point(348, 148), Width = 265, Font = new Font("Segoe UI", 9.5f) };
        SetupNullableDatePicker(_dtpSgkExit);

        var btnCleanSgkExit = new Button
        {
            Text = "✕",
            Location = new Point(622, 148),
            Size = new Size(30, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(148, 163, 184),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnCleanSgkExit.FlatAppearance.BorderSize = 0;
        btnCleanSgkExit.Click += (s, e) => { _dtpSgkExit.CustomFormat = " "; MarkAsDirty(); };

        _pnlSgk.Controls.AddRange([
            lblSgkHeader,
            _chkFamilyInsurance, lblEduPlace, _cmbEduPlace,
            lblSgkPrim, _cmbSgkPrimKodu,
            lblSgkEntry, _dtpSgkEntry, btnCleanSgkEntry,
            lblSgkExit, _dtpSgkExit, btnCleanSgkExit
        ]);
        y += 196;

        // Atanan İşletme
        _lblEnt = new Label { Text = "Atanan İşletme", Location = new Point(28, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 24;

        _cmbEnterprise = new ComboBox { Location = new Point(28, y), Width = 668, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbEnterprise.SelectedIndexChanged += CmbEnterprise_SelectedIndexChanged;
        y += 34;

        _lblEnterpriseInfo = new Label
        {
            Text = "🚗 Okula Uzaklık: - | Kontenjan: -",
            Location = new Point(28, y),
            Size = new Size(668, 28),
            BackColor = Color.FromArgb(243, 232, 255), // purple-50
            ForeColor = Color.FromArgb(88, 28, 135), // purple-900
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Visible = false
        };
        y += 34;

        // Koordinatör Öğretmen
        _lblTeacher = new Label { Text = "Koordinatör Öğretmen", Location = new Point(28, y), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        y += 24;

        _cmbTeacher = new ComboBox { Location = new Point(28, y), Width = 668, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTeacher.SelectedIndexChanged += CmbTeacher_SelectedIndexChanged;
        y += 34;

        _lblTeacherInfo = new Label
        {
            Text = "🗓️ Görev Günleri: - | Branş: -",
            Location = new Point(28, y),
            Size = new Size(668, 28),
            BackColor = Color.FromArgb(239, 246, 255), // blue-50
            ForeColor = Color.FromArgb(30, 58, 138), // blue-900
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Visible = false
        };
        y += 34;

        // Koordinatör Öğretmenin İşletmeye Gittiği Görev Günü
        _lblDutyDays = new Label
        {
            Text = "🗓️ Koordinatör Öğretmenin İşletmeye Gittiği Görev Günü * (Görev Formuna Yazılır)",
            Location = new Point(28, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138)
        };
        y += 24;

        _pnlDutyDays = new FlowLayoutPanel
        {
            Location = new Point(28, y),
            Size = new Size(668, 36),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8, 6, 8, 6)
        };

        foreach (var day in days)
        {
            var chk = new CheckBox
            {
                Text = day,
                AutoSize = true,
                Margin = new Padding(0, 2, 28, 0),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            chk.CheckedChanged += (s, e) => MarkAsDirty();
            _dutyDayCheckBoxes[day] = chk;
            _pnlDutyDays.Controls.Add(chk);
        }

        pnlBody.Controls.AddRange([
            lblNum, lblName,
            _txtNumber, _txtName,
            lblTcNo, lblGender, lblBirthDate,
            _txtTcNo, _cmbGender, _dtpBirthDate, btnCleanBirth,
            lblParentName, lblPhone, lblParentPhone,
            _txtParentName, _txtPhone, _txtParentPhone,
            lblIban, _txtIban,
            lblClass, lblEduType, lblField,
            _cmbClass, _cmbEducationType, _cmbField,
            _lblInternshipDays, _pnlInternshipDays,
            _pnlSgk,
            _lblEnt, _cmbEnterprise, _lblEnterpriseInfo,
            _lblTeacher, _cmbTeacher, _lblTeacherInfo,
            _lblDutyDays, _pnlDutyDays
        ]);

        // Bottom Actions Panel
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(28, 12, 28, 12)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var btnSave = new Button
        {
            Text = "Kaydet",
            Size = new Size(120, 38),
            Location = new Point(pnlBottom.Width - 148, 13),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
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
            Size = new Size(100, 38),
            Location = new Point(pnlBottom.Width - 260, 13),
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
        _isInitializing = true;
        try
        {
            _classes = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();
            _fields = (await _fieldRepo.GetAllAsync()).OrderBy(f => f.Name).ToList();
            _enterprises = (await _enterpriseRepo.GetAllAsync()).OrderBy(e => e.Name).ToList();
            _teachers = (await _teacherRepo.GetAllAsync()).OrderBy(t => t.FullName).ToList();

            // Populate Fields
            _cmbField.Items.Clear();
            foreach (var f in _fields)
            {
                _cmbField.Items.Add(f.Name);
            }

            // Populate Classes
            _cmbClass.Items.Clear();
            _cmbClass.Items.Add("-- Sınıf Seçin --");
            foreach (var c in _classes)
            {
                _cmbClass.Items.Add(c.Name);
            }
            _cmbClass.SelectedIndex = 0;

            // Populate Enterprises
            _cmbEnterprise.Items.Clear();
            _cmbEnterprise.Items.Add("-- İşletme Seçilmedi --");
            foreach (var ent in _enterprises)
            {
                _cmbEnterprise.Items.Add(ent.Name);
            }
            _cmbEnterprise.SelectedIndex = 0;

            // Populate Teachers
            _cmbTeacher.Items.Clear();
            _cmbTeacher.Items.Add("-- Öğretmen Seçilmedi --");
            foreach (var t in _teachers)
            {
                _cmbTeacher.Items.Add(t.FullName);
            }
            _cmbTeacher.SelectedIndex = 0;

            if (_existingStudent != null)
            {
                _txtNumber.Text = _existingStudent.StudentNumber;
                _txtName.Text = _existingStudent.FullName;
                _txtTcNo.Text = _existingStudent.TcKimlikNo ?? "";
                _txtParentName.Text = _existingStudent.ParentName ?? "";
                _txtPhone.Text = _existingStudent.Phone ?? "";
                _txtParentPhone.Text = _existingStudent.ParentPhone ?? "";
                _txtIban.Text = _existingStudent.Iban ?? "";

                SetDateValue(_dtpBirthDate, _existingStudent.BirthDate);
                UpdateAgeDisplay();
                SetDateValue(_dtpSgkEntry, _existingStudent.SgkEntryDate);
                SetDateValue(_dtpSgkExit, _existingStudent.SgkExitDate);

                _cmbGender.SelectedItem = string.Equals(_existingStudent.Gender, "Kız", StringComparison.OrdinalIgnoreCase) ? "Kız" : "Erkek";
                _chkFamilyInsurance.Checked = _existingStudent.HasFamilyHealthInsurance;
                _cmbEduPlace.SelectedItem = string.Equals(_existingStudent.EducationPlace, "Okulda", StringComparison.OrdinalIgnoreCase) ? "Okulda" : "İşletmede (Staj)";

                // Select class
                var classIdx = _classes.FindIndex(c => c.Name.Equals(_existingStudent.Classroom, StringComparison.OrdinalIgnoreCase));
                if (classIdx >= 0)
                {
                    _cmbClass.SelectedIndex = classIdx + 1;
                    _pnlSgk.Enabled = true;

                    var selClass = _classes[classIdx];
                    var edu = !string.IsNullOrWhiteSpace(selClass.EducationType)
                        ? selClass.EducationType
                        : (!string.IsNullOrWhiteSpace(_existingStudent.EducationType) ? _existingStudent.EducationType : "Örgün Eğitim");

                    if (!_cmbEducationType.Items.Contains(edu))
                    {
                        _cmbEducationType.Items.Add(edu);
                    }
                    _cmbEducationType.SelectedItem = edu;

                    bool isMesem = string.Equals(selClass.InstitutionType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(selClass.EducationType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                                   (selClass.Name != null && selClass.Name.Contains("MESEM", StringComparison.OrdinalIgnoreCase));
                    UpdateSgkPrimItems(isMesem);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(_existingStudent.FieldName))
                    {
                        _cmbField.Text = _existingStudent.FieldName;
                    }
                    if (!string.IsNullOrWhiteSpace(_existingStudent.EducationType))
                    {
                        if (!_cmbEducationType.Items.Contains(_existingStudent.EducationType))
                        {
                            _cmbEducationType.Items.Add(_existingStudent.EducationType);
                        }
                        _cmbEducationType.SelectedItem = _existingStudent.EducationType;
                    }
                }

                // Restore SGK Prim Kodu
                if (!string.IsNullOrWhiteSpace(_existingStudent.SgkPrimKodu))
                {
                    bool found = false;
                    for (int i = 0; i < _cmbSgkPrimKodu.Items.Count; i++)
                    {
                        var itemStr = _cmbSgkPrimKodu.Items[i]?.ToString() ?? "";
                        if (itemStr.StartsWith(_existingStudent.SgkPrimKodu, StringComparison.OrdinalIgnoreCase) ||
                            itemStr.Contains(_existingStudent.SgkPrimKodu, StringComparison.OrdinalIgnoreCase))
                        {
                            _cmbSgkPrimKodu.SelectedIndex = i;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        _cmbSgkPrimKodu.Items.Add(_existingStudent.SgkPrimKodu);
                        _cmbSgkPrimKodu.SelectedItem = _existingStudent.SgkPrimKodu;
                    }
                }

                // Select enterprise
                if (_existingStudent.EnterpriseId.HasValue)
                {
                    var entIdx = _enterprises.FindIndex(e => e.Id == _existingStudent.EnterpriseId.Value);
                    if (entIdx >= 0)
                    {
                        _cmbEnterprise.SelectedIndex = entIdx + 1;
                    }
                }

                // Select teacher
                if (_existingStudent.TeacherId.HasValue)
                {
                    var teachIdx = _teachers.FindIndex(t => t.Id == _existingStudent.TeacherId.Value);
                    if (teachIdx >= 0)
                    {
                        _cmbTeacher.SelectedIndex = teachIdx + 1;
                    }
                }

                // Duty days
                var dutyList = _existingStudent.GetDutyDaysList();
                foreach (var kv in _dutyDayCheckBoxes)
                {
                    kv.Value.Checked = dutyList.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                _txtTcNo.Text = "";
                _txtParentName.Text = "";
                _txtPhone.Text = "";
                _txtParentPhone.Text = "";
                _txtIban.Text = "";
                _dtpBirthDate.CustomFormat = " ";
                
                _dtpSgkEntry.CustomFormat = " ";
                _dtpSgkExit.CustomFormat = " ";
                _cmbGender.SelectedIndex = 0;
                _chkFamilyInsurance.Checked = true;
                _cmbEduPlace.SelectedIndex = 0;
                _pnlSgk.Enabled = false;
            }
        }
        catch
        {
            // Ignore load errors
        }
        finally
        {
            _previousClassIndex = _cmbClass.SelectedIndex;
            _isInitializing = false;
            if (_existingStudent == null && _cmbClass.SelectedIndex > 0)
            {
                EvaluateSgkPrimFormula();
            }
            UpdateEnterpriseControlsState();
            _isDirty = false;
        }
    }

    private void UpdateSgkPrimItems(bool isMesem)
    {
        string? currentVal = _cmbSgkPrimKodu.SelectedItem?.ToString();
        _cmbSgkPrimKodu.Items.Clear();

        if (isMesem)
        {
            // Öğretim türü MESEM ise listede 7 ve 42 aktif
            _cmbSgkPrimKodu.Items.Add(SgkPrim7);
            _cmbSgkPrimKodu.Items.Add(SgkPrim42);
        }
        else
        {
            // Örgün eğitimse listede 49 ve 50 aktif
            _cmbSgkPrimKodu.Items.Add(SgkPrim49);
            _cmbSgkPrimKodu.Items.Add(SgkPrim50);
        }

        if (!string.IsNullOrEmpty(currentVal))
        {
            for (int i = 0; i < _cmbSgkPrimKodu.Items.Count; i++)
            {
                if (_cmbSgkPrimKodu.Items[i]?.ToString() == currentVal)
                {
                    _cmbSgkPrimKodu.SelectedIndex = i;
                    return;
                }
            }
        }

        if (_cmbSgkPrimKodu.Items.Count > 0 && _cmbSgkPrimKodu.SelectedIndex < 0)
        {
            _cmbSgkPrimKodu.SelectedIndex = 0;
        }
    }

    private void EvaluateSgkPrimFormula()
    {
        if (_isInitializing) return;
        if (_cmbClass.SelectedIndex <= 0 || _cmbClass.SelectedIndex - 1 >= _classes.Count) return;

        var selectedClass = _classes[_cmbClass.SelectedIndex - 1];

        // Öğretim Türü: MESEM mi?
        bool isMesem = string.Equals(selectedClass.InstitutionType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(selectedClass.EducationType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                       (selectedClass.Name != null && selectedClass.Name.Contains("MESEM", StringComparison.OrdinalIgnoreCase));

        // Cinsiyet Erkek mi?
        bool isMale = (_cmbGender.SelectedItem?.ToString() ?? "Erkek").Equals("Erkek", StringComparison.OrdinalIgnoreCase);

        // Yaş >= 20 mi?
        int age = -1;
        var birthDate = GetDateValue(_dtpBirthDate);
        if (birthDate.HasValue)
        {
            var today = DateTime.Today;
            var b = birthDate.Value.Date;
            age = today.Year - b.Year;
            if (b > today.AddYears(-age)) age--;
        }
        bool isMaleOver20 = isMale && (age >= 20);

        // Aile Sağlık Güvencesi Var mı?
        bool hasFamilyInsurance = _chkFamilyInsurance.Checked;

        string targetPrefix;
        if (isMesem)
        {
            // MESEM: Öğrenci Erkek ve Yaş ≥ 20 VEYA Aile Sağlık Güvencesi Yok ⇒ 42 Nolu; Diğer ⇒ 7 Nolu
            targetPrefix = (isMaleOver20 || !hasFamilyInsurance) ? "42 Nolu" : "7 Nolu";
        }
        else
        {
            // Örgün Eğitim: Aile Güvencesi Var ⇒ 49 Nolu; Aile Güvencesi Yok ⇒ 50 Nolu
            targetPrefix = hasFamilyInsurance ? "49 Nolu" : "50 Nolu";
        }

        // Dropdown'da ilgili prefix ile başlayan elemanı seç
        for (int i = 0; i < _cmbSgkPrimKodu.Items.Count; i++)
        {
            if (_cmbSgkPrimKodu.Items[i]?.ToString()?.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                _cmbSgkPrimKodu.SelectedIndex = i;
                break;
            }
        }
    }

    private void SetupNullableDatePicker(DateTimePicker dtp)
    {
        dtp.Format = DateTimePickerFormat.Custom;
        dtp.CustomFormat = " ";
        dtp.ValueChanged += (s, e) =>
        {
            if (!_isInitializing)
            {
                if (dtp.CustomFormat == " ")
                    dtp.CustomFormat = "dd.MM.yyyy";
                MarkAsDirty();
            }
        };
        dtp.CloseUp += (s, e) =>
        {
            if (!_isInitializing)
            {
                dtp.CustomFormat = "dd.MM.yyyy";
                MarkAsDirty();
            }
        };
        dtp.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                dtp.CustomFormat = " ";
                MarkAsDirty();
            }
        };
    }

    private static void SetDateValue(DateTimePicker dtp, DateTime? date)
    {
        if (date.HasValue)
        {
            dtp.CustomFormat = "dd.MM.yyyy";
            dtp.Value = date.Value;
        }
        else
        {
            dtp.CustomFormat = " ";
        }
    }

    private static DateTime? GetDateValue(DateTimePicker dtp)
    {
        if (dtp.CustomFormat == " ")
            return null;
        return dtp.Value.Date;
    }

    private void CmbClass_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isRevertingClass)
            return;

        if (!_isInitializing && _previousClassIndex > 0 && _cmbClass.SelectedIndex != _previousClassIndex)
        {
            var result = MessageBox.Show(
                "Öğrenci sınıfı değiştiğinde SGK Bilgileri değişmektedir.\r\n\r\nDeğişsin mi?",
                "Sınıf Değişikliği",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                _isRevertingClass = true;
                try
                {
                    _cmbClass.SelectedIndex = _previousClassIndex;
                }
                finally
                {
                    _isRevertingClass = false;
                }
                return;
            }
        }

        _previousClassIndex = _cmbClass.SelectedIndex;
        MarkAsDirty();

        bool hasClass = _cmbClass.SelectedIndex > 0 && _cmbClass.SelectedIndex - 1 < _classes.Count;
        _pnlSgk.Enabled = hasClass;

        if (hasClass)
        {
            var selectedClass = _classes[_cmbClass.SelectedIndex - 1];
            _cmbField.Text = selectedClass.FieldName ?? "";

            string eduType = "Örgün Eğitim";
            if (!string.IsNullOrWhiteSpace(selectedClass.EducationType))
            {
                eduType = selectedClass.EducationType;
            }
            else if (string.Equals(selectedClass.InstitutionType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                     (selectedClass.Name != null && selectedClass.Name.Contains("MESEM", StringComparison.OrdinalIgnoreCase)))
            {
                eduType = "MESEM";
            }

            if (!_cmbEducationType.Items.Contains(eduType))
            {
                _cmbEducationType.Items.Add(eduType);
            }
            _cmbEducationType.SelectedItem = eduType;

            var days = selectedClass.GetDaysList();
            foreach (var kv in _internshipDayCheckBoxes)
            {
                kv.Value.Checked = days.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);
            }

            bool isMesem = string.Equals(selectedClass.InstitutionType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(selectedClass.EducationType, "MESEM", StringComparison.OrdinalIgnoreCase) ||
                           (selectedClass.Name != null && selectedClass.Name.Contains("MESEM", StringComparison.OrdinalIgnoreCase));

            UpdateSgkPrimItems(isMesem);
        }
        else
        {
            _cmbField.Text = "";
            _cmbEducationType.SelectedIndex = -1;
            foreach (var kv in _internshipDayCheckBoxes)
            {
                kv.Value.Checked = false;
            }
            _cmbSgkPrimKodu.Items.Clear();
            _cmbSgkPrimKodu.Items.AddRange([SgkPrim7, SgkPrim42, SgkPrim49, SgkPrim50]);
        }

        EvaluateSgkPrimFormula();
        UpdateEnterpriseControlsState();
    }

    private void UpdateEnterpriseControlsState()
    {
        bool hasClass = _cmbClass.SelectedIndex > 0 && _cmbClass.SelectedIndex - 1 < _classes.Count;
        bool hasInternship = false;

        if (hasClass)
        {
            var selectedClass = _classes[_cmbClass.SelectedIndex - 1];
            hasInternship = selectedClass.HasInternship;
        }

        _lblInternshipDays.Enabled = hasInternship;
        _pnlInternshipDays.Enabled = hasInternship;
        _lblEnt.Enabled = hasInternship;
        _cmbEnterprise.Enabled = hasInternship;
        _lblTeacher.Enabled = hasInternship;
        _cmbTeacher.Enabled = hasInternship;
        _lblDutyDays.Enabled = hasInternship;
        _pnlDutyDays.Enabled = hasInternship;

        if (!hasInternship)
        {
            if (_cmbEnterprise.Items.Count > 0)
                _cmbEnterprise.SelectedIndex = 0;
            if (_cmbTeacher.Items.Count > 0)
                _cmbTeacher.SelectedIndex = 0;

            _lblEnterpriseInfo.Visible = false;
            _lblTeacherInfo.Visible = false;

            foreach (var kv in _dutyDayCheckBoxes)
            {
                kv.Value.Checked = false;
                kv.Value.Enabled = false;
            }
            foreach (var kv in _internshipDayCheckBoxes)
            {
                kv.Value.Checked = false;
            }
        }
        else
        {
            foreach (var kv in _dutyDayCheckBoxes)
            {
                kv.Value.Enabled = true;
            }
        }
    }

    private void CmbEnterprise_SelectedIndexChanged(object? sender, EventArgs e)
    {
        MarkAsDirty();
        if (_cmbEnterprise.SelectedIndex > 0 && _cmbEnterprise.SelectedIndex - 1 < _enterprises.Count)
        {
            var ent = _enterprises[_cmbEnterprise.SelectedIndex - 1];
            var distText = string.IsNullOrWhiteSpace(ent.DistanceText) ? "Belirtilmemiş" : ent.DistanceText;
            _lblEnterpriseInfo.Text = $"🚗 Okula Uzaklık: {distText} | Kontenjan: {ent.Capacity} Öğrenci";
            _lblEnterpriseInfo.Visible = true;
        }
        else
        {
            _lblEnterpriseInfo.Visible = false;
        }
    }

    private void CmbTeacher_SelectedIndexChanged(object? sender, EventArgs e)
    {
        MarkAsDirty();
        if (_cmbTeacher.SelectedIndex > 0 && _cmbTeacher.SelectedIndex - 1 < _teachers.Count)
        {
            var teacher = _teachers[_cmbTeacher.SelectedIndex - 1];
            var days = string.IsNullOrWhiteSpace(teacher.AvailableDays) ? "Belirtilmemiş" : teacher.AvailableDays;
            var branch = string.IsNullOrWhiteSpace(teacher.Branch) ? "" : teacher.Branch;
            _lblTeacherInfo.Text = $"🗓️ Görev Günleri: {days} | Branş: {branch}";
            _lblTeacherInfo.Visible = true;
        }
        else
        {
            _lblTeacherInfo.Visible = false;
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen öğrenci adını soyadını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        if (_cmbClass.SelectedIndex <= 0)
        {
            MessageBox.Show("Lütfen bir sınıf seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbClass.Focus();
            return;
        }

        // Öğrenci numarası tekrar kontrolü
        var studentNumber = _txtNumber.Text.Trim();
        if (!string.IsNullOrWhiteSpace(studentNumber))
        {
            var allStudents = await _studentRepo.GetAllAsync();
            var existingWithSameNumber = allStudents
                .FirstOrDefault(s => s.IsActive
                                  && s.StudentNumber.Trim() == studentNumber
                                  && s.Id != (_existingStudent?.Id ?? 0));
            if (existingWithSameNumber != null)
            {
                MessageBox.Show(
                    $"Bu öğrenci numarası ({studentNumber}) zaten kayıtlı:\n{existingWithSameNumber.FullName} ({existingWithSameNumber.Classroom})\n\nLütfen farklı bir numara giriniz.",
                    "Mükerrer Öğrenci Numarası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _txtNumber.Focus();
                return;
            }
        }

        var selectedClass = _classes[_cmbClass.SelectedIndex - 1];

        int? enterpriseId = null;
        string? enterpriseName = null;
        if (selectedClass.HasInternship && _cmbEnterprise.SelectedIndex > 0 && _cmbEnterprise.SelectedIndex - 1 < _enterprises.Count)
        {
            var ent = _enterprises[_cmbEnterprise.SelectedIndex - 1];
            enterpriseId = ent.Id;
            enterpriseName = ent.Name;
        }

        int? teacherId = null;
        string? teacherName = null;
        if (selectedClass.HasInternship && _cmbTeacher.SelectedIndex > 0 && _cmbTeacher.SelectedIndex - 1 < _teachers.Count)
        {
            var t = _teachers[_cmbTeacher.SelectedIndex - 1];
            teacherId = t.Id;
            teacherName = t.FullName;
        }

        var dutyDays = selectedClass.HasInternship
            ? string.Join(",", _dutyDayCheckBoxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key))
            : string.Empty;
        var (gradeLevel, _) = FeeCalculator.ParseClassAndBranch(selectedClass.Name);

        // SGK İşe Giriş Tarihi Kontrolleri ve İki Kademeli Onay
        var sgkEntryDate = GetDateValue(_dtpSgkEntry);
        var sgkExitDate = GetDateValue(_dtpSgkExit);

        if (sgkEntryDate.HasValue)
        {
            var entry = sgkEntryDate.Value.Date;
            var today = DateTime.Today;
            var minAllowed = SgkDateValidator.GetDefaultSgkEntryDate(today);

            // Pazartesi günü aynı gün (today) geçerli, diğer günlerde en erken ertesi iş günü (minAllowed)
            bool isEarlierThanAllowed = today.DayOfWeek == DayOfWeek.Monday ? (entry < today) : (entry < minAllowed);

            if (isEarlierThanAllowed)
            {
                string ruleText = today.DayOfWeek == DayOfWeek.Monday
                    ? "Bugün Pazartesi olduğu için işe giriş tarihi en erken bugün (aynı gün) olabilir."
                    : $"Bugün Pazartesi olmadığı için işe giriş tarihi mevzuat gereği en erken ertesi iş günü ({minAllowed:dd.MM.yyyy dddd}) olabilir.";

                // 1. UYARI
                var confirm1 = MessageBox.Show(
                    $"⚠️ [1. UYARI: GEÇMİŞ / ERKEN İŞE GİRİŞ TARİHİ]\n\n" +
                    $"Seçtiğiniz SGK İşe Giriş Tarihi: {entry:dd.MM.yyyy dddd}\n\n" +
                    $"{ruleText}\n\n" +
                    "Önceki bir tarihe işe giriş kaydetmek cezai duruma veya SGK bildirim gecikmesine sebep olabilir!\n\n" +
                    "Yine de bu tarihi kaydetmek istiyor musunuz?",
                    "SGK İşe Giriş Tarihi Uyarısı (1. Onay)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm1 != DialogResult.Yes)
                {
                    _dtpSgkEntry.Focus();
                    return;
                }

                // 2. UYARI (İkinci güvenlik onayı)
                var confirm2 = MessageBox.Show(
                    $"🚨 [2. VE SON ONAY: DİKKAT!]\n\n" +
                    $"{entry:dd.MM.yyyy} tarihi SGK mevzuatında öngörülen bildirim süresinden öncedir!\n\n" +
                    "Tüm sorumluluğu kabul ederek bu işe giriş tarihini kaydetmeyi kesin olarak onaylıyor musunuz?",
                    "SGK İşe Giriş Tarihini Onayla (2. Onay)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm2 != DialogResult.Yes)
                {
                    _dtpSgkEntry.Focus();
                    return;
                }
            }
        }

        if (sgkExitDate.HasValue)
        {
            if (!SgkDateValidator.ValidateSgkExitDate(sgkExitDate.Value, sgkEntryDate, DateTime.Today, out string exitError))
            {
                MessageBox.Show(exitError, "Geçersiz SGK İşten Çıkış Tarihi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dtpSgkExit.Focus();
                return;
            }
        }

        StudentResult = new Student
        {
            Id = _existingStudent?.Id ?? 0,
            TcKimlikNo = _txtTcNo.Text.Trim(),
            Iban = string.IsNullOrWhiteSpace(_txtIban.Text) ? null : _txtIban.Text.Trim().ToUpperInvariant(),
            Gender = _cmbGender.SelectedItem?.ToString() ?? "Erkek",
            HasFamilyHealthInsurance = _chkFamilyInsurance.Checked,
            EducationPlace = _cmbEduPlace.SelectedItem?.ToString() ?? "İşletmede (Staj)",
            SgkPrimKodu = _cmbSgkPrimKodu.SelectedItem?.ToString(),
            BirthDate = GetDateValue(_dtpBirthDate),
            SgkEntryDate = GetDateValue(_dtpSgkEntry),
            SgkExitDate = GetDateValue(_dtpSgkExit),
            StudentNumber = _txtNumber.Text.Trim(),
            FullName = name,
            ClassId = selectedClass.Id,
            Classroom = selectedClass.Name,
            Grade = gradeLevel > 0 ? gradeLevel.ToString() : (_existingStudent?.Grade ?? "12"),
            FieldId = selectedClass.FieldId,
            FieldName = selectedClass.FieldName,
            InternshipDays = selectedClass.InternshipDays,
            EducationType = _cmbEducationType.SelectedItem?.ToString() ?? selectedClass.EducationType ?? "Örgün Eğitim",
            EnterpriseId = enterpriseId,
            EnterpriseName = enterpriseName,
            TeacherId = teacherId,
            TeacherName = teacherName,
            DutyDays = dutyDays,
            FeeCategory = _existingStudent?.FeeCategory ?? "kat1",
            ParentName = string.IsNullOrWhiteSpace(_txtParentName.Text) ? null : _txtParentName.Text.Trim(),
            Phone = string.IsNullOrWhiteSpace(_txtPhone.Text) ? null : _txtPhone.Text.Trim(),
            ParentPhone = string.IsNullOrWhiteSpace(_txtParentPhone.Text) ? null : _txtParentPhone.Text.Trim(),
            IsActive = _existingStudent?.IsActive ?? true
        };

        _isDirty = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateAgeDisplay()
    {
        if (_lblAgeDisplay == null) return;
        if (_dtpBirthDate.CustomFormat != " " && _dtpBirthDate.Value > new DateTime(1900, 1, 1))
        {
            var birth = _dtpBirthDate.Value.Date;
            var today = DateTime.Today;
            int age = today.Year - birth.Year;
            if (birth > today.AddYears(-age)) age--;
            if (age >= 0 && age < 120)
            {
                _lblAgeDisplay.Text = $"(Yaş: {age})";
                _lblAgeDisplay.ForeColor = Color.FromArgb(37, 99, 235);
                return;
            }
        }
        _lblAgeDisplay.Text = "";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (DialogResult == DialogResult.OK || !_isDirty)
            return;

        var result = MessageBox.Show(
            "Yaptığınız değişiklikler iptal edilecektir.\r\n\r\nÇıkmak istediğinize emin misiniz?",
            "Uyarı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            e.Cancel = true;
            DialogResult = DialogResult.None;
        }
    }
}
