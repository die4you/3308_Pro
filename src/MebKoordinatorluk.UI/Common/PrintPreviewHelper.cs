using System;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace MebKoordinatorluk.UI.Common;

public static class PrintPreviewHelper
{
    /// <summary>
    /// PrintPreviewDialog penceresini yapılandırır: tam ekran açar, otomatik yakınlaştırmayı ayarlar
    /// ve araç çubuğundaki 'Yazdır' butonuna tıklandığında doğrudan varsayılan yazıcıya göndermek yerine
    /// kullanıcının yazıcı seçebileceği PrintDialog penceresini açar.
    /// </summary>
    public static PrintPreviewDialog Create(PrintDocument document, string title)
    {
        var preview = new PrintPreviewDialog
        {
            Document     = document,
            Text         = title,
            WindowState  = FormWindowState.Maximized,
            UseAntiAlias = true
        };

        if (preview.Controls.OfType<PrintPreviewControl>().FirstOrDefault() is { } ppc)
        {
            ppc.AutoZoom = true;
        }

        AttachCustomPrintButton(preview, document);

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
    /// PrintPreviewDialog içerisindeki ToolStrip yazdırma butonunu özelleştirerek PrintDialog açmasını sağlar.
    /// </summary>
    public static void AttachCustomPrintButton(PrintPreviewDialog preview, PrintDocument document)
    {
        try
        {
            var toolStrip = preview.Controls.OfType<ToolStrip>().FirstOrDefault();
            if (toolStrip == null || toolStrip.Items.Count == 0) return;

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
