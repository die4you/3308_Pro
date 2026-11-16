using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Common;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class EnterprisesView : UserControl
{
    private readonly IEnterpriseRepository _enterpriseRepo;
    private readonly IAddressGroupRepository _addressGroupRepo;
    private readonly IFieldRepository _fieldRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly ISettingRepository? _settingRepo;
    private readonly ILicenseService? _licenseService;
    private AppSettings? _settings;

    private List<Enterprise> _allEnterprises = [];
    private List<AddressGroup> _addressGroups = [];
    private List<Field> _fields = [];
    private List<Student> _allStudents = [];

    private DataGridView _grid = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbFilterField = null!;
    private ComboBox _cmbFilterGroup = null!;

    public EnterprisesView(
        IEnterpriseRepository enterpriseRepo,
        IAddressGroupRepository addressGroupRepo,
        IFieldRepository fieldRepo,
        IStudentRepository studentRepo,
        ISettingRepository? settingRepo = null,
        ILicenseService? licenseService = null)
    {
        _enterpriseRepo = enterpriseRepo;
        _addressGroupRepo = addressGroupRepo;
        _fieldRepo = fieldRepo;
        _studentRepo = studentRepo;
        _settingRepo = settingRepo;
        _licenseService = licenseService;

        InitializeComponent();
        LoadLookupsAndDataAsync();
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
            BackColor = Color.Transparent,
            Padding = new Padding(24, 16, 24, 0)
        };

        var lblTitle = new Label
        {
            Text = "🏢 Staj İşletmeleri Yönetimi",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 4),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Öğrencilerin beceri eğitimi gördüğü işletmeler, yetkililer ve kontenjanlar",
            Font = new Font("Segoe UI", 9f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(26, 32),
            AutoSize = true
        };

        // Header Action Buttons
        var pnlTopButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 600,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var btnAddEnterprise = new Button
        {
            Text = "➕ Yeni İşletme Ekle",
            Size = new Size(160, 36),
            BackColor = Color.FromArgb(147, 51, 234), // purple-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnAddEnterprise.FlatAppearance.BorderSize = 0;
        btnAddEnterprise.Click += (s, e) => OpenEnterpriseDialog(null);

        var btnPrint = new Button
        {
            Text = "🖨️ Yazdır / PDF",
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(30, 64, 175), // blue-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnPrint.FlatAppearance.BorderSize = 0;
        btnPrint.Click += BtnPrint_Click;

        var btnUploadExcel = new Button
        {
            Text = "📊 Excel'den Yükle",
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(5, 150, 105), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnUploadExcel.FlatAppearance.BorderSize = 0;
        btnUploadExcel.Click += BtnUploadExcel_Click;

        var btnDownloadTemplate = new Button
        {
            Text = "📥 Şablon İndir",
            Size = new Size(110, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnDownloadTemplate.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

        pnlTopButtons.Controls.AddRange([btnAddEnterprise, btnPrint, btnUploadExcel, btnDownloadTemplate]);

        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
        {
            btnAddEnterprise.Visible = false;
            btnUploadExcel.Visible = false;
        }

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle, pnlTopButtons]);

        // Filter Bar Panel
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFilter.Width, 0);
            e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        _txtSearch = new TextBox
        {
            PlaceholderText = "🔍 İşletme adı, sektör veya ilçe ara...",
            Location = new Point(24, 12),
            Width = 240,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        _cmbFilterField = new ComboBox
        {
            Location = new Point(274, 12),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterField.SelectedIndexChanged += (s, e) => ApplyFilter();

        _cmbFilterGroup = new ComboBox
        {
            Location = new Point(444, 12),
            Width = 170,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilterGroup.SelectedIndexChanged += (s, e) => ApplyFilter();

        var lblSyncNote = new Label
        {
            Text = "* Kontenjan doluluk durumu öğrenci atamaları ile anlık senkronizedir.",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            AutoSize = true,
            Location = new Point(630, 16)
        };

        pnlFilter.Controls.AddRange([_txtSearch, _cmbFilterField, _cmbFilterGroup, lblSyncNote]);

        // Grid Container
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 16, 24, 16)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 40 }
        };
        UITheme.ApplyModernGridStyle(_grid);

        SetupGridColumns();
        _grid.CellContentClick += Grid_CellContentClick;

        pnlGrid.Controls.Add(_grid);

        Controls.Add(pnlGrid);
        Controls.Add(pnlFilter);
        Controls.Add(pnlHeader);
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();

        var colIdx = new DataGridViewTextBoxColumn
        {
            Name = "colIdx",
            HeaderText = "#",
            Width = 40,
            FillWeight = 4,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "İşletme Adı & Konum",
            FillWeight = 22,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
        };

        var colGroup = new DataGridViewTextBoxColumn
        {
            Name = "colGroup",
            HeaderText = "Adres Grubu",
            FillWeight = 16
        };

        var colSector = new DataGridViewTextBoxColumn
        {
            Name = "colSector",
            HeaderText = "Alanı / Sektör",
            FillWeight = 15
        };

        var colEmpCount = new DataGridViewTextBoxColumn
        {
            Name = "colEmpCount",
            HeaderText = "Personel",
            FillWeight = 9,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colDevletKatkisi = new DataGridViewTextBoxColumn
        {
            Name = "colDevletKatkisi",
            HeaderText = "Devlet Katkısı",
            FillWeight = 15,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colDistance = new DataGridViewTextBoxColumn
        {
            Name = "colDistance",
            HeaderText = "🚗 Uzaklık",
            FillWeight = 9,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colDays = new DataGridViewTextBoxColumn
        {
            Name = "colDays",
            HeaderText = "Staj Günleri",
            FillWeight = 14
        };

        var colCapacity = new DataGridViewTextBoxColumn
        {
            Name = "colCapacity",
            HeaderText = "Doluluk / Kapasite",
            FillWeight = 12,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colTeacher = new DataGridViewTextBoxColumn
        {
            Name = "colTeacher",
            HeaderText = "Koordinatör Öğretmen",
            FillWeight = 14
        };

        var colAction = new DataGridViewButtonColumn
        {
            Name = "colAction",
            HeaderText = "İşlemler",
            Text = "✏️ Düzenle",
            UseColumnTextForButtonValue = true,
            FillWeight = 9
        };

        var colDelete = new DataGridViewButtonColumn
        {
            Name = "colDelete",
            HeaderText = "",
            Text = "🗑️ Sil",
            UseColumnTextForButtonValue = true,
            FillWeight = 7
        };

        if (MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin)
        {
            _grid.Columns.AddRange([colIdx, colName, colGroup, colSector, colEmpCount, colDevletKatkisi, colDistance, colDays, colCapacity, colTeacher, colAction, colDelete]);
        }
        else
        {
            _grid.Columns.AddRange([colIdx, colName, colGroup, colSector, colEmpCount, colDevletKatkisi, colDistance, colDays, colCapacity, colTeacher]);
        }
    }

    private async void LoadLookupsAndDataAsync()
    {
        try
        {
            if (_settingRepo != null)
            {
                _settings = await _settingRepo.GetAppSettingsAsync();
            }

            _addressGroups = (await _addressGroupRepo.GetAllAsync()).OrderBy(g => g.Name).ToList();
            _fields = (await _fieldRepo.GetAllAsync()).OrderBy(f => f.Name).ToList();

            _cmbFilterField.Items.Clear();
            _cmbFilterField.Items.Add("Tüm Alanlar");
            foreach (var f in _fields) _cmbFilterField.Items.Add(f.Name);
            _cmbFilterField.SelectedIndex = 0;

            _cmbFilterGroup.Items.Clear();
            _cmbFilterGroup.Items.Add("Tüm Adres Grupları");
            foreach (var g in _addressGroups) _cmbFilterGroup.Items.Add(g.Name);
            _cmbFilterGroup.SelectedIndex = 0;

            await RefreshEnterprisesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Veriler yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public async Task RefreshEnterprisesAsync()
    {
        _allEnterprises = (await _enterpriseRepo.GetAllAsync()).OrderBy(e => e.Name).ToList();
        _allStudents = (await _studentRepo.GetAllAsync()).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _txtSearch.Text.Trim().ToLowerInvariant();
        var selectedField = _cmbFilterField.SelectedIndex > 0 ? _cmbFilterField.SelectedItem?.ToString() : null;
        var selectedGroup = _cmbFilterGroup.SelectedIndex > 0 ? _cmbFilterGroup.SelectedItem?.ToString() : null;

        var filtered = _allEnterprises.Where(e =>
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var match = e.Name.ToLowerInvariant().Contains(q) ||
                            (e.Sector ?? "").ToLowerInvariant().Contains(q) ||
                            (e.District ?? "").ToLowerInvariant().Contains(q) ||
                            (e.AddressGroupName ?? "").ToLowerInvariant().Contains(q) ||
                            (e.Iban ?? "").ToLowerInvariant().Contains(q) ||
                            (e.SgkSicilNo ?? "").ToLowerInvariant().Contains(q);
                if (!match) return false;
            }

            if (!string.IsNullOrWhiteSpace(selectedField) && !(e.Sector ?? "").Contains(selectedField, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(selectedGroup) && !string.Equals(e.AddressGroupName, selectedGroup, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }).ToList();

        _grid.Rows.Clear();
        int idx = 1;
        foreach (var ent in filtered)
        {
            var assignedCount = _allStudents.Count(s => s.EnterpriseId == ent.Id);
            var capText = $"{assignedCount} / {ent.Capacity} Öğrenci";
            var distText = string.IsNullOrWhiteSpace(ent.DistanceText) ? "-" : ent.DistanceText;

            // Teacher of the enterprise from students or ent
            var assignedTeacher = ent.TeacherName;
            if (string.IsNullOrWhiteSpace(assignedTeacher))
            {
                var studentWithTeacher = _allStudents.FirstOrDefault(s => s.EnterpriseId == ent.Id && !string.IsNullOrWhiteSpace(s.TeacherName));
                if (studentWithTeacher != null)
                {
                    assignedTeacher = studentWithTeacher.TeacherName;
                }
            }

            var locText = !string.IsNullOrWhiteSpace(ent.District) ? $"{ent.Name} ({ent.District})" : ent.Name;

            string stateKatkiText;
            if (ent.OptOutStateContribution)
            {
                stateKatkiText = "İstemiyor (Tam Maliyet)";
            }
            else
            {
                stateKatkiText = ent.EmployeeCount < 20 ? "İstiyor (2/3 Katkı)" : "İstiyor (1/3 Katkı)";
            }

            var rowIndex = _grid.Rows.Add(
                idx++,
                locText,
                ent.AddressGroupName ?? "Bağımsız",
                ent.Sector ?? "-",
                $"{ent.EmployeeCount} Kişi",
                stateKatkiText,
                distText,
                ent.ActiveDays ?? "Pazartesi,Salı,Çarşamba",
                capText,
                assignedTeacher ?? "Atanmadı"
            );

            _grid.Rows[rowIndex].Tag = ent;

            // Style colDevletKatkisi
            if (ent.OptOutStateContribution)
            {
                _grid.Rows[rowIndex].Cells["colDevletKatkisi"].Style.ForeColor = Color.FromArgb(225, 29, 72); // rose-600
                _grid.Rows[rowIndex].Cells["colDevletKatkisi"].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _grid.Rows[rowIndex].Cells["colDevletKatkisi"].Style.ForeColor = Color.FromArgb(5, 150, 105); // emerald-600
            }

            // Tooltip for IBAN, SGK Sicil No & Contact
            var tip = new StringBuilder();
            tip.AppendLine($"👥 Çalışan Sayısı: {ent.EmployeeCount} Personel");
            tip.AppendLine($"🏛️ Devlet Katkısı: {(ent.OptOutStateContribution ? "Devlet katkısı talep etmiyor" : (ent.EmployeeCount < 20 ? "20'den az personel (2/3 Devlet Katkısı)" : "20 ve üzeri personel (1/3 Devlet Katkısı)"))}");
            if (!string.IsNullOrWhiteSpace(ent.Iban)) tip.AppendLine($"🏦 IBAN: {ent.Iban}");
            if (!string.IsNullOrWhiteSpace(ent.SgkSicilNo)) tip.AppendLine($"🛡️ SGK Sicil: {ent.SgkSicilNo}");
            if (!string.IsNullOrWhiteSpace(ent.ContactPerson)) tip.AppendLine($"👤 Yetkili: {ent.ContactPerson} ({ent.Phone ?? "-"})");
            _grid.Rows[rowIndex].Cells["colName"].ToolTipText = tip.ToString().TrimEnd();

            // Color capacity
            if (assignedCount >= ent.Capacity)
            {
                _grid.Rows[rowIndex].Cells["colCapacity"].Style.ForeColor = Color.FromArgb(180, 83, 9); // amber/full
                _grid.Rows[rowIndex].Cells["colCapacity"].Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _grid.Rows[rowIndex].Cells["colCapacity"].Style.ForeColor = Color.FromArgb(22, 101, 52); // green
            }
        }
    }

    private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (!MebKoordinatorluk.Core.Security.CurrentUserSession.IsAdmin) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not Enterprise ent) return;

        if (_grid.Columns.Contains("colAction") && e.ColumnIndex == _grid.Columns["colAction"].Index)
        {
            OpenEnterpriseDialog(ent);
        }
        else if (_grid.Columns.Contains("colDelete") && e.ColumnIndex == _grid.Columns["colDelete"].Index)
        {
            var res = MessageBox.Show(
                $"{ent.Name} isimli işletmeyi silmek istediğinize emin misiniz?",
                "İşletme Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                _ = DeleteEnterpriseAsync(ent.Id);
            }
        }
    }

    private async Task DeleteEnterpriseAsync(int id)
    {
        await _enterpriseRepo.DeleteAsync(id);
        await RefreshEnterprisesAsync();
    }

    private void OpenEnterpriseDialog(Enterprise? existing)
    {
        if (existing == null && _licenseService != null && !_licenseService.CanAddEnterprise(_allEnterprises.Count))
        {
            var res = MessageBox.Show(
                "3308 Pro Deneme Sürümü Kısıtlaması:\n\nDeneme sürümünde en fazla 3 işletme kaydedilebilir.\nSınırsız işletme kaydı ve tüm özelliklerin kilidini açmak için lütfen lisansınızı etkinleştiriniz.\n\nLisans aktivasyon penceresini açmak ister misiniz?",
                "Demo Sürüm Kısıtlaması (3308 Pro)",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                using var licDlg = new LicenseActivationDialog(_licenseService);
                licDlg.ShowDialog(this);
            }
            return;
        }

        using var dlg = new EnterpriseEditDialog(_addressGroupRepo, _fieldRepo, existing);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _ = SaveEnterpriseAsync(dlg.EnterpriseResult);
        }
    }

    private async Task SaveEnterpriseAsync(Enterprise ent)
    {
        if (ent.Id == 0)
            await _enterpriseRepo.AddAsync(ent);
        else
            await _enterpriseRepo.UpdateAsync(ent);

        await RefreshEnterprisesAsync();
    }

    private async void BtnUploadExcel_Click(object? sender, EventArgs e)
    {
        if (_licenseService != null && !_licenseService.CanAddEnterprise(_allEnterprises.Count))
        {
            MessageBox.Show(
                "3308 Pro Deneme Sürümü Kısıtlaması:\n\nDeneme sürümünde en fazla 3 işletme kaydedilebilir. Excel'den toplu işletme aktarımı yapabilmek için lütfen lisansınızı etkinleştiriniz.",
                "Demo Sürüm Kısıtlaması",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var ofd = new OpenFileDialog
        {
            Filter = "CSV / Excel Dosyaları (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|Tüm Dosyalar (*.*)|*.*",
            Title = "İşletme Listesi Dosyasını Seçin"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var lines = File.ReadAllLines(ofd.FileName, Encoding.UTF8);
                if (lines.Length <= 1)
                {
                    MessageBox.Show("Seçilen dosyada veri satırı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Başlık satırını analiz et
                var headerLine = lines[0];
                var headers = headerLine.Split(new[] { ',', '\t', ';' }).Select(h => h.Trim().ToLowerInvariant()).ToArray();

                int idxName = Array.FindIndex(headers, h => h.Contains("işletme") || h.Contains("isletme") || h.Contains("unvan") || h.Contains("ad"));
                int idxSector = Array.FindIndex(headers, h => h.Contains("sektör") || h.Contains("sektor") || h.Contains("faaliyet") || h.Contains("alan"));
                int idxCap = Array.FindIndex(headers, h => h.Contains("kontenjan") || h.Contains("kapasite"));
                int idxDist = Array.FindIndex(headers, h => h.Contains("uzaklık") || h.Contains("uzaklik") || h.Contains("mesafe") || h.Contains("km"));
                int idxDistType = Array.FindIndex(headers, h => h.Contains("mücavir") || h.Contains("mucavir"));
                int idxDistrict = Array.FindIndex(headers, h => h.Contains("ilçe") || h.Contains("ilce"));
                int idxCity = Array.FindIndex(headers, h => h == "il" || h.Contains("şehir") || h.Contains("sehir"));
                int idxAddress = Array.FindIndex(headers, h => h.Contains("adres") && !h.Contains("grup"));
                int idxContact = Array.FindIndex(headers, h => h.Contains("yetkili") || h.Contains("ilgili"));
                int idxPhone = Array.FindIndex(headers, h => h.Contains("tel"));
                int idxEmail = Array.FindIndex(headers, h => h.Contains("posta") || h.Contains("mail"));
                int idxGroup = Array.FindIndex(headers, h => h.Contains("grup"));
                int idxDays = Array.FindIndex(headers, h => h.Contains("gün") || h.Contains("gun") || h.Contains("staj"));
                int idxTeacher = Array.FindIndex(headers, h => h.Contains("öğretmen") || h.Contains("ogretmen") || h.Contains("koordinatör"));
                int idxTax = Array.FindIndex(headers, h => h.Contains("vergi"));
                int idxIban = Array.FindIndex(headers, h => h.Contains("iban") || h.Contains("hesap"));
                int idxSgk = Array.FindIndex(headers, h => h.Contains("sgk") || h.Contains("sicil"));
                int idxTrainer = Array.FindIndex(headers, h => h.Contains("usta") || h.Contains("eğitici") || h.Contains("egitici"));
                int idxNotes = Array.FindIndex(headers, h => h.Contains("not") || h.Contains("açıklama") || h.Contains("aciklama"));

                int count = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(new[] { ',', '\t', ';' });
                    if (parts.Length == 0) continue;

                    string GetCol(int colIdx, int fallbackIdx)
                    {
                        if (colIdx >= 0 && colIdx < parts.Length) return parts[colIdx].Trim();
                        if (fallbackIdx >= 0 && fallbackIdx < parts.Length) return parts[fallbackIdx].Trim();
                        return "";
                    }

                    string name = GetCol(idxName, 0);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string sector = GetCol(idxSector, 1);
                    if (string.IsNullOrWhiteSpace(sector)) sector = "Bilişim Teknolojileri";

                    string capStr = GetCol(idxCap, 2);
                    int capacity = int.TryParse(capStr, out var c) && c > 0 ? c : 4;

                    string distStr = GetCol(idxDist, 3);
                    if (string.IsNullOrWhiteSpace(distStr)) distStr = "5.0 km";

                    string distTypeStr = GetCol(idxDistType, 4);
                    var distType = DistanceType.MucavirAlanIci;
                    if (distTypeStr.Contains("dış", StringComparison.OrdinalIgnoreCase) || distTypeStr.Contains("dis", StringComparison.OrdinalIgnoreCase))
                    {
                        distType = DistanceType.MucavirAlanDisi;
                    }
                    else if (double.TryParse(distStr.Replace("km", "").Trim(), out var distKm) && distKm > 30)
                    {
                        distType = DistanceType.MucavirAlanDisi;
                    }

                    string district = GetCol(idxDistrict, 5);
                    string city = GetCol(idxCity, 6);
                    if (string.IsNullOrWhiteSpace(city)) city = "İSTANBUL";

                    string address = GetCol(idxAddress, 7);
                    string contact = GetCol(idxContact, 8);
                    string phone = GetCol(idxPhone, 9);
                    string email = GetCol(idxEmail, 10);
                    string groupName = GetCol(idxGroup, 11);
                    string days = GetCol(idxDays, 12);
                    if (string.IsNullOrWhiteSpace(days)) days = "Pazartesi,Salı,Çarşamba";

                    string teacherName = GetCol(idxTeacher, 13);
                    string taxNo = GetCol(idxTax, 14);
                    string iban = GetCol(idxIban, 15);
                    string sgk = GetCol(idxSgk, 16);
                    string trainer = GetCol(idxTrainer, 17);
                    string notes = GetCol(idxNotes, 18);

                    int? groupId = null;
                    if (!string.IsNullOrWhiteSpace(groupName))
                    {
                        var grp = _addressGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                        if (grp != null) groupId = grp.Id;
                    }

                    var ent = new Enterprise
                    {
                        Name = name,
                        Sector = sector,
                        Capacity = capacity,
                        DistanceText = distStr,
                        DistanceType = distType,
                        District = string.IsNullOrWhiteSpace(district) ? null : district,
                        City = string.IsNullOrWhiteSpace(city) ? "İSTANBUL" : city,
                        Address = string.IsNullOrWhiteSpace(address) ? null : address,
                        ContactPerson = string.IsNullOrWhiteSpace(contact) ? null : contact,
                        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                        AddressGroupId = groupId,
                        AddressGroupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName,
                        ActiveDays = days,
                        TeacherName = string.IsNullOrWhiteSpace(teacherName) ? null : teacherName,
                        TaxNumber = string.IsNullOrWhiteSpace(taxNo) ? null : taxNo,
                        Iban = string.IsNullOrWhiteSpace(iban) ? null : iban.ToUpperInvariant(),
                        SgkSicilNo = string.IsNullOrWhiteSpace(sgk) ? null : sgk,
                        MasterTrainerName = string.IsNullOrWhiteSpace(trainer) ? null : trainer,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                        IsActive = true
                    };

                    await _enterpriseRepo.AddAsync(ent);
                    count++;
                }

                MessageBox.Show($"{count} işletme başarıyla içe aktarıldı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshEnterprisesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya okunurken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = "Ornek_Isletmeler.csv"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var content = "İşletme Adı,Faaliyet Alanı / Sektör,Kontenjan,Okula Uzaklık,Mücavir Alan Durumu,İlçe,İl,Açık Adres,İşletme Yetkilisi,Telefon,E-Posta,Adres Grubu,Staj Günleri,Koordinatör Öğretmen,Vergi No,IBAN,SGK Sicil No,Usta Öğretici,Notlar\n" +
                          "TeknoSoft Bilişim A.Ş.,Bilişim Teknolojileri,4,5.2 km,Mücavir Alan İçi,Kadıköy,İSTANBUL,Koşuyolu Mah. No:12,Murat ÇELİK,02163334455,info@teknosoft.com,Kadıköy Grubu,Pazartesi;Salı;Çarşamba,,1234567890,TR120006200000000123456789,21234010112345670341234,Kemal Usta,Kurumsal yazılım geliştirme ofisi\n" +
                          "Delta Otomotiv San. Ltd.,Motorlu Araçlar Teknolojisi,3,8.0 km,Mücavir Alan İçi,Kartal,İSTANBUL,Sanayi Sit. B Blok No:4,Ahmet YILMAZ,02164445566,iletisim@deltaoto.com,Kartal Sanayi,Çarşamba;Perşembe;Cuma,,9876543210,TR560006200000000987654321,29876020298765430345678,Hasan Usta,Oto bakım onarım servisi\n";
            File.WriteAllText(sfd.FileName, content, Encoding.UTF8);
            MessageBox.Show("Örnek şablon başarıyla kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ── Yazdır / PDF İşlemleri ─────────────────────────────────────────────

    private sealed class EnterprisePrintItem
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sector { get; set; } = "-";
        public string Address { get; set; } = "-";
        public string ContactPerson { get; set; } = "-";
        public string PhoneEmail { get; set; } = "-";
        public string Days { get; set; } = "-";
        public string CapacityText { get; set; } = "-";
        public string TeacherName { get; set; } = "-";
        public int RowHeight { get; set; } = 28;
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (_grid.Rows.Count == 0)
        {
            MessageBox.Show("Yazdırılacak işletme kaydı bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var pd = BuildEnterprisePrintDocument();
        PrintPreviewHelper.ShowPreview(pd, "Staj ve Beceri Eğitimi İşletmeleri Listesi Önizleme", this);
    }

    private static int EstimatePrintRowHeight(Graphics g, Font fontRegular, Font fontBold, string name, string address, string phoneEmail, int[] colWidths)
    {
        var szName = g.MeasureString(name, fontBold, colWidths[1] - 8);
        var szAddr = g.MeasureString(address, fontRegular, colWidths[3] - 8);
        var szContact = g.MeasureString(phoneEmail, fontRegular, colWidths[5] - 8);

        float maxH = Math.Max(szName.Height, Math.Max(szAddr.Height, szContact.Height));
        return Math.Max(26, (int)Math.Ceiling(maxH) + 8);
    }

    private PrintDocument BuildEnterprisePrintDocument()
    {
        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

        int printPageIndex = 0;
        var printPages = new List<List<EnterprisePrintItem>>();
        var printItems = new List<EnterprisePrintItem>();

        int[] colWidths = [34, 175, 110, 270, 115, 110, 95, 70, 110];

        pd.BeginPrint += (s, e) =>
        {
            printPageIndex = 0;
            printPages.Clear();
            printItems.Clear();

            using var bmpTemp = new Bitmap(1, 1);
            using var gTemp = Graphics.FromImage(bmpTemp);
            using var fTestRegular = new Font("Segoe UI", 8f);
            using var fTestBold = new Font("Segoe UI", 8f, FontStyle.Bold);

            int idx = 1;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is not Enterprise ent) continue;

                var assignedCount = _allStudents.Count(st => st.EnterpriseId == ent.Id);
                string capText = $"{assignedCount} / {ent.Capacity}";

                string fullAddr = ent.Address ?? "";
                if (!string.IsNullOrWhiteSpace(ent.District))
                {
                    if (string.IsNullOrWhiteSpace(fullAddr))
                        fullAddr = ent.District;
                    else if (!fullAddr.Contains(ent.District, StringComparison.OrdinalIgnoreCase))
                        fullAddr = $"{fullAddr} / {ent.District}";
                }
                if (string.IsNullOrWhiteSpace(fullAddr)) fullAddr = "-";

                string contactInfo = ent.Phone ?? "";
                if (!string.IsNullOrWhiteSpace(ent.Email))
                {
                    contactInfo = string.IsNullOrWhiteSpace(contactInfo) ? ent.Email : $"{contactInfo}\n{ent.Email}";
                }
                if (string.IsNullOrWhiteSpace(contactInfo)) contactInfo = "-";

                string teacherName = ent.TeacherName ?? "";
                if (string.IsNullOrWhiteSpace(teacherName))
                {
                    var studentWithTeacher = _allStudents.FirstOrDefault(st => st.EnterpriseId == ent.Id && !string.IsNullOrWhiteSpace(st.TeacherName));
                    teacherName = studentWithTeacher?.TeacherName ?? "Atanmadı";
                }

                string days = TurkishTextHelper.FormatDays(ent.ActiveDays ?? "-");
                int rH = EstimatePrintRowHeight(gTemp, fTestRegular, fTestBold, ent.Name, fullAddr, contactInfo, colWidths);

                printItems.Add(new EnterprisePrintItem
                {
                    Index = idx++,
                    Name = ent.Name,
                    Sector = ent.Sector ?? "-",
                    Address = fullAddr,
                    ContactPerson = ent.ContactPerson ?? "-",
                    PhoneEmail = contactInfo,
                    Days = days,
                    CapacityText = capText,
                    TeacherName = teacherName,
                    RowHeight = rH
                });
            }

            var currentPage = new List<EnterprisePrintItem>();
            int currentHeight = 0;
            int maxHeight = 580; // Sayfa 1 tablo alanı sınırı

            for (int i = 0; i < printItems.Count; i++)
            {
                var it = printItems[i];
                if (currentHeight + it.RowHeight > maxHeight && currentPage.Count > 0)
                {
                    printPages.Add(currentPage);
                    currentPage = new List<EnterprisePrintItem>();
                    currentHeight = 0;
                    maxHeight = 650; // Sonraki sayfalar için sınır
                }
                currentPage.Add(it);
                currentHeight += it.RowHeight;
            }

            if (currentPage.Count > 0)
            {
                printPages.Add(currentPage);
            }
        };

        pd.PrintPage += (s, ev) =>
        {
            if (ev.Graphics == null || printPageIndex >= printPages.Count)
            {
                ev.HasMorePages = false;
                return;
            }

            var g = ev.Graphics;
            var bounds = ev.MarginBounds;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            using var fTc = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fSchool = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var fTitle = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var fSummary = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fTableHead = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fCell = new Font("Segoe UI", 8f, FontStyle.Regular);
            using var fCellBold = new Font("Segoe UI", 8f, FontStyle.Bold);
            using var fFooter = new Font("Segoe UI", 8f, FontStyle.Regular);

            using var bBlack = new SolidBrush(Color.Black);
            using var bDark = new SolidBrush(Color.FromArgb(30, 41, 59));
            using var bBlue = new SolidBrush(Color.FromArgb(30, 64, 175));
            using var bWhite = new SolidBrush(Color.White);
            using var bAltBg = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var bHdrBg = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var bSumBg = new SolidBrush(Color.FromArgb(239, 246, 255));
            using var pBorder = new Pen(Color.FromArgb(148, 163, 184), 1f);

            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            using var sfLeftWrap = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            using var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            int x = bounds.X;
            int y = bounds.Y;
            int w = bounds.Width;

            bool isFirstPage = printPageIndex == 0;
            if (isFirstPage)
            {
                string val = _settings?.DistrictGovernorate?.ToUpperTr() ?? _settings?.Valilik?.ToUpperTr() ?? "İSTANBUL VALİLİĞİ";
                string mem = !string.IsNullOrWhiteSpace(_settings?.DistrictMem) ? "\n" + _settings.DistrictMem.ToUpperTr() : "";
                string tcVal = "T.C.\n" + val + mem;
                string school = !string.IsNullOrWhiteSpace(_settings?.SchoolName) ? _settings.SchoolName : "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ MÜDÜRLÜĞÜ";
                string titleStr = $"{_settings?.AcademicYear ?? "2024-2025"} EĞİTİM ÖĞRETİM YILI STAJ VE İŞLETMELERDE BECERİ EĞİTİMİ KURUMLARI LİSTESİ";

                int tcH = string.IsNullOrWhiteSpace(mem) ? 32 : 46;
                g.DrawString(tcVal, fTc, bBlack, new RectangleF(x, y, w, tcH), sfCenter);
                y += tcH + 2;
                g.DrawString(school, fSchool, bBlack, new RectangleF(x, y, w, 20), sfCenter);
                y += 22;
                g.DrawString(titleStr, fTitle, bBlue, new RectangleF(x, y, w, 22), sfCenter);
                y += 26;

                // Özet Bilgi Çubuğu
                int totalEnt = printItems.Count;
                int totalCap = _allEnterprises.Where(e => printItems.Any(p => p.Name == e.Name)).Sum(e => e.Capacity);
                int totalAssigned = _allStudents.Count(st => st.EnterpriseId.HasValue && printItems.Any(p => p.Name == st.EnterpriseName));

                int sumH = 22;
                g.FillRectangle(bSumBg, x, y, w, sumH);
                g.DrawRectangle(pBorder, x, y, w, sumH);

                float colW4 = w / 4f;
                g.DrawString($"Toplam İşletme: {totalEnt}", fSummary, bDark, new RectangleF(x + 8, y, colW4 - 8, sumH), sfLeft);
                g.DrawString($"Toplam Kontenjan: {totalCap} (Yerleşen: {totalAssigned})", fSummary, bDark, new RectangleF(x + colW4, y, colW4, sumH), sfLeft);
                g.DrawString($"Tarih: {DateTime.Today:dd.MM.yyyy}", fSummary, bDark, new RectangleF(x + colW4 * 2, y, colW4, sumH), sfLeft);
                g.DrawString("3308 Mesleki Eğitim Kanunu Kapsamında", fSummary, bDark, new RectangleF(x + colW4 * 3, y, colW4 - 8, sumH), sfRight);
                y += sumH + 8;
            }
            else
            {
                string p2Title = $"{_settings?.SchoolName ?? "Mimar Sinan MTAL"} — Staj ve Beceri Eğitimi İşletmeleri Listesi (Devam)";
                g.DrawString(p2Title, fTitle, bDark, new RectangleF(x, y, w, 22), sfLeft);
                y += 26;
            }

            // Tablo Başlığı
            string[] headers = ["S.No", "İşletme Adı / Unvanı", "Alanı / Sektör", "Açık Adres & İlçe", "İşletme Yetkilisi", "İletişim (Tel / E-posta)", "Staj Günleri", "Kontenjan", "Koordinatör Öğretmen"];
            int hdrH = 26;
            g.FillRectangle(bHdrBg, x, y, w, hdrH);
            g.DrawRectangle(pBorder, x, y, w, hdrH);

            int cx = x;
            for (int col = 0; col < headers.Length; col++)
            {
                var sfHdr = (col == 0 || col == 6 || col == 7) ? sfCenter : sfLeft;
                g.DrawString(headers[col], fTableHead, bDark, new RectangleF(cx + 4, y, colWidths[col] - 8, hdrH), sfHdr);
                if (col < headers.Length - 1)
                    g.DrawLine(pBorder, cx + colWidths[col], y, cx + colWidths[col], y + hdrH);
                cx += colWidths[col];
            }
            y += hdrH;

            // Tablo Satırları
            var pageItems = printPages[printPageIndex];
            for (int r = 0; r < pageItems.Count; r++)
            {
                var item = pageItems[r];
                int rH = item.RowHeight;

                if (r % 2 == 1)
                {
                    g.FillRectangle(bAltBg, x, y, w, rH);
                }
                g.DrawRectangle(pBorder, x, y, w, rH);

                cx = x;
                // Col 0: S.No
                g.DrawString(item.Index.ToString(), fCell, bBlack, new RectangleF(cx, y, colWidths[0], rH), sfCenter);
                g.DrawLine(pBorder, cx + colWidths[0], y, cx + colWidths[0], y + rH);
                cx += colWidths[0];

                // Col 1: İşletme Adı
                g.DrawString(item.Name, fCellBold, bBlack, new RectangleF(cx + 4, y + 4, colWidths[1] - 8, rH - 8), sfLeftWrap);
                g.DrawLine(pBorder, cx + colWidths[1], y, cx + colWidths[1], y + rH);
                cx += colWidths[1];

                // Col 2: Alanı / Sektör
                g.DrawString(item.Sector, fCell, bBlack, new RectangleF(cx + 4, y + 4, colWidths[2] - 8, rH - 8), sfLeftWrap);
                g.DrawLine(pBorder, cx + colWidths[2], y, cx + colWidths[2], y + rH);
                cx += colWidths[2];

                // Col 3: Açık Adres & İlçe
                g.DrawString(item.Address, fCell, bBlack, new RectangleF(cx + 4, y + 4, colWidths[3] - 8, rH - 8), sfLeftWrap);
                g.DrawLine(pBorder, cx + colWidths[3], y, cx + colWidths[3], y + rH);
                cx += colWidths[3];

                // Col 4: İşletme Yetkilisi
                g.DrawString(item.ContactPerson, fCell, bBlack, new RectangleF(cx + 4, y + 4, colWidths[4] - 8, rH - 8), sfLeftWrap);
                g.DrawLine(pBorder, cx + colWidths[4], y, cx + colWidths[4], y + rH);
                cx += colWidths[4];

                // Col 5: İletişim (Telefon / E-posta)
                g.DrawString(item.PhoneEmail, fCell, bBlack, new RectangleF(cx + 4, y + 4, colWidths[5] - 8, rH - 8), sfLeftWrap);
                g.DrawLine(pBorder, cx + colWidths[5], y, cx + colWidths[5], y + rH);
                cx += colWidths[5];

                // Col 6: Staj Günleri
                g.DrawString(item.Days, fCell, bBlack, new RectangleF(cx, y, colWidths[6], rH), sfCenter);
                g.DrawLine(pBorder, cx + colWidths[6], y, cx + colWidths[6], y + rH);
                cx += colWidths[6];

                // Col 7: Kontenjan
                g.DrawString(item.CapacityText, fCellBold, bBlue, new RectangleF(cx, y, colWidths[7], rH), sfCenter);
                g.DrawLine(pBorder, cx + colWidths[7], y, cx + colWidths[7], y + rH);
                cx += colWidths[7];

                // Col 8: Koordinatör Öğretmen
                g.DrawString(item.TeacherName, fCell, bBlack, new RectangleF(cx + 4, y + 4, colWidths[8] - 8, rH - 8), sfLeftWrap);

                y += rH;
            }

            // Alt Bilgi (Footer)
            int footerY = bounds.Bottom - 18;
            g.DrawString("3308 Pro - İşletmelerde Beceri Eğitimi & Koordinatörlük Sistemi", fFooter, new SolidBrush(Color.FromArgb(148, 163, 184)), new RectangleF(x, footerY, 500, 18), sfLeft);
            g.DrawString($"Sayfa {printPageIndex + 1} / {printPages.Count}", fFooter, new SolidBrush(Color.FromArgb(148, 163, 184)), new RectangleF(x + w - 200, footerY, 200, 18), sfRight);

            printPageIndex++;
            ev.HasMorePages = printPageIndex < printPages.Count;
        };

        return pd;
    }
}
