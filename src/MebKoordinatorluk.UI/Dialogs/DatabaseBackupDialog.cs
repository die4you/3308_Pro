using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class DatabaseBackupDialog : Form
{
    private readonly IDatabaseBackupService _backupService;
    private readonly Action? _onDatabaseRestored;

    private DataGridView _gridBackups = null!;
    private Label _lblDbLocation = null!;
    private Label _lblDbSize = null!;
    private Label _lblBackupCount = null!;
    private Button _btnBackupNow = null!;
    private Button _btnBackupCustom = null!;
    private Button _btnRestore = null!;
    private Button _btnOpenFolder = null!;

    public DatabaseBackupDialog(IDatabaseBackupService backupService, Action? onDatabaseRestored = null)
    {
        _backupService = backupService;
        _onDatabaseRestored = onDatabaseRestored;

        InitializeComponent();
        LoadBackupsList();
    }

    private void InitializeComponent()
    {
        Text = "Veritabanı Yedekleme ve Kurtarma Merkezi — 3308 Pro";
        Size = new Size(820, 620);
        MinimumSize = new Size(740, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(248, 250, 252); // slate-50
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

        var lblIcon = new Label
        {
            Text = "🛡️",
            Font = new Font("Segoe UI Emoji", 24f),
            Size = new Size(48, 48),
            Location = new Point(20, 10),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblTitle = new Label
        {
            Text = "Veritabanı Yedekleme ve Kurtarma Merkezi",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(72, 12),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Elektrik kesintisi ve veri kaybına karşı tam koruma: Tek tıkla yedek alın veya önceki bir tarihe geri dönün.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(74, 38),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange([lblIcon, lblTitle, lblSubtitle]);

        // Info Card
        var pnlInfo = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(238, 242, 255), // indigo-50
            Padding = new Padding(24, 10, 24, 10)
        };
        pnlInfo.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(199, 210, 254), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlInfo.Width - 1, pnlInfo.Height - 1);
        };

        _lblDbLocation = new Label
        {
            Text = "📁 Veritabanı: Yükleniyor...",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(49, 46, 129),
            Location = new Point(24, 10),
            AutoSize = true
        };

        _lblDbSize = new Label
        {
            Text = "💾 Aktif Boyut: ...",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(67, 56, 202),
            Location = new Point(24, 34),
            AutoSize = true
        };

        _lblBackupCount = new Label
        {
            Text = "📦 Toplam Yedek: ...",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(49, 46, 129),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(560, 20),
            AutoSize = true
        };

        pnlInfo.Controls.AddRange([_lblDbLocation, _lblDbSize, _lblBackupCount]);

        // Buttons Bar
        var pnlButtons = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.White,
            Padding = new Padding(20, 8, 20, 8)
        };
        pnlButtons.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlButtons.Height - 1, pnlButtons.Width, pnlButtons.Height - 1);
        };

        _btnBackupNow = new Button
        {
            Text = "💾 Şimdi Yedek Al",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235), // blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(140, 32),
            Location = new Point(20, 9),
            Cursor = Cursors.Hand
        };
        _btnBackupNow.FlatAppearance.BorderSize = 0;
        _btnBackupNow.Click += async (s, e) => await BackupNowAsync();

        _btnBackupCustom = new Button
        {
            Text = "📤 Dışa Aktar (USB/Farklı Konum)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(205, 32),
            Location = new Point(168, 9),
            Cursor = Cursors.Hand
        };
        _btnBackupCustom.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnBackupCustom.Click += async (s, e) => await BackupCustomLocationAsync();

        _btnRestore = new Button
        {
            Text = "🔄 Seçili Yedeğe Geri Dön",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(254, 242, 242), // rose-50
            ForeColor = Color.FromArgb(190, 18, 60), // rose-700
            FlatStyle = FlatStyle.Flat,
            Size = new Size(185, 32),
            Location = new Point(381, 9),
            Cursor = Cursors.Hand
        };
        _btnRestore.FlatAppearance.BorderColor = Color.FromArgb(254, 205, 211);
        _btnRestore.Click += async (s, e) => await RestoreSelectedBackupAsync();

        _btnOpenFolder = new Button
        {
            Text = "📂 Klasörü Aç",
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(100, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(pnlButtons.Width - 120, 9),
            Cursor = Cursors.Hand
        };
        _btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnOpenFolder.Click += (s, e) => OpenBackupFolder();

        pnlButtons.Controls.AddRange([_btnBackupNow, _btnBackupCustom, _btnRestore, _btnOpenFolder]);

        pnlButtons.Resize += (s, e) =>
        {
            _btnOpenFolder.Location = new Point(pnlButtons.Width - 120, 9);
        };

        // Grid Container
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 12, 20, 12),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        _gridBackups = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
            RowTemplate = { Height = 34 }
        };
        UITheme.ApplyModernGridStyle(_gridBackups);

        _gridBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colFileName",
            HeaderText = "Yedek Dosyası Adı",
            Width = 320,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _gridBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colDate",
            HeaderText = "Yedek Alma Tarihi & Saati",
            Width = 190
        });

        _gridBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colSize",
            HeaderText = "Dosya Boyutu",
            Width = 120,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });

        _gridBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colType",
            HeaderText = "Yedek Türü",
            Width = 120,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _gridBackups.CellDoubleClick += async (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                await RestoreSelectedBackupAsync();
            }
        };

        pnlGridContainer.Controls.Add(_gridBackups);

        // Bottom Bar (Close & Tip)
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Color.White,
            Padding = new Padding(20, 8, 20, 8)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var lblTip = new Label
        {
            Text = "💡 İpucu: Program her kapatıldığında güncel veriler otomatik olarak 'Data\\Yedekler' altına güvenle yedeklenir.",
            Font = new Font("Segoe UI", 8.2f, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(20, 14),
            AutoSize = true
        };

        var btnClose = new Button
        {
            Text = "Kapat",
            Font = new Font("Segoe UI", 9f),
            Size = new Size(85, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(pnlBottom.Width - 105, 9),
            Cursor = Cursors.Hand
        };
        btnClose.Click += (s, e) => Close();

        pnlBottom.Controls.AddRange([lblTip, btnClose]);
        pnlBottom.Resize += (s, e) => btnClose.Location = new Point(pnlBottom.Width - 105, 9);

        Controls.Add(pnlGridContainer);
        Controls.Add(pnlButtons);
        Controls.Add(pnlInfo);
        Controls.Add(pnlHeader);
        Controls.Add(pnlBottom);
    }

    private void LoadBackupsList()
    {
        try
        {
            var backupDir = _backupService.GetBackupDirectory();
            var dbDir = Path.GetDirectoryName(backupDir);
            var dbPath = Path.Combine(dbDir ?? "", "meb_koordinatorluk.db");

            if (File.Exists(dbPath))
            {
                var fi = new FileInfo(dbPath);
                _lblDbLocation.Text = $"📁 Aktif Veritabanı: {fi.FullName}";
                _lblDbSize.Text = $"💾 Aktif Boyut: {(fi.Length / 1024.0 / 1024.0):F2} MB (Son İşlem: {fi.LastWriteTime:dd.MM.yyyy HH:mm})";
            }
            else
            {
                _lblDbLocation.Text = "📁 Aktif Veritabanı: meb_koordinatorluk.db";
                _lblDbSize.Text = "💾 Aktif Boyut: Bilinmiyor";
            }

            var backups = _backupService.GetExistingBackups();
            _lblBackupCount.Text = $"📦 Toplam Mevcut Yedek: {backups.Count} adet";

            _gridBackups.Rows.Clear();
            foreach (var b in backups)
            {
                string typeName = "Manuel";
                if (b.FileName.Contains("gunluk") || b.FileName.Contains("auto"))
                {
                    typeName = "🕒 Otomatik";
                }
                else if (b.FileName.Contains("safety"))
                {
                    typeName = "🛡️ Güvenlik";
                }

                string sizeStr = b.FileSizeBytes >= 1024 * 1024
                    ? $"{(b.FileSizeBytes / 1024.0 / 1024.0):F2} MB"
                    : $"{(b.FileSizeBytes / 1024.0):F1} KB";

                int rowIdx = _gridBackups.Rows.Add(
                    b.FileName,
                    b.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss"),
                    sizeStr,
                    typeName);

                _gridBackups.Rows[rowIdx].Tag = b;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yedek listesi yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task BackupNowAsync()
    {
        try
        {
            _btnBackupNow.Enabled = false;
            _btnBackupNow.Text = "⏳ Alınıyor...";

            string savedPath = await _backupService.CreateBackupAsync();
            LoadBackupsList();

            MessageBox.Show(
                $"Veritabanı yedeği başarıyla alındı!\n\nKonum:\n{savedPath}",
                "Yedekleme Başarılı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yedek alınırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnBackupNow.Enabled = true;
            _btnBackupNow.Text = "💾 Şimdi Yedek Al";
        }
    }

    private async Task BackupCustomLocationAsync()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Veritabanı Yedeği (*.bak)|*.bak|SQLite Veritabanı (*.db)|*.db|Tüm Dosyalar (*.*)|*.*",
            FileName = $"meb_koordinatorluk_yedek_{DateTime.Now:yyyyMMdd_HHmm}.bak",
            Title = "Yedeğin Kaydedileceği Konumu Seçin (USB Flash Bellek veya Klasör)"
        };

        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            string targetFolder = Path.GetDirectoryName(sfd.FileName) ?? "";
            string targetName = Path.GetFileName(sfd.FileName);

            await _backupService.CreateBackupAsync(targetName, targetFolder);

            MessageBox.Show(
                $"Veritabanı harici konuma başarıyla aktarıldı!\n\nDosya: {sfd.FileName}",
                "Harici Yedek Alındı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            LoadBackupsList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Harici yedek kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RestoreSelectedBackupAsync()
    {
        if (_gridBackups.SelectedRows.Count == 0)
        {
            MessageBox.Show("Lütfen geri yüklemek istediğiniz yedek dosyasını tablodan seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedRow = _gridBackups.SelectedRows[0];
        if (selectedRow.Tag is not BackupFileInfo backupInfo) return;

        var confirm = MessageBox.Show(
            $"⚠️ DİKKAT: Seçili yedek geri yüklenecektir!\n\n" +
            $"Yedek Dosyası: {backupInfo.FileName}\n" +
            $"Tarih: {backupInfo.CreatedAt:dd.MM.yyyy HH:mm:ss}\n\n" +
            "Geri yükleme yapılmadan önce mevcut veritabanınızın 'güvenlik kopyası' otomatik olarak alınacaktır.\n\n" +
            "Devam etmek ve bu yedeğe geri dönmek istiyor musunuz?",
            "Veritabanı Geri Yükleme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            _btnRestore.Enabled = false;
            _btnRestore.Text = "⏳ Yükleniyor...";

            bool success = await _backupService.RestoreBackupAsync(backupInfo.FullPath);

            if (success)
            {
                MessageBox.Show(
                    "Veritabanı seçilen yedeğe başarıyla geri döndürüldü!\n\n" +
                    "Tüm verilerin tam olarak güncellenmesi için form yeniden yüklenecektir.",
                    "Geri Yükleme Başarılı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _onDatabaseRestored?.Invoke();
                LoadBackupsList();
            }
            else
            {
                MessageBox.Show(
                    "Geri yüklenen veritabanı dosyasının bütünlük testinde sorun tespit edildi!",
                    "Uyarı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Geri yükleme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnRestore.Enabled = true;
            _btnRestore.Text = "🔄 Seçili Yedeğe Geri Dön";
        }
    }

    private void OpenBackupFolder()
    {
        try
        {
            var dir = _backupService.GetBackupDirectory();
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Klasör açılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
