using System.Data;
using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class AddAbsenceStudentDialog : Form
{
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;

    private ComboBox _cmbClass = null!;
    private DataGridView _grid = null!;
    private CheckBox _chkSelectAll = null!;
    private CheckBox _chkSetSgkExit = null!;
    private Label _lblExitDate = null!;
    private DateTimePicker _dtpExitDate = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;
    private Label _lblSummary = null!;

    private List<SchoolClass> _classes = new();
    private List<Student> _currentClassStudents = new();

    public AddAbsenceStudentDialog(IStudentRepository studentRepo, ISchoolClassRepository classRepo)
    {
        _studentRepo = studentRepo;
        _classRepo = classRepo;

        InitializeComponent();
        LoadClassesAsync();
    }

    private void InitializeComponent()
    {
        Text = "➕ Sürekli Devamsız Öğrenci Ekle";
        Size = new Size(860, 560);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 480);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "➕ Sürekli Devamsız Öğrenci Ekle",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 10),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Lütfen önce bir sınıf seçiniz, ardından devamsız olarak işaretlenecek öğrencileri seçiniz.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(21, 34),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle]);

        // Filter / Selection Panel
        var pnlSelection = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblClass = new Label
        {
            Text = "Sınıf Seçiniz:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(20, 14),
            AutoSize = true
        };

        _cmbClass = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9.5f),
            Location = new Point(110, 10),
            Width = 220
        };
        _cmbClass.SelectedIndexChanged += CmbClass_SelectedIndexChanged;

        _chkSelectAll = new CheckBox
        {
            Text = "Tümünü Seç / Kaldır",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(360, 12),
            AutoSize = true,
            Enabled = false
        };
        _chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;

        _lblSummary = new Label
        {
            Text = "0 öğrenci seçildi",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(540, 14),
            AutoSize = true
        };

        pnlSelection.Controls.AddRange([lblClass, _cmbClass, _chkSelectAll, _lblSummary]);

        // Bottom Footer Panel
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        _chkSetSgkExit = new CheckBox
        {
            Text = "SGK İşten Çıkış Bildirgesi Yapıldı",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(20, 20),
            AutoSize = true,
            Checked = false
        };

        _lblExitDate = new Label
        {
            Text = "Çıkış Tarihi:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Gray,
            Location = new Point(245, 21),
            AutoSize = true
        };

        _dtpExitDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today,
            Location = new Point(325, 18),
            Width = 110,
            Enabled = false,
            Font = new Font("Segoe UI", 9f)
        };

        _chkSetSgkExit.CheckedChanged += (s, e) =>
        {
            _dtpExitDate.Enabled = _chkSetSgkExit.Checked;
            _lblExitDate.ForeColor = _chkSetSgkExit.Checked ? Color.FromArgb(30, 41, 59) : Color.Gray;
        };

        _btnSave = new Button
        {
            Text = "➕ Devamsız Olarak Ekle",
            Size = new Size(185, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Enabled = false
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            Text = "İptal",
            Size = new Size(90, 36),
            BackColor = Color.White,
            ForeColor = UITheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        void PositionFooterButtons()
        {
            _btnSave.Location = new Point(pnlFooter.ClientSize.Width - 200, 14);
            _btnCancel.Location = new Point(_btnSave.Left - 100, 14);
        }
        pnlFooter.Resize += (s, e) => PositionFooterButtons();
        PositionFooterButtons();

        pnlFooter.Controls.AddRange([_chkSetSgkExit, _lblExitDate, _dtpExitDate, _btnSave, _btnCancel]);

        // DataGridView
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = false,
            Enabled = false,
            Font = new Font("Segoe UI", 9f)
        };

        UITheme.ApplyGridStyle(_grid);
        _grid.ReadOnly = false;

        var colCheck = new DataGridViewCheckBoxColumn
        {
            Name = "ColSelect",
            HeaderText = "Seç",
            Width = 50,
            ReadOnly = false
        };

        var colNum = new DataGridViewTextBoxColumn
        {
            Name = "StudentNumber",
            HeaderText = "Okul No",
            Width = 90,
            ReadOnly = true
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "FullName",
            HeaderText = "Ad Soyad",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        };

        var colClass = new DataGridViewTextBoxColumn
        {
            Name = "Classroom",
            HeaderText = "Sınıf",
            Width = 100,
            ReadOnly = true
        };

        var colEnterprise = new DataGridViewTextBoxColumn
        {
            Name = "EnterpriseName",
            HeaderText = "Staj İşletmesi",
            Width = 200,
            ReadOnly = true
        };

        _grid.Columns.AddRange([colCheck, colNum, colName, colClass, colEnterprise]);

        bool isUpdatingSelectAll = false;

        _grid.CellValueChanged += (s, e) =>
        {
            if (e.ColumnIndex == 0 && !isUpdatingSelectAll) UpdateSelectedCount();
        };

        _grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        _grid.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Enabled && e.ColumnIndex != 0)
            {
                var cell = _grid.Rows[e.RowIndex].Cells[0];
                bool currentVal = cell.Value is true;
                cell.Value = !currentVal;
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                UpdateSelectedCount();
            }
        };

        Controls.Add(_grid);
        Controls.Add(pnlSelection);
        Controls.Add(pnlHeader);
        Controls.Add(pnlFooter);
    }

    private async void LoadClassesAsync()
    {
        var classes = (await _classRepo.GetAllAsync()).OrderBy(c => c.Name).ToList();
        _classes = classes;

        _cmbClass.Items.Clear();
        _cmbClass.Items.Add("-- Sınıf Seçiniz --");
        foreach (var c in classes)
        {
            _cmbClass.Items.Add(c.Name);
        }
        _cmbClass.SelectedIndex = 0;
    }

    private async void CmbClass_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cmbClass.SelectedIndex <= 0)
        {
            _grid.Rows.Clear();
            _grid.Enabled = false;
            _chkSelectAll.Enabled = false;
            _chkSelectAll.Checked = false;
            _btnSave.Enabled = false;
            _lblSummary.Text = "0 öğrenci seçildi";
            return;
        }

        string selectedClassName = _cmbClass.SelectedItem.ToString()!;
        var selectedClass = _classes.FirstOrDefault(c => c.Name == selectedClassName);

        var allStudents = (await _studentRepo.GetAllAsync()).ToList();
        _currentClassStudents = allStudents
            .Where(s => (s.ClassId.HasValue && selectedClass != null && s.ClassId == selectedClass.Id) ||
                        string.Equals(s.Classroom, selectedClassName, StringComparison.OrdinalIgnoreCase))
            .Where(s => !s.IsSurekliDevamsiz)
            .OrderBy(s => s.StudentNumber)
            .ThenBy(s => s.FullName)
            .ToList();

        _grid.Rows.Clear();
        foreach (var st in _currentClassStudents)
        {
            int rowIndex = _grid.Rows.Add(false, st.StudentNumber, st.FullName, st.Classroom, st.EnterpriseName);
            _grid.Rows[rowIndex].Tag = st;
        }

        _grid.Enabled = _currentClassStudents.Count > 0;
        _chkSelectAll.Enabled = _currentClassStudents.Count > 0;
        _chkSelectAll.Checked = false;
        UpdateSelectedCount();
    }

    private void ChkSelectAll_CheckedChanged(object? sender, EventArgs e)
    {
        bool isChecked = _chkSelectAll.Checked;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Cells[0].Value = isChecked;
        }
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        int count = 0;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells[0].Value is true) count++;
        }

        _lblSummary.Text = $"{count} / {_grid.Rows.Count} öğrenci seçildi";
        _btnSave.Enabled = count > 0;
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        var selectedStudents = new List<Student>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells[0].Value is true && row.Tag is Student st)
            {
                selectedStudents.Add(st);
            }
        }

        if (selectedStudents.Count == 0)
        {
            MessageBox.Show("Lütfen en az bir öğrenci seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"{selectedStudents.Count} adet öğrenci sürekli devamsız olarak işaretlenecektir. Onaylıyor musunuz?",
            "Onay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        _btnSave.Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            DateTime? exitDateToSet = _chkSetSgkExit.Checked ? _dtpExitDate.Value.Date : null;

            foreach (var st in selectedStudents)
            {
                st.IsSurekliDevamsiz = true;
                st.SgkExitDate = exitDateToSet;
                await _studentRepo.UpdateAsync(st);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
            _btnSave.Enabled = true;
        }
    }
}
