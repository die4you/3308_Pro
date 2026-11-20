using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public enum ClassListPrintChoice
{
    Cancel,
    Preview,
    DirectPrint
}

public class ClassListPrintDialog : Form
{
    private readonly ISchoolClassRepository _classRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly ISettingRepository _settingRepo;

    // UI Controls
    private ComboBox _cmbClassroom = null!;
    private RadioButton _rbNewPagePerClass = null!;
    private RadioButton _rbContinuousPage = null!;
    private RadioButton _rbPortrait = null!;
    private RadioButton _rbLandscape = null!;

    private CheckBox _chkShowTcNo = null!;
    private CheckBox _chkShowGender = null!;
    private CheckBox _chkShowField = null!;
    private CheckBox _chkShowEnterprise = null!;
    private CheckBox _chkShowPhone = null!;
    private CheckBox _chkShowParentContact = null!;
    private CheckBox _chkShowSignature = null!;

    // Özel Sütun Kontrolleri
    private CheckBox _chkCustomColumn = null!;
    private ComboBox _cmbCustomColumnTitle = null!;
    private ComboBox _cmbCustomColumnPlacement = null!;
    private NumericUpDown _numCustomWidth = null!;

    private Button _btnPreview = null!;
    private Button _btnPrint = null!;
    private Button _btnCancel = null!;

    public ClassListPrintChoice Choice { get; private set; } = ClassListPrintChoice.Cancel;
    public ClassListPrintOptions PrintOptions { get; private set; } = new();

    public ClassListPrintDialog(
        ISchoolClassRepository classRepo,
        IStudentRepository studentRepo,
        ISettingRepository settingRepo)
    {
        _classRepo = classRepo;
        _studentRepo = studentRepo;
        _settingRepo = settingRepo;

        InitializeComponent();
        LoadClassesAsync();
    }

    private void InitializeComponent()
    {
        Text = "🖨️ Sınıf Listeleri Yazdırma Seçenekleri";
        Size = new Size(540, 620);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "🖨️ Sınıf Listeleri Toplu Yazdırma",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(20, 10),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Okul genelindeki sınıfların öğrenci listelerini MEB formatında yazdırın veya önizleyin.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(20, 36),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle]);

        // Main Options Container
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 15, 20, 15)
        };

        // 1. Classroom Selector
        var lblClass = new Label
        {
            Text = "Yazdırılacak Sınıf / Şube:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(20, 18),
            AutoSize = true
        };

        _cmbClassroom = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(180, 14),
            Width = 310,
            Font = new Font("Segoe UI", 9.5f)
        };
        _cmbClassroom.Items.Add("Tüm Sınıflar (Tüm Okul)");
        _cmbClassroom.SelectedIndex = 0;

        // 2. Page Break Options Group
        var grpPageBreak = new GroupBox
        {
            Text = "Sayfa Düzeni ve Kesme Ayarları",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(20, 48),
            Size = new Size(470, 70)
        };

        _rbNewPagePerClass = new RadioButton
        {
            Text = "Her Sınıf Ayrı Sayfada Başlasın (Sayfa Başı Sınıf)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = true,
            Location = new Point(16, 22),
            AutoSize = true
        };

        _rbContinuousPage = new RadioButton
        {
            Text = "Sınıflar Kesintisiz Devam Etsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Location = new Point(16, 44),
            AutoSize = true
        };
        grpPageBreak.Controls.AddRange([_rbNewPagePerClass, _rbContinuousPage]);

        // 3. Orientation Group
        var grpOrientation = new GroupBox
        {
            Text = "Sayfa Yönlendirmesi",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(20, 124),
            Size = new Size(470, 50)
        };

        _rbPortrait = new RadioButton
        {
            Text = "📄 Dikey (Portrait)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = true,
            Location = new Point(16, 20),
            AutoSize = true
        };

        _rbLandscape = new RadioButton
        {
            Text = "📜 Yatay (Landscape)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Location = new Point(200, 20),
            AutoSize = true
        };
        grpOrientation.Controls.AddRange([_rbPortrait, _rbLandscape]);

        // 4. Column Selection Group
        var grpColumns = new GroupBox
        {
            Text = "Gösterilecek Standart Sütunlar",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(20, 180),
            Size = new Size(470, 116)
        };

        _chkShowTcNo = new CheckBox
        {
            Text = "T.C. Kimlik No Gösterilsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = true,
            Location = new Point(16, 22),
            AutoSize = true
        };

        _chkShowGender = new CheckBox
        {
            Text = "Cinsiyet Gösterilsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = true,
            Location = new Point(230, 22),
            AutoSize = true
        };

        _chkShowField = new CheckBox
        {
            Text = "Alan / Dal Bilgisi Gösterilsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = true,
            Location = new Point(16, 44),
            AutoSize = true
        };

        _chkShowEnterprise = new CheckBox
        {
            Text = "Staj Yapılan İşletme Gösterilsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = true,
            Location = new Point(230, 44),
            AutoSize = true
        };

        _chkShowPhone = new CheckBox
        {
            Text = "Öğrenci Telefonu (Tel 1) Gösterilsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = false,
            Location = new Point(16, 66),
            AutoSize = true
        };

        _chkShowParentContact = new CheckBox
        {
            Text = "Veli Bilgisi (Ad & Tel 2) Gösterilsin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = false,
            Location = new Point(230, 66),
            AutoSize = true
        };

        _chkShowSignature = new CheckBox
        {
            Text = "Yoklama / İmza Sütunu Eklensin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Checked = false,
            Location = new Point(16, 88),
            AutoSize = true
        };
        grpColumns.Controls.AddRange([_chkShowTcNo, _chkShowGender, _chkShowField, _chkShowEnterprise, _chkShowPhone, _chkShowParentContact, _chkShowSignature]);

        // 5. Custom Column Group (Özel / Boş Sütun Ekleme)
        var grpCustomCol = new GroupBox
        {
            Text = "Özel Sütun Ekle (Not, İmza vb.)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(20, 304),
            Size = new Size(470, 105)
        };

        _chkCustomColumn = new CheckBox
        {
            Text = "Çıktıya Özel / Boş Sütun Eklensin",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138), // blue-900
            Checked = false,
            Location = new Point(16, 22),
            AutoSize = true
        };

        var lblCustomTitle = new Label
        {
            Text = "Sütun Başlığı:",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Location = new Point(16, 48),
            AutoSize = true
        };

        _cmbCustomColumnTitle = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(16, 68),
            Width = 145,
            Enabled = false
        };
        _cmbCustomColumnTitle.Items.AddRange([
            "Veli Adı Soyadı",
            "Veli Telefonu",
            "Veli İmzası",
            "Açıklama / Not",
            "İmza",
            "Görüşme Tarihi"
        ]);
        _cmbCustomColumnTitle.Text = "Veli Adı Soyadı";

        var lblCustomPlacement = new Label
        {
            Text = "Sütun Konumu (Şundan Sonra):",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Location = new Point(170, 48),
            AutoSize = true
        };

        _cmbCustomColumnPlacement = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(170, 68),
            Width = 195,
            Enabled = false
        };
        _cmbCustomColumnPlacement.Items.AddRange([
            "Öğrenci Adı Soyadı'ndan Sonra",
            "Okul No'dan Sonra",
            "T.C. Kimlik No'dan Sonra",
            "Cinsiyet'ten Sonra",
            "Alan / Dal'dan Sonra",
            "Staj İşletmesi'nden Sonra",
            "Tablonun En Sonuna (En Sağa)"
        ]);
        _cmbCustomColumnPlacement.SelectedIndex = 0;

        var lblCustomWidth = new Label
        {
            Text = "Genişlik (px):",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Location = new Point(375, 48),
            AutoSize = true
        };

        _numCustomWidth = new NumericUpDown
        {
            Font = new Font("Segoe UI", 9f),
            Location = new Point(375, 68),
            Width = 78,
            Minimum = 40,
            Maximum = 400,
            Increment = 10,
            Value = 140,
            Enabled = false
        };

        _chkCustomColumn.CheckedChanged += (s, e) =>
        {
            _cmbCustomColumnTitle.Enabled = _chkCustomColumn.Checked;
            _cmbCustomColumnPlacement.Enabled = _chkCustomColumn.Checked;
            _numCustomWidth.Enabled = _chkCustomColumn.Checked;
        };

        grpCustomCol.Controls.AddRange([
            _chkCustomColumn,
            lblCustomTitle, _cmbCustomColumnTitle,
            lblCustomPlacement, _cmbCustomColumnPlacement,
            lblCustomWidth, _numCustomWidth
        ]);

        pnlBody.Controls.AddRange([lblClass, _cmbClassroom, grpPageBreak, grpOrientation, grpColumns, grpCustomCol]);

        // Footer Action Panel
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        var btnExportExcel = new Button
        {
            Text = "📄 Excel'e Kaydet",
            Size = new Size(135, 36),
            BackColor = Color.FromArgb(71, 85, 105), // slate-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(90, 10)
        };
        btnExportExcel.FlatAppearance.BorderSize = 0;
        btnExportExcel.Click += (s, e) => ExportToExcelAsync(this);

        _btnPreview = new Button
        {
            Text = "👁️ Önizleme",
            Size = new Size(125, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(235, 10)
        };
        _btnPreview.FlatAppearance.BorderSize = 0;
        _btnPreview.Click += async (s, e) => await ShowPreviewInternalAsync();

        _btnPrint = new Button
        {
            Text = "🖨️ Yazdır",
            Size = new Size(125, 36),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(370, 10)
        };
        _btnPrint.FlatAppearance.BorderSize = 0;
        _btnPrint.Click += async (s, e) => await DirectPrintInternalAsync();

        _btnCancel = new Button
        {
            Text = "Kapat",
            Size = new Size(70, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(12, 10)
        };
        _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnCancel.Click += (s, e) => Close();

        pnlFooter.Controls.AddRange([_btnCancel, btnExportExcel, _btnPreview, _btnPrint]);

        Controls.Add(pnlBody);
        Controls.Add(pnlHeader);
        Controls.Add(pnlFooter);
    }

    private async void LoadClassesAsync()
    {
        try
        {
            var classes = await _classRepo.GetAllAsync();
            foreach (var c in classes.OrderBy(c => c.Name))
            {
                _cmbClassroom.Items.Add(c.Name);
            }
        }
        catch
        {
            // Fallback: Continue with default
        }
    }

    private string GetSelectedPlacementKey()
    {
        return _cmbCustomColumnPlacement.SelectedIndex switch
        {
            0 => "after-name",
            1 => "after-no",
            2 => "after-tc",
            3 => "after-gender",
            4 => "after-field",
            5 => "after-enterprise",
            _ => "end"
        };
    }

    public ClassListPrintOptions BuildCurrentOptions()
    {
        string selectedClass = _cmbClassroom.SelectedItem?.ToString() ?? "Tüm Sınıflar (Tüm Okul)";
        if (selectedClass.StartsWith("Tüm Sınıflar"))
        {
            selectedClass = "Tüm Sınıflar";
        }

        PrintOptions = new ClassListPrintOptions
        {
            SelectedClassroom = selectedClass,
            NewPagePerClass = _rbNewPagePerClass.Checked,
            Landscape = _rbLandscape.Checked,
            ShowTcNo = _chkShowTcNo.Checked,
            ShowGender = _chkShowGender.Checked,
            ShowField = _chkShowField.Checked,
            ShowEnterprise = _chkShowEnterprise.Checked,
            ShowPhone = _chkShowPhone.Checked,
            ShowParentContact = _chkShowParentContact.Checked,
            ShowSignatureColumn = _chkShowSignature.Checked,
            AddCustomColumn = _chkCustomColumn.Checked,
            CustomColumnTitle = string.IsNullOrWhiteSpace(_cmbCustomColumnTitle.Text) ? "Özel Sütun" : _cmbCustomColumnTitle.Text.Trim(),
            CustomColumnPlacement = GetSelectedPlacementKey(),
            CustomColumnWidth = (int)_numCustomWidth.Value
        };

        return PrintOptions;
    }

    private async Task ShowPreviewInternalAsync()
    {
        try
        {
            _btnPreview.Enabled = false;
            Choice = ClassListPrintChoice.Preview;
            var opts = BuildCurrentOptions();
            var students = (await _studentRepo.GetAllAsync()).ToList();
            var settings = await _settingRepo.GetAppSettingsAsync();

            using var pd = ClassListPrintHelper.CreatePrintDocument(students, settings, opts);
            // Modal olarak açıyoruz, böylece önizleme kapanınca bu pencere açık kalır!
            PrintPreviewHelper.ShowPreview(pd, "Sınıf Listesi Baskı Önizleme", this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Önizleme açılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnPreview.Enabled = true;
        }
    }

    private async Task DirectPrintInternalAsync()
    {
        try
        {
            _btnPrint.Enabled = false;
            Choice = ClassListPrintChoice.DirectPrint;
            var opts = BuildCurrentOptions();
            var students = (await _studentRepo.GetAllAsync()).ToList();
            var settings = await _settingRepo.GetAppSettingsAsync();

            using var pd = ClassListPrintHelper.CreatePrintDocument(students, settings, opts);
            using var printDlg = new PrintDialog
            {
                Document = pd,
                UseEXDialog = true,
                AllowSomePages = true
            };

            if (printDlg.ShowDialog(this) == DialogResult.OK)
            {
                pd.Print();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yazdırma işlemi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnPrint.Enabled = true;
        }
    }

    public async Task ProcessPrintingAsync(Control parentControl)
    {
        if (Choice == ClassListPrintChoice.Preview)
        {
            await ShowPreviewInternalAsync();
        }
        else if (Choice == ClassListPrintChoice.DirectPrint)
        {
            await DirectPrintInternalAsync();
        }
    }

    private async void ExportToExcelAsync(Control parentControl)
    {
        try
        {
            string selectedClass = _cmbClassroom.SelectedItem?.ToString() ?? "Tüm Sınıflar (Tüm Okul)";
            if (selectedClass.StartsWith("Tüm Sınıflar")) selectedClass = "Tüm Sınıflar";

            var students = (await _studentRepo.GetAllAsync()).Where(s => s.IsActive).ToList();
            if (selectedClass != "Tüm Sınıflar")
            {
                students = students.Where(s => string.Equals(s.Classroom, selectedClass, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (students.Count == 0)
            {
                MessageBox.Show("Aktarılacak öğrenci bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Uyumlu CSV Dosyası (*.csv)|*.csv|Tüm Dosyalar (*.*)|*.*",
                FileName = $"Sinif_Listesi_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Sınıf Listesini Excel'e Kaydet"
            };

            if (sfd.ShowDialog(parentControl) == DialogResult.OK)
            {
                var sb = new System.Text.StringBuilder();

                bool showTc = _chkShowTcNo.Checked;
                bool showGender = _chkShowGender.Checked;
                bool showField = _chkShowField.Checked;
                bool showEnterprise = _chkShowEnterprise.Checked;
                bool showPhone = _chkShowPhone.Checked;
                bool showParent = _chkShowParentContact.Checked;
                bool showSig = _chkShowSignature.Checked;
                bool addCustom = _chkCustomColumn.Checked;
                string customTitle = string.IsNullOrWhiteSpace(_cmbCustomColumnTitle.Text) ? "Özel Sütun" : _cmbCustomColumnTitle.Text.Trim();
                string placement = GetSelectedPlacementKey();

                var headerCols = new List<(string Key, string Title)>
                {
                    ("class", "Sınıf"),
                    ("seq", "Sıra No"),
                    ("no", "Okul No")
                };

                if (showTc) headerCols.Add(("tc", "T.C. Kimlik No"));
                headerCols.Add(("name", "Adı Soyadı"));
                if (showGender) headerCols.Add(("gender", "Cinsiyet"));
                if (showField) headerCols.Add(("field", "Alan / Dal"));
                if (showEnterprise) headerCols.Add(("enterprise", "Staj İşletmesi"));
                if (showPhone) headerCols.Add(("phone", "Öğrenci Telefonu"));
                if (showParent) headerCols.Add(("parent", "Veli (Ad & Tel)"));
                if (showSig) headerCols.Add(("signature", "İmza / Yoklama"));

                if (addCustom)
                {
                    var customHeader = ("custom", customTitle);
                    int targetIdx = -1;
                    switch (placement)
                    {
                        case "after-no":
                            targetIdx = headerCols.FindIndex(c => c.Key == "no");
                            break;
                        case "after-tc":
                            targetIdx = headerCols.FindIndex(c => c.Key == "tc");
                            if (targetIdx == -1) targetIdx = headerCols.FindIndex(c => c.Key == "no");
                            break;
                        case "after-name":
                            targetIdx = headerCols.FindIndex(c => c.Key == "name");
                            break;
                        case "after-gender":
                            targetIdx = headerCols.FindIndex(c => c.Key == "gender");
                            if (targetIdx == -1) targetIdx = headerCols.FindIndex(c => c.Key == "name");
                            break;
                        case "after-field":
                            targetIdx = headerCols.FindIndex(c => c.Key == "field");
                            if (targetIdx == -1) targetIdx = headerCols.FindIndex(c => c.Key == "name");
                            break;
                        case "after-enterprise":
                            targetIdx = headerCols.FindIndex(c => c.Key == "enterprise");
                            break;
                        default:
                            targetIdx = -1;
                            break;
                    }

                    if (targetIdx >= 0) headerCols.Insert(targetIdx + 1, customHeader);
                    else headerCols.Add(customHeader);
                }

                sb.AppendLine("\uFEFF" + string.Join(";", headerCols.Select(c => c.Title)));

                var groups = students
                    .GroupBy(s => string.IsNullOrWhiteSpace(s.Classroom) ? "Sınıfsız" : s.Classroom.Trim())
                    .OrderBy(g => g.Key);

                foreach (var grp in groups)
                {
                    int seq = 1;
                    foreach (var s in grp.OrderBy(x => x.StudentNumber).ThenBy(x => x.FullName))
                    {
                        var rowVals = new List<string>();
                        foreach (var col in headerCols)
                        {
                            string val = col.Key switch
                            {
                                "class" => grp.Key,
                                "seq" => seq.ToString(),
                                "no" => s.StudentNumber ?? "",
                                "tc" => s.TcKimlikNo ?? "",
                                "name" => s.FullName,
                                "gender" => s.Gender ?? "",
                                "field" => s.FieldName ?? "",
                                "enterprise" => s.EnterpriseName ?? "",
                                "phone" => s.Phone ?? "",
                                "parent" => (!string.IsNullOrWhiteSpace(s.ParentName) || !string.IsNullOrWhiteSpace(s.ParentPhone))
                                                ? string.Join(" - ", new[] { s.ParentName, s.ParentPhone }.Where(p => !string.IsNullOrWhiteSpace(p)))
                                                : "",
                                "signature" => "",
                                "custom" => "",
                                _ => ""
                            };
                            rowVals.Add($"\"{val.Replace("\"", "\"\"")}\"");
                        }
                        sb.AppendLine(string.Join(";", rowVals));
                        seq++;
                    }
                }

                System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show($"Sınıf listeleri başarıyla kaydedildi:\n{sfd.FileName}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
