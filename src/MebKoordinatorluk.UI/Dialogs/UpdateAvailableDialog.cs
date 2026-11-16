using System.Diagnostics;
using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.UI.Dialogs;

public class UpdateAvailableDialog : Form
{
    private readonly UpdateCheckResult _updateInfo;
    private readonly GitHubUpdateService _updateService;
    private readonly bool _isLicensed;

    private ProgressBar _progressBar = null!;
    private Label _lblDownloadProgress = null!;
    private Button _btnInstall = null!;
    private Button _btnWeb = null!;
    private Button _btnLater = null!;
    private CancellationTokenSource? _cts;

    public UpdateAvailableDialog(UpdateCheckResult updateInfo, GitHubUpdateService updateService, bool isLicensed = true)
    {
        _updateInfo = updateInfo;
        _updateService = updateService;
        _isLicensed = isLicensed;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "🚀 Yeni Güncelleme Mevcut — 3308 Pro";
        this.Size = new Size(620, 520);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.Font = new Font("Segoe UI", 9f);

        // Header Panel (Koyu Mavi - Modern)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(30, 58, 138), // Dark Blue
            Padding = new Padding(24, 16, 24, 16)
        };

        var lblHeaderTitle = new Label
        {
            Text = "🎉 Yeni Güncelleme Yayınlandı!",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 16),
            AutoSize = true
        };

        var lblHeaderSub = new Label
        {
            Text = _isLicensed
                ? "3308 Pro'nun en yeni sürümünü tek tıkla indirip otomatik kurabilirsiniz."
                : "Yeni sürüm güncellemeleri yalnızca aktif lisansı bulunan kurumlara açıktır.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(219, 234, 254),
            Location = new Point(22, 46),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblHeaderTitle);
        pnlHeader.Controls.Add(lblHeaderSub);

        // Sürüm Rozetleri Paneli
        var pnlVersionBadge = new Panel
        {
            Location = new Point(24, 100),
            Size = new Size(556, 44),
            BackColor = Color.FromArgb(241, 245, 249)
        };
        pnlVersionBadge.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawRectangle(pen, 0, 0, pnlVersionBadge.Width - 1, pnlVersionBadge.Height - 1);
        };

        var lblVersions = new Label
        {
            Text = $"Mevcut Sürüm: v{_updateInfo.CurrentVersion}   ➜   Yeni Sürüm: v{_updateInfo.LatestVersion}  {(!string.IsNullOrWhiteSpace(_updateInfo.FormattedFileSize) ? $"({_updateInfo.FormattedFileSize})" : "")}",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(14, 12),
            AutoSize = true
        };
        pnlVersionBadge.Controls.Add(lblVersions);

        // Sürüm Notları Başlığı
        var lblNotesTitle = new Label
        {
            Text = $"📋 Yenilikler ve Değişiklikler ({_updateInfo.ReleaseTitle}):",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(24, 156),
            AutoSize = true
        };

        // Sürüm Notları Metin Kutusu
        var txtNotes = new TextBox
        {
            Location = new Point(24, 180),
            Size = new Size(556, 175),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 9f),
            Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes)
                ? "Bu sürüm için detaylı not eklenmemiş. Hata düzeltmeleri ve performans iyileştirmeleri içerir."
                : _updateInfo.ReleaseNotes.Replace("\n", "\r\n")
        };

        // İndirme İlerleme Çubuğu ve Durumu (Başlangıçta gizli)
        _progressBar = new ProgressBar
        {
            Location = new Point(24, 370),
            Size = new Size(556, 18),
            Style = ProgressBarStyle.Continuous,
            Visible = false
        };

        _lblDownloadProgress = new Label
        {
            Location = new Point(24, 392),
            Size = new Size(556, 18),
            ForeColor = Color.FromArgb(71, 85, 105),
            Font = new Font("Segoe UI", 8.5f),
            Text = "",
            Visible = false
        };

        // Alt Buton Paneli
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        _btnInstall = new Button
        {
            Text = _isLicensed ? "🚀 Şimdi İndir ve Güncelle" : "🔒 Güncelleme İçin Lisans Gerekli",
            Location = new Point(320, 14),
            Size = new Size(260, 38),
            BackColor = _isLicensed ? Color.FromArgb(37, 99, 235) : Color.FromArgb(148, 163, 184),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = _isLicensed ? Cursors.Hand : Cursors.Default,
            Enabled = _isLicensed
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += async (s, e) => await StartDownloadAndInstallAsync();

        _btnWeb = new Button
        {
            Text = "🌐 GitHub'da Gör",
            Location = new Point(170, 14),
            Size = new Size(140, 38),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnWeb.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnWeb.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_updateInfo.HtmlUrl))
            {
                Process.Start(new ProcessStartInfo(_updateInfo.HtmlUrl) { UseShellExecute = true });
            }
        };

        _btnLater = new Button
        {
            Text = "Daha Sonra",
            Location = new Point(24, 14),
            Size = new Size(100, 38),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(100, 116, 139),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLater.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnLater.Click += (s, e) =>
        {
            _cts?.Cancel();
            this.Close();
        };

        pnlBottom.Controls.Add(_btnInstall);
        pnlBottom.Controls.Add(_btnWeb);
        pnlBottom.Controls.Add(_btnLater);

        this.Controls.Add(pnlHeader);
        this.Controls.Add(pnlVersionBadge);
        this.Controls.Add(lblNotesTitle);
        this.Controls.Add(txtNotes);
        this.Controls.Add(_progressBar);
        this.Controls.Add(_lblDownloadProgress);
        this.Controls.Add(pnlBottom);
    }

    private async Task StartDownloadAndInstallAsync()
    {
        if (!_isLicensed)
        {
            MessageBox.Show(
                "Yeni sürümleri indirip kurabilmek için 3308 Pro yazılım lisansınızın aktif olması gerekmektedir.\n\nLütfen lisansınızı etkinleştiriniz.",
                "Lisans Gerekli — 3308 Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_updateInfo.DownloadUrl))
        {
            MessageBox.Show("Güncelleme indirme bağlantısı bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_updateInfo.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(_updateInfo.DownloadUrl) { UseShellExecute = true });
            return;
        }

        _btnInstall.Enabled = false;
        _btnLater.Text = "İptal";
        _progressBar.Visible = true;
        _lblDownloadProgress.Visible = true;
        _progressBar.Value = 0;
        _lblDownloadProgress.Text = "İndirme başlatılıyor...";

        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<(long bytesDownloaded, long totalBytes, int percent)>(p =>
            {
                _progressBar.Value = Math.Clamp(p.percent, 0, 100);
                double dlMb = p.bytesDownloaded / (1024.0 * 1024.0);
                double totalMb = p.totalBytes / (1024.0 * 1024.0);
                _lblDownloadProgress.Text = $"İndiriliyor: %{p.percent} ({dlMb:F1} MB / {totalMb:F1} MB)";
            });

            var downloadedFile = await _updateService.DownloadUpdateAsync(_updateInfo.DownloadUrl, progress, _cts.Token);

            _lblDownloadProgress.Text = "✓ İndirme tamamlandı! Kurulum başlatılıyor...";
            await Task.Delay(800);

            var psi = new ProcessStartInfo
            {
                FileName = downloadedFile,
                UseShellExecute = true
            };
            Process.Start(psi);

            Application.Exit();
        }
        catch (OperationCanceledException)
        {
            _progressBar.Visible = false;
            _lblDownloadProgress.Text = "İndirme iptal edildi.";
            _btnInstall.Enabled = _isLicensed;
            _btnLater.Text = "Daha Sonra";
        }
        catch (Exception ex)
        {
            _progressBar.Visible = false;
            _lblDownloadProgress.Text = "İndirme başarısız.";
            _btnInstall.Enabled = _isLicensed;
            _btnLater.Text = "Kapat";
            MessageBox.Show($"Güncelleme indirilemedi:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
