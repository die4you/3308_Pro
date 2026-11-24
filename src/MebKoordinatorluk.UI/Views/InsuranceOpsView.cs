using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Forms;
using System.Xml.Linq;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Models;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.Data.Services;
using MebKoordinatorluk.UI.Common;
using MebKoordinatorluk.UI.Dialogs;

namespace MebKoordinatorluk.UI.Views;

public class InsuranceOpsView : UserControl
{
    private readonly string _opType;
    private readonly IStudentRepository _studentRepo;
    private readonly ISchoolClassRepository _classRepo;
    private readonly ISettingRepository _settingRepo;
    private readonly IAttendanceRepository? _attendanceRepo;
    private readonly Action<string>? _onNavigate;
    private readonly ISgkCrossCheckService _crossCheckService;

    private DataGridView _grid = null!;
    private Label _lblTitle = null!;
    private Label _lblSubtitle = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cmbClassFilter = null!;

    // XML e-Bildirge Özel Filtre Kontrolleri
    private ComboBox _cmbYear = null!;
    private ComboBox _cmbMonth = null!;
    private ComboBox _cmbDocumentType = null!;  // ASIL / EK
    private ComboBox _cmbSgkBelgeType = null!;  // 49, 50, 07, 42
    private ComboBox _cmbEBildirgeFilter = null!; // Tüm Öğrenciler (MESEM Hariç) / Sadece MESEM Öğrencileri
    private ComboBox? _cmbIsgCertFilter; // İSG Sertifika Durumu Filtresi
    private ComboBox? _cmbGradeLevelFilter; // Sınıf Seviyesi Filtresi
    private ComboBox? _cmbEduTypeFilter; // Öğretim Türü Filtresi (Örgün / MESEM)
    private ComboBox? _cmbEntryExitFilter; // İşe Giriş/Çıkış Tarihi Filtresi
    private ComboBox? _cmbSgkExitStatusFilter; // Sürekli Devamsızlar SGK Çıkış Durumu Filtresi

    // Toplu İşe Giriş/Çıkış Tarihi Kontrolleri
    private RadioButton _rbApplyEntry = null!;
    private DateTimePicker _dtpBulkEntry = null!;
    private RadioButton _rbApplyExit = null!;
    private DateTimePicker _dtpBulkExit = null!;
    private CheckBox _chkSelectAll = null!;

    // İSG Toplu Sertifika Kontrolleri
    private ComboBox? _cmbBulkIsgStatus;
    private DateTimePicker? _dtpBulkIsgDate;
    private TextBox? _txtBulkIsgCertNo;
    private CheckBox? _chkIsgDocPrinted;
    private Button? _btnApplyIsgBulk;
    private int _nextIsgCertNo = 1001;

    private Button _btnPrimaryAction = null!;
    private Button _btnSecondaryAction = null!;
    private Button? _btnPreAudit;
    private Button? _btnEBildirgeGenerate;
    private Label _lblSummary = null!;

    // XML e-Bildirge Kart İstatistik Kontrolleri
    private Label? _lblEBildirgeCardTotal;
    private Label? _lblEBildirgeCardOrgun;
    private Label? _lblEBildirgeCardMesem;
    private Label? _lblEBildirgeCardYukumlu;
    private Label? _lblEBildirgeCardYukumsuz;
    private Label? _lblEBildirgeCardDesc;

    private List<Student> _allStudents = new();

    public InsuranceOpsView(
        string opType,
        IStudentRepository studentRepo,
        ISchoolClassRepository classRepo,
        ISettingRepository settingRepo,
        IAttendanceRepository? attendanceRepo = null,
        Action<string>? onNavigate = null,
        ISgkCrossCheckService? crossCheckService = null)
    {
        _opType = opType;
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _settingRepo = settingRepo;
        _attendanceRepo = attendanceRepo;
        _onNavigate = onNavigate;
        _crossCheckService = crossCheckService ?? new SgkCrossCheckService(attendanceRepo);

        InitializeComponent();
        LoadDataAsync();

        VisibleChanged += (s, e) =>
        {
            if (Visible)
            {
                LoadDataAsync();
            }
        };
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.White,
            Padding = new Padding(24, 12, 24, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        _lblTitle = new Label
        {
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Location = new Point(24, 10),
            AutoSize = true
        };

        _lblSubtitle = new Label
        {
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = UITheme.TextSecondary,
            Location = new Point(25, 38),
            AutoSize = true
        };

        _btnPrimaryAction = new Button
        {
            Size = new Size(200, 36),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnPrimaryAction.FlatAppearance.BorderSize = 0;
        _btnPrimaryAction.Click += BtnPrimaryAction_Click;

        _btnSecondaryAction = new Button
        {
            Size = new Size(180, 36),
            BackColor = Color.FromArgb(15, 23, 42), // slate-900
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnSecondaryAction.FlatAppearance.BorderSize = 0;
        _btnSecondaryAction.Click += BtnSecondaryAction_Click;

        void PositionHeaderButtons()
        {
            if (_opType == "toplu-isegiris-cikis")
            {
                _btnSecondaryAction.Visible = false;
            }
            else if (_opType == "xml-ebildirge")
            {
                _btnPrimaryAction.Visible = false;
                _btnSecondaryAction.Visible = false;
            }
            else if (_opType == "isg-islemleri")
            {
                _btnPrimaryAction.Visible = true;
                _btnSecondaryAction.Visible = false;
                _btnPrimaryAction.Location = new Point(pnlHeader.ClientSize.Width - 224, 16);
            }
            else if (_opType == "surekli-devamsizlar")
            {
                _btnPrimaryAction.Visible = true;
                _btnSecondaryAction.Visible = true;
                _btnPrimaryAction.Text = "➕ Sürekli Devamsız Ekle";
                _btnSecondaryAction.Text = "📅 SGK Çıkış Tarihi İşle";
                _btnSecondaryAction.Size = new Size(195, 36);
                _btnPrimaryAction.Location = new Point(pnlHeader.ClientSize.Width - 224, 16);
                _btnSecondaryAction.Location = new Point(_btnPrimaryAction.Left - 205, 16);
            }
            else
            {
                _btnPrimaryAction.Location = new Point(pnlHeader.ClientSize.Width - 224, 16);
                _btnSecondaryAction.Location = new Point(_btnPrimaryAction.Left - 190, 16);
            }
        }
        pnlHeader.Resize += (s, e) => PositionHeaderButtons();
        PositionHeaderButtons();

        pnlHeader.Controls.AddRange([_lblTitle, _lblSubtitle, _btnPrimaryAction, _btnSecondaryAction]);

        // Filter Bar Panel
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris" || _opType == "xml-ebildirge") ? 78 : 54,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(24, 8, 24, 8)
        };
        pnlFilter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
        };

        _lblSummary = new Label
        {
            Text = "Toplam 0 kayıt",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 58, 138),
            AutoSize = true
        };

        void PositionSummary()
        {
            if (_lblSummary != null && pnlFilter != null)
            {
                if (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris" || _opType == "xml-ebildirge")
                {
                    _lblSummary.Location = new Point(Math.Max(12, (pnlFilter.ClientSize.Width - _lblSummary.Width) / 2), 48);
                }
                else
                {
                    _lblSummary.Location = new Point(Math.Max(24, pnlFilter.ClientSize.Width - _lblSummary.Width - 24), Math.Max(0, (pnlFilter.ClientSize.Height - _lblSummary.Height) / 2));
                }
            }
        }
        pnlFilter.Resize += (s, e) => PositionSummary();

        if (_opType == "xml-ebildirge")
        {
            // e-Bildirge için ortalanmış çoklu filtre paneli (Görsellerdeki 5 dropdown + Yeşil Hazırla Butonu)
            var pnlFilterCenter = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            // 1. Öğrenci Grubu (Görsel 1: Seçiniz., Tüm Öğrenciler (MESEM Hariç), Sadece MESEM Öğrencileri)
            _cmbEBildirgeFilter = new ComboBox
            {
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 2, 8, 0)
            };
            _cmbEBildirgeFilter.Items.AddRange(new[]
            {
                "Seçiniz.",
                "Tüm Öğrenciler (MESEM Hariç)",
                "Sadece MESEM Öğrencileri"
            });
            _cmbEBildirgeFilter.SelectedIndex = 0;

            // 2. SGK Belge Kodu (Görsel 3: 49, 50, 07, 42)
            _cmbSgkBelgeType = new ComboBox
            {
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 2, 8, 0)
            };
            UpdateSgkBelgeTypeDropdown();

            _cmbEBildirgeFilter.SelectedIndexChanged += (s, e) =>
            {
                UpdateSgkBelgeTypeDropdown();
                FilterData();
            };
            _cmbSgkBelgeType.SelectedIndexChanged += (s, e) => FilterData();

            // 3. Bildirge Türü (Görsel 2: Seçiniz., ASIL, EK, EK-9)
            _cmbDocumentType = new ComboBox
            {
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 2, 8, 0)
            };
            _cmbDocumentType.Items.AddRange(new[] { "Seçiniz.", "ASIL", "EK", "EK-9" });
            _cmbDocumentType.SelectedIndex = 1; // Default: ASIL
            _cmbDocumentType.SelectedIndexChanged += (s, e) => FilterData();

            // 4. Yıl (Görsel 4: 2025, 2026, 2027...)
            _cmbYear = new ComboBox
            {
                Width = 85,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 2, 8, 0)
            };
            int currentYear = DateTime.Now.Year;
            _cmbYear.Items.AddRange(new object[] { currentYear - 1, currentYear, currentYear + 1 });
            _cmbYear.SelectedItem = currentYear;
            _cmbYear.SelectedIndexChanged += (s, e) => FilterData();

            // 5. Ay (Görsel 4: Ekim...)
            _cmbMonth = new ComboBox
            {
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 2, 10, 0)
            };
            _cmbMonth.Items.AddRange(new[] { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" });
            _cmbMonth.SelectedIndex = DateTime.Now.Month - 1;
            _cmbMonth.SelectedIndexChanged += (s, e) => FilterData();

            // 6. Mavi SGK Çapraz Denetim Butonu
            _btnPreAudit = new Button
            {
                Text = "🔍 Çapraz Denetim Yap",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(37, 99, 235), // Blue-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(175, 29),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnPreAudit.FlatAppearance.BorderSize = 0;
            _btnPreAudit.Click += (s, e) => _ = RunSgkCrossCheckManualAsync();

            // 7. Yeşil e-Bildirge XML Hazırla Butonu (Görsel 4)
            _btnEBildirgeGenerate = new Button
            {
                Text = "e-Bildirge XML Hazırla",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(40, 167, 69), // Bootstrap / MEB style green
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 29),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnEBildirgeGenerate.FlatAppearance.BorderSize = 0;
            _btnEBildirgeGenerate.Click += (s, e) => _ = ExportXmlEBildirgeAsync();

            pnlFilterCenter.Controls.AddRange([_cmbEBildirgeFilter, _cmbSgkBelgeType, _cmbDocumentType, _cmbYear, _cmbMonth, _btnPreAudit, _btnEBildirgeGenerate]);

            void CenterFilterBar()
            {
                if (pnlFilter.Width <= 0 || pnlFilterCenter.Width <= 0) return;
                int x = Math.Max(12, (pnlFilter.ClientSize.Width - pnlFilterCenter.Width) / 2);
                pnlFilterCenter.Location = new Point(x, 10);
                if (_lblSummary != null)
                {
                    _lblSummary.Location = new Point(Math.Max(12, (pnlFilter.ClientSize.Width - _lblSummary.Width) / 2), 48);
                }
            }

            pnlFilter.Resize += (s, e) => CenterFilterBar();
            pnlFilterCenter.SizeChanged += (s, e) => CenterFilterBar();
            _lblSummary.SizeChanged += (s, e) => CenterFilterBar();
            CenterFilterBar();

            pnlFilter.Controls.AddRange([pnlFilterCenter, _lblSummary]);
        }
        else if (_opType.StartsWith("isg-"))
        {
            // İSG Ekranı: Sınıf, Seviye, Sertifika Durumu ve Arama Filtreleri
            var lblFilterClass = new Label
            {
                Text = "Sınıf:",
                Location = new Point(24, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbClassFilter = new ComboBox
            {
                Location = new Point(65, 12),
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbClassFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblGradeLevel = new Label
            {
                Text = "Seviye:",
                Location = new Point(185, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbGradeLevelFilter = new ComboBox
            {
                Location = new Point(235, 12),
                Width = 115,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbGradeLevelFilter.Items.AddRange(["Tüm Seviyeler", "9. Sınıf", "10. Sınıf", "11. Sınıf", "12. Sınıf", "MESEM"]);
            _cmbGradeLevelFilter.SelectedIndex = 0;
            _cmbGradeLevelFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblIsgCert = new Label
            {
                Text = "Sertifika Durumu:",
                Location = new Point(360, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbIsgCertFilter = new ComboBox
            {
                Location = new Point(475, 12),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbIsgCertFilter.Items.AddRange(new[] { "Tüm Durumlar", "Sertifikalı (Tamamlandı)", "Sertifikasız (Eksik)", "Belge Basılanlar (Evet)", "Belge Basılmayanlar (Hayır)" });
            _cmbIsgCertFilter.SelectedIndex = 0;
            _cmbIsgCertFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblSearch = new Label
            {
                Text = "Öğrenci Ara:",
                Location = new Point(665, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _txtSearch = new TextBox
            {
                Location = new Point(745, 12),
                Width = 140,
                PlaceholderText = "Ad, Soyad..."
            };
            _txtSearch.TextChanged += (s, e) => FilterData();

            var btnBulkPrintIsgDoc = new Button
            {
                Text = "🖨️ Toplu İSG Belgesi Bas",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(175, 32),
                Location = new Point(895, 10),
                Cursor = Cursors.Hand
            };
            btnBulkPrintIsgDoc.FlatAppearance.BorderSize = 0;
            btnBulkPrintIsgDoc.Click += (s, e) => PrintIsgCertificatesForSelectedRows();

            pnlFilter.Controls.AddRange([lblFilterClass, _cmbClassFilter, lblGradeLevel, _cmbGradeLevelFilter, lblIsgCert, _cmbIsgCertFilter, lblSearch, _txtSearch, btnBulkPrintIsgDoc, _lblSummary]);
        }
        else if (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris")
        {
            var pnlFilterCenter = new Panel
            {
                Size = new Size(950, 34),
                BackColor = Color.Transparent
            };

            var lblFilterClass = new Label
            {
                Text = "Sınıf:",
                Location = new Point(0, 7),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbClassFilter = new ComboBox
            {
                Location = new Point(40, 3),
                Width = 90,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbClassFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblGradeLevel = new Label
            {
                Text = "Seviye:",
                Location = new Point(138, 7),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbGradeLevelFilter = new ComboBox
            {
                Location = new Point(190, 3),
                Width = 95,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbGradeLevelFilter.Items.AddRange(["Tüm Seviyeler", "9. Sınıf", "10. Sınıf", "11. Sınıf", "12. Sınıf", "MESEM"]);
            _cmbGradeLevelFilter.SelectedIndex = 0;
            _cmbGradeLevelFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblEduType = new Label
            {
                Text = "Öğretim Türü:",
                Location = new Point(295, 7),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbEduTypeFilter = new ComboBox
            {
                Location = new Point(380, 3),
                Width = 115,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbEduTypeFilter.Items.AddRange(["Tüm Öğretim Türleri", "Örgün Eğitim", "MESEM"]);
            _cmbEduTypeFilter.SelectedIndex = 0;
            _cmbEduTypeFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblDateFilter = new Label
            {
                Text = "Tarih Filtresi:",
                Location = new Point(505, 7),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbEntryExitFilter = new ComboBox
            {
                Location = new Point(585, 3),
                Width = 195,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbEntryExitFilter.Items.AddRange(new[]
            {
                "Tüm Öğrenciler",
                "İşe Giriş Tarihi Var",
                "İşe Giriş Tarihi Yok (Eksik)",
                "İşten Çıkış Tarihi Var",
                "İşten Çıkış Tarihi Yok (Eksik)",
                "Giriş veya Çıkış Tarihi Var"
            });
            _cmbEntryExitFilter.SelectedIndex = _opType == "xml-isegiris" ? 5 : 0;
            _cmbEntryExitFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblSearch = new Label
            {
                Text = "Ara:",
                Location = new Point(790, 7),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _txtSearch = new TextBox
            {
                Location = new Point(825, 3),
                Width = 120,
                PlaceholderText = "Ad, Soyad..."
            };
            _txtSearch.TextChanged += (s, e) => FilterData();

            pnlFilterCenter.Controls.AddRange([lblFilterClass, _cmbClassFilter, lblGradeLevel, _cmbGradeLevelFilter, lblEduType, _cmbEduTypeFilter, lblDateFilter, _cmbEntryExitFilter, lblSearch, _txtSearch]);

            void CenterFilterBar()
            {
                if (pnlFilter.Width <= 0 || pnlFilterCenter.Width <= 0) return;
                int x = Math.Max(12, (pnlFilter.ClientSize.Width - pnlFilterCenter.Width) / 2);
                pnlFilterCenter.Location = new Point(x, 8);
                if (_lblSummary != null)
                {
                    _lblSummary.Location = new Point(Math.Max(12, (pnlFilter.ClientSize.Width - _lblSummary.Width) / 2), 48);
                }
            }

            pnlFilter.Resize += (s, e) => CenterFilterBar();
            _lblSummary.SizeChanged += (s, e) => CenterFilterBar();
            CenterFilterBar();

            pnlFilter.Controls.AddRange([pnlFilterCenter, _lblSummary]);
        }
        else if (_opType == "surekli-devamsizlar")
        {
            var lblFilterClass = new Label
            {
                Text = "Sınıf:",
                Location = new Point(24, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbClassFilter = new ComboBox
            {
                Location = new Point(65, 12),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbClassFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblGradeLevel = new Label
            {
                Text = "Seviye:",
                Location = new Point(175, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbGradeLevelFilter = new ComboBox
            {
                Location = new Point(225, 12),
                Width = 105,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbGradeLevelFilter.Items.AddRange(["Tüm Seviyeler", "9. Sınıf", "10. Sınıf", "11. Sınıf", "12. Sınıf", "MESEM"]);
            _cmbGradeLevelFilter.SelectedIndex = 0;
            _cmbGradeLevelFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblExitStatus = new Label
            {
                Text = "SGK Çıkış Durumu:",
                Location = new Point(340, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbSgkExitStatusFilter = new ComboBox
            {
                Location = new Point(460, 12),
                Width = 230,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbSgkExitStatusFilter.Items.AddRange([
                "Tüm Sürekli Devamsızlar",
                "⏳ SGK Çıkışı Bekleyenler (Tarih Boş)",
                "✔️ SGK Çıkışı Yapılmış Olanlar"
            ]);
            _cmbSgkExitStatusFilter.SelectedIndex = 0;
            _cmbSgkExitStatusFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblSearch = new Label
            {
                Text = "Öğrenci Ara:",
                Location = new Point(700, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _txtSearch = new TextBox
            {
                Location = new Point(780, 12),
                Width = 150,
                PlaceholderText = "Ad, Soyad veya Okul No..."
            };
            _txtSearch.TextChanged += (s, e) => FilterData();

            pnlFilter.Controls.AddRange([lblFilterClass, _cmbClassFilter, lblGradeLevel, _cmbGradeLevelFilter, lblExitStatus, _cmbSgkExitStatusFilter, lblSearch, _txtSearch, _lblSummary]);
        }
        else
        {
            // Standart Sınıf, Seviye, Öğretim Türü ve Arama Filtreleri
            var lblFilterClass = new Label
            {
                Text = "Sınıf:",
                Location = new Point(24, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbClassFilter = new ComboBox
            {
                Location = new Point(65, 12),
                Width = 105,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbClassFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblGradeLevel = new Label
            {
                Text = "Seviye:",
                Location = new Point(180, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbGradeLevelFilter = new ComboBox
            {
                Location = new Point(230, 12),
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbGradeLevelFilter.Items.AddRange(["Tüm Seviyeler", "9. Sınıf", "10. Sınıf", "11. Sınıf", "12. Sınıf", "MESEM"]);
            _cmbGradeLevelFilter.SelectedIndex = 0;
            _cmbGradeLevelFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblEduType = new Label
            {
                Text = "Öğretim Türü:",
                Location = new Point(350, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _cmbEduTypeFilter = new ComboBox
            {
                Location = new Point(435, 12),
                Width = 135,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbEduTypeFilter.Items.AddRange(["Tüm Öğretim Türleri", "Örgün Eğitim", "MESEM"]);
            _cmbEduTypeFilter.SelectedIndex = 0;
            _cmbEduTypeFilter.SelectedIndexChanged += (s, e) => FilterData();

            var lblSearch = new Label
            {
                Text = "Öğrenci Ara:",
                Location = new Point(580, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            _txtSearch = new TextBox
            {
                Location = new Point(660, 12),
                Width = 160,
                PlaceholderText = "Ad, Soyad veya Okul No..."
            };
            _txtSearch.TextChanged += (s, e) => FilterData();

            pnlFilter.Controls.AddRange([lblFilterClass, _cmbClassFilter, lblGradeLevel, _cmbGradeLevelFilter, lblEduType, _cmbEduTypeFilter, lblSearch, _txtSearch, _lblSummary]);
        }

        // Data Grid Container
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
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
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            RowTemplate = { Height = 36 }
        };

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        _grid.EnableHeadersVisualStyles = false;
        _grid.CellContentClick += Grid_CellContentClick;

        _grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex == -1 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "colSelect")
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                bool allChecked = true;
                bool hasCheckableRows = false;
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Cells["colSelect"] == null || row.Cells["colSelect"].ReadOnly) continue;
                    hasCheckableRows = true;
                    if (!Convert.ToBoolean(row.Cells["colSelect"].Value))
                    {
                        allChecked = false;
                        break;
                    }
                }
                if (!hasCheckableRows) allChecked = false;

                var state = allChecked ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                Size checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                Point checkLocation = new Point(
                    e.CellBounds.Left + (e.CellBounds.Width - checkSize.Width) / 2,
                    e.CellBounds.Top + (e.CellBounds.Height - checkSize.Height) / 2
                );

                CheckBoxRenderer.DrawCheckBox(e.Graphics, checkLocation, state);
                e.Handled = true;
            }
            else if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "colSelect")
            {
                if (_opType == "xml-isegiris" && _grid.Rows[e.RowIndex].Cells["colSelect"]?.ReadOnly == true)
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
                    var state = System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled;
                    Size checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                    Point checkLocation = new Point(
                        e.CellBounds.Left + (e.CellBounds.Width - checkSize.Width) / 2,
                        e.CellBounds.Top + (e.CellBounds.Height - checkSize.Height) / 2
                    );
                    CheckBoxRenderer.DrawCheckBox(e.Graphics, checkLocation, state);
                    e.Handled = true;
                }
            }
        };

        _grid.ColumnHeaderMouseClick += (s, e) =>
        {
            if ((_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris" || _opType.StartsWith("isg-")) && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "colSelect")
            {
                _grid.EndEdit();

                bool allChecked = true;
                bool hasCheckableRows = false;
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Cells["colSelect"] == null || row.Cells["colSelect"].ReadOnly) continue;
                    hasCheckableRows = true;
                    if (!Convert.ToBoolean(row.Cells["colSelect"].Value))
                    {
                        allChecked = false;
                        break;
                    }
                }
                if (!hasCheckableRows) allChecked = false;

                bool targetVal = !allChecked;
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Cells["colSelect"] != null)
                    {
                        if (row.Cells["colSelect"].ReadOnly)
                        {
                            row.Cells["colSelect"].Value = false;
                        }
                        else
                        {
                            row.Cells["colSelect"].Value = targetVal;
                        }
                    }
                }
                _grid.EndEdit();
                _grid.InvalidateCell(e.ColumnIndex, -1);
            }
        };

        _grid.CellBeginEdit += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "colSelect")
            {
                if (_grid.Rows[e.RowIndex].Cells["colSelect"]?.ReadOnly == true)
                {
                    e.Cancel = true;
                }
            }
        };

        _grid.CellClick += (s, e) =>
        {
            if ((_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris" || _opType.StartsWith("isg-")) && e.RowIndex >= 0 && e.ColumnIndex != 0)
            {
                if (_grid.Columns.Contains("colSelect") && _grid.Rows[e.RowIndex].Cells["colSelect"] != null)
                {
                    var cell = _grid.Rows[e.RowIndex].Cells["colSelect"];
                    if (cell.ReadOnly) return;

                    _grid.EndEdit();
                    bool currentVal = cell.Value is true;
                    cell.Value = !currentVal;
                    _grid.EndEdit();
                    if (_grid.Columns.Contains("colSelect"))
                    {
                        _grid.InvalidateCell(_grid.Columns["colSelect"].Index, -1);
                    }
                }
            }
        };

        _grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if ((_opType == "toplu-isegiris-cikis" || _opType.StartsWith("isg-")) && _grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                if (_grid.Columns.Contains("colSelect"))
                {
                    _grid.InvalidateCell(_grid.Columns["colSelect"].Index, -1);
                }
            }
        };

        if (_opType == "xml-ebildirge")
        {
            // Tablo yerine bilgilendirme, istatistik ve yönerge paneli
            var pnlEBildirgeCenter = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UITheme.Background,
                Padding = new Padding(24, 20, 24, 20),
                AutoScroll = true
            };

            var cardInfo = new Panel
            {
                Size = new Size(880, 440),
                BackColor = Color.White,
                Padding = new Padding(24)
            };
            cardInfo.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, cardInfo.Width - 1, cardInfo.Height - 1);
            };

            var lblCardTitle = new Label
            {
                Text = "📑 SGK e-Bildirge XML Aktarım Modülü",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(24, 20),
                AutoSize = true
            };

            var lblInstructions = new Label
            {
                Text = "Bu ekranda stajyer öğrencilerin aylık SGK prim bildirgeleri standart SGK e-Bildirge XML (V2) formatında oluşturulur.\n" +
                       "Yukarıdaki seçim panelinden bildirge kriterlerini belirleyip doğrudan 'e-Bildirge XML Hazırla' butonuna tıklayınız.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(24, 52),
                Size = new Size(830, 42)
            };

            // Stat Cards Container
            var flpStats = new FlowLayoutPanel
            {
                Location = new Point(24, 106),
                Size = new Size(832, 90),
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                WrapContents = false
            };

            Panel CreateMiniStat(string title, out Label valLabel, Color bg, Color textCol, int w = 158)
            {
                var pnl = new Panel
                {
                    Size = new Size(w, 82),
                    BackColor = bg,
                    Margin = new Padding(0, 0, 10, 0),
                    Padding = new Padding(12, 10, 12, 10)
                };
                pnl.Paint += (s, e) =>
                {
                    using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
                    e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
                };

                var lblT = new Label
                {
                    Text = title,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Dock = DockStyle.Top,
                    Height = 22
                };
                valLabel = new Label
                {
                    Text = "0",
                    Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                    ForeColor = textCol,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                pnl.Controls.AddRange([valLabel, lblT]);
                return pnl;
            }

            var statTotal = CreateMiniStat("👥 Toplam Öğrenci", out _lblEBildirgeCardTotal, Color.FromArgb(248, 250, 252), Color.FromArgb(30, 58, 138));
            var statOrgun = CreateMiniStat("🏫 Örgün Eğitim", out _lblEBildirgeCardOrgun, Color.FromArgb(240, 253, 244), Color.FromArgb(21, 128, 61));
            var statMesem = CreateMiniStat("🏢 MESEM", out _lblEBildirgeCardMesem, Color.FromArgb(254, 242, 242), Color.FromArgb(185, 28, 28));
            var statYuk = CreateMiniStat("🛡️ Bakmakla Yükümlü", out _lblEBildirgeCardYukumlu, Color.FromArgb(239, 246, 255), Color.FromArgb(29, 78, 216));
            var statYuksuz = CreateMiniStat("⚠️ Yükümlü Olmayan", out _lblEBildirgeCardYukumsuz, Color.FromArgb(254, 243, 199), Color.FromArgb(180, 83, 9));

            flpStats.Controls.AddRange([statTotal, statOrgun, statMesem, statYuk, statYuksuz]);

            _lblEBildirgeCardDesc = new Label
            {
                Text = "ℹ️ Filtrelere göre bildirgeye dahil edilecek kayıtlar otomatik hesaplanmaktadır.",
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(24, 206),
                AutoSize = true
            };

            // e-Okul Öğrenci Kontrolü Tavsiye Paneli
            var pnlAuditAlert = new Panel
            {
                Location = new Point(24, 232),
                Size = new Size(830, 72),
                BackColor = Color.FromArgb(254, 243, 199), // amber-100
                Padding = new Padding(16, 10, 16, 10)
            };
            pnlAuditAlert.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(245, 158, 11), 1); // amber-500
                e.Graphics.DrawRectangle(pen, 0, 0, pnlAuditAlert.Width - 1, pnlAuditAlert.Height - 1);
            };

            var lblAuditIconTitle = new Label
            {
                Text = "⚠️ SGK Bildirge Güvenliği — e-Okul Öğrenci Kontrolü Tavsiye Edilir",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(146, 64, 14), // amber-900
                Location = new Point(14, 10),
                AutoSize = true
            };

            var lblAuditAlertDesc = new Label
            {
                Text = "Bildirgeyi kesinleştirmeden önce; nakil giden veya yeni başlayan öğrencilerin tespit edilmesi ve prim cezası riskini önlemek için kontrol yapılması önerilir.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(180, 83, 9), // amber-800
                Location = new Point(14, 34),
                Size = new Size(620, 28)
            };

            var btnAuditQuick = new Button
            {
                Text = "🔍 Kontrolü Başlat",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(217, 119, 6), // amber-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(160, 36),
                Location = new Point(pnlAuditAlert.Width - 176, 18),
                Cursor = Cursors.Hand
            };
            btnAuditQuick.FlatAppearance.BorderSize = 0;
            btnAuditQuick.Click += (s, e) =>
            {
                using var dlg = new Dialogs.StudentAuditDialog(_studentRepo, _classRepo, null, _settingRepo);
                dlg.ShowDialog(this.FindForm());
                LoadDataAsync();
            };

            pnlAuditAlert.Controls.AddRange([lblAuditIconTitle, lblAuditAlertDesc, btnAuditQuick]);

            var pnlTips = new Panel
            {
                Location = new Point(24, 318),
                Size = new Size(830, 95),
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(16, 12, 16, 12)
            };
            var lblTips = new Label
            {
                Text = "💡 Önemli Bilgiler:\n" +
                       "• Belge Türü Kodları: 49 (Örgün - Bakmakla Yükümlü), 50 (Örgün - Yükümlü Olmayan), 07 (MESEM - Yükümlü), 42 (MESEM - Yükümlü Olmayan)\n" +
                       "• Bildirge Türü: ASIL bildirge için 'ASIL', ek dönemler için 'EK' veya 'EK-9' seçilmelidir.\n" +
                       "• Hazırlanan XML dosyası e-Bildirge V2 standartlarına uygun olup doğrudan SGK işveren portalına yüklenebilir.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(51, 65, 85),
                Dock = DockStyle.Fill
            };
            pnlTips.Controls.Add(lblTips);

            cardInfo.Controls.AddRange([lblCardTitle, lblInstructions, flpStats, _lblEBildirgeCardDesc, pnlAuditAlert, pnlTips]);
            pnlEBildirgeCenter.Controls.Add(cardInfo);

            void CenterCard()
            {
                int x = Math.Max(24, (pnlEBildirgeCenter.ClientSize.Width - cardInfo.Width) / 2);
                cardInfo.Location = new Point(x, 24);
            }
            pnlEBildirgeCenter.Resize += (s, e) => CenterCard();
            CenterCard();

            Controls.Add(pnlEBildirgeCenter);
        }
        else
        {
            pnlGridContainer.Controls.Add(_grid);
            Controls.Add(pnlGridContainer);
        }

        if (_opType == "toplu-isegiris-cikis")
        {
            var pnlBulkDates = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(239, 246, 255), // blue-50
                Padding = new Padding(24, 6, 24, 6)
            };

            var flpBulk = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _rbApplyEntry = new RadioButton
            {
                Text = "SGK İşe Giriş Tarihi Uygula:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138),
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 6, 4, 0),
                Cursor = Cursors.Hand
            };

            _dtpBulkEntry = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = SgkDateValidator.GetDefaultSgkEntryDate(DateTime.Today),
                Width = 110,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 3, 16, 0),
                Enabled = true
            };

            _rbApplyExit = new RadioButton
            {
                Text = "SGK İşten Çıkış Tarihi Uygula:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 83, 9),
                Checked = false,
                AutoSize = true,
                Margin = new Padding(0, 6, 4, 0),
                Cursor = Cursors.Hand
            };

            _dtpBulkExit = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Width = 110,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 3, 16, 0),
                Enabled = false
            };

            _rbApplyEntry.CheckedChanged += (s, e) =>
            {
                _dtpBulkEntry.Enabled = _rbApplyEntry.Checked;
            };
            _rbApplyExit.CheckedChanged += (s, e) =>
            {
                _dtpBulkExit.Enabled = _rbApplyExit.Checked;
            };

            _chkSelectAll = new CheckBox
            {
                Text = _opType == "xml-isegiris" ? "İşlem Bekleyenleri Seç / Kaldır" : "Tümünü Seç / Kaldır",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Checked = false,
                AutoSize = true,
                Margin = new Padding(0, 6, 16, 0),
                Cursor = Cursors.Hand
            };
            _chkSelectAll.CheckedChanged += (s, e) =>
            {
                bool isChecked = _chkSelectAll.Checked;
                var today = DateTime.Today;
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (_grid.Columns.Contains("colSelect") && row.Cells["colSelect"] != null)
                    {
                        if (_opType == "xml-isegiris" && isChecked)
                        {
                            if (row.Tag is Student st)
                            {
                                row.Cells["colSelect"].Value = IsActiveSgkRecord(st, today);
                            }
                            else
                            {
                                row.Cells["colSelect"].Value = false;
                            }
                        }
                        else
                        {
                            row.Cells["colSelect"].Value = isChecked;
                        }
                    }
                }
            };

            _btnPrimaryAction.Size = new Size(160, 32);
            _btnPrimaryAction.Margin = new Padding(0, 0, 6, 0);

            var btnClearEntry = new Button
            {
                Text = "🗑️ İşe Girişleri Sil",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(225, 29, 72), // rose-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(150, 32),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            btnClearEntry.FlatAppearance.BorderSize = 0;
            btnClearEntry.Click += (s, e) => _ = ClearBulkSgkDatesAsync(clearEntry: true, clearExit: false);

            var btnClearExit = new Button
            {
                Text = "🗑️ İşe Çıkışları Sil",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(190, 18, 60), // rose-700
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(150, 32),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnClearExit.FlatAppearance.BorderSize = 0;
            btnClearExit.Click += (s, e) => _ = ClearBulkSgkDatesAsync(clearEntry: false, clearExit: true);

            flpBulk.Controls.AddRange([_rbApplyEntry, _dtpBulkEntry, _rbApplyExit, _dtpBulkExit, _chkSelectAll, _btnPrimaryAction, btnClearEntry, btnClearExit]);
            pnlBulkDates.Controls.Add(flpBulk);

            void CenterBulkBar()
            {
                flpBulk.Location = new Point(
                    Math.Max(24, (pnlBulkDates.ClientSize.Width - flpBulk.Width) / 2),
                    Math.Max(0, (pnlBulkDates.ClientSize.Height - flpBulk.Height) / 2)
                );
            }
            pnlBulkDates.Resize += (s, e) => CenterBulkBar();
            flpBulk.SizeChanged += (s, e) => CenterBulkBar();
            CenterBulkBar();

            Controls.Add(pnlBulkDates);
        }
        else if (_opType == "isg-islemleri")
        {
            var pnlIsgBulk = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(239, 246, 255), // blue-50
                Padding = new Padding(24, 6, 24, 6)
            };

            var flpIsg = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var lblStatus = new Label
            {
                Text = "Sertifika Durumu:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138),
                AutoSize = true,
                Margin = new Padding(0, 6, 4, 0)
            };

            _cmbBulkIsgStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 3, 16, 0)
            };
            _cmbBulkIsgStatus.Items.AddRange(new[] { "Sertifikalı (Tamamlandı)", "Sertifikasız (Eksik)" });
            _cmbBulkIsgStatus.SelectedIndex = 0;

            var lblDate = new Label
            {
                Text = "Sertifika Tarihi:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138),
                AutoSize = true,
                Margin = new Padding(0, 6, 4, 0)
            };

            _dtpBulkIsgDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Width = 110,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 3, 16, 0)
            };

            var lblCertNo = new Label
            {
                Text = "Sertifika No:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138),
                AutoSize = true,
                Margin = new Padding(0, 6, 4, 0)
            };

            _txtBulkIsgCertNo = new TextBox
            {
                Text = _nextIsgCertNo.ToString(),
                Width = 80,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 3, 16, 0)
            };

            _chkIsgDocPrinted = new CheckBox
            {
                Text = "Belge Basıldı (Evet)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138),
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 6, 16, 0)
            };

            _btnApplyIsgBulk = new Button
            {
                Text = "⚡ Seçilenlere Sertifika Tanımla",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(210, 32),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnApplyIsgBulk.FlatAppearance.BorderSize = 0;
            _btnApplyIsgBulk.Click += (s, e) => ApplyBulkIsgCertificates();

            flpIsg.Controls.AddRange([lblStatus, _cmbBulkIsgStatus, lblDate, _dtpBulkIsgDate, lblCertNo, _txtBulkIsgCertNo, _chkIsgDocPrinted, _btnApplyIsgBulk]);
            pnlIsgBulk.Controls.Add(flpIsg);

            void CenterIsgBar()
            {
                flpIsg.Location = new Point(
                    Math.Max(24, (pnlIsgBulk.ClientSize.Width - flpIsg.Width) / 2),
                    Math.Max(0, (pnlIsgBulk.ClientSize.Height - flpIsg.Height) / 2)
                );
            }
            pnlIsgBulk.Resize += (s, e) => CenterIsgBar();
            flpIsg.SizeChanged += (s, e) => CenterIsgBar();
            CenterIsgBar();

            Controls.Add(pnlIsgBulk);
        }

        Controls.Add(pnlFilter);
        Controls.Add(pnlHeader);

        ConfigureOpTypeUI();
    }

    private void ConfigureOpTypeUI()
    {
        switch (_opType)
        {
            case "toplu-isegiris-cikis":
                _lblTitle.Text = "📅 Toplu İşe Giriş / Çıkış Tarihi Ekleme & Güncelleme";
                _lblSubtitle.Text = "Öğrencilerin SGK işe giriş ve işten ayrılış tarihlerini toplu olarak girin ve güncelleyin.";
                _btnPrimaryAction.Text = "⚡ Tarihleri Uygula";
                _btnSecondaryAction.Visible = false;
                break;
            case "xml-ebildirge":
                _lblTitle.Text = "📄 XML e-Bildirge Oluşturma ve Dışa Aktarım";
                _lblSubtitle.Text = "Öğrenci staj sigortası e-Bildirge XML dosyalarını hazırlayın ve SGK portalına aktarın.";
                _btnPrimaryAction.Text = "📥 XML Bildirge İndir";
                _btnSecondaryAction.Text = "📊 Bildirge Listesi Al";
                break;

            case "xml-isegiris":
                _lblTitle.Text = "📄 XML İşe Giriş / İşten Çıkış Bildirgeleri";
                _lblSubtitle.Text = "Stajyer öğrencilerin SGK işe giriş ve işten ayrılış bildirgelerini XML formatında dışa aktarın. (İşe giriş bekleyenler ve çıkışı son 10 güne kadar olan aktif kayıtlar en üstte listelenir)";
                _btnPrimaryAction.Text = "📥 İşe Giriş XML İndir";
                _btnSecondaryAction.Text = "📤 İşten Çıkış XML İndir";
                break;

            case "surekli-devamsizlar":
                _lblTitle.Text = "⚠️ Sürekli Devamsız Öğrenciler Takip Ekranı";
                _lblSubtitle.Text = "Stajına devam etmeyen veya devamsızlık sınırını aşan stajyer öğrencilerin takip kayıtları.";
                _btnPrimaryAction.Text = "➕ Sürekli Devamsız Ekle";
                _btnSecondaryAction.Visible = false;
                break;

            case "yaz-staji":
                _lblTitle.Text = "☀️ Yaz Stajı Öğrencileri Yönetimi";
                _lblSubtitle.Text = "Yaz döneminde kurum ve işletmelerde staj yapan öğrencilerin kayıt ve sigorta durumları.";
                _btnPrimaryAction.Text = "➕ Yaz Stajına Ekle";
                _btnSecondaryAction.Text = "📊 Yaz Stajı Listesi";
                break;

            case "tatil-kayitlari":
                _lblTitle.Text = "🏖️ Okul ve Resmi Tatil Kayıtları";
                _lblSubtitle.Text = "Resmi tatil günleri, ara tatil, sömestr ve yaz tatili tarihlerinin yönetimi.";
                _btnPrimaryAction.Text = "➕ Yeni Tatil Ekle";
                _btnSecondaryAction.Text = "📅 Tatil Takvimi Al";
                break;

            case "isg-islemleri":
                _lblTitle.Text = "🦺 İSG Öğrenci Takip Ekranı";
                _lblSubtitle.Text = "Stajyer öğrencilerin İSG eğitim durumları, sertifikaları ve sağlık kontrollerinin genel takibi.";
                _btnPrimaryAction.Text = "📊 Liste Yazdır";
                _btnSecondaryAction.Visible = false;
                break;

            case "isg-egitimler":
                _lblTitle.Text = "🎓 İSG Eğitim ve Sertifika Yönetimi";
                _lblSubtitle.Text = "Öğrencilere verilen iş sağlığı ve güvenliği eğitimleri ile sertifika geçerlilik tarihleri.";
                _btnPrimaryAction.Text = "➕ Yeni Eğitim Tanımla";
                _btnSecondaryAction.Text = "📄 Sertifika Bas";
                break;

            case "isg-evraklar":
                _lblTitle.Text = "📄 İSG Taahhütname ve Risk Belgeleri";
                _lblSubtitle.Text = "İşletme ve okul bazlı İSG sözleşmeleri, taahhütnameler ve veli izin belgeleri.";
                _btnPrimaryAction.Text = "➕ Evrak Yükle/Ekle";
                _btnSecondaryAction.Text = "📑 Toplu Yazdır";
                break;

            default:
                _lblTitle.Text = "🛡️ Sigorta İşlemleri";
                _lblSubtitle.Text = "SGK bildirimleri ve staj sigorta yönetimi.";
                _btnPrimaryAction.Text = "İşlem Yap";
                _btnSecondaryAction.Text = "Yazdır";
                break;
        }
    }

    public async void LoadDataAsync()
    {
        try
        {
            _allStudents = (await _studentRepo.GetAllAsync()).Where(s => s.IsActive).ToList();

            // İSG ekranlarında veritabanındaki en büyük sertifika numarasını bulup bir fazlasını hazırla
            if (_opType.StartsWith("isg-"))
            {
                int maxCertNo = 1000;
                foreach (var s in _allStudents)
                {
                    if (!string.IsNullOrWhiteSpace(s.IsgCertificateNo))
                    {
                        var digits = new string(s.IsgCertificateNo.Trim().Where(char.IsDigit).ToArray());
                        if (int.TryParse(digits, out int parsedNo) && parsedNo > maxCertNo)
                        {
                            maxCertNo = parsedNo;
                        }
                    }
                }

                _nextIsgCertNo = maxCertNo + 1;
                if (_txtBulkIsgCertNo != null)
                {
                    _txtBulkIsgCertNo.Text = _nextIsgCertNo.ToString();
                }
            }

            if (_opType != "xml-ebildirge" && _cmbClassFilter != null)
            {
                string? selectedClass = _cmbClassFilter.SelectedItem?.ToString();
                var classes = await _classRepo.GetAllAsync();
                _cmbClassFilter.BeginUpdate();
                _cmbClassFilter.Items.Clear();
                _cmbClassFilter.Items.Add("Tüm Sınıflar");
                foreach (var c in classes)
                {
                    _cmbClassFilter.Items.Add(c.Name);
                }
                if (!string.IsNullOrEmpty(selectedClass) && _cmbClassFilter.Items.Contains(selectedClass))
                {
                    _cmbClassFilter.SelectedItem = selectedClass;
                }
                else
                {
                    _cmbClassFilter.SelectedIndex = 0;
                }
                _cmbClassFilter.EndUpdate();
            }

            FilterData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Veriler yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// XML İşe Giriş / Çıkış ekranında bir kaydın aktif (işlem bekleyen) olup olmadığını belirler.
    /// İşe giriş tarihi bugün veya gelecekte olan (ve henüz çıkışı verilmemiş) olanlar ile,
    /// SGK çıkış tarihi geriye dönük 10 gün öncesine kadar (yasal bildirim süresi içinde) olanlar aktiftir.
    /// </summary>
    private static bool IsActiveSgkRecord(Student s, DateTime today)
    {
        var tenDaysAgo = today.AddDays(-10);
        bool entryActive = s.SgkEntryDate.HasValue && !s.SgkExitDate.HasValue && s.SgkEntryDate.Value.Date >= today;
        bool exitActive = s.SgkExitDate.HasValue && s.SgkExitDate.Value.Date >= tenDaysAgo;
        return entryActive || exitActive;
    }

    private static bool IsMesemStudent(Student s)
    {
        if (!string.IsNullOrWhiteSpace(s.EducationType))
        {
            if (s.EducationType.Contains("MESEM", StringComparison.OrdinalIgnoreCase) ||
                s.EducationType.Contains("Çırak", StringComparison.OrdinalIgnoreCase) ||
                s.EducationType.Contains("Cirak", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(s.Classroom))
        {
            if (s.Classroom.Contains("MESEM", StringComparison.OrdinalIgnoreCase) ||
                s.Classroom.Contains("Çırak", StringComparison.OrdinalIgnoreCase) ||
                s.Classroom.Contains("Cirak", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(s.Grade) &&
            (s.Grade.Contains("MESEM", StringComparison.OrdinalIgnoreCase) ||
             s.Grade.Contains("Çırak", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(s.FieldName) &&
            (s.FieldName.Contains("MESEM", StringComparison.OrdinalIgnoreCase) ||
             s.FieldName.Contains("Çırak", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(s.SgkPrimKodu))
        {
            var p = s.SgkPrimKodu.Trim();
            if (p.StartsWith("7") || p.StartsWith("07") || p.StartsWith("42"))
                return true;
        }

        return false;
    }

    private static string ExtractGradeLevel(Student s)
    {
        if (IsMesemStudent(s)) return "MESEM";
        if (!string.IsNullOrWhiteSpace(s.Grade))
        {
            var g = s.Grade.Trim();
            if (g == "9" || g.StartsWith("9.")) return "9. Sınıf";
            if (g == "10" || g.StartsWith("10.")) return "10. Sınıf";
            if (g == "11" || g.StartsWith("11.")) return "11. Sınıf";
            if (g == "12" || g.StartsWith("12.")) return "12. Sınıf";
        }
        var cls = (s.Classroom ?? "").Trim();
        if (cls.StartsWith("9") || cls.Contains(" 9") || cls.Contains("9-") || cls.Contains("9/")) return "9. Sınıf";
        if (cls.StartsWith("10") || cls.Contains(" 10") || cls.Contains("10-") || cls.Contains("10/")) return "10. Sınıf";
        if (cls.StartsWith("11") || cls.Contains(" 11") || cls.Contains("11-") || cls.Contains("11/")) return "11. Sınıf";
        if (cls.StartsWith("12") || cls.Contains(" 12") || cls.Contains("12-") || cls.Contains("12/")) return "12. Sınıf";

        return "Diğer";
    }

    private static bool MatchesSgkBelgeCode(Student s, string selectedSgkCode)
    {
        if (string.IsNullOrWhiteSpace(selectedSgkCode) || selectedSgkCode == "Seçiniz.")
            return true;

        var prim = (s.SgkPrimKodu ?? "").Trim();
        bool hasPrim = !string.IsNullOrWhiteSpace(prim);

        if (selectedSgkCode.StartsWith("49"))
        {
            if (hasPrim)
            {
                return prim.StartsWith("49") || prim.Contains("49 Nolu");
            }
            return !IsMesemStudent(s) && s.HasFamilyHealthInsurance;
        }

        if (selectedSgkCode.StartsWith("50"))
        {
            if (hasPrim)
            {
                return prim.StartsWith("50") || prim.Contains("50 Nolu");
            }
            return !IsMesemStudent(s) && !s.HasFamilyHealthInsurance;
        }

        if (selectedSgkCode.StartsWith("07") || selectedSgkCode.StartsWith("7"))
        {
            if (hasPrim)
            {
                return prim == "7" || prim == "07" || prim.StartsWith("7-") || prim.StartsWith("7 ") || prim.StartsWith("07") || prim.Contains("7 Nolu") || prim.Contains("07 Nolu");
            }
            return IsMesemStudent(s) && s.HasFamilyHealthInsurance;
        }

        if (selectedSgkCode.StartsWith("42"))
        {
            if (hasPrim)
            {
                return prim.StartsWith("42") || prim.Contains("42 Nolu");
            }
            return IsMesemStudent(s) && !s.HasFamilyHealthInsurance;
        }

        return true;
    }

    private static bool IsBakmaklaYukumlu(Student s)
    {
        var prim = (s.SgkPrimKodu ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(prim))
        {
            if (prim.StartsWith("49") || prim.Contains("49 Nolu") ||
                prim == "7" || prim == "07" || prim.StartsWith("7-") || prim.StartsWith("7 ") || prim.StartsWith("07") || prim.Contains("7 Nolu") || prim.Contains("07 Nolu"))
            {
                return true;
            }
            if (prim.StartsWith("50") || prim.Contains("50 Nolu") ||
                prim.StartsWith("42") || prim.Contains("42 Nolu"))
            {
                return false;
            }
        }
        return s.HasFamilyHealthInsurance;
    }

    private void UpdateSgkBelgeTypeDropdown()
    {
        if (_cmbSgkBelgeType == null || _cmbEBildirgeFilter == null) return;

        var selectedGroup = _cmbEBildirgeFilter.SelectedItem?.ToString() ?? "Seçiniz.";
        var currentSelectedCode = _cmbSgkBelgeType.SelectedItem?.ToString();

        _cmbSgkBelgeType.BeginUpdate();
        _cmbSgkBelgeType.Items.Clear();
        _cmbSgkBelgeType.Items.Add("Seçiniz.");

        if (selectedGroup == "Tüm Öğrenciler (MESEM Hariç)")
        {
            _cmbSgkBelgeType.Items.Add("49-Bakmakla Yükümlü Olunan");
            _cmbSgkBelgeType.Items.Add("50-Bakmakla Yükümlü Olunmayan");
        }
        else if (selectedGroup == "Sadece MESEM Öğrencileri")
        {
            _cmbSgkBelgeType.Items.Add("07-Bakmakla Yükümlü Olunan(MESEM)");
            _cmbSgkBelgeType.Items.Add("42-Bakmakla Yükümlü Olunmayan(MESEM)");
        }
        else
        {
            _cmbSgkBelgeType.Items.Add("49-Bakmakla Yükümlü Olunan");
            _cmbSgkBelgeType.Items.Add("50-Bakmakla Yükümlü Olunmayan");
            _cmbSgkBelgeType.Items.Add("07-Bakmakla Yükümlü Olunan(MESEM)");
            _cmbSgkBelgeType.Items.Add("42-Bakmakla Yükümlü Olunmayan(MESEM)");
        }

        if (!string.IsNullOrEmpty(currentSelectedCode) && _cmbSgkBelgeType.Items.Contains(currentSelectedCode))
        {
            _cmbSgkBelgeType.SelectedItem = currentSelectedCode;
        }
        else
        {
            _cmbSgkBelgeType.SelectedIndex = 0;
        }

        _cmbSgkBelgeType.EndUpdate();
    }

    private void FilterData()
    {
        var filtered = _allStudents.AsEnumerable();

        if (_opType == "surekli-devamsizlar")
        {
            filtered = filtered.Where(s => s.IsSurekliDevamsiz);

            if (_cmbSgkExitStatusFilter != null && _cmbSgkExitStatusFilter.SelectedIndex > 0)
            {
                if (_cmbSgkExitStatusFilter.SelectedIndex == 1)
                {
                    // ⏳ SGK Çıkışı Bekleyenler (Tarih Boş)
                    filtered = filtered.Where(s => !s.SgkExitDate.HasValue);
                }
                else if (_cmbSgkExitStatusFilter.SelectedIndex == 2)
                {
                    // ✔️ SGK Çıkışı Yapılmış Olanlar
                    filtered = filtered.Where(s => s.SgkExitDate.HasValue);
                }
            }
        }

        if (_opType == "xml-ebildirge")
        {
            var selectedGroup = _cmbEBildirgeFilter?.SelectedItem?.ToString() ?? "Seçiniz.";
            var selectedSgkCode = _cmbSgkBelgeType?.SelectedItem?.ToString() ?? "Seçiniz.";
            var selectedDocType = _cmbDocumentType?.SelectedItem?.ToString() ?? "Seçiniz.";

            // 1. Öğrenci grubu filtresi (MESEM / MESEM Hariç)
            if (selectedGroup == "Tüm Öğrenciler (MESEM Hariç)")
            {
                filtered = filtered.Where(s => !IsMesemStudent(s));
            }
            else if (selectedGroup == "Sadece MESEM Öğrencileri")
            {
                filtered = filtered.Where(s => IsMesemStudent(s));
            }

            // 2. SGK Belge Türü Kodu filtresi (Öğrencinin SgkPrimKodu alanına göre)
            if (selectedSgkCode != "Seçiniz.")
            {
                filtered = filtered.Where(s => MatchesSgkBelgeCode(s, selectedSgkCode));
            }
        }
        else
        {
            var selectedClass = _cmbClassFilter?.SelectedItem?.ToString() ?? "Tüm Sınıflar";
            var searchText = (_txtSearch?.Text ?? string.Empty).Trim();

            if (selectedClass != "Tüm Sınıflar")
            {
                filtered = filtered.Where(s => string.Equals(s.Classroom, selectedClass, StringComparison.OrdinalIgnoreCase));
            }

            if (_cmbEntryExitFilter != null)
            {
                var dateFilter = _cmbEntryExitFilter.SelectedItem?.ToString() ?? "Tüm Öğrenciler";
                if (dateFilter == "İşe Giriş Tarihi Var" || dateFilter == "Sadece İşe Giriş Tarihi Var")
                {
                    filtered = filtered.Where(s => s.SgkEntryDate.HasValue);
                }
                else if (dateFilter == "İşe Giriş Tarihi Yok (Eksik)")
                {
                    filtered = filtered.Where(s => !s.SgkEntryDate.HasValue);
                }
                else if (dateFilter == "İşten Çıkış Tarihi Var" || dateFilter == "Sadece İşten Çıkış Tarihi Var")
                {
                    filtered = filtered.Where(s => s.SgkExitDate.HasValue);
                }
                else if (dateFilter == "İşten Çıkış Tarihi Yok (Eksik)")
                {
                    filtered = filtered.Where(s => !s.SgkExitDate.HasValue);
                }
                else if (dateFilter == "Giriş veya Çıkış Tarihi Var")
                {
                    filtered = filtered.Where(s => s.SgkEntryDate.HasValue || s.SgkExitDate.HasValue);
                }
            }

            if (_cmbGradeLevelFilter != null)
            {
                var gradeSel = _cmbGradeLevelFilter.SelectedItem?.ToString() ?? "Tüm Seviyeler";
                if (gradeSel == "MESEM")
                {
                    filtered = filtered.Where(IsMesemStudent);
                }
                else if (gradeSel != "Tüm Seviyeler")
                {
                    filtered = filtered.Where(s => string.Equals(ExtractGradeLevel(s), gradeSel, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (_cmbEduTypeFilter != null)
            {
                var eduSel = _cmbEduTypeFilter.SelectedItem?.ToString() ?? "Tüm Öğretim Türleri";
                if (eduSel == "Örgün Eğitim")
                {
                    filtered = filtered.Where(s => !IsMesemStudent(s));
                }
                else if (eduSel == "MESEM")
                {
                    filtered = filtered.Where(IsMesemStudent);
                }
            }

            if (_cmbIsgCertFilter != null && _opType.StartsWith("isg-"))
            {
                var certFilter = _cmbIsgCertFilter.SelectedItem?.ToString() ?? "Tüm Durumlar";
                if (certFilter == "Sertifikalı (Tamamlandı)")
                {
                    filtered = filtered.Where(s => string.Equals(s.IsgStatus, "Sertifikalı (Tamamlandı)", StringComparison.OrdinalIgnoreCase) || s.IsgCertificateDate.HasValue || !string.IsNullOrWhiteSpace(s.IsgCertificateNo));
                }
                else if (certFilter == "Sertifikasız (Eksik)")
                {
                    filtered = filtered.Where(s => !string.Equals(s.IsgStatus, "Sertifikalı (Tamamlandı)", StringComparison.OrdinalIgnoreCase) && !s.IsgCertificateDate.HasValue && string.IsNullOrWhiteSpace(s.IsgCertificateNo));
                }
                else if (certFilter == "Belge Basılanlar (Evet)")
                {
                    filtered = filtered.Where(s => s.IsIsgDocumentPrinted && (!string.IsNullOrWhiteSpace(s.IsgSecurityNo) || s.IsgCertificateDate.HasValue || string.Equals(s.IsgStatus, "Sertifikalı (Tamamlandı)", StringComparison.OrdinalIgnoreCase)));
                }
                else if (certFilter == "Belge Basılmayanlar (Hayır)")
                {
                    filtered = filtered.Where(s => !s.IsIsgDocumentPrinted && (!string.IsNullOrWhiteSpace(s.IsgSecurityNo) || s.IsgCertificateDate.HasValue || string.Equals(s.IsgStatus, "Sertifikalı (Tamamlandı)", StringComparison.OrdinalIgnoreCase)));
                }
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(s =>
                    s.FullName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (s.StudentNumber ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }
        }

        var list = filtered.ToList();

        if (_opType == "xml-isegiris")
        {
            var today = DateTime.Today;
            var tenDaysAgo = today.AddDays(-10);

            list = list
                .OrderByDescending(s =>
                {
                    // 1: Aktif olanlar (Giriş >= bugün VEYA Çıkış >= 10 gün öncesi) en üste (true > false)
                    return IsActiveSgkRecord(s, today);
                })
                .ThenBy(s =>
                {
                    // Aktif olanlar kendi içinde en yakın tarihten en uzağa
                    if (IsActiveSgkRecord(s, today))
                    {
                        DateTime minActive = DateTime.MaxValue;
                        if (s.SgkEntryDate.HasValue && !s.SgkExitDate.HasValue && s.SgkEntryDate.Value.Date >= today)
                            minActive = s.SgkEntryDate.Value.Date;
                        if (s.SgkExitDate.HasValue && s.SgkExitDate.Value.Date >= tenDaysAgo && s.SgkExitDate.Value.Date < minActive)
                            minActive = s.SgkExitDate.Value.Date;
                        return minActive;
                    }
                    return DateTime.MaxValue;
                })
                .ThenByDescending(s =>
                {
                    // Pasif geçmiş olanlar kendi içinde en yeni geçmişten eskiye doğru
                    DateTime maxPast = DateTime.MinValue;
                    if (s.SgkEntryDate.HasValue && s.SgkEntryDate.Value.Date < today)
                        maxPast = s.SgkEntryDate.Value.Date;
                    if (s.SgkExitDate.HasValue && s.SgkExitDate.Value.Date < tenDaysAgo && s.SgkExitDate.Value.Date > maxPast)
                        maxPast = s.SgkExitDate.Value.Date;
                    return maxPast;
                })
                .ThenBy(s => s.FullName)
                .ToList();
        }

        if (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris")
        {
            int total = list.Count;
            int entryExist = list.Count(s => s.SgkEntryDate.HasValue);
            int entryMissing = total - entryExist;
            int exitExist = list.Count(s => s.SgkExitDate.HasValue);
            int exitMissing = total - exitExist;

            if (_opType == "xml-isegiris")
            {
                var today = DateTime.Today;
                int activeCount = list.Count(s => IsActiveSgkRecord(s, today));
                int passiveCount = total - activeCount;

                _lblSummary.Text = $"👥 Toplam: {total}  |  🚀 İşlem Bekleyen / Aktif (Son 10 Gün ve Sonrası): {activeCount}  |  📁 Süresi Dolan Geçmiş: {passiveCount}  |  🟢 Giriş: {entryExist}  |  🟡 Çıkış: {exitExist}";
            }
            else
            {
                _lblSummary.Text = $"👥 Toplam Öğrenci: {total}  |  🟢 Giriş Var: {entryExist}  |  🔴 Giriş Yok (Eksik): {entryMissing}  |  🟡 Çıkış Var: {exitExist}  |  ⚪ Çıkış Yok: {exitMissing}";
            }
        }
        else if (_opType == "xml-ebildirge")
        {
            int total = list.Count;
            int orgunCount = list.Count(s => !IsMesemStudent(s));
            int mesemCount = list.Count(IsMesemStudent);
            int bakmaklaYukumlu = list.Count(IsBakmaklaYukumlu);
            int bakmaklaYukumsuz = total - bakmaklaYukumlu;

            _lblSummary.Text = $"👥 Bildirge Kapsamındaki Öğrenci: {total}  |  🏫 Örgün: {orgunCount}  |  🏢 MESEM: {mesemCount}  |  🛡️ Bakmakla Yükümlü: {bakmaklaYukumlu}  |  ⚠️ Yükümlü Olmayan: {bakmaklaYukumsuz}";

            if (_lblEBildirgeCardTotal != null) _lblEBildirgeCardTotal.Text = total.ToString();
            if (_lblEBildirgeCardOrgun != null) _lblEBildirgeCardOrgun.Text = orgunCount.ToString();
            if (_lblEBildirgeCardMesem != null) _lblEBildirgeCardMesem.Text = mesemCount.ToString();
            if (_lblEBildirgeCardYukumlu != null) _lblEBildirgeCardYukumlu.Text = bakmaklaYukumlu.ToString();
            if (_lblEBildirgeCardYukumsuz != null) _lblEBildirgeCardYukumsuz.Text = bakmaklaYukumsuz.ToString();

            if (_lblEBildirgeCardDesc != null)
            {
                var grp = _cmbEBildirgeFilter?.SelectedItem?.ToString() ?? "Seçiniz.";
                var code = _cmbSgkBelgeType?.SelectedItem?.ToString() ?? "Seçiniz.";
                var doc = _cmbDocumentType?.SelectedItem?.ToString() ?? "ASIL";
                var yr = _cmbYear?.SelectedItem?.ToString() ?? DateTime.Now.Year.ToString();
                var mo = _cmbMonth?.SelectedItem?.ToString() ?? "";

                _lblEBildirgeCardDesc.Text = $"📍 Seçili Kriterler: {yr} {mo} Dönemi | Tür: {doc} | SGK Kodu: {code} | Kapsam: {grp} ({total} öğrenci hazır)";
            }
        }
        else
        {
            _lblSummary.Text = $"Toplam {list.Count} kayıt";
        }

        if (_lblSummary.Parent != null)
        {
            if (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris" || _opType == "xml-ebildirge")
            {
                _lblSummary.Location = new Point(Math.Max(12, (_lblSummary.Parent.ClientSize.Width - _lblSummary.Width) / 2), 48);
            }
            else
            {
                _lblSummary.Location = new Point(Math.Max(24, _lblSummary.Parent.ClientSize.Width - _lblSummary.Width - 24), Math.Max(0, (_lblSummary.Parent.ClientSize.Height - _lblSummary.Height) / 2));
            }
        }

        // Bind Grid Columns based on OpType
        _grid.Columns.Clear();

        if (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris" || _opType.StartsWith("isg-") || _opType == "surekli-devamsizlar")
        {
            _grid.ReadOnly = false;
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                HeaderText = "",
                Width = 38,
                ReadOnly = false
            };
            _grid.Columns.Add(colCheck);
        }

        _grid.Columns.Add("RowNo", "Sıra No");
        _grid.Columns["RowNo"].AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
        _grid.Columns["RowNo"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.Columns.Add("StudentNumber", "Okul No");
        _grid.Columns.Add("FullName", "Öğrenci Adı Soyadı");
        _grid.Columns.Add("Classroom", "Sınıf");

        if (!_opType.StartsWith("isg-"))
        {
            _grid.Columns.Add("EducationType", "Öğretim Türü");
            _grid.Columns.Add("EnterpriseName", "Staj İşletmesi");
            _grid.Columns.Add("SgkEntryDate", "SGK Giriş Tarihi");
        }

        if (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris")
        {
            _grid.Columns.Add("SgkExitDate", "SGK Çıkış Tarihi");
        }
        else if (_opType == "xml-ebildirge")
        {
            _grid.Columns.Add("DocType", "Bildirge Türü");
            _grid.Columns.Add("SgkBelgeKodu", "SGK Belge Kodu");
        }
        else if (_opType == "surekli-devamsizlar")
        {
            _grid.Columns.Add("SgkExitDate", "SGK Çıkış Tarihi");
            _grid.Columns.Add("StatusText", "Devamsızlık Durumu");

            var btnExit = new DataGridViewButtonColumn
            {
                Name = "ColSetExitDate",
                HeaderText = "SGK Çıkış",
                Text = "📅 Çıkış Tarihi",
                UseColumnTextForButtonValue = true,
                Width = 115
            };
            _grid.Columns.Add(btnExit);

            var btnArchive = new DataGridViewButtonColumn
            {
                Name = "ColArchive",
                HeaderText = "Nakil / Ayrılış",
                Text = "📁 Arşive Taşı",
                UseColumnTextForButtonValue = true,
                Width = 110
            };
            _grid.Columns.Add(btnArchive);

            var btnRemove = new DataGridViewButtonColumn
            {
                Name = "ColRemove",
                HeaderText = "Staja Dönüş",
                Text = "↩️ Devamsızlıktan Sil",
                UseColumnTextForButtonValue = true,
                Width = 140
            };
            _grid.Columns.Add(btnRemove);
        }
        else if (_opType == "yaz-staji")
        {
            _grid.Columns.Add("InternshipDays", "Staj Günleri");
            _grid.Columns.Add("FeeCategory", "Ücret Kategori");
        }
        else if (_opType == "isg-islemleri" || _opType == "isg-egitimler")
        {
            _grid.Columns.Add("IsgStatus", "İSG Sertifika Durumu");
            _grid.Columns.Add("IsgDate", "Sertifika Tarihi");
            _grid.Columns.Add("IsgCertNo", "Sertifika No");
            _grid.Columns.Add("IsgSecurityNo", "Güvenlik Belge No");
            _grid.Columns.Add("IsgDocPrinted", "Belge Basıldı");

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "ColPrintIsgDoc",
                HeaderText = "İşlem",
                Text = "📄 Belge Bas",
                UseColumnTextForButtonValue = true,
                Width = 110
            };
            _grid.Columns.Add(btnCol);
        }
        else if (_opType == "isg-evraklar")
        {
            _grid.Columns.Add("DocStatus", "Taahhüt / İzin Belgesi");
            _grid.Columns.Add("DocDate", "Evrak Tarihi");
        }
        else
        {
            _grid.Columns.Add("SgkPrimKodu", "SGK Prim Kodu");
        }

        _grid.Rows.Clear();
        int rowNo = 1;
        foreach (var s in list)
        {
            string sgkEntry = s.SgkEntryDate.HasValue ? s.SgkEntryDate.Value.ToString("dd.MM.yyyy") : "-";
            string sgkExit = s.SgkExitDate.HasValue ? s.SgkExitDate.Value.ToString("dd.MM.yyyy") : "-";
            string eduType = string.IsNullOrWhiteSpace(s.EducationType) ? (IsMesemStudent(s) ? "MESEM" : "Örgün Eğitim") : s.EducationType;

            if (_opType == "toplu-isegiris-cikis" || _opType == "xml-isegiris")
            {
                var today = DateTime.Today;
                var tenDaysAgo = today.AddDays(-10);
                bool isActive = IsActiveSgkRecord(s, today);

                bool initialSelect;
                if (_opType == "xml-isegiris")
                {
                    // xml-isegiris ekranında varsayılan olarak işlem bekleyen (aktif) kayıtlar seçili gelir
                    initialSelect = isActive;
                }
                else
                {
                    initialSelect = _chkSelectAll?.Checked ?? false;
                }

                int rowIndex = _grid.Rows.Add(initialSelect, rowNo++, s.StudentNumber, s.FullName, s.Classroom, eduType, s.EnterpriseName, sgkEntry, sgkExit);
                var row = _grid.Rows[rowIndex];
                row.Tag = s;

                if (_opType == "xml-isegiris")
                {
                    if (isActive)
                    {
                        // Aktif Kayıtlar (Giriş >= bugün veya Çıkış >= son 10 gün): Net ve canlı satır stili
                        row.DefaultCellStyle.BackColor = Color.White;
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42); // slate-900

                        if (s.SgkEntryDate.HasValue && !s.SgkExitDate.HasValue && s.SgkEntryDate.Value.Date >= today)
                        {
                            row.Cells["SgkEntryDate"].Style.ForeColor = Color.FromArgb(5, 150, 105); // emerald-600
                            row.Cells["SgkEntryDate"].Style.Font = new Font(_grid.Font, FontStyle.Bold);
                        }
                        if (s.SgkExitDate.HasValue && s.SgkExitDate.Value.Date >= tenDaysAgo)
                        {
                            row.Cells["SgkExitDate"].Style.ForeColor = Color.FromArgb(220, 38, 38); // red-600
                            row.Cells["SgkExitDate"].Style.Font = new Font(_grid.Font, FontStyle.Bold);
                        }
                    }
                    else
                    {
                        // Süresi Dolan Geçmiş Kayıtlar: Pasif satır stili (soluk açık gri zemin ve açık gri yazı)
                        row.DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); // slate-50
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184); // slate-400
                        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(226, 232, 240);
                        row.DefaultCellStyle.SelectionForeColor = Color.FromArgb(100, 116, 139);

                        // Checkbox'ı kilitle ve seçilemez yap
                        if (row.Cells["colSelect"] != null)
                        {
                            row.Cells["colSelect"].Value = false;
                            row.Cells["colSelect"].ReadOnly = true;
                            row.Cells["colSelect"].ToolTipText = "Yasal bildirim süresi (10 gün) geçmiş kayıt olduğu için seçilemez.";
                        }
                    }
                }
            }
            else if (_opType == "xml-ebildirge")
            {
                string docType = _cmbDocumentType?.SelectedItem?.ToString() == "EK" ? "EK" : "ASIL";
                string belgeCode = _cmbSgkBelgeType?.SelectedItem?.ToString() ?? "49-Bakmakla Yükümlü Olunan";
                _grid.Rows.Add(rowNo++, s.StudentNumber, s.FullName, s.Classroom, eduType, s.EnterpriseName, sgkEntry, docType, belgeCode);
            }
            else if (_opType == "surekli-devamsizlar")
            {
                string exitDateStr = s.SgkExitDate.HasValue
                    ? s.SgkExitDate.Value.ToString("dd.MM.yyyy")
                    : "⏳ Çıkış Bekliyor";
                string statusDesc = s.SgkExitDate.HasValue
                    ? "Sürekli Devamsız (Çıkış Yapıldı)"
                    : "Sürekli Devamsız (Çıkış Bekliyor)";

                int rowIndex = _grid.Rows.Add(false, rowNo++, s.StudentNumber, s.FullName, s.Classroom, eduType, s.EnterpriseName, sgkEntry, exitDateStr, statusDesc);
                var row = _grid.Rows[rowIndex];
                row.Tag = s;

                if (!s.SgkExitDate.HasValue)
                {
                    row.Cells["SgkExitDate"].Style.ForeColor = Color.FromArgb(217, 119, 6); // amber-600
                    row.Cells["SgkExitDate"].Style.Font = new Font(_grid.Font, FontStyle.Bold);
                }
                else
                {
                    row.Cells["SgkExitDate"].Style.ForeColor = Color.FromArgb(5, 150, 105); // emerald-600
                    row.Cells["SgkExitDate"].Style.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
            else if (_opType == "yaz-staji")
            {
                _grid.Rows.Add(rowNo++, s.StudentNumber, s.FullName, s.Classroom, eduType, s.EnterpriseName, sgkEntry, s.InternshipDays, s.FeeCategory);
            }
            else if (_opType == "isg-islemleri" || _opType == "isg-egitimler")
            {
                string isgStatus = string.IsNullOrWhiteSpace(s.IsgStatus) ? "Sertifikasız (Eksik)" : s.IsgStatus;
                string isgDate = s.IsgCertificateDate.HasValue ? s.IsgCertificateDate.Value.ToString("dd.MM.yyyy") : "-";
                string isgCertNo = string.IsNullOrWhiteSpace(s.IsgCertificateNo) ? "-" : s.IsgCertificateNo;
                string isgSecNo = string.IsNullOrWhiteSpace(s.IsgSecurityNo) ? "-" : s.IsgSecurityNo;
                string isgDocPrinted = s.IsIsgDocumentPrinted ? "Evet" : "Hayır";

                int rowIndex = _grid.Rows.Add(false, rowNo++, s.StudentNumber, s.FullName, s.Classroom, isgStatus, isgDate, isgCertNo, isgSecNo, isgDocPrinted);
                _grid.Rows[rowIndex].Tag = s;
            }
            else if (_opType == "isg-evraklar")
            {
                int rowIndex = _grid.Rows.Add(false, rowNo++, s.StudentNumber, s.FullName, s.Classroom, "Taahhütname Eksik", "-", "-", "-");
                _grid.Rows[rowIndex].Tag = s;
            }
            else
            {
                _grid.Rows.Add(rowNo++, s.StudentNumber, s.FullName, s.Classroom, eduType, s.EnterpriseName, sgkEntry, s.SgkPrimKodu ?? "3308 Çırak/Stajyer");
            }
        }
    }

    private async void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (_opType == "surekli-devamsizlar" && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ColSetExitDate")
        {
            if (_grid.Rows[e.RowIndex].Tag is Student student)
            {
                await PromptAndSetSingleStudentSgkExitDateAsync(student);
            }
        }
        else if (_opType == "surekli-devamsizlar" && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ColArchive")
        {
            if (_grid.Rows[e.RowIndex].Tag is Student student)
            {
                var confirm = MessageBox.Show(
                    $"{student.FullName} ({student.StudentNumber}) adlı öğrencinin okuldan nakil / resmi ayrılış işlemi yapıldı mı?\n\n" +
                    "• Öğrenci 'Öğrenci Arşivi'ne taşınacak ve aktif sınıf listelerinden kaldırılacaktır.\n" +
                    "• Bu işlemi velisi resmi nakil aldığında veya öğrencinin okul ile ilişiği tamamen kesildiğinde yapmanız önerilir.\n\n" +
                    "Öğrenciyi arşive taşımak istiyor musunuz?",
                    "Nakil / Arşive Taşıma Onayı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    student.IsActive = false;
                    await _studentRepo.UpdateAsync(student);
                    LoadDataAsync();
                }
            }
        }
        else if (_opType == "surekli-devamsizlar" && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ColRemove")
        {
            if (_grid.Rows[e.RowIndex].Tag is Student student)
            {
                var confirm = MessageBox.Show(
                    $"{student.FullName} adlı öğrenci sürekli devamsızlar listesinden çıkarılarak tekrar aktif staj/ders ve ücret takibine dahil edilecektir.\n\nOnaylıyor musunuz?",
                    "Staja/Derse Geri Döndür",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    student.IsSurekliDevamsiz = false;
                    await _studentRepo.UpdateAsync(student);
                    LoadDataAsync();
                }
            }
        }
        else if (e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ColPrintIsgDoc")
        {
            if (_grid.Rows[e.RowIndex].Tag is Student student)
            {
                if (!HasValidIsgCertificate(student))
                {
                    MessageBox.Show(
                        $"{student.FullName} adlı öğrencinin henüz Sertifika No veya Güvenlik Belge No kaydı bulunmamaktadır.\n\n" +
                        "Belge basabilmek için lütfen önce '⚡ Seçilenlere Sertifika Tanımla' butonunu kullanarak sertifika verilerini oluşturunuz.",
                        "Sertifika Bilgisi Eksik",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                await ProcessIsgCertificatePrintingAsync(new List<Student> { student });
            }
        }
    }

    private static bool HasValidIsgCertificate(Student s)
    {
        return !string.IsNullOrWhiteSpace(s.IsgCertificateNo) &&
               s.IsgCertificateNo != "-" &&
               !string.IsNullOrWhiteSpace(s.IsgSecurityNo) &&
               s.IsgSecurityNo != "-";
    }

    private void BtnPrimaryAction_Click(object? sender, EventArgs e)
    {
        switch (_opType)
        {
            case "toplu-isegiris-cikis":
                _ = ApplyBulkSgkDatesAsync();
                break;
            case "xml-ebildirge":
                _ = ExportXmlEBildirgeAsync();
                break;

            case "xml-isegiris":
                _ = ExportIseGirisXmlAsync(isEntry: true);
                break;
            case "surekli-devamsizlar":
                using (var dialog = new AddAbsenceStudentDialog(_studentRepo, _classRepo))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        LoadDataAsync();
                    }
                }
                break;
            case "yaz-staji":
                MessageBox.Show("Yaz stajı öğrenci kayıt penceresi açılıyor...", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            case "tatil-kayitlari":
                MessageBox.Show("Resmi tatil günleri tanım penceresi açılıyor...", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            case "isg-islemleri":
                _ = PrintIsgStudentListReportAsync();
                break;
            case "isg-egitimler":
                MessageBox.Show("İSG eğitim & sertifika tanımlama penceresi açılıyor...", "İSG Eğitim Yönetimi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            case "isg-evraklar":
                MessageBox.Show("İSG taahhütname ve risk izin belgesi yükleme penceresi açılıyor...", "İSG Evrak Yönetimi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
        }
    }

    private async void ApplyBulkIsgCertificates()
    {
        _grid.EndEdit();
        var selectedRows = new List<DataGridViewRow>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"] != null &&
                row.Cells["colSelect"].Value != null &&
                Convert.ToBoolean(row.Cells["colSelect"].Value))
            {
                selectedRows.Add(row);
            }
        }

        if (selectedRows.Count == 0)
        {
            MessageBox.Show(
                "Lütfen sertifika tanımlamak istediğiniz öğrencileri tablodan seçiniz.",
                "Öğrenci Seçilmedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        string status = _cmbBulkIsgStatus?.SelectedItem?.ToString() ?? "Sertifikalı (Tamamlandı)";
        DateTime? certDate = status == "Sertifikalı (Tamamlandı)" ? _dtpBulkIsgDate?.Value.Date : null;
        string certDateStr = certDate.HasValue ? certDate.Value.ToString("dd.MM.yyyy") : "-";

        if (!int.TryParse(_txtBulkIsgCertNo?.Text, out int startCertNo))
        {
            startCertNo = _nextIsgCertNo;
        }

        int count = 0;
        int currentNo = startCertNo;

        bool isDocPrinted = _chkIsgDocPrinted?.Checked ?? true;

        foreach (var row in selectedRows)
        {
            count++;
            string certNoStr = status == "Sertifikalı (Tamamlandı)" ? currentNo.ToString() : "-";
            string securityNoStr = status == "Sertifikalı (Tamamlandı)" ? $"ISG-SEC-{Guid.NewGuid().ToString()[..8].ToUpper()}" : "-";
            bool printedState = status == "Sertifikalı (Tamamlandı)" && isDocPrinted;

            if (row.Tag is Student student)
            {
                student.IsgStatus = status;
                student.IsgCertificateDate = certDate;
                student.IsgCertificateNo = status == "Sertifikalı (Tamamlandı)" ? certNoStr : null;
                student.IsgSecurityNo = status == "Sertifikalı (Tamamlandı)" ? securityNoStr : null;
                student.IsIsgDocumentPrinted = printedState;
                await _studentRepo.UpdateAsync(student);
            }

            if (_grid.Columns.Contains("IsgStatus")) row.Cells["IsgStatus"].Value = status;
            if (_grid.Columns.Contains("IsgDate")) row.Cells["IsgDate"].Value = certDateStr;
            if (_grid.Columns.Contains("IsgCertNo")) row.Cells["IsgCertNo"].Value = certNoStr;
            if (_grid.Columns.Contains("IsgSecurityNo")) row.Cells["IsgSecurityNo"].Value = securityNoStr;
            if (_grid.Columns.Contains("IsgDocPrinted")) row.Cells["IsgDocPrinted"].Value = printedState ? "Evet" : "Hayır";

            if (status == "Sertifikalı (Tamamlandı)")
            {
                currentNo++;
            }
        }

        _nextIsgCertNo = currentNo;
        if (_txtBulkIsgCertNo != null) _txtBulkIsgCertNo.Text = _nextIsgCertNo.ToString();

        MessageBox.Show(
            $"{count} öğrencinin İSG sertifika veritabanı kayıtları başarıyla güncellendi.\n\n" +
            $"• Sertifika Durumu: {status}\n" +
            (status == "Sertifikalı (Tamamlandı)" ? $"• Verilen Sertifika No Aralığı: {startCertNo} - {currentNo - 1}\n• Benzersiz Güvenlik Belge No'ları veritabanına kaydedildi." : ""),
            "İSG Sertifika Veritabanına Kaydedildi",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        FilterData();
    }

    private async void PrintIsgCertificatesForSelectedRows()
    {
        _grid.EndEdit();
        var selectedStudents = new List<Student>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"] != null &&
                row.Cells["colSelect"].Value != null &&
                Convert.ToBoolean(row.Cells["colSelect"].Value) &&
                row.Tag is Student student)
            {
                selectedStudents.Add(student);
            }
        }

        if (selectedStudents.Count == 0)
        {
            MessageBox.Show(
                "Lütfen İSG belgesi basılacak öğrencileri tablodan seçiniz.",
                "Öğrenci Seçilmedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var eligibleStudents = selectedStudents.Where(HasValidIsgCertificate).ToList();

        if (eligibleStudents.Count == 0)
        {
            MessageBox.Show(
                "Seçilen öğrenciler içerisinde Sertifika No ve Güvenlik Belge No tanımlı öğrenci bulunamadı.\n\n" +
                "Belge basabilmek için lütfen önce '⚡ Seçilenlere Sertifika Tanımla' butonunu kullanarak sertifika verilerini oluşturunuz.",
                "Sertifikasız Öğrenciler Seçildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (eligibleStudents.Count < selectedStudents.Count)
        {
            int skippedCount = selectedStudents.Count - eligibleStudents.Count;
            var confirm = MessageBox.Show(
                $"Seçilen {selectedStudents.Count} öğrenciden {skippedCount} tanesinin Sertifika No veya Güvenlik Belge No kaydı bulunmamaktadır.\n\n" +
                $"Sadece sertifikası tanımlı olan {eligibleStudents.Count} öğrenci için belge basılacaktır. Devam etmek istiyor musunuz?",
                "Sertifika Eksik Öğrenciler Atlanacak",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (confirm != DialogResult.Yes) return;
        }

        await ProcessIsgCertificatePrintingAsync(eligibleStudents);
    }

    private async Task ProcessIsgCertificatePrintingAsync(List<Student> studentsToPrint)
    {
        if (studentsToPrint == null || studentsToPrint.Count == 0) return;

        using var dlg = new IsgPrintOptionDialog(studentsToPrint.Count);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Choice == IsgPrintChoice.Cancel)
        {
            return;
        }

        var settings = await _settingRepo.GetAppSettingsAsync();
        using var pd = IsgCertificatePrintHelper.CreatePrintDocument(studentsToPrint, settings);

        bool printedSuccess = false;

        if (dlg.Choice == IsgPrintChoice.Preview)
        {
            PrintPreviewHelper.ShowPreview(pd, "İSG Katılım Belgesi Baskı Önizleme", this);
            printedSuccess = true;
        }
        else if (dlg.Choice == IsgPrintChoice.DirectPrint)
        {
            using var printDlg = new PrintDialog
            {
                Document = pd,
                UseEXDialog = true,
                AllowSomePages = true
            };

            if (printDlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    pd.Print();
                    printedSuccess = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Yazdırma işlemi sırasında hata oluştu: {ex.Message}", "Yazdırma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        if (printedSuccess)
        {
            foreach (var student in studentsToPrint)
            {
                student.IsIsgDocumentPrinted = true;
                await _studentRepo.UpdateAsync(student);
            }

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is Student s && studentsToPrint.Any(sp => sp.Id == s.Id))
                {
                    if (_grid.Columns.Contains("IsgDocPrinted"))
                    {
                        row.Cells["IsgDocPrinted"].Value = "Evet";
                    }
                }
            }

            MessageBox.Show(
                $"{studentsToPrint.Count} öğrenci için İSG Katılım Belgesi basıldı/önizlendi ve veritabanında 'Belge Basıldı = Evet' olarak güncellendi.",
                "İşlem Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private async Task PrintIsgStudentListReportAsync()
    {
        var filteredStudents = new List<Student>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is Student student)
            {
                filteredStudents.Add(student);
            }
        }

        if (filteredStudents.Count == 0)
        {
            MessageBox.Show(
                "Yazdırılacak öğrenci bulunamadı. Lütfen filtrelerinizi kontrol ediniz.",
                "Öğrenci Bulunamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new IsgPrintOptionDialog(filteredStudents.Count);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Choice == IsgPrintChoice.Cancel)
        {
            return;
        }

        var settings = await _settingRepo.GetAppSettingsAsync();
        using var pd = BuildIsgStudentListPrintDocument(filteredStudents, settings);

        if (dlg.Choice == IsgPrintChoice.Preview)
        {
            PrintPreviewHelper.ShowPreview(pd, "İSG Öğrenci Listesi Raporu Baskı Önizleme", this);
        }
        else if (dlg.Choice == IsgPrintChoice.DirectPrint)
        {
            using var printDlg = new PrintDialog
            {
                Document = pd,
                UseEXDialog = true,
                AllowSomePages = true
            };

            if (printDlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    pd.Print();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Yazdırma işlemi sırasında hata oluştu: {ex.Message}", "Yazdırma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private PrintDocument BuildIsgStudentListPrintDocument(List<Student> students, AppSettings settings)
    {
        var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(40, 40, 40, 40);

        int currentStudentIndex = 0;
        int pageNumber = 1;

        pd.BeginPrint += (sender, e) =>
        {
            currentStudentIndex = 0;
            pageNumber = 1;
        };

        pd.PrintPage += (sender, e) =>
        {
            var g = e.Graphics!;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var bounds = e.MarginBounds;
            int left = bounds.Left;
            int right = bounds.Right;
            int top = bounds.Top;
            int width = bounds.Width;

            using var fontHeaderTitle = new Font("Segoe UI", 13f, FontStyle.Bold);
            using var fontSubTitle = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fontColHeader = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fontCell = new Font("Segoe UI", 8f);
            using var fontFooter = new Font("Segoe UI", 8f, FontStyle.Italic);

            using var penBorder = new Pen(Color.FromArgb(203, 213, 225), 1);
            using var brushHeaderBg = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var brushText = new SolidBrush(Color.FromArgb(15, 23, 42));

            string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ" : settings.SchoolName;
            string title = $"{schoolName}";
            string subtitle = "İŞ SAĞLIĞI VE GÜVENLİĞİ (İSG) ÖĞRENCİ TAKİP VE SERTİFİKA LİSTESİ";

            var formatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(title, fontHeaderTitle, brushText, new RectangleF(left, top, width, 24), formatCenter);
            g.DrawString(subtitle, fontSubTitle, brushText, new RectangleF(left, top + 24, width, 20), formatCenter);

            int currentY = top + 50;

            string infoText = $"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}   |   Toplam Kayıt: {students.Count}";
            g.DrawString(infoText, fontFooter, brushText, new PointF(left, currentY));
            currentY += 18;

            int[] colWidths = [40, 75, 200, 80, 150, 95, 95, 160, 80];
            string[] colTitles = ["Sıra", "Okul No", "Öğrenci Adı Soyadı", "Sınıf", "İSG Sertifika Durumu", "Sert. Tarihi", "Sertifika No", "Güvenlik Belge No", "Belge Basıldı"];

            int sumWidth = colWidths.Sum();
            if (sumWidth < width)
            {
                colWidths[2] += (width - sumWidth);
            }

            int rowHeight = 22;
            int curX = left;
            for (int i = 0; i < colTitles.Length; i++)
            {
                var cellRect = new Rectangle(curX, currentY, colWidths[i], rowHeight);
                g.FillRectangle(brushHeaderBg, cellRect);
                g.DrawRectangle(penBorder, cellRect);

                var sf = new StringFormat
                {
                    Alignment = (i == 0 || i == 1 || i == 3 || i == 5 || i == 8) ? StringAlignment.Center : StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                g.DrawString(colTitles[i], fontColHeader, brushText, new RectangleF(cellRect.X + 3, cellRect.Y, cellRect.Width - 6, cellRect.Height), sf);
                curX += colWidths[i];
            }

            currentY += rowHeight;

            while (currentStudentIndex < students.Count)
            {
                if (currentY + rowHeight > bounds.Bottom - 25)
                {
                    e.HasMorePages = true;
                    g.DrawString($"Sayfa {pageNumber}", fontFooter, brushText, new PointF(right - 60, bounds.Bottom - 18));
                    pageNumber++;
                    return;
                }

                var s = students[currentStudentIndex];
                string isgStatus = string.IsNullOrWhiteSpace(s.IsgStatus) ? "Sertifikasız (Eksik)" : s.IsgStatus;
                string isgDate = s.IsgCertificateDate.HasValue ? s.IsgCertificateDate.Value.ToString("dd.MM.yyyy") : "-";
                string isgCertNo = string.IsNullOrWhiteSpace(s.IsgCertificateNo) ? "-" : s.IsgCertificateNo;
                string isgSecNo = string.IsNullOrWhiteSpace(s.IsgSecurityNo) ? "-" : s.IsgSecurityNo;
                string isgDocPrinted = s.IsIsgDocumentPrinted ? "Evet" : "Hayır";

                string[] cellValues = [
                    (currentStudentIndex + 1).ToString(),
                    s.StudentNumber ?? "-",
                    s.FullName,
                    s.Classroom ?? "-",
                    isgStatus,
                    isgDate,
                    isgCertNo,
                    isgSecNo,
                    isgDocPrinted
                ];

                curX = left;
                for (int i = 0; i < cellValues.Length; i++)
                {
                    var cellRect = new Rectangle(curX, currentY, colWidths[i], rowHeight);
                    g.DrawRectangle(penBorder, cellRect);

                    var sf = new StringFormat
                    {
                        Alignment = (i == 0 || i == 1 || i == 3 || i == 5 || i == 8) ? StringAlignment.Center : StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(cellValues[i], fontCell, brushText, new RectangleF(cellRect.X + 3, cellRect.Y, cellRect.Width - 6, cellRect.Height), sf);
                    curX += colWidths[i];
                }

                currentY += rowHeight;
                currentStudentIndex++;
            }

            e.HasMorePages = false;
            g.DrawString($"Sayfa {pageNumber}", fontFooter, brushText, new PointF(right - 60, bounds.Bottom - 18));
        };

        return pd;
    }

    private List<Student> GetCurrentFilteredStudents()
    {
        var filtered = _allStudents.AsEnumerable();

        if (_opType == "xml-ebildirge")
        {
            var selectedGroup = _cmbEBildirgeFilter?.SelectedItem?.ToString() ?? "Seçiniz.";
            var selectedSgkCode = _cmbSgkBelgeType?.SelectedItem?.ToString() ?? "Seçiniz.";

            if (selectedGroup == "Tüm Öğrenciler (MESEM Hariç)")
            {
                filtered = filtered.Where(s => !IsMesemStudent(s));
            }
            else if (selectedGroup == "Sadece MESEM Öğrencileri")
            {
                filtered = filtered.Where(s => IsMesemStudent(s));
            }

            if (selectedSgkCode != "Seçiniz.")
            {
                filtered = filtered.Where(s => MatchesSgkBelgeCode(s, selectedSgkCode));
            }
        }

        return filtered.ToList();
    }

    private async Task ApplyBulkSgkDatesAsync()
    {
        _grid.EndEdit();
        bool applyEntry = _rbApplyEntry?.Checked ?? false;
        bool applyExit = _rbApplyExit?.Checked ?? false;

        if (!applyEntry && !applyExit)
        {
            MessageBox.Show(
                "Lütfen uygulanacak işlemi seçiniz (İşe Giriş Tarihi veya İşten Çıkış Tarihi).",
                "Tarih Seçimi Yapılmadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var selectedStudents = new List<Student>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"] != null &&
                row.Cells["colSelect"].Value != null &&
                Convert.ToBoolean(row.Cells["colSelect"].Value) &&
                row.Tag is Student student)
            {
                selectedStudents.Add(student);
            }
        }

        if (selectedStudents.Count == 0)
        {
            MessageBox.Show(
                "Lütfen tarih uygulamak istediğiniz öğrencileri seçiniz.",
                "Öğrenci Seçilmedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        DateTime? entryDate = applyEntry ? _dtpBulkEntry.Value.Date : null;
        DateTime? exitDate = applyExit ? _dtpBulkExit.Value.Date : null;
        bool isBackdatedConfirmed = false;

        if (applyEntry && entryDate.HasValue)
        {
            var entry = entryDate.Value.Date;
            var today = DateTime.Today;
            var minAllowed = SgkDateValidator.GetDefaultSgkEntryDate(today);

            // Pazartesi günü aynı gün (today) geçerli, diğer günlerde en erken ertesi iş günü (minAllowed)
            bool isEarlierThanAllowed = today.DayOfWeek == DayOfWeek.Monday ? (entry < today) : (entry < minAllowed);

            if (isEarlierThanAllowed)
            {
                string ruleText = today.DayOfWeek == DayOfWeek.Monday
                    ? "Bugün Pazartesi olduğu için mevzuat gereği işe giriş tarihi en erken bugün (aynı gün) olabilir."
                    : $"Bugün Pazartesi olmadığı için mevzuat gereği işe giriş tarihi en erken ertesi iş günü ({minAllowed:dd.MM.yyyy dddd}) olabilir.";

                // 1. ONAY
                var confirm1 = MessageBox.Show(
                    $"⚠️ [1. UYARI: GERİYE DÖNÜK / ERKEN İŞE GİRİŞ TARİHİ]\n\n" +
                    $"Seçtiğiniz SGK İşe Giriş Tarihi: {entry:dd.MM.yyyy dddd}\n\n" +
                    $"{ruleText}\n\n" +
                    "Geriye dönük veya mevzuat bildirim süresinden önceki bir tarihe işe giriş kaydetmek SGK bildirim gecikmesine ve idari para cezasına sebep olabilir!\n\n" +
                    $"Seçilen {selectedStudents.Count} öğrenci için yine de bu tarihi kaydetmek istiyor musunuz?",
                    "Toplu SGK İşe Giriş Uyarısı (1. Onay)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm1 != DialogResult.Yes) return;

                // 2. ONAY (İkinci güvenlik onayı)
                var confirm2 = MessageBox.Show(
                    $"🚨 [2. VE SON ONAY: DİKKAT!]\n\n" +
                    $"{entry:dd.MM.yyyy} tarihi SGK mevzuatında öngörülen bildirim süresinden öncedir!\n\n" +
                    $"Tüm sorumluluğu kabul ederek seçilen {selectedStudents.Count} öğrenciye bu işe giriş tarihini kaydetmeyi KESİN OLARAK ONAYLIYOR MUSUNUZ?",
                    "Geriye Dönük SGK İşe Giriş Kesin Onayı (2. Onay)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm2 != DialogResult.Yes) return;

                isBackdatedConfirmed = true;
            }
            else
            {
                if (!SgkDateValidator.ValidateSgkEntryDate(entryDate.Value, DateTime.Today, out string errorMsg))
                {
                    MessageBox.Show(
                        errorMsg,
                        "Geçersiz SGK İşe Giriş Tarihi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        if (applyExit && exitDate.HasValue)
        {
            var exit = exitDate.Value.Date;
            var today = DateTime.Today;
            var minRetroDate = today.AddDays(-10);

            foreach (var student in selectedStudents)
            {
                DateTime? effectiveEntryDate = applyEntry ? entryDate : student.SgkEntryDate;
                if (!effectiveEntryDate.HasValue)
                {
                    MessageBox.Show(
                        $"{student.FullName} adlı öğrencinin SGK işe giriş tarihi bulunmadığından işten çıkış tarihi verilemez!",
                        "Geçersiz SGK İşten Çıkış Tarihi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (exit <= effectiveEntryDate.Value.Date)
                {
                    MessageBox.Show(
                        $"{student.FullName} adlı öğrenci için işten çıkış tarihi ({exit:dd.MM.yyyy}), işe giriş tarihinden ({effectiveEntryDate.Value:dd.MM.yyyy}) sonraki bir tarih olmalıdır!",
                        "Geçersiz SGK İşten Çıkış Tarihi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            // Geriye dönük 10 günden fazla çıkış tarihi için 2 kademeli onay (eğer girişte zaten onaylanmadıysa)
            if (exit < minRetroDate && !isBackdatedConfirmed)
            {
                var confirmExit1 = MessageBox.Show(
                    $"⚠️ [1. UYARI: GERİYE DÖNÜK İŞTEN ÇIKIŞ TARİHİ]\n\n" +
                    $"Seçtiğiniz SGK İşten Çıkış Tarihi: {exit:dd.MM.yyyy dddd}\n\n" +
                    $"SGK mevzuatına göre işten ayrılış bildirgesi en fazla 10 gün geriye dönük verilebilir (Yasal sınır: {minRetroDate:dd.MM.yyyy}).\n\n" +
                    $"Seçilen {selectedStudents.Count} öğrenci için yine de bu tarihi kaydetmek istiyor musunuz?",
                    "SGK İşten Çıkış Tarihi Uyarısı (1. Onay)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmExit1 != DialogResult.Yes) return;

                var confirmExit2 = MessageBox.Show(
                    $"🚨 [2. VE SON ONAY: DİKKAT!]\n\n" +
                    $"{exit:dd.MM.yyyy} tarihi 10 günlük yasal bildirim süresinden öncedir!\n\n" +
                    $"Tüm sorumluluğu kabul ederek seçilen {selectedStudents.Count} öğrenciye bu işten çıkış tarihini kaydetmeyi KESİN OLARAK ONAYLIYOR MUSUNUZ?",
                    "Geriye Dönük SGK İşten Çıkış Kesin Onayı (2. Onay)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmExit2 != DialogResult.Yes) return;

                isBackdatedConfirmed = true;
            }
        }

        if (!isBackdatedConfirmed)
        {
            var confirm = MessageBox.Show(
                $"Seçilen {selectedStudents.Count} öğrenci için SGK tarihleri güncellenecektir:\n\n" +
                (applyEntry ? $"• İşe Giriş Tarihi: {entryDate:dd.MM.yyyy}\n" : "") +
                (applyExit ? $"• İşten Çıkış Tarihi: {exitDate:dd.MM.yyyy}\n" : "") +
                "\nDevam etmek istiyor musunuz?",
                "Toplu SGK Tarih Güncelleme",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;
        }

        try
        {
            foreach (var student in selectedStudents)
            {
                if (applyEntry) student.SgkEntryDate = entryDate;
                if (applyExit) student.SgkExitDate = exitDate;
                await _studentRepo.UpdateAsync(student);
            }

            MessageBox.Show(
                $"{selectedStudents.Count} öğrencinin SGK tarihi başarıyla güncellendi.",
                "İşlem Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Tarihler güncellenirken bir hata oluştu:\n{ex.Message}",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task ClearBulkSgkDatesAsync(bool clearEntry, bool clearExit)
    {
        _grid.EndEdit();
        var selectedStudents = new List<Student>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"] != null &&
                row.Cells["colSelect"].Value != null &&
                Convert.ToBoolean(row.Cells["colSelect"].Value) &&
                row.Tag is Student student)
            {
                selectedStudents.Add(student);
            }
        }

        if (selectedStudents.Count == 0)
        {
            string typeStr = clearEntry && clearExit ? "tarihleri" : clearEntry ? "işe giriş tarihi" : "işten çıkış tarihi";
            MessageBox.Show(
                $"Lütfen {typeStr} silinecek öğrencileri seçiniz.",
                "Öğrenci Seçilmedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        string actionName = clearEntry && clearExit ? "İşe Giriş ve Çıkış Tarihleri" : clearEntry ? "İşe Giriş Tarihleri" : "İşten Çıkış Tarihleri";
        
        // 1. Onay
        var confirm1 = MessageBox.Show(
            $"[1. ONAY] Seçilen {selectedStudents.Count} öğrencinin SGK {actionName} silinecektir (boş bırakılacaktır).\n\n" +
            "Devam etmek istiyor musunuz?",
            $"{actionName} Silme - 1. Onay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm1 != DialogResult.Yes) return;

        // 2. Onay (İkinci güvenlik kontrolü)
        var confirm2 = MessageBox.Show(
            $"⚠️ [2. VE SON ONAY] Bu işlem geri alınamaz!\n\n" +
            $"Seçilen {selectedStudents.Count} öğrencinin SGK {actionName.ToLower(new CultureInfo("tr-TR"))} veritabanından silinecektir.\n\n" +
            "Tarihleri silmeyi onaylıyor musunuz?",
            $"DİKKAT: {actionName} Silinecek (2. Onay)",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm2 != DialogResult.Yes) return;

        try
        {
            var studentIds = selectedStudents.Select(s => s.Id).ToList();
            await _studentRepo.ClearBulkSgkDatesAsync(studentIds, clearEntry, clearExit);

            MessageBox.Show(
                $"{selectedStudents.Count} öğrencinin {actionName.ToLower(new CultureInfo("tr-TR"))} başarıyla silindi.",
                "İşlem Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Tarihler silinirken bir hata oluştu:\n{ex.Message}",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task ExportXmlEBildirgeAsync()
    {
        if (!Common.LicenseUIHelper.CheckFeature("SGK e-Bildirge XML Dosyası Üretimi", this))
            return;

        try
        {
            if (_cmbYear?.SelectedItem == null || _cmbMonth?.SelectedItem == null)
            {
                MessageBox.Show("Lütfen yıl ve ay seçimini yapınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_cmbSgkBelgeType?.SelectedItem == null || _cmbSgkBelgeType.SelectedItem.ToString() == "Seçiniz.")
            {
                MessageBox.Show("Lütfen SGK Belge Türü Kodu (49, 50, 07 veya 42) seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int year = Convert.ToInt32(_cmbYear.SelectedItem);
            int monthIndex = _cmbMonth.SelectedIndex + 1;
            string monthName = _cmbMonth.SelectedItem.ToString() ?? "";
            string docType = _cmbDocumentType?.SelectedItem?.ToString() ?? "ASIL";
            string belgeKoduFull = _cmbSgkBelgeType.SelectedItem.ToString()!;

            // Extract clean 2-digit Belge Kodu
            string belgeKoduClean = "49";
            if (belgeKoduFull.StartsWith("49")) belgeKoduClean = "49";
            else if (belgeKoduFull.StartsWith("50")) belgeKoduClean = "50";
            else if (belgeKoduFull.StartsWith("07") || belgeKoduFull.StartsWith("7")) belgeKoduClean = "07";
            else if (belgeKoduFull.StartsWith("42")) belgeKoduClean = "42";

            var trCulture = new CultureInfo("tr-TR");
            var studentsToExport = GetCurrentFilteredStudents()
                .OrderBy(s => (s.FirstName ?? "").Trim(), StringComparer.Create(trCulture, true))
                .ThenBy(s => (s.LastName ?? "").Trim(), StringComparer.Create(trCulture, true))
                .ToList();
            if (studentsToExport.Count == 0)
            {
                MessageBox.Show("Seçilen filtrelere uygun öğrenci kaydı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // SGK Bildirge Öncesi Öğrenci Kontrolü (Audit) Hatırlatması
            var lastAuditStr = await _settingRepo.GetValueAsync("LastStudentAuditDate");
            DateTime? lastAuditDate = null;
            if (DateTime.TryParse(lastAuditStr, out var parsedDt)) lastAuditDate = parsedDt;

            bool auditNeeded = !lastAuditDate.HasValue || (DateTime.Today - lastAuditDate.Value.Date).TotalDays > 25 || lastAuditDate.Value.Month != DateTime.Today.Month;
            if (auditNeeded)
            {
                var auditPrompt = MessageBox.Show(
                    "⚠️ SGK Bildirge Öncesi Önemli Hatırlatma:\n\n" +
                    (lastAuditDate.HasValue
                        ? $"En son e-Okul Öğrenci Kontrolü: {lastAuditDate.Value:dd.MM.yyyy HH:mm} tarihinde yapılmış.\n\n"
                        : "Bu ay henüz e-Okul Öğrenci Kontrolü yapılmadı.\n\n") +
                    "Ayrılan/nakil giden veya yeni başlayan öğrencilerin SGK prim bildirgelerinde hatalı yer almaması ve olası idari para cezalarının önlenmesi için güncel e-Okul listesiyle 'Öğrenci Kontrolü' yapmanız tavsiye edilir.\n\n" +
                    "Şimdi e-Okul Öğrenci Kontrolü ekranını açmak ister misiniz?\n" +
                    "• [EVET] : Öğrenci Kontrolü penceresini açar.\n" +
                    "• [HAYIR] : Kontrol yapmadan doğrudan XML oluşturmaya devam eder.",
                    "Öğrenci Kontrolü Tavsiyesi",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (auditPrompt == DialogResult.Yes)
                {
                    using var dlg = new Dialogs.StudentAuditDialog(_studentRepo, _classRepo, null, _settingRepo);
                    dlg.ShowDialog(this.FindForm());
                    LoadDataAsync();
                    return;
                }
            }

            // SGK e-Bildirge & Devamsızlık Çapraz Denetim Kontrolü
            var auditSettings = await _settingRepo.GetAppSettingsAsync();
            var auditReport = await _crossCheckService.RunAuditAsync(year, monthIndex, studentsToExport, auditSettings);
            if (auditReport.HasErrors || auditReport.HasWarnings)
            {
                using var auditDlg = new SgkAuditDialog(auditReport, _onNavigate);
                if (auditDlg.ShowDialog(this.FindForm()) != DialogResult.OK)
                {
                    return; // Kullanıcı bildirgeyi durdurdu veya devamsızlık ekranına yönlendi
                }
            }

            // Ask user where to save the file
            using var sfd = new SaveFileDialog
            {
                Filter = "XML Dosyası (*.xml)|*.xml|Tüm Dosyalar (*.*)|*.*",
                FileName = $"SGK_eBildirge_{year}_{monthIndex:D2}_Kod{belgeKoduClean}_{docType}.xml",
                Title = "e-Bildirge XML Dosyasının Kaydedileceği Yeri Seçin"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            var settings = await _settingRepo.GetAppSettingsAsync();
            var yearlyWage = await _settingRepo.GetEffectiveWageForYearAsync(year);

            // Fetch attendance for selected period
            List<AttendanceEntry> monthAttendance = new();
            if (_attendanceRepo != null)
            {
                try
                {
                    monthAttendance = await _attendanceRepo.GetForMonthAsync(year, monthIndex);
                }
                catch { }
            }

            // Construct SGK e-Bildirge V2 XML
            var xmlDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("AYLIKBILDIRGELER",
                    new XAttribute("VER", "2"),

                    new XElement("ISYERI",
                        new XAttribute("ISYERISECIL", string.IsNullOrWhiteSpace(settings.SgkSicilNo) ? "105320101100442706005" : settings.SgkSicilNo),
                        new XAttribute("KONTROLNO", string.IsNullOrWhiteSpace(settings.SgkKontrolNo) ? "90" : settings.SgkKontrolNo),
                        new XAttribute("ISYERIARACINO", string.IsNullOrWhiteSpace(settings.SgkAraciNo) ? "000" : settings.SgkAraciNo),
                        new XAttribute("ISYERIUNVAN", (settings.SchoolName ?? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ").ToUpper(new System.Globalization.CultureInfo("tr-TR"))),
                        new XAttribute("ISYERIADRES", (settings.SchoolName ?? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ").ToUpper(new System.Globalization.CultureInfo("tr-TR"))),
                        new XAttribute("ISYERIVERSINO", string.IsNullOrWhiteSpace(settings.VergiNo) ? "6310639052" : settings.VergiNo)
                    ),

                    new XElement("BORDRO",
                        new XAttribute("DONEMAY", monthIndex.ToString("D2")),
                        new XAttribute("DONEMYIL", year.ToString()),
                        new XAttribute("HIZMETDONEMAY", monthIndex.ToString("D2")),
                        new XAttribute("HIZMETDONEMYIL", year.ToString()),
                        new XAttribute("TAHAKKUKNEDEN", "A"),
                        new XAttribute("BELGENAHIYET", docType.StartsWith("EK", StringComparison.OrdinalIgnoreCase) ? "E" : "A")
                    ),

                    new XElement("BILDIRGELER",
                        new XAttribute("BELGETURU", belgeKoduClean),
                        new XAttribute("KANUN", "00000"),

                        new XElement("SIGORTALILAR",
                            studentsToExport.Select((s, index) =>
                            {
                                int sira = index + 1;
                                int daysInMonth = DateTime.DaysInMonth(year, monthIndex);

                                // Giriş / Çıkış Günleri
                                string girisGun = "0";
                                string cikisGun = "0";
                                bool entryInMonth = s.SgkEntryDate.HasValue && s.SgkEntryDate.Value.Year == year && s.SgkEntryDate.Value.Month == monthIndex;
                                bool exitInMonth = s.SgkExitDate.HasValue && s.SgkExitDate.Value.Year == year && s.SgkExitDate.Value.Month == monthIndex;

                                if (entryInMonth)
                                {
                                    girisGun = $"{s.SgkEntryDate!.Value.Day:D2}{monthIndex:D2}";
                                }
                                if (exitInMonth)
                                {
                                    cikisGun = $"{s.SgkExitDate!.Value.Day:D2}{monthIndex:D2}";
                                }

                                // Attendance / Devamsızlık hesabı ve R (Raporlu) kontrolü
                                var studentAtt = monthAttendance.Where(a => a.StudentId == s.Id).ToList();
                                bool hasReport = studentAtt.Any(a => MebKoordinatorluk.Core.Services.AttendanceCodeHelper.IsHealthReport(a.Code));

                                // Yalnızca öğrencinin aktif olduğu çalışma günleri aralığındaki devamsızlıklar sayılır
                                var activeAtt = studentAtt.Where(a =>
                                    (!entryInMonth || a.DayOfMonth >= s.SgkEntryDate!.Value.Day) &&
                                    (!exitInMonth || a.DayOfMonth <= s.SgkExitDate!.Value.Day)
                                ).ToList();

                                double absentCount = 0;
                                foreach (var a in activeAtt)
                                {
                                    absentCount += MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetSgkAbsenceDays(a.Code);
                                }
                                int absentDays = (int)Math.Round(absentCount);

                                // SGK 2020/20 Genelgesi Parmak Hesabı (31 çeken aylarda 1 gün devamsızlıkta 30 gün prim, 0 gün eksik gün)
                                var (paidDays, missingDays) = MebKoordinatorluk.Core.Services.SgkCalculationHelper.CalculateSgkDays(
                                    year,
                                    monthIndex,
                                    s.SgkEntryDate,
                                    s.SgkExitDate,
                                    absentDays);

                                // PEK (Prime Esas Kazanç) Hesabı: (Brüt Asgari Ücret / 2) -> 30 gün tam gelenler için
                                // Günlük PEK = (Brüt Asgari Ücret / 2) / 30.0 -> Gün sayısı ile çarpılarak PEK bulunur
                                decimal grossWage = yearlyWage.GrossMinimumWage > 0 ? yearlyWage.GrossMinimumWage : settings.EffectiveGrossMinimumWage;
                                decimal fullMonthPek = grossWage / 2.0m;
                                decimal dailyPek = fullMonthPek / 30.0m;
                                decimal totalPek = Math.Round(dailyPek * paidDays, 2);
                                string pekStr = totalPek.ToString("0.00", CultureInfo.InvariantCulture);

                                string firstName = (s.FirstName ?? "").Trim().ToUpper(new CultureInfo("tr-TR"));
                                string lastName = (s.LastName ?? "").Trim().ToUpper(new CultureInfo("tr-TR"));

                                var attrs = new List<XAttribute>
                                {
                                    new("SIRA", sira.ToString()),
                                    new("TCKNO", s.TcKimlikNo ?? ""),
                                    new("AD", firstName),
                                    new("SOYAD", lastName),
                                    new("PEK", pekStr),
                                    new("GUN", paidDays.ToString()),
                                    new("GIRISGUN", girisGun),
                                    new("CIKISGUN", cikisGun),
                                    new("EKSIKGUNSAYISI", missingDays.ToString())
                                };

                                if (missingDays > 0)
                                {
                                    var absentCodes = activeAtt
                                        .Where(a => MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetSgkAbsenceDays(a.Code) > 0)
                                        .Select(a => a.Code);
                                    string missingReasonCode = MebKoordinatorluk.Core.Services.AttendanceCodeHelper.DetermineSgkMissingReasonCode(absentCodes);
                                    attrs.Add(new XAttribute("EKSIKGUNNEDENI", missingReasonCode));
                                }

                                attrs.Add(new XAttribute("MESLEKKOD", "9901.02"));

                                if (hasReport)
                                {
                                    attrs.Add(new XAttribute("RAPCALISTI", "2"));
                                }

                                return new XElement("SIGORTALI", attrs);
                            })
                        )
                    )
                )
            );

            var xmlSettings = new System.Xml.XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            };

            using (var writer = System.Xml.XmlWriter.Create(sfd.FileName, xmlSettings))
            {
                xmlDoc.Save(writer);
            }

            MessageBox.Show(
                $"SGK e-Bildirge XML dosyası başarıyla oluşturuldu!\n\n" +
                $"📍 Kayıt Yeri: {sfd.FileName}\n" +
                $"📅 Dönem: {year} / {monthName} ({monthIndex:D2})\n" +
                $"📑 Bildirge Türü: {docType}\n" +
                $"🏷️ SGK Belge Kodu: {belgeKoduClean}\n" +
                $"👥 Toplam Sigortalı Sayısı: {studentsToExport.Count}",
                "XML e-Bildirge Başarıyla Kaydedildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            // Prim hesaplaması biten ay içinde SGK çıkışı yapılmış aktif öğrencileri tespit et
            var exitedInPeriod = _allStudents.Where(s =>
                s.IsActive &&
                s.SgkExitDate.HasValue &&
                s.SgkExitDate.Value.Year == year &&
                s.SgkExitDate.Value.Month == monthIndex
            ).ToList();

            if (exitedInPeriod.Count > 0)
            {
                using var archivePrompt = new Dialogs.ArchivePromptDialog(exitedInPeriod, _studentRepo, $"{monthName} {year}");
                if (archivePrompt.ShowDialog(FindForm()) == DialogResult.OK && archivePrompt.ArchivedCount > 0)
                {
                    LoadDataAsync();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"XML dosyası oluşturulurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunSgkCrossCheckManualAsync()
    {
        try
        {
            if (_cmbYear?.SelectedItem == null || _cmbMonth?.SelectedItem == null)
            {
                MessageBox.Show("Lütfen yıl ve ay seçimini yapınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int year = Convert.ToInt32(_cmbYear.SelectedItem);
            int monthIndex = _cmbMonth.SelectedIndex + 1;
            var studentsToAudit = GetCurrentFilteredStudents();
            if (studentsToAudit.Count == 0)
            {
                MessageBox.Show("Denetim yapılacak stajyer öğrenci kaydı bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var settings = await _settingRepo.GetAppSettingsAsync();
            var report = await _crossCheckService.RunAuditAsync(year, monthIndex, studentsToAudit, settings);

            if (report.IsClean)
            {
                MessageBox.Show(
                    $"✅ Tebrikler! {year}/{monthIndex:D2} dönemi için seçilen {report.TotalStudentsAudited} öğrencinin SGK günleri, devamsızlık verileri ve TC kimlik kayıtları tam uyumlu.\n\nHiçbir hata veya uyarı tespit edilmedi. e-Bildirge XML oluşturmaya hazırsınız.",
                    "SGK Çapraz Denetim Başarılı — 3308 Pro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SgkAuditDialog(report, _onNavigate);
            dlg.ShowDialog(this.FindForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Çapraz denetim gerçekleştirilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ExportIseGirisXmlAsync(bool isEntry)
    {
        string opName = isEntry ? "SGK İşe Giriş XML Üretimi" : "SGK İşten Çıkış XML Üretimi";
        if (!Common.LicenseUIHelper.CheckFeature(opName, this))
            return;

        try
        {
            _grid.EndEdit();
            var selectedStudents = new List<Student>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["colSelect"] != null &&
                    row.Cells["colSelect"].Value != null &&
                    Convert.ToBoolean(row.Cells["colSelect"].Value) &&
                    row.Tag is Student student)
                {
                    selectedStudents.Add(student);
                }
            }

            var trCulture = new CultureInfo("tr-TR");
            selectedStudents = selectedStudents
                .OrderBy(s => (s.FirstName ?? "").Trim(), StringComparer.Create(trCulture, true))
                .ThenBy(s => (s.LastName ?? "").Trim(), StringComparer.Create(trCulture, true))
                .ToList();

            if (selectedStudents.Count == 0)
            {
                MessageBox.Show(
                    "Lütfen XML dosyası oluşturulacak en az bir öğrenci seçiniz.",
                    "Öğrenci Seçilmedi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int totalSelected = selectedStudents.Count;
            if (!isEntry)
            {
                // SGK İşten Çıkış Dökümü: SADECE işten çıkış tarihi tanımlı olan ve tutarlı öğrenciler işleme alınır
                var validWithExit = selectedStudents.Where(s =>
                    s.SgkExitDate.HasValue &&
                    (!s.SgkEntryDate.HasValue || s.SgkExitDate.Value.Date >= s.SgkEntryDate.Value.Date)).ToList();

                var studentsWithoutExit = selectedStudents.Where(s => !s.SgkExitDate.HasValue).ToList();
                var studentsWithInvalidDates = selectedStudents.Where(s =>
                    s.SgkExitDate.HasValue && s.SgkEntryDate.HasValue && s.SgkExitDate.Value.Date < s.SgkEntryDate.Value.Date).ToList();

                if (validWithExit.Count == 0)
                {
                    if (studentsWithInvalidDates.Count > 0)
                    {
                        MessageBox.Show(
                            "Seçilen öğrencilerin 'İşten Çıkış Tarihi', 'İşe Giriş Tarihi'nden önce olarak görünmektedir!\n\n" +
                            "Tarih tutarsızlığı olan kayıtlar için SGK İşten Çıkış XML'i üretilemez. Lütfen tarihleri kontrol ediniz.",
                            "Geçersiz Çıkış Tarihi Sıralaması",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    MessageBox.Show(
                        "Seçilen öğrenciler arasında 'İşten Çıkış Tarihi' tanımlı olan öğrenci bulunamadı!\n\n" +
                        "SGK İşten Çıkış dökümü/XML'i yalnızca işten çıkış tarihi olan öğrencileri kapsar. " +
                        "Çıkış tarihi olmayan aktif öğrenciler güvenlik gerekçesiyle döküme dahil edilmez.",
                        "İşten Çıkış Tarihi Bulunamadı",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (validWithExit.Count < totalSelected)
                {
                    var msgExit = new System.Text.StringBuilder();
                    msgExit.AppendLine($"Seçilen {totalSelected} öğrencinin {validWithExit.Count} tanesi İşten Çıkış XML bildirimi için uygundur.\n");
                    if (studentsWithoutExit.Count > 0)
                    {
                        msgExit.AppendLine($"• {studentsWithoutExit.Count} aktif öğrencinin 'İşten Çıkış Tarihi' tanımlı olmadığı için çıkış dökümünden hariç tutulacaktır.");
                    }
                    if (studentsWithInvalidDates.Count > 0)
                    {
                        msgExit.AppendLine($"• {studentsWithInvalidDates.Count} öğrencinin çıkış tarihi giriş tarihinden önce olduğu için güvenlik nedeniyle hariç tutulacaktır.");
                    }
                    msgExit.AppendLine($"\nYalnızca geçerli işten çıkış kaydı olan {validWithExit.Count} öğrenci için devam etmek istiyor musunuz?");

                    var confirm = MessageBox.Show(
                        msgExit.ToString(),
                        "İşten Çıkış Dökümü Güvenlik Filtresi",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes)
                    {
                        return;
                    }
                }

                selectedStudents = validWithExit;
            }
            else
            {
                // SGK İşe Giriş Dökümü: SADECE işe giriş tarihi tanımlı olan VE İŞTEN ÇIKIŞI BULUNMAYAN öğrenciler işleme alınır
                var validWithEntry = selectedStudents.Where(s => s.SgkEntryDate.HasValue && !s.SgkExitDate.HasValue).ToList();
                var studentsWithExit = selectedStudents.Where(s => s.SgkExitDate.HasValue).ToList();
                var studentsWithoutEntry = selectedStudents.Where(s => !s.SgkEntryDate.HasValue).ToList();

                if (validWithEntry.Count == 0)
                {
                    if (studentsWithExit.Count > 0)
                    {
                        MessageBox.Show(
                            "Seçilen öğrencilerin tamamında 'İşten Çıkış Tarihi' tanımlıdır (staj ilişiği kesilmiştir)!\n\n" +
                            "SGK İşten Çıkış tarihi girilmiş olan öğrencilere güvenlik gerekçesiyle İşe Giriş Bildirgesi XML'i üretilemez.\n\n" +
                            "İşe Giriş Bildirgesi yalnızca stajı devam eden veya yeni başlayacak aktif öğrenciler için düzenlenebilir.",
                            "İşe Giriş Bildirgesi Üretilemez",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    MessageBox.Show(
                        "Seçilen öğrenciler arasında geçerli 'İşe Giriş Tarihi' tanımlı olan öğrenci bulunamadı!\n\n" +
                        "SGK İşe Giriş dökümü/XML'i yalnızca işe giriş tarihi olan ve çıkış verilmemiş aktif öğrencileri kapsar.",
                        "İşe Giriş Tarihi Bulunamadı",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (validWithEntry.Count < totalSelected)
                {
                    var msgEntry = new System.Text.StringBuilder();
                    msgEntry.AppendLine($"Seçilen {totalSelected} öğrencinin {validWithEntry.Count} tanesi İşe Giriş XML bildirimi için uygundur.\n");
                    if (studentsWithExit.Count > 0)
                    {
                        msgEntry.AppendLine($"• {studentsWithExit.Count} öğrencinin 'İşten Çıkış Tarihi' bulunduğu için (staj ilişiği sonlandırıldığından) İşe Giriş XML'ine dahil edilmeyecektir.");
                    }
                    if (studentsWithoutEntry.Count > 0)
                    {
                        msgEntry.AppendLine($"• {studentsWithoutEntry.Count} öğrencinin 'İşe Giriş Tarihi' tanımlı olmadığı için hariç tutulacaktır.");
                    }
                    msgEntry.AppendLine($"\nYalnızca aktif ve çıkış kaydı bulunmayan {validWithEntry.Count} öğrenci için devam etmek istiyor musunuz?");

                    var confirm = MessageBox.Show(
                        msgEntry.ToString(),
                        "İşe Giriş Dökümü Güvenlik Filtresi",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes)
                    {
                        return;
                    }
                }

                selectedStudents = validWithEntry;
            }

            var settings = await _settingRepo.GetAppSettingsAsync();
            string actionName = isEntry ? "İşe Giriş" : "İşten Çıkış";
            string defaultFileName = isEntry ? $"SGK_Ise_Giris_{DateTime.Now:yyyyMMdd}.xml" : $"SGK_Isten_Cikis_{DateTime.Now:yyyyMMdd}.xml";

            using var sfd = new SaveFileDialog
            {
                Filter = "XML Dosyası (*.xml)|*.xml|Tüm Dosyalar (*.*)|*.*",
                FileName = defaultFileName,
                Title = $"SGK 4/A {actionName} XML Dosyasının Kaydedileceği Yeri Seçin"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            XElement rootElement;

            if (isEntry)
            {
                rootElement = new XElement("SGK4AISEGIRIS",
                    new XElement("ISYERI",
                        new XAttribute("ISYERISICIL", string.IsNullOrWhiteSpace(settings.SgkSicilNo) ? "185320101100442706005" : settings.SgkSicilNo),
                        new XAttribute("ISYERIARACINO", string.IsNullOrWhiteSpace(settings.SgkAraciNo) ? "000" : settings.SgkAraciNo),
                        new XAttribute("ISYERIUNVAN", (settings.SchoolName ?? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ").ToUpper(new CultureInfo("tr-TR"))),
                        new XAttribute("ISYERIADRES", (settings.SchoolName ?? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ").ToUpper(new CultureInfo("tr-TR")))
                    ),
                    new XElement("SIGORTALILAR",
                        selectedStudents.Select(s =>
                        {
                            string firstName = (s.FirstName ?? "").Trim().ToUpper(new CultureInfo("tr-TR"));
                            string lastName = (s.LastName ?? "").Trim().ToUpper(new CultureInfo("tr-TR"));
                            string entryDateStr = s.SgkEntryDate!.Value.ToString("yyyy-MM-dd");

                            bool isMesem = IsMesemStudent(s);
                            string sigortaKolu = isMesem
                                ? (string.IsNullOrWhiteSpace(settings.MesemSigortaKolu) ? "7" : settings.MesemSigortaKolu)
                                : (string.IsNullOrWhiteSpace(settings.OrgunSigortaKolu) ? "37" : settings.OrgunSigortaKolu);
                            string ogrenimKodu = isMesem
                                ? (string.IsNullOrWhiteSpace(settings.MesemOgrenimKodu) ? "3" : settings.MesemOgrenimKodu)
                                : (string.IsNullOrWhiteSpace(settings.OrgunOgrenimKodu) ? "3" : settings.OrgunOgrenimKodu);
                            string meslekKodu = isMesem
                                ? (string.IsNullOrWhiteSpace(settings.MesemMeslekKodu) ? "9901.03" : settings.MesemMeslekKodu)
                                : (string.IsNullOrWhiteSpace(settings.OrgunMeslekKodu) ? "9901.02" : settings.OrgunMeslekKodu);
                            string gorevKodu = isMesem
                                ? (string.IsNullOrWhiteSpace(settings.MesemGorevKodu) ? "5" : settings.MesemGorevKodu)
                                : (string.IsNullOrWhiteSpace(settings.OrgunGorevKodu) ? "5" : settings.OrgunGorevKodu);

                            return new XElement("SIGORTALI",
                                new XAttribute("TCKNO", s.TcKimlikNo ?? ""),
                                new XAttribute("AD", firstName),
                                new XAttribute("SOYAD", lastName),
                                new XAttribute("ISEGIRISTARIHI", entryDateStr),
                                new XAttribute("SIGORTAKOLU", sigortaKolu),
                                new XAttribute("OZURLUKODU", "H"),
                                new XAttribute("ESKIHUKUMLU", "H"),
                                new XAttribute("OGRENIMKODU", ogrenimKodu),
                                new XAttribute("MEZUNIYETYILI", "0"),
                                new XAttribute("MEZUNIYETBOLUMU", ""),
                                new XAttribute("MESLEKKODU", meslekKodu),
                                new XAttribute("GOREVKODU", gorevKodu)
                            );
                        })
                    )
                );
            }
            else
            {
                // Fetch required attendance records for all periods touched by selected students
                var distinctPeriods = selectedStudents
                    .Select(s => s.SgkExitDate ?? DateTime.Today)
                    .SelectMany(d => new[] { (d.Year, d.Month), (d.AddMonths(-1).Year, d.AddMonths(-1).Month) })
                    .Distinct()
                    .ToList();

                var attendanceCache = new Dictionary<(int Year, int Month), List<AttendanceEntry>>();
                var wageCache = new Dictionary<int, decimal>();
                if (_attendanceRepo != null)
                {
                    foreach (var (y, m) in distinctPeriods)
                    {
                        try
                        {
                            attendanceCache[(y, m)] = await _attendanceRepo.GetForMonthAsync(y, m);
                        }
                        catch
                        {
                            attendanceCache[(y, m)] = new List<AttendanceEntry>();
                        }

                        if (!wageCache.ContainsKey(y))
                        {
                            try
                            {
                                var yw = await _settingRepo.GetEffectiveWageForYearAsync(y);
                                wageCache[y] = yw.GrossMinimumWage;
                            }
                            catch
                            {
                                wageCache[y] = settings.EffectiveGrossMinimumWage;
                            }
                        }
                    }
                }

                string schoolNameUpper = (settings.SchoolName ?? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ").ToUpper(new CultureInfo("tr-TR"));

                rootElement = new XElement("SGK4AISTENCIKIS",
                    new XElement("ISYERI",
                        new XAttribute("ISYERIADRES", schoolNameUpper),
                        new XAttribute("ISYERIARACINO", string.IsNullOrWhiteSpace(settings.SgkAraciNo) ? "000" : settings.SgkAraciNo),
                        new XAttribute("ISYERISICIL", string.IsNullOrWhiteSpace(settings.SgkSicilNo) ? "185320101100442706005" : settings.SgkSicilNo),
                        new XAttribute("ISYERIUNVAN", schoolNameUpper)
                    ),
                    new XElement("SIGORTALILAR",
                        selectedStudents.Select(s =>
                        {
                            string firstName = (s.FirstName ?? "").Trim();
                            string lastName = (s.LastName ?? "").Trim().ToUpper(new CultureInfo("tr-TR"));
                            DateTime exitDate = s.SgkExitDate!.Value;
                            string exitDateStr = exitDate.ToString("yyyy-MM-dd");

                            bool isMesem = IsMesemStudent(s);
                            string meslekKodu = isMesem
                                ? (string.IsNullOrWhiteSpace(settings.MesemMeslekKodu) ? "9901.03" : settings.MesemMeslekKodu)
                                : (string.IsNullOrWhiteSpace(settings.OrgunMeslekKodu) ? "9901.02" : settings.OrgunMeslekKodu);

                            string belgeTuru = "49";
                            if (!string.IsNullOrWhiteSpace(s.SgkPrimKodu))
                            {
                                var digits = new string(s.SgkPrimKodu.Where(char.IsDigit).ToArray());
                                if (!string.IsNullOrEmpty(digits))
                                {
                                    belgeTuru = digits.PadLeft(2, '0');
                                    if (belgeTuru.Length > 2) belgeTuru = belgeTuru[..2];
                                }
                            }

                            // Önceki Dönem
                            DateTime prevDate = exitDate.AddMonths(-1);
                            attendanceCache.TryGetValue((prevDate.Year, prevDate.Month), out var prevAtt);
                            decimal? prevGross = wageCache.TryGetValue(prevDate.Year, out var pg) ? pg : null;
                            var prevDetails = CalculatePeriodDetails(s, prevDate.Year, prevDate.Month, prevAtt ?? new List<AttendanceEntry>(), settings, isPreviousPeriod: true, customGrossWage: prevGross);

                            // Bulunduğumuz Dönem
                            attendanceCache.TryGetValue((exitDate.Year, exitDate.Month), out var curAtt);
                            decimal? curGross = wageCache.TryGetValue(exitDate.Year, out var cg) ? cg : null;
                            var curDetails = CalculatePeriodDetails(s, exitDate.Year, exitDate.Month, curAtt ?? new List<AttendanceEntry>(), settings, isPreviousPeriod: false, customGrossWage: curGross);

                            return new XElement("SIGORTALI",
                                new XAttribute("TCKNO", s.TcKimlikNo ?? ""),
                                new XAttribute("AD", firstName),
                                new XAttribute("SOYAD", lastName),
                                new XAttribute("ISTENCIKISTARIHI", exitDateStr),
                                new XAttribute("MESLEKKODU", meslekKodu),
                                new XAttribute("ISTENAYRILISNEDENI", "22"),
                                new XAttribute("CSGBISKOLU", "0"),
                                new XAttribute("UCRETYUZDEUSULU", "H"),
                                new XElement("ONCEKIDONEM",
                                    new XAttribute("BELGETURU", belgeTuru),
                                    new XAttribute("HAKEDILENUCRET", prevDetails.wage.ToString("0.00", CultureInfo.InvariantCulture)),
                                    new XAttribute("PRIMIKRAMIYE", "0"),
                                    new XAttribute("EKSIKGUNSAYISI", prevDetails.eksikGunSayisi),
                                    new XAttribute("EKSIKGUNNEDENI", prevDetails.eksikGunNedeni)
                                ),
                                new XElement("BULUNDUGUMUZDONEM",
                                    new XAttribute("BELGETURU", belgeTuru),
                                    new XAttribute("HAKEDILENUCRET", curDetails.wage.ToString("0.00", CultureInfo.InvariantCulture)),
                                    new XAttribute("EKSIKGUNSAYISI", curDetails.eksikGunSayisi),
                                    new XAttribute("EKSIKGUNNEDENI", curDetails.eksikGunNedeni)
                                )
                            );
                        })
                    )
                );
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var encoding = System.Text.Encoding.GetEncoding("ISO-8859-9");

            var sb = new System.Text.StringBuilder();
            using (var sw = new System.IO.StringWriter(sb))
            using (var writer = System.Xml.XmlWriter.Create(sw, new System.Xml.XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true
            }))
            {
                rootElement.Save(writer);
            }

            string fullXml = "<?xml version=\"1.0\" encoding=\"iSO-8859-9\"?>\r\n" + sb.ToString();
            await System.IO.File.WriteAllTextAsync(sfd.FileName, fullXml, encoding);

            MessageBox.Show(
                $"SGK 4/A {actionName} XML dosyası başarıyla oluşturuldu!\n\n" +
                $"📍 Kayıt Yeri: {sfd.FileName}\n" +
                $"👥 Toplam Sigortalı Sayısı: {selectedStudents.Count}",
                $"SGK {actionName} XML Başarıyla Kaydedildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"XML dosyası oluşturulurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnSecondaryAction_Click(object? sender, EventArgs e)
    {
        if (_opType == "xml-isegiris")
        {
            _ = ExportIseGirisXmlAsync(isEntry: false);
        }
        else if (_opType == "surekli-devamsizlar")
        {
            _ = ApplyBulkSgkExitDateForAbsenceAsync();
        }
        else
        {
            MessageBox.Show("İlgili listenin çıktısı/raporu hazırlandı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task PromptAndSetSingleStudentSgkExitDateAsync(Student student)
    {
        using var dlg = new Form
        {
            Text = "📅 SGK İşten Çıkış Tarihi Belirle",
            Size = new Size(420, 230),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9f)
        };

        var lblInfo = new Label
        {
            Text = $"{student.FullName} ({student.StudentNumber})\nSınıf: {student.Classroom} | İşletme: {student.EnterpriseName ?? "-"}",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 16),
            Size = new Size(365, 42)
        };

        var lblDate = new Label
        {
            Text = "SGK İşten Çıkış Tarihi:",
            Location = new Point(20, 72),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        var dtp = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Value = student.SgkExitDate ?? DateTime.Today,
            Location = new Point(175, 68),
            Width = 125,
            Font = new Font("Segoe UI", 9.5f)
        };

        var btnSave = new Button
        {
            Text = "💾 Kaydet",
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(190, 135),
            Size = new Size(100, 36),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;

        var btnClear = new Button
        {
            Text = "🗑️ Çıkışı Kaldır",
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(220, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(20, 135),
            Size = new Size(130, 36),
            Cursor = Cursors.Hand
        };
        btnClear.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);

        var btnCancel = new Button
        {
            Text = "Vazgeç",
            DialogResult = DialogResult.Cancel,
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(300, 135),
            Size = new Size(80, 36),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        btnClear.Click += async (s, e) =>
        {
            student.SgkExitDate = null;
            await _studentRepo.UpdateAsync(student);
            dlg.DialogResult = DialogResult.Abort; // İşlem tamamlandı (temizlendi)
            dlg.Close();
        };

        dlg.Controls.AddRange([lblInfo, lblDate, dtp, btnClear, btnSave, btnCancel]);
        dlg.AcceptButton = btnSave;
        dlg.CancelButton = btnCancel;

        var res = dlg.ShowDialog(this);
        if (res == DialogResult.OK)
        {
            student.SgkExitDate = dtp.Value.Date;
            await _studentRepo.UpdateAsync(student);
            LoadDataAsync();
        }
        else if (res == DialogResult.Abort)
        {
            LoadDataAsync();
        }
    }

    private async Task ApplyBulkSgkExitDateForAbsenceAsync()
    {
        _grid.EndEdit();
        var selectedStudents = new List<Student>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["colSelect"] != null &&
                row.Cells["colSelect"].Value != null &&
                Convert.ToBoolean(row.Cells["colSelect"].Value) &&
                row.Tag is Student student)
            {
                selectedStudents.Add(student);
            }
        }

        if (selectedStudents.Count == 0)
        {
            MessageBox.Show(
                "Lütfen SGK çıkış tarihi işlenecek öğrencileri tablonun solundaki kutucukları işaretleyerek seçiniz.",
                "Öğrenci Seçilmedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new Form
        {
            Text = "📅 Toplu SGK Çıkış Tarihi İşleme",
            Size = new Size(450, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9f)
        };

        var lblInfo = new Label
        {
            Text = $"Seçilen {selectedStudents.Count} sürekli devamsız öğrenci için SGK İşten Çıkış Tarihi belirleyiniz:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 16),
            Size = new Size(395, 42)
        };

        var lblDate = new Label
        {
            Text = "SGK Çıkış Tarihi:",
            Location = new Point(20, 74),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        var dtp = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today,
            Location = new Point(165, 70),
            Width = 130,
            Font = new Font("Segoe UI", 9.5f)
        };

        var btnSave = new Button
        {
            Text = "💾 Tarihi Uygula",
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(185, 140),
            Size = new Size(130, 36),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;

        var btnClear = new Button
        {
            Text = "🗑️ Çıkışları Temizle",
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(220, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(20, 140),
            Size = new Size(145, 36),
            Cursor = Cursors.Hand
        };
        btnClear.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);

        var btnCancel = new Button
        {
            Text = "Vazgeç",
            DialogResult = DialogResult.Cancel,
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(325, 140),
            Size = new Size(80, 36),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        btnClear.Click += async (s, e) =>
        {
            foreach (var st in selectedStudents)
            {
                st.SgkExitDate = null;
                await _studentRepo.UpdateAsync(st);
            }
            dlg.DialogResult = DialogResult.Abort;
            dlg.Close();
        };

        dlg.Controls.AddRange([lblInfo, lblDate, dtp, btnClear, btnSave, btnCancel]);
        dlg.AcceptButton = btnSave;
        dlg.CancelButton = btnCancel;

        var res = dlg.ShowDialog(this);
        if (res == DialogResult.OK)
        {
            foreach (var st in selectedStudents)
            {
                st.SgkExitDate = dtp.Value.Date;
                await _studentRepo.UpdateAsync(st);
            }
            MessageBox.Show(
                $"{selectedStudents.Count} öğrenci için SGK çıkış tarihi kaydedildi.",
                "İşlem Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            LoadDataAsync();
        }
        else if (res == DialogResult.Abort)
        {
            MessageBox.Show(
                $"{selectedStudents.Count} öğrenci için SGK çıkış durumu 'Çıkış Bekliyor' olarak güncellendi.",
                "İşlem Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            LoadDataAsync();
        }
    }

    private static (int missingDays, string eksikGunNedeni, string eksikGunSayisi, decimal wage) CalculatePeriodDetails(
        Student s,
        int year,
        int month,
        List<AttendanceEntry> attendance,
        AppSettings settings,
        bool isPreviousPeriod,
        decimal? customGrossWage = null)
    {
        // 1. Öğrencinin aylık baz hak edilen ücreti (SGK 4/A e-Bildirge usulü: Brüt Asgari Ücret / 2):
        decimal grossWage = customGrossWage.HasValue && customGrossWage.Value > 0
            ? customGrossWage.Value
            : (settings.EffectiveGrossMinimumWage > 0
                ? settings.EffectiveGrossMinimumWage
                : (settings.GrossMinimumWage > 0 ? settings.GrossMinimumWage : 17616.00m));
        decimal monthlyWage = Math.Round(grossWage / 2.0m, 2);
        if (monthlyWage <= 0) monthlyWage = 8808.00m;

        int daysInMonth = DateTime.DaysInMonth(year, month);
        var periodStart = new DateTime(year, month, 1);
        var periodEnd = new DateTime(year, month, daysInMonth);

        // Eğer öğrenci bu dönemde henüz işe girmemişse (örn: İşe giriş 12 Ekim, önceki dönem Eylül):
        if (s.SgkEntryDate.HasValue && s.SgkEntryDate.Value.Date > periodEnd)
        {
            return (0, "00", "00", 0.00m);
        }

        // Eğer öğrenci bu dönem başlamadan önce zaten işten ayrılmışsa:
        if (s.SgkExitDate.HasValue && s.SgkExitDate.Value.Date < periodStart)
        {
            return (0, "00", "00", 0.00m);
        }

        int totalMonthDays = 30; // SGK standart gün hesabı
        int missingDays = 0;

        // 2. Başlama tarihine (SgkEntryDate) ve İşten Çıkış tarihine (SgkExitDate) göre aktif gün kontrolü:
        bool entryInThisMonth = s.SgkEntryDate.HasValue &&
                                s.SgkEntryDate.Value.Year == year &&
                                s.SgkEntryDate.Value.Month == month;

        bool exitInThisMonth = !isPreviousPeriod && s.SgkExitDate.HasValue &&
                               s.SgkExitDate.Value.Year == year &&
                               s.SgkExitDate.Value.Month == month;

        DateTime? calcEntry = entryInThisMonth ? s.SgkEntryDate : null;
        DateTime? calcExit = exitInThisMonth ? s.SgkExitDate : null;

        // 3. Devamsızlık (Yoklama) kayıtlarından gelen devamsızlık günleri:
        var studentAtt = attendance.Where(a => a.StudentId == s.Id).ToList();

        // Eğer ay içinde işe giriş varsa girişten sonraki devamsızlıklar; 
        // bulunulan ayda ve işten çıkış varsa çıkış tarihine kadarki devamsızlıklar sayılır
        var activeAtt = studentAtt.Where(a =>
            (!entryInThisMonth || a.DayOfMonth >= s.SgkEntryDate!.Value.Day) &&
            (isPreviousPeriod || !s.SgkExitDate.HasValue || s.SgkExitDate.Value.Month != month || a.DayOfMonth <= s.SgkExitDate.Value.Day)
        ).ToList();

        double absentCount = 0;
        foreach (var a in activeAtt)
        {
            absentCount += MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetSgkAbsenceDays(a.Code);
        }
        int absentDays = (int)Math.Round(absentCount);

        // SGK 2020/20 Genelgesi ve SgkCalculationHelper kuralları:
        // Ay içi giriş, çıkış veya tam ay parmak hesabı
        var sgkRes = MebKoordinatorluk.Core.Services.SgkCalculationHelper.Calculate(
            year,
            month,
            calcEntry,
            calcExit,
            absentDays);

        int paidDays = sgkRes.BildirilecekPrimGunu;
        missingDays = sgkRes.BildirilecekEksikGun;

        // 4. Hak Edilen Ücret hesabı:
        // Çıkış tarihine kadar (veya girişten itibaren) çalışılan gün sayısına göre Günlük PEK * paidDays olarak hesaplanır.
        // Ayın tamamında eksiksiz çalışanlarda aylık PEK tutarı tam korunur.
        decimal dailyWage = monthlyWage / 30.0m;
        decimal earnedWage = (paidDays >= totalMonthDays) ? monthlyWage : Math.Round(dailyWage * paidDays, 2);

        // 5. Eksik Gün Neden ve Sayı Formatı:
        string eksikGunNedeni;
        string eksikGunSayisi;

        if (missingDays <= 0)
        {
            eksikGunNedeni = "00";
            eksikGunSayisi = "00";
        }
        else
        {
            var absentCodes = activeAtt
                .Where(a => MebKoordinatorluk.Core.Services.AttendanceCodeHelper.GetSgkAbsenceDays(a.Code) > 0)
                .Select(a => a.Code);
            eksikGunNedeni = MebKoordinatorluk.Core.Services.AttendanceCodeHelper.DetermineSgkMissingReasonCode(absentCodes);
            eksikGunSayisi = missingDays.ToString();
        }

        return (missingDays, eksikGunNedeni, eksikGunSayisi, earnedWage);
    }
}
