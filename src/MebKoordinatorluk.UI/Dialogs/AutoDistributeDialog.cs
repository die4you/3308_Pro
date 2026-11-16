using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.UI.Dialogs;

public class AutoDistributeDialog : Form
{
    private readonly ITeacherRepository _teacherRepo;
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly List<Student>? _targetStudents;
    private readonly string? _scopeDescription;

    private CheckBox _chkDayMatch = null!;
    private CheckBox _chkBranchMatch = null!;
    private CheckBox _chkClusterEnterprises = null!;
    private CheckBox _chkClusterAddressGroups = null!;
    private CheckBox _chkBalanceWorkload = null!;
    private CheckBox _chkAssignEnterprises = null!;

    public DistributionResult? Result { get; private set; }

    public AutoDistributeDialog(
        ITeacherRepository teacherRepo,
        IEnterpriseRepository enterpriseRepo,
        IStudentRepository studentRepo,
        List<Student>? targetStudents = null,
        string? scopeDescription = null)
    {
        _teacherRepo = teacherRepo;
        _enterpriseRepo = enterpriseRepo;
        _studentRepo = studentRepo;
        _targetStudents = targetStudents;
        _scopeDescription = scopeDescription;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "⚡ Akıllı Koordinatörlük Otomatik Dağıtım Motoru";
        this.Size = new Size(580, _targetStudents != null ? 535 : 480);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.Font = new Font("Segoe UI", 9f);

        var lblTitle = new Label
        {
            Text = "Akıllı Dağıtım Algoritması Parametreleri",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 18),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = "Sistem, öğretmen koordinatörlük günleri ile öğrenci staj günlerini eşleştirir;\nalan/branş uyumunu ve adil yük dengesini gözeterek atamaları yapar.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(24, 44),
            Size = new Size(520, 36)
        };

        int optionsTop = 96;
        if (_targetStudents != null)
        {
            var pnlScope = new Panel
            {
                Location = new Point(24, 84),
                Size = new Size(520, 38),
                BackColor = Color.FromArgb(239, 246, 255), // blue-50
                Padding = new Padding(10, 8, 10, 8)
            };
            pnlScope.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(191, 219, 254), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlScope.Width - 1, pnlScope.Height - 1);
            };

            var lblScopeText = new Label
            {
                Text = $"🎯 Dağıtım Kapsamı: {_targetStudents.Count} Öğrenci ({_scopeDescription ?? "Ortak Havuz"})",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 78, 216), // blue-700
                AutoSize = true,
                Location = new Point(8, 9)
            };
            pnlScope.Controls.Add(lblScopeText);
            this.Controls.Add(pnlScope);
            optionsTop = 130;
        }

        var pnlOptions = new Panel
        {
            Location = new Point(24, optionsTop),
            Size = new Size(520, 280),
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(16)
        };
        pnlOptions.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlOptions.Width - 1, pnlOptions.Height - 1);
        };

        _chkDayMatch = new CheckBox
        {
            Text = "📅 Gün Uyumu Zorunlu Olsun (Öğretmen ve öğrenci günleri örtüşsün)",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(16, 16),
            Size = new Size(480, 24)
        };

        _chkBranchMatch = new CheckBox
        {
            Text = "📚 Alan / Branş Uyumu Öncelikli Olsun (Öğretmenin alanı öğrenciyle eşleşsin)",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(16, 54),
            Size = new Size(480, 24)
        };

        _chkClusterEnterprises = new CheckBox
        {
            Text = "🏢 İşletme Bütünlüğü (Aynı işletmedeki öğrencilere aynı koordinatörü ver)",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(16, 92),
            Size = new Size(480, 24)
        };

        _chkClusterAddressGroups = new CheckBox
        {
            Text = "📍 Adres Grubu Kümeleme (Birbirine yakın işletmeleri aynı öğretmene ata)",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(16, 130),
            Size = new Size(480, 24)
        };

        _chkBalanceWorkload = new CheckBox
        {
            Text = "⚖️ Adil Yük Dengeleme (Öğretmenler arası öğrenci sayılarını eşit dağıt)",
            Checked = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(16, 168),
            Size = new Size(480, 24)
        };

        _chkAssignEnterprises = new CheckBox
        {
            Text = "⚡ İşletmesi Olmayanları Yerleştir (Boş kontenjanı olan işletmelere ata)",
            Checked = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 185, 129),
            Location = new Point(16, 206),
            Size = new Size(480, 24)
        };

        pnlOptions.Controls.Add(_chkDayMatch);
        pnlOptions.Controls.Add(_chkBranchMatch);
        pnlOptions.Controls.Add(_chkClusterEnterprises);
        pnlOptions.Controls.Add(_chkClusterAddressGroups);
        pnlOptions.Controls.Add(_chkBalanceWorkload);
        pnlOptions.Controls.Add(_chkAssignEnterprises);

        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var btnRun = new Button
        {
            Text = "⚡ Dağıtımı Başlat",
            Location = new Point(360, 12),
            Size = new Size(180, 36),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnRun.FlatAppearance.BorderSize = 0;
        btnRun.Click += async (s, e) => await ExecuteDistributionAsync();

        var btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(260, 12),
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => { this.Close(); };

        pnlBottom.Controls.Add(btnRun);
        pnlBottom.Controls.Add(btnCancel);

        this.Controls.Add(lblTitle);
        this.Controls.Add(lblDesc);
        this.Controls.Add(pnlOptions);
        this.Controls.Add(pnlBottom);
    }

    private async Task ExecuteDistributionAsync()
    {
        var teachers = (await _teacherRepo.GetAllAsync()).ToList();
        var enterprises = (await _enterpriseRepo.GetAllAsync()).ToList();
        var students = _targetStudents ?? (await _studentRepo.GetAllAsync()).ToList();

        if (students.Count == 0)
        {
            MessageBox.Show("Dağıtım yapılacak öğrenci bulunamadı!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirmResult = MessageBox.Show(
            "Otomatik dağıtım başlatıldığında kapsamdaki öğrencilerin mevcut koordinatör ve işletme atama verileri güncellenecek ve değişecektir.\n\nDağıtım işlemini onaylayıp başlatmak istiyor musunuz?",
            "Otomatik Dağıtım Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmResult != DialogResult.Yes)
        {
            return;
        }

        var options = new DistributionOptions
        {
            PrioritizeDayMatch = _chkDayMatch.Checked,
            PrioritizeBranchMatch = _chkBranchMatch.Checked,
            ClusterEnterprises = _chkClusterEnterprises.Checked,
            ClusterAddressGroups = _chkClusterAddressGroups.Checked,
            BalanceWorkload = _chkBalanceWorkload.Checked,
            AssignUnassignedEnterprises = _chkAssignEnterprises.Checked
        };

        Result = SmartDistributionEngine.RunAutoDistribution(options, teachers, enterprises, students);

        if (!Result.Success)
        {
            MessageBox.Show(Result.Message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (var s in students)
        {
            await _studentRepo.UpdateAsync(s);
        }

        foreach (var e in enterprises)
        {
            await _enterpriseRepo.UpdateAsync(e);
        }

        MessageBox.Show(Result.Message, "Akıllı Dağıtım Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
