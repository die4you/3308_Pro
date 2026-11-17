using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace MebKoordinatorluk.UI.Common;

public static class PrintPreviewHelper
{
    /// <summary>
    /// PrintPreviewDialog penceresini yapılandırır: tam ekran açar, otomatik yakınlaştırmayı ayarlar,
    /// demo modundaysa filigran ve 1. sayfa kısıtlamasını uygular
    /// ve araç çubuğundaki 'Yazdır' butonuna tıklandığında kullanıcının yazıcı seçebileceği PrintDialog penceresini açar.
    /// </summary>
    public static PrintPreviewDialog Create(PrintDocument document, string title)
    {
        bool isDemo = !LicenseUIHelper.IsLicensed;
        string displayTitle = isDemo ? $"[DEMO SÜRÜM - 1. SAYFA] {title}" : title;

        // Demo modundaysa filigranı ve ilk sayfa kısıtlamasını ekle
        if (isDemo)
        {
            ApplyDemoRestrictions(document);
        }

        var preview = new PrintPreviewDialog
        {
            Document     = document,
            Text         = displayTitle,
            WindowState  = FormWindowState.Maximized,
            UseAntiAlias = true
        };

        if (preview.Controls.OfType<PrintPreviewControl>().FirstOrDefault() is { } ppc)
        {
            ppc.AutoZoom = true;
        }

        AttachCustomPrintButton(preview, document, isDemo);

        return preview;
    }

    /// <summary>
    /// Önizleme penceresini görüntüler.
    /// </summary>
    public static DialogResult ShowPreview(PrintDocument document, string title, IWin32Window? owner = null)
    {
        using var preview = Create(document, title);
        return owner != null ? preview.ShowDialog(owner) : preview.ShowDialog();
    }

    /// <summary>
    /// Demo sürümde yazdırılan belgenin üzerine resmi evrak olarak kullanılmasını engelleyen
    /// çapraz filigran basar ve çok sayfalı belgelerde sadece ilk sayfayı yazdırır.
    /// </summary>
    public static void ApplyDemoRestrictions(PrintDocument document)
    {
        document.PrintPage += (s, ev) =>
        {
            if (ev.Graphics != null)
            {
                DrawDemoWatermark(ev.Graphics, ev.PageBounds);
            }

            // Demo modunda çok sayfalı listelerde yalnızca 1. sayfa basılır
            ev.HasMorePages = false;
        };
    }

    private static void DrawDemoWatermark(Graphics g, Rectangle bounds)
    {
        var state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Sayfanın merkezine konumlandır
            float cx = bounds.Width / 2f;
            float cy = bounds.Height / 2f;
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-30f); // 30 derece çapraz eğim

            using var brushWatermark = new SolidBrush(Color.FromArgb(48, 220, 38, 38)); // Yarı saydam kırmızı
            using var fontMain = new Font("Segoe UI", 34f, FontStyle.Bold);
            using var fontSub = new Font("Segoe UI", 16f, FontStyle.Bold);

            string textMain = "3308 PRO - LİSANSSIZ DEMO SÜRÜMÜ";
            string textSub = "Resmi Evrak / Belge Niteliği Taşımaz";

            var sizeMain = g.MeasureString(textMain, fontMain);
            var sizeSub = g.MeasureString(textSub, fontSub);

            // Metinleri ortalayarak çiz
            g.DrawString(textMain, fontMain, brushWatermark, -sizeMain.Width / 2f, -sizeMain.Height);
            g.DrawString(textSub, fontSub, brushWatermark, -sizeSub.Width / 2f, 10f);

            // Çerçeve çiz
            using var penWatermark = new Pen(Color.FromArgb(48, 220, 38, 38), 3f);
            float boxW = Math.Max(sizeMain.Width, sizeSub.Width) + 50f;
            float boxH = sizeMain.Height + sizeSub.Height + 40f;
            g.DrawRectangle(penWatermark, -boxW / 2f, -sizeMain.Height - 15f, boxW, boxH);
        }
        finally
        {
            g.Restore(state);
        }
    }

    /// <summary>
    /// PrintPreviewDialog içerisindeki ToolStrip yazdırma butonunu özelleştirerek PrintDialog açmasını sağlar.
    /// </summary>
    public static void AttachCustomPrintButton(PrintPreviewDialog preview, PrintDocument document, bool isDemo = false)
    {
        try
        {
            var toolStrip = preview.Controls.OfType<ToolStrip>().FirstOrDefault();
            if (toolStrip == null || toolStrip.Items.Count == 0) return;

            // Demo uyarısını araç çubuğuna ekle
            if (isDemo)
            {
                var lblDemoWarning = new ToolStripLabel
                {
                    Text = "⚠️ DEMO SÜRÜM KISITLAMASI: Çıktılar filigranlı ve yalnızca 1. sayfa olarak oluşturulur. Tam döküm için lisans edininiz.",
                    ForeColor = Color.FromArgb(185, 28, 28),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Alignment = ToolStripItemAlignment.Right
                };
                toolStrip.Items.Add(lblDemoWarning);
            }

            // Yazdır butonunu bul (name="printToolStripButton" veya ilk öğe)
            ToolStripItem? originalPrintBtn = null;
            int printIndex = -1;

            for (int i = 0; i < toolStrip.Items.Count; i++)
            {
                var item = toolStrip.Items[i];
                if (string.Equals(item.Name, "printToolStripButton", StringComparison.OrdinalIgnoreCase) ||
                    (item.ToolTipText != null && item.ToolTipText.IndexOf("yazdır", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (item.ToolTipText != null && item.ToolTipText.IndexOf("print", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    i == 0)
                {
                    originalPrintBtn = item;
                    printIndex = i;
                    break;
                }
            }

            if (originalPrintBtn == null || printIndex < 0) return;

            var btnImage = originalPrintBtn.Image;
            var tooltipText = originalPrintBtn.ToolTipText;

            var customPrintBtn = new ToolStripButton
            {
                Name = "customPrintToolStripButton",
                Image = btnImage,
                ToolTipText = string.IsNullOrWhiteSpace(tooltipText) ? "Yazdır (Yazıcı Seç)" : $"{tooltipText} (Yazıcı Seç)",
                DisplayStyle = ToolStripItemDisplayStyle.Image
            };

            customPrintBtn.Click += (s, e) =>
            {
                using var printDlg = new PrintDialog
                {
                    Document = document,
                    UseEXDialog = true,
                    AllowSomePages = true
                };

                if (printDlg.ShowDialog(preview) == DialogResult.OK)
                {
                    try
                    {
                        document.Print();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Yazdırma işlemi sırasında bir hata oluştu:\n{ex.Message}",
                            "Yazdırma Hatası",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            };

            toolStrip.Items.RemoveAt(printIndex);
            toolStrip.Items.Insert(printIndex, customPrintBtn);
        }
        catch
        {
            // Olası bir platform veya WinForms sürüm kısıtlamasında sessizce geç
        }
    }
}
