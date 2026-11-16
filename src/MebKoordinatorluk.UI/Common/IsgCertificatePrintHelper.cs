using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.UI.Common;

public static class IsgCertificatePrintHelper
{
    public static PrintDocument CreatePrintDocument(List<Student> students, AppSettings settings)
    {
        var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

        int currentIndex = 0;

        pd.PrintPage += (s, e) =>
        {
            if (currentIndex >= students.Count)
            {
                e.HasMorePages = false;
                return;
            }

            var student = students[currentIndex];
            DrawCertificatePage(e.Graphics, student, settings, e.PageBounds);

            currentIndex++;
            e.HasMorePages = currentIndex < students.Count;
        };

        pd.BeginPrint += (s, e) =>
        {
            currentIndex = 0;
        };

        return pd;
    }

    public static void DrawCertificatePage(Graphics g, Student student, AppSettings settings, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background
        g.Clear(Color.White);

        // 1. MEB Logosu (Üst Orta)
        int logoCenterX = bounds.Width / 2;
        int logoCenterY = 95;
        int logoSize = 115;

        Image? logoImg = null;
        string[] possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "meb_logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "MebKoordinatorluk.UI", "Assets", "meb_logo.png"),
            @"C:\Users\trgy\.gemini\antigravity\brain\df8d7f3c-0a95-4e24-9fa1-60548dd5d9e1\.user_uploaded\media_1791234249758.png",
            @"C:\Users\trgy\.gemini\antigravity\brain\df8d7f3c-0a95-4e24-9fa1-60548dd5d9e1\.user_uploaded\media_1791233928813.png"
        };

        foreach (var p in possiblePaths)
        {
            if (File.Exists(p))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(p);
                    using var ms = new MemoryStream(bytes);
                    logoImg = Image.FromStream(ms);
                    break;
                }
                catch { }
            }
        }

        if (logoImg != null)
        {
            using (logoImg)
            {
                g.DrawImage(logoImg, logoCenterX - (logoSize / 2), logoCenterY - (logoSize / 2), logoSize, logoSize);
            }
        }
        else
        {
            int logoRadius = logoSize / 2;
            using var penRed = new Pen(Color.FromArgb(190, 18, 60), 2.5f);
            using var brushRed = new SolidBrush(Color.FromArgb(190, 18, 60));
            using var fontLogo = new Font("Arial", 6.5f, FontStyle.Bold);
            g.DrawEllipse(penRed, logoCenterX - logoRadius, logoCenterY - logoRadius, logoRadius * 2, logoRadius * 2);
            g.DrawEllipse(penRed, logoCenterX - logoRadius + 4, logoCenterY - logoRadius + 4, (logoRadius - 4) * 2, (logoRadius - 4) * 2);

            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("T.C.\nM.E.B.", fontLogo, brushRed, logoCenterX, logoCenterY, sfCenter);
        }

        // 2. Başlık: KATILIM BELGESİ
        int titleY = 175;
        using (var fontTitle = new Font("Times New Roman", 24, FontStyle.Bold))
        using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center })
        {
            g.DrawString("KATILIM BELGESİ", fontTitle, Brushes.Black, bounds.Width / 2f, titleY, sfCenter);
        }

        // 3. Tablo (Çerçeveli Bilgi Matrisi)
        int tableLeft = 60;
        int tableWidth = bounds.Width - 120; // ~1049 px
        int tableTop = 235;
        int rowHeight = 32;
        int totalRows = 7;
        int tableHeight = rowHeight * totalRows;

        using (var penBorder = new Pen(Color.Black, 1.2f))
        using (var fontLabel = new Font("Segoe UI", 9.5f, FontStyle.Bold))
        using (var fontVal = new Font("Segoe UI", 9.5f, FontStyle.Regular))
        {
            // Tablo dış çerçevesi
            g.DrawRectangle(penBorder, tableLeft, tableTop, tableWidth, tableHeight);

            // Yatay Ara Çizgiler
            for (int r = 1; r < totalRows; r++)
            {
                int y = tableTop + (r * rowHeight);
                g.DrawLine(penBorder, tableLeft, y, tableLeft + tableWidth, y);
            }

            // Dikey Ara Çizgiler (Sol / Sağ Sütun Ayrımı)
            int xMid = tableLeft + (tableWidth / 2);
            int xLeftLabelRight = tableLeft + 165;
            int xRightLabelRight = xMid + 175;

            // İlk 6 satırda dikey orta çizgi ve etiketi değerden ayıran dikey çizgiler çizilir
            g.DrawLine(penBorder, xMid, tableTop, xMid, tableTop + (6 * rowHeight));
            g.DrawLine(penBorder, xLeftLabelRight, tableTop, xLeftLabelRight, tableTop + (7 * rowHeight));
            g.DrawLine(penBorder, xRightLabelRight, tableTop, xRightLabelRight, tableTop + (6 * rowHeight));

            // Hücre İçi Metin Çizim Helper
            void DrawCellText(string label, string val, int xStart, int yStart, int labelWidth, int valWidth)
            {
                var sfLeft = new StringFormat { LineAlignment = StringAlignment.Center };
                g.DrawString(label, fontLabel, Brushes.Black, new RectangleF(xStart + 6, yStart, labelWidth - 8, rowHeight), sfLeft);
                g.DrawString(val, fontVal, Brushes.Black, new RectangleF(xStart + labelWidth + 6, yStart, valWidth - 8, rowHeight), sfLeft);
            }

            string tcNo = string.IsNullOrWhiteSpace(student.TcKimlikNo) ? "-" : student.TcKimlikNo;
            string fullName = string.IsNullOrWhiteSpace(student.FullName) ? "-" : student.FullName;
            string birthStr = student.BirthDate.HasValue ? student.BirthDate.Value.ToString("dd.MM.yyyy") : "-";
            string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ" : settings.SchoolName;
            string districtCity = settings.DistrictAndCity;

            string studentNo = string.IsNullOrWhiteSpace(student.StudentNumber) ? "-" : student.StudentNumber;
            string certNo = string.IsNullOrWhiteSpace(student.IsgCertificateNo) ? "-" : student.IsgCertificateNo;
            string certDate = student.IsgCertificateDate.HasValue ? student.IsgCertificateDate.Value.ToString("dd.MM.yyyy") : DateTime.Today.ToString("dd.MM.yyyy");
            string securityNo = string.IsNullOrWhiteSpace(student.IsgSecurityNo) ? "-" : student.IsgSecurityNo;
            string fieldName = string.IsNullOrWhiteSpace(student.FieldName) ? (string.IsNullOrWhiteSpace(student.DepartmentName) ? "-" : student.DepartmentName) : student.FieldName;

            int y0 = tableTop;
            int y1 = tableTop + rowHeight;
            int y2 = tableTop + (2 * rowHeight);
            int y3 = tableTop + (3 * rowHeight);
            int y4 = tableTop + (4 * rowHeight);
            int y5 = tableTop + (5 * rowHeight);
            int y6 = tableTop + (6 * rowHeight);

            // Satır 1
            DrawCellText("T.C. Kimlik No", tcNo, tableLeft, y0, 165, xMid - tableLeft - 165);
            DrawCellText("Okulun Bulunduğu İlçe, İl", districtCity, xMid, y0, 175, bounds.Width - tableLeft - xMid - 175);

            // Satır 2: Okul Numarası directly under İlçe/İl
            DrawCellText("Adı Soyadı", fullName, tableLeft, y1, 165, xMid - tableLeft - 165);
            DrawCellText("Okul Numarası", studentNo, xMid, y1, 175, bounds.Width - tableLeft - xMid - 175);

            // Satır 3: Belge Numarası
            DrawCellText("Baba Adı", "-", tableLeft, y2, 165, xMid - tableLeft - 165);
            DrawCellText("Belge Numarası", certNo, xMid, y2, 175, bounds.Width - tableLeft - xMid - 175);

            // Satır 4: Belge Tarihi
            DrawCellText("Ana Adı", "-", tableLeft, y3, 165, xMid - tableLeft - 165);
            DrawCellText("Belge Tarihi", certDate, xMid, y3, 175, bounds.Width - tableLeft - xMid - 175);

            // Satır 5: Belge Güvenlik No
            DrawCellText("Doğum Yeri ve Tarihi", birthStr, tableLeft, y4, 165, xMid - tableLeft - 165);
            DrawCellText("Belge Güvenlik No", securityNo, xMid, y4, 175, bounds.Width - tableLeft - xMid - 175);

            // Satır 6: Alanı/Dalı & Belgeyi Veren Okul
            DrawCellText("Belgeyi Veren Okul", schoolName, tableLeft, y5, 165, xMid - tableLeft - 165);
            DrawCellText("Alanı/Dalı", fieldName, xMid, y5, 175, bounds.Width - tableLeft - xMid - 175);

            // Satır 7: Belge Adı (Tam Genişlik)
            DrawCellText("Belge Adı", "Meslekî Eğitimde İş Sağlığı ve Güvenliği Eğitimi Katılım Belgesi", tableLeft, y6, 165, tableWidth - 165);
        }

        // 4. Paragraf Metni (İki Yana Yaslı / Justified Alignment & 8 Ders Saati)
        int bodyY = tableTop + tableHeight + 35;
        string certDateFull = student.IsgCertificateDate.HasValue ? student.IsgCertificateDate.Value.ToString("dd/MM/yyyy") : DateTime.Today.ToString("dd/MM/yyyy");
        string bodyText = $"Yukarıda bilgileri yer alan öğrenci, 8 (sekiz) ders saati meslekî eğitimde iş sağlığı ve güvenliği eğitimini tamamladığı için bu belgeyi almaya hak kazanmıştır. {certDateFull}";

        using (var fontBody = new Font("Segoe UI", 11, FontStyle.Regular))
        {
            var bodyRect = new RectangleF(tableLeft, bodyY, tableWidth, 45);
            DrawJustifiedParagraph(g, bodyText, fontBody, Brushes.Black, bodyRect);
        }

        // 5. İmzalar
        int signY = bounds.Height - 110;
        int leftSignX = tableLeft + 150;
        int rightSignX = tableLeft + tableWidth - 150;

        using (var fontSignTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
        using (var fontSignName = new Font("Segoe UI", 9.5f, FontStyle.Regular))
        using (var sfSign = new StringFormat { Alignment = StringAlignment.Center })
        {
            // Noktalı Çizgiler
            g.DrawString("..................................................", fontSignName, Brushes.Black, leftSignX, signY - 20, sfSign);
            g.DrawString("..................................................", fontSignName, Brushes.Black, rightSignX, signY - 20, sfSign);

            // Unvanlar
            g.DrawString("Müdür Yardımcısı", fontSignTitle, Brushes.Black, leftSignX, signY, sfSign);
            g.DrawString("Müdür", fontSignTitle, Brushes.Black, rightSignX, signY, sfSign);

            // İsimler (Varsa)
            if (!string.IsNullOrWhiteSpace(settings.VicePrincipalName))
            {
                g.DrawString(settings.VicePrincipalName, fontSignName, Brushes.Black, leftSignX, signY + 20, sfSign);
            }
            if (!string.IsNullOrWhiteSpace(settings.PrincipalName))
            {
                g.DrawString(settings.PrincipalName, fontSignName, Brushes.Black, rightSignX, signY + 20, sfSign);
            }
        }
    }

    private static void DrawJustifiedParagraph(Graphics g, string text, Font font, Brush brush, RectangleF rect)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return;

        float spaceWidth = g.MeasureString(" ", font).Width;

        List<List<string>> lines = new();
        List<string> currentLine = new();
        float currentLineWidth = 0;

        foreach (var word in words)
        {
            float wordWidth = g.MeasureString(word, font).Width;
            if (currentLine.Count > 0 && currentLineWidth + spaceWidth + wordWidth > rect.Width)
            {
                lines.Add(currentLine);
                currentLine = new List<string> { word };
                currentLineWidth = wordWidth;
            }
            else
            {
                currentLine.Add(word);
                currentLineWidth += (currentLine.Count == 1 ? 0 : spaceWidth) + wordWidth;
            }
        }
        if (currentLine.Count > 0)
        {
            lines.Add(currentLine);
        }

        float fontHeight = font.GetHeight(g);
        float currentY = rect.Y;

        for (int i = 0; i < lines.Count; i++)
        {
            var lineWords = lines[i];
            bool isLastLine = (i == lines.Count - 1);

            if (isLastLine || lineWords.Count <= 1)
            {
                float totalWidth = lineWords.Sum(w => g.MeasureString(w, font).Width) + (lineWords.Count - 1) * spaceWidth;
                float x = rect.X + (rect.Width - totalWidth) / 2f;
                foreach (var word in lineWords)
                {
                    g.DrawString(word, font, brush, x, currentY);
                    x += g.MeasureString(word, font).Width + spaceWidth;
                }
            }
            else
            {
                float totalWordsWidth = lineWords.Sum(w => g.MeasureString(w, font).Width);
                float gapWidth = (rect.Width - totalWordsWidth) / (lineWords.Count - 1);

                float x = rect.X;
                foreach (var word in lineWords)
                {
                    g.DrawString(word, font, brush, x, currentY);
                    x += g.MeasureString(word, font).Width + gapWidth;
                }
            }

            currentY += fontHeight + 6;
        }
    }
}
