using System.Text;
using System.Text.RegularExpressions;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;

namespace MebKoordinatorluk.UI.Dialogs;

public class BulkStudentsDialog : Form
{
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly IFieldRepository _fieldRepo;

    private List<SchoolClass> _allClasses = [];
    private List<Field> _allFields = [];

    private TextBox _txtData = null!;
    private Label _lblStatus = null!;
    private Button _btnImport = null!;

    public int ImportedCount { get; private set; }

    public BulkStudentsDialog(
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        IFieldRepository fieldRepo)
    {
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _fieldRepo = fieldRepo;

        InitializeComponent();
        LoadDataAsync();
    }

    private void InitializeComponent()
    {
        this.Text = "📋 Toplu Öğrenci Listesi Yapıştır (e-Okul / Excel)";
        this.Size = new Size(880, 680);
        this.MinimumSize = new Size(840, 620);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.Font = new Font("Segoe UI", 9f);

        // 1. Üst Başlık & Açıklama
        var lblTitle = new Label
        {
            Text = "Metin / Liste Yapıştırarak Toplu Öğrenci Ekleme",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 14),
            AutoSize = true
        };

        var lblHelp = new Label
        {
            Text = "e-Okul veya Excel'den kopyaladığınız 5 sütunlu listeyi (T.C., Okul No, Ad Soyad, Sınıf, Alan) doğrudan yapıştırabilirsiniz.\nSistemde henüz kayıtlı olmayan Sınıf ve Alanlar otomatik oluşturulur, birbirine bağlanır ve öğrenciler atanır.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 38),
            Size = new Size(825, 34)
        };

        // 2. Yardımcı Eylem Araç Çubuğu (Excel Şablonu, Örnek Metin, Temizle)
        var btnOpenExcel = new Button
        {
            Text = "📥 Excel Şablonunu Aç",
            Location = new Point(20, 78),
            Size = new Size(165, 30),
            BackColor = Color.FromArgb(238, 242, 255),
            ForeColor = Color.FromArgb(67, 56, 202),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnOpenExcel.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);
        btnOpenExcel.Click += (s, e) => OpenExcelTemplate();

        var btnSample = new Button
        {
            Text = "📋 Örnek Metni Doldur",
            Location = new Point(195, 78),
            Size = new Size(165, 30),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSample.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnSample.Click += (s, e) => LoadSampleData();

        var btnClear = new Button
        {
            Text = "🧹 Temizle",
            Location = new Point(370, 78),
            Size = new Size(95, 30),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(100, 116, 139),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        btnClear.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnClear.Click += (s, e) =>
        {
            _txtData.Clear();
            _txtData.Focus();
            _lblStatus.Text = "Kutu temizlendi. Excel'den kopyaladığınız listeyi yapıştırabilirsiniz.";
            _lblStatus.ForeColor = Color.FromArgb(71, 85, 105);
        };

        var lblHint = new Label
        {
            Text = "💡 Excel'de sütunları seçip kopyalayın (Ctrl+C), ardından buraya yapıştırın (Ctrl+V).",
            Location = new Point(480, 84),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139)
        };

        // 3. Görsel Sütun Başlıkları Şeridi (Visual Column Header Strip)
        var pnlHeaders = new Panel
        {
            Location = new Point(20, 118),
            Size = new Size(820, 32),
            BackColor = Color.FromArgb(241, 245, 249)
        };
        pnlHeaders.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawRectangle(pen, 0, 0, pnlHeaders.Width - 1, pnlHeaders.Height - 1);
            // Sütun dikey ayrıcıları
            e.Graphics.DrawLine(pen, 155, 0, 155, pnlHeaders.Height);
            e.Graphics.DrawLine(pen, 250, 0, 250, pnlHeaders.Height);
            e.Graphics.DrawLine(pen, 490, 0, 490, pnlHeaders.Height);
            e.Graphics.DrawLine(pen, 595, 0, 595, pnlHeaders.Height);
        };

        var colTc = new Label
        {
            Text = "🆔 T.C. Kimlik No",
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(8, 7),
            Size = new Size(142, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var colNo = new Label
        {
            Text = "📋 Okul No",
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(160, 7),
            Size = new Size(85, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var colName = new Label
        {
            Text = "👤 Adı Soyadı",
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(255, 7),
            Size = new Size(230, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var colClass = new Label
        {
            Text = "🏫 Sınıf",
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(495, 7),
            Size = new Size(95, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var colField = new Label
        {
            Text = "⚡ Alan / Dal",
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(600, 7),
            Size = new Size(210, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };

        pnlHeaders.Controls.Add(colTc);
        pnlHeaders.Controls.Add(colNo);
        pnlHeaders.Controls.Add(colName);
        pnlHeaders.Controls.Add(colClass);
        pnlHeaders.Controls.Add(colField);

        // 4. Metin Giriş Kutusu (_txtData)
        _txtData = new TextBox
        {
            Location = new Point(20, 149),
            Size = new Size(820, 415),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9.5f),
            WordWrap = false
        };
        _txtData.TextChanged += (s, e) => UpdateLiveStatus();

        // 5. Alt Buton & Durum Çubuğu
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        _lblStatus = new Label
        {
            Text = "Yukarıdaki alana Excel veya e-Okul'dan kopyaladığınız satırları yapıştırın.",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        _btnImport = new Button
        {
            Text = "✓ Öğrencileri İçe Aktar",
            Location = new Point(620, 12),
            Size = new Size(220, 36),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnImport.FlatAppearance.BorderSize = 0;
        _btnImport.Click += async (s, e) => await ParseAndImportAsync();

        var btnCancel = new Button
        {
            Text = "Kapat",
            Location = new Point(515, 12),
            Size = new Size(95, 36),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => { this.Close(); };

        pnlBottom.Controls.Add(_lblStatus);
        pnlBottom.Controls.Add(_btnImport);
        pnlBottom.Controls.Add(btnCancel);

        this.Controls.Add(lblTitle);
        this.Controls.Add(lblHelp);
        this.Controls.Add(btnOpenExcel);
        this.Controls.Add(btnSample);
        this.Controls.Add(btnClear);
        this.Controls.Add(lblHint);
        this.Controls.Add(pnlHeaders);
        this.Controls.Add(_txtData);
        this.Controls.Add(pnlBottom);
    }

    private void OpenExcelTemplate()
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "3308_Pro_Ogrenci_Yukleme_Sablonu.csv");
            var csv = new StringBuilder();
            csv.AppendLine("T.C. Kimlik No;Okul No;Adı Soyadı;Sınıf;Alan");
            csv.AppendLine("11111111110;101;Ali Mert KOÇ;12-A;Bilişim Teknolojileri");
            csv.AppendLine("22222222220;102;Zehra TUNÇ;12-A;Bilişim Teknolojileri");
            csv.AppendLine("33333333330;103;Burak SOYLU;12-B;Elektrik-Elektronik Teknolojisi");
            csv.AppendLine("44444444440;104;Gamze KILIÇ;12-B;Elektrik-Elektronik Teknolojisi");
            csv.AppendLine("55555555550;105;Emre KESKİN;12-A;Bilişim Teknolojileri");

            File.WriteAllText(tempPath, csv.ToString(), new UTF8Encoding(true));

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            _lblStatus.Text = "✓ Excel şablon dosyası açıldı. Verilerinizi düzenleyip kopyalayarak buraya yapıştırabilirsiniz.";
            _lblStatus.ForeColor = Color.FromArgb(22, 101, 52);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel şablonu açılamadı:\n{ex.Message}", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LoadSampleData()
    {
        _txtData.Text =
            "11111111110\t101\tAli Mert KOÇ\t12-A\tBilişim Teknolojileri\r\n" +
            "22222222220\t102\tZehra TUNÇ\t12-A\tBilişim Teknolojileri\r\n" +
            "33333333330\t103\tBurak SOYLU\t12-B\tElektrik-Elektronik Teknolojisi\r\n" +
            "44444444440\t104\tGamze KILIÇ\t12-B\tElektrik-Elektronik Teknolojisi\r\n" +
            "55555555550\t105\tEmre KESKİN\t12-A\tBilişim Teknolojileri";

        try
        {
            Clipboard.SetText(_txtData.Text);
        }
        catch { }

        _txtData.Focus();
        _txtData.SelectAll();

        _lblStatus.Text = "✓ 5 örnek öğrenci yüklendi ve panoya kopyalandı. Dilerseniz Excel'e de yapıştırabilirsiniz.";
        _lblStatus.ForeColor = Color.FromArgb(22, 101, 52);
    }

    private async void LoadDataAsync()
    {
        try
        {
            _allClasses = (await _classRepo.GetAllAsync()).ToList();
            _allFields = (await _fieldRepo.GetAllAsync()).ToList();
        }
        catch
        {
            // Sessizce devam et
        }
    }

    private void UpdateLiveStatus()
    {
        var raw = _txtData.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            _lblStatus.Text = "Yukarıdaki alana Excel veya e-Okul'dan kopyaladığınız satırları yapıştırın.";
            _lblStatus.ForeColor = Color.FromArgb(71, 85, 105);
            return;
        }

        var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int validRows = 0;
        int tcCount = 0;
        int classCount = 0;
        int fieldCount = 0;

        string fallbackClass = _allClasses.FirstOrDefault()?.Name ?? "12-A";
        string fallbackField = _allFields.FirstOrDefault()?.Name ?? "Bilişim Teknolojileri";

        foreach (var line in lines)
        {
            if (IsHeaderLine(line)) continue;

            var row = ParseLine(line, fallbackClass, fallbackField);
            if (row != null && !string.IsNullOrWhiteSpace(row.StudentNumber))
            {
                validRows++;
                if (!string.IsNullOrWhiteSpace(row.TcKimlikNo)) tcCount++;
                if (!string.IsNullOrWhiteSpace(row.Classroom)) classCount++;
                if (!string.IsNullOrWhiteSpace(row.FieldName)) fieldCount++;
            }
        }

        if (validRows > 0)
        {
            _lblStatus.Text = $"📊 {validRows} öğrenci satırı algılandı ({tcCount} T.C. No, {classCount} Sınıf, {fieldCount} Alan tespit edildi)";
            _lblStatus.ForeColor = Color.FromArgb(22, 101, 52); // Koyu yeşil
        }
        else
        {
            _lblStatus.Text = "⚠️ Geçerli öğrenci satırı algılanamadı. Sütun formatını kontrol ediniz.";
            _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
        }
    }

    private static bool IsHeaderLine(string line)
    {
        var lower = line.ToLower(new System.Globalization.CultureInfo("tr-TR"));
        int keywords = 0;
        if (lower.Contains("kimlik") || lower.Contains("t.c") || lower.Contains("tc no")) keywords++;
        if (lower.Contains("okul no") || lower.Contains("öğrenci no") || lower.Contains("numara")) keywords++;
        if (lower.Contains("ad soyad") || lower.Contains("adı soyadı") || lower.Contains("öğrenci adı")) keywords++;
        if (lower.Contains("sınıf") || lower.Contains("şube")) keywords++;
        if (lower.Contains("alan") || lower.Contains("dal") || lower.Contains("bölüm")) keywords++;
        return keywords >= 2;
    }

    private class ParsedRow
    {
        public string TcKimlikNo { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
    }

    private ParsedRow? ParseLine(string line, string fallbackClass, string fallbackField)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        string[] parts;
        if (line.Contains('\t'))
        {
            parts = line.Split('\t', StringSplitOptions.TrimEntries);
        }
        else if (line.Contains(';'))
        {
            parts = line.Split(';', StringSplitOptions.TrimEntries);
        }
        else if (line.Contains(','))
        {
            parts = line.Split(',', StringSplitOptions.TrimEntries);
        }
        else
        {
            parts = Regex.Split(line.Trim(), @"\s{2,}");
            if (parts.Length < 2)
            {
                parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
        }

        parts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        if (parts.Length == 0) return null;

        // 1. 11 Haneli T.C. Kimlik No Algılama
        string tc = parts.FirstOrDefault(p => p.Length == 11 && p.All(char.IsDigit)) ?? "";

        // 2. Okul Numarası (T.C. olmayan ve 1-8 hane sayısal token)
        string no = parts.FirstOrDefault(p => p != tc && p.Length <= 8 && p.All(char.IsDigit)) ?? "";

        // Sayısal bulunamadıysa ve ilk token tc değilse ilk token'ı numara kabul et
        if (string.IsNullOrWhiteSpace(no) && parts.Length > 0 && parts[0] != tc)
        {
            no = parts[0];
        }

        // 3. Sınıf Algılama (12-A, 11/B, 10A veya veritabanındaki sınıf isimleri ile eşleşen)
        string className = parts.FirstOrDefault(p => p != tc && p != no && IsClassToken(p)) ?? "";

        // 4. Alan Algılama (Veritabanındaki alanlar veya anahtar kelimeler)
        string fieldName = parts.FirstOrDefault(p => p != tc && p != no && p != className && IsFieldToken(p)) ?? "";

        // 5. Adı Soyadı Algılama (TC, No, Sınıf, Alan harici kalan tokenlar)
        var nameParts = parts.Where(p => p != tc && p != no && p != className && p != fieldName).ToList();
        string name = string.Join(" ", nameParts).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Öğrenci {no}".Trim();
        }

        return new ParsedRow
        {
            TcKimlikNo = tc,
            StudentNumber = no,
            FullName = name,
            Classroom = string.IsNullOrWhiteSpace(className) ? fallbackClass : className,
            FieldName = string.IsNullOrWhiteSpace(fieldName) ? fallbackField : fieldName
        };
    }

    private bool IsClassToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        if (_allClasses.Any(c => c.Name.Equals(token, StringComparison.OrdinalIgnoreCase)))
            return true;

        return Regex.IsMatch(token.Trim(), @"^(?:9|10|11|12)[-/\s]?[A-Za-zÇĞİÖŞÜçğıöşü0-9]+$", RegexOptions.IgnoreCase);
    }

    private bool IsFieldToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        if (_allFields.Any(f => f.Name.Equals(token, StringComparison.OrdinalIgnoreCase)))
            return true;

        var lower = token.ToLower(new System.Globalization.CultureInfo("tr-TR"));
        return lower.Contains("bilişim") ||
               lower.Contains("elektrik") ||
               lower.Contains("elektronik") ||
               lower.Contains("muhasebe") ||
               lower.Contains("çocuk") ||
               lower.Contains("güzellik") ||
               lower.Contains("makine") ||
               lower.Contains("motor") ||
               lower.Contains("tasarım") ||
               lower.Contains("grafik") ||
               lower.Contains("inşaat") ||
               lower.Contains("kimya") ||
               lower.Contains("sağlık") ||
               lower.Contains("büro") ||
               lower.Contains("yiyecek") ||
               lower.Contains("konaklama") ||
               lower.Contains("pazarlama") ||
               lower.Contains("lojistik") ||
               lower.Contains("uçak") ||
               lower.Contains("metal") ||
               lower.Contains("adalet") ||
               lower.Contains("tarım") ||
               lower.Contains("hayvan") ||
               lower.Contains("tekstil") ||
               lower.Contains("moda");
    }

    private async Task ParseAndImportAsync()
    {
        var raw = _txtData.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            MessageBox.Show("Lütfen yapıştırılacak öğrenci verisi girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            MessageBox.Show("İşlenecek öğrenci satırı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnImport.Enabled = false;
        _btnImport.Text = "⏳ Aktarılıyor...";

        try
        {
            string fallbackClass = _allClasses.FirstOrDefault()?.Name ?? "12-A";
            string fallbackField = _allFields.FirstOrDefault()?.Name ?? "Bilişim Teknolojileri";

            var existingStudents = (await _studentRepo.GetAllAsync(includeInactive: true)).ToList();
            int addedCount = 0;
            int updatedCount = 0;
            int createdClassCount = 0;
            int createdFieldCount = 0;

            foreach (var line in lines)
            {
                if (IsHeaderLine(line)) continue;

                var parsed = ParseLine(line, fallbackClass, fallbackField);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.StudentNumber)) continue;

                // 1. Alan Kontrolü ve Yoksa Otomatik Oluşturma
                Field? currentField = null;
                string cleanFieldName = (parsed.FieldName ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanFieldName))
                {
                    currentField = _allFields.FirstOrDefault(f => f.Name.Equals(cleanFieldName, StringComparison.OrdinalIgnoreCase));
                    if (currentField == null)
                    {
                        currentField = new Field
                        {
                            Name = cleanFieldName,
                            Description = cleanFieldName,
                            Branches = cleanFieldName,
                            IsActive = true
                        };
                        currentField.Id = await _fieldRepo.AddAsync(currentField);
                        _allFields.Add(currentField);
                        createdFieldCount++;
                    }
                }

                // 2. Sınıf Kontrolü, Yoksa Otomatik Oluşturma ve Alana Bağlama
                SchoolClass? currentClass = null;
                string cleanClassName = (parsed.Classroom ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanClassName))
                {
                    currentClass = _allClasses.FirstOrDefault(c => c.Name.Equals(cleanClassName, StringComparison.OrdinalIgnoreCase));
                    if (currentClass == null)
                    {
                        currentClass = new SchoolClass
                        {
                            Name = cleanClassName,
                            FieldId = currentField?.Id,
                            FieldName = currentField?.Name ?? cleanFieldName,
                            InstitutionType = "Anadolu Meslek Programı",
                            EducationType = "Örgün Eğitim",
                            InternshipDays = "Pazartesi,Salı,Çarşamba",
                            HasInternship = true,
                            IsActive = true
                        };
                        currentClass.Id = await _classRepo.AddAsync(currentClass);
                        _allClasses.Add(currentClass);
                        createdClassCount++;
                    }
                    else
                    {
                        // Sınıf var ama alana bağlanmamışsa, alana bağla
                        if ((currentClass.FieldId == null || currentClass.FieldId == 0) && currentField != null)
                        {
                            currentClass.FieldId = currentField.Id;
                            currentClass.FieldName = currentField.Name;
                            await _classRepo.UpdateAsync(currentClass);
                        }
                    }
                }

                // 3. İsim - Soyisim ayrıştırma (Türkçe son kelime soyad standardı)
                string firstName = parsed.FullName;
                string lastName = "";
                var nameTokens = parsed.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (nameTokens.Length > 1)
                {
                    firstName = string.Join(" ", nameTokens.Take(nameTokens.Length - 1));
                    lastName = nameTokens.Last();
                }

                // Sınıf seviyesi (Grade) çıkarma (örn: "12-A" -> "12")
                string grade = "12";
                var gradeMatch = Regex.Match(cleanClassName, @"^\d+");
                if (gradeMatch.Success) grade = gradeMatch.Value;

                var finalClassName = currentClass?.Name ?? cleanClassName;
                var finalClassId = currentClass?.Id;
                var finalFieldName = currentField?.Name ?? currentClass?.FieldName ?? cleanFieldName;
                var finalFieldId = currentField?.Id ?? currentClass?.FieldId;
                var finalInternshipDays = !string.IsNullOrWhiteSpace(currentClass?.InternshipDays)
                    ? currentClass.InternshipDays
                    : "Pazartesi,Salı,Çarşamba";

                // 4. Öğrenci Kayıt veya Güncelleme
                var existing = existingStudents.FirstOrDefault(s => s.StudentNumber == parsed.StudentNumber);
                if (existing != null)
                {
                    if (!string.IsNullOrWhiteSpace(parsed.TcKimlikNo)) existing.TcKimlikNo = parsed.TcKimlikNo;
                    if (!string.IsNullOrWhiteSpace(firstName)) existing.FirstName = firstName;
                    if (!string.IsNullOrWhiteSpace(lastName)) existing.LastName = lastName;
                    existing.Classroom = finalClassName;
                    if (finalClassId.HasValue) existing.ClassId = finalClassId;
                    existing.FieldName = finalFieldName;
                    if (finalFieldId.HasValue)
                    {
                        existing.FieldId = finalFieldId;
                        existing.DepartmentId = finalFieldId.Value;
                    }
                    existing.InternshipDays = finalInternshipDays;
                    existing.IsActive = true;

                    await _studentRepo.UpdateAsync(existing);
                    updatedCount++;
                }
                else
                {
                    var newStudent = new Student
                    {
                        TcKimlikNo = parsed.TcKimlikNo,
                        StudentNumber = parsed.StudentNumber,
                        FirstName = firstName,
                        LastName = lastName,
                        Classroom = finalClassName,
                        Grade = grade,
                        ClassId = finalClassId,
                        FieldName = finalFieldName,
                        FieldId = finalFieldId,
                        DepartmentId = finalFieldId ?? 1,
                        InternshipDays = finalInternshipDays,
                        FeeCategory = "kat1",
                        EducationType = "Örgün Eğitim",
                        IsActive = true
                    };

                    await _studentRepo.AddAsync(newStudent);
                    existingStudents.Add(newStudent);
                    addedCount++;
                }
            }

            ImportedCount = addedCount + updatedCount;

            var sb = new StringBuilder();
            sb.AppendLine($"✓ {addedCount} yeni öğrenci sisteme kaydedildi.");
            if (updatedCount > 0)
                sb.AppendLine($"✓ {updatedCount} mevcut öğrencinin bilgileri güncellendi.");
            if (createdClassCount > 0)
                sb.AppendLine($"✓ {createdClassCount} yeni sınıf otomatik oluşturuldu ve alanına bağlandı.");
            if (createdFieldCount > 0)
                sb.AppendLine($"✓ {createdFieldCount} yeni meslek alanı otomatik oluşturuldu.");

            MessageBox.Show(sb.ToString(), "Toplu İçe Aktarım Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Aktarım sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnImport.Enabled = true;
            _btnImport.Text = "✓ Öğrencileri İçe Aktar";
        }
    }
}
