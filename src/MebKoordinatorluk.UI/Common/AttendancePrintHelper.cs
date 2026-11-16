using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.UI.Common;

public class AttendancePrintStudentRow
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Classroom { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string EnterpriseName { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string[] DayCodes { get; set; } = Array.Empty<string>();
    public string TotalD { get; set; } = "0";
    public string TotalI { get; set; } = "0";
    public string TotalDays { get; set; } = "0";
}

public class AttendancePrintClassGroup
{
    public string ClassName { get; set; } = "";
    public string FieldName { get; set; } = "";
    public List<string> CoordinatorTeachers { get; set; } = new();
    public List<AttendancePrintStudentRow> Students { get; set; } = new();
}

public static class AttendancePrintHelper
{
    public static PrintDocument CreatePrintDocument(
        DataGridView grid,
        DateTime selectedMonth,
        AppSettings? settings)
    {
        settings ??= new AppSettings();

        int daysInMonth = DateTime.DaysInMonth(selectedMonth.Year, selectedMonth.Month);
        var studentRows = new List<AttendancePrintStudentRow>();

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            if (grid.Columns.Contains("colSelect") && row.Cells["colSelect"]?.Value is not true)
                continue;

            var student = row.Tag as Student;

            string studentNo = row.Cells["colNumber"]?.Value?.ToString() ?? student?.StudentNumber ?? "";
            string name = row.Cells["colName"]?.Value?.ToString() ?? student?.FullName ?? "";
            string classroom = row.Cells["colClass"]?.Value?.ToString() ?? student?.Classroom ?? "Sınıfsız";
            if (string.IsNullOrWhiteSpace(classroom)) classroom = "Sınıfsız";

            string teacher = student?.TeacherName ?? "";
            if (string.IsNullOrWhiteSpace(teacher)) teacher = "-";

            string enterprise = row.Cells["colEnterprise"]?.Value?.ToString() ?? student?.EnterpriseName ?? "-";
            if (string.IsNullOrWhiteSpace(enterprise)) enterprise = "-";

            string field = student?.FieldName ?? "";

            var dayCodes = new string[daysInMonth + 1];
            for (int d = 1; d <= daysInMonth; d++)
            {
                string code = "";
                if (grid.Columns.Contains($"colDay_{d}"))
                {
                    code = row.Cells[$"colDay_{d}"]?.Value?.ToString()?.Trim() ?? "";
                }
                dayCodes[d] = code;
            }

            string totalD = grid.Columns.Contains("colTotalD") ? (row.Cells["colTotalD"]?.Value?.ToString() ?? "0") : "0";
            string totalI = grid.Columns.Contains("colTotalI") ? (row.Cells["colTotalI"]?.Value?.ToString() ?? "0") : "0";
            string totalDays = grid.Columns.Contains("colTotalDays") ? (row.Cells["colTotalDays"]?.Value?.ToString() ?? "0") : "0";

            studentRows.Add(new AttendancePrintStudentRow
            {
                StudentId = student?.Id ?? 0,
                StudentNumber = studentNo,
                FullName = name,
                Classroom = classroom,
                TeacherName = teacher,
                EnterpriseName = enterprise,
                FieldName = field,
                DayCodes = dayCodes,
                TotalD = totalD,
                TotalI = totalI,
                TotalDays = totalDays
            });
        }

        var classGroups = studentRows
            .GroupBy(r => r.Classroom.Trim())
            .OrderBy(g => NaturalSortKey(g.Key))
            .Select(g => new AttendancePrintClassGroup
            {
                ClassName = g.Key,
                FieldName = g.Select(x => x.FieldName).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f)) ?? "",
                CoordinatorTeachers = g.Select(x => x.TeacherName)
                                       .Where(t => !string.IsNullOrWhiteSpace(t) && t != "-")
                                       .Distinct(StringComparer.CurrentCultureIgnoreCase)
                                       .OrderBy(t => t)
                                       .ToList(),
                Students = g.OrderBy(r => FeeCalculator.ParseStudentNumber(r.StudentNumber))
                            .ThenBy(r => r.FullName)
                            .ToList()
            })
            .ToList();

        var pd = new PrintDocument();
        pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);

        int groupIndex = 0;
        int studentIndexInGroup = 0;
        int pageInGroup = 1;

        pd.BeginPrint += (s, e) =>
        {
            groupIndex = 0;
            studentIndexInGroup = 0;
            pageInGroup = 1;
        };

        pd.PrintPage += (s, ev) =>
        {
            if (ev.Graphics == null) return;
            var g = ev.Graphics;
            var bounds = ev.MarginBounds;

            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            if (groupIndex >= classGroups.Count)
            {
                ev.HasMorePages = false;
                return;
            }

            var currentGroup = classGroups[groupIndex];

            // Renk ve fırça tanımları
            using var fontTc = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var fontHeaderMain = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            using var fontTitle = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var fontInfoBold = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fontInfoRegular = new Font("Segoe UI", 8.5f);
            using var fontColHeader = new Font("Segoe UI", 6.5f, FontStyle.Bold);
            using var fontCell = new Font("Segoe UI", 6.2f);
            using var fontCellBold = new Font("Segoe UI", 6.2f, FontStyle.Bold);
            using var fontFooterText = new Font("Segoe UI", 6.5f);
            using var fontFooterBold = new Font("Segoe UI", 6.8f, FontStyle.Bold);
            using var fontSignName = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var fontSignTitle = new Font("Segoe UI", 7f);
            using var fontSignSub = new Font("Segoe UI", 6.5f, FontStyle.Italic);

            using var brushDark = new SolidBrush(Color.FromArgb(15, 23, 42));
            using var brushGray = new SolidBrush(Color.FromArgb(71, 85, 105));
            using var brushMuted = new SolidBrush(Color.FromArgb(148, 163, 184));
            using var brushWhite = new SolidBrush(Color.White);
            using var brushRed = new SolidBrush(Color.FromArgb(220, 38, 38));
            using var brushBlue = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var brushAltRow = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var brushInfoBg = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var brushWknd = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var brushHdrBlue = new SolidBrush(Color.FromArgb(30, 58, 138));
            using var brushHdrWknd = new SolidBrush(Color.FromArgb(71, 85, 105));

            using var penGrid = new Pen(Color.FromArgb(203, 213, 225), 0.75f);
            using var penBorder = new Pen(Color.FromArgb(100, 116, 139), 1f);

            var formatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            var formatLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            var formatRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            int left = bounds.Left;
            int right = bounds.Right;
            int availableWidth = bounds.Width;
            int curY = bounds.Top;

            // 1. Antet ve Resmi Başlık
            string valilik = string.IsNullOrWhiteSpace(settings.Valilik) ? "İSTANBUL VALİLİĞİ" : settings.Valilik.ToUpper(new CultureInfo("tr-TR"));
            string mem = !string.IsNullOrWhiteSpace(settings.DistrictMem) ? settings.DistrictMem.ToUpper(new CultureInfo("tr-TR")) : "";
            string okulLine = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ" : settings.SchoolName;
            string year = string.IsNullOrWhiteSpace(settings.AcademicYear) ? $"{selectedMonth.Year}-{selectedMonth.Year + 1}" : settings.AcademicYear;
            string monthName = selectedMonth.ToString("MMMM yyyy", new CultureInfo("tr-TR")).ToUpper(new CultureInfo("tr-TR"));
            string titleLine = $"{monthName} AYI İŞLETMELERDE MESLEKİ EĞİTİM / STAJ DEVAM-DEVAMSIZLIK ÇİZELGESİ";

            g.DrawString("T.C.", fontTc, brushDark, new RectangleF(left, curY, availableWidth, 14), formatCenter);
            curY += 13;
            g.DrawString(valilik, fontTc, brushDark, new RectangleF(left, curY, availableWidth, 14), formatCenter);
            curY += 13;
            if (!string.IsNullOrWhiteSpace(mem))
            {
                g.DrawString(mem, fontTc, brushDark, new RectangleF(left, curY, availableWidth, 14), formatCenter);
                curY += 13;
            }
            g.DrawString(okulLine, fontHeaderMain, brushDark, new RectangleF(left, curY, availableWidth, 17), formatCenter);
            curY += 17;
            g.DrawString(titleLine, fontTitle, brushHdrBlue, new RectangleF(left, curY, availableWidth, 18), formatCenter);
            curY += 20;

            // 2. Bilgi Şeridi (Sınıf, Alan, Öğretim Yılı, Sayfa)
            int infoBoxH = 22;
            var infoBoxRect = new Rectangle(left, curY, availableWidth, infoBoxH);
            g.FillRectangle(brushInfoBg, infoBoxRect);
            g.DrawRectangle(penBorder, infoBoxRect);

            string infoLeft = $"Sınıf / Şube: {currentGroup.ClassName}";
            if (!string.IsNullOrWhiteSpace(currentGroup.FieldName))
            {
                infoLeft += $"    |    Alan / Dal: {currentGroup.FieldName}";
            }
            infoLeft += $"    |    Öğretim Yılı: {year}";

            string infoRight = $"Sayfa: {pageInGroup}";

            g.DrawString(infoLeft, fontInfoBold, brushDark, new RectangleF(left + 8, curY, availableWidth - 120, infoBoxH), formatLeft);
            g.DrawString(infoRight, fontInfoRegular, brushGray, new RectangleF(right - 100, curY, 92, infoBoxH), formatRight);

            curY += infoBoxH + 5;

            // 3. Tablo Sütunlarını İnşa Et
            var colDefs = BuildColumns(daysInMonth, selectedMonth, availableWidth);
            int topBandH = 13;
            int subBandH = 15;
            int totalHdrH = topBandH + subBandH;

            // --- Üst Hafta Bandı Çizimi ---
            using var fontWeekHdr = new Font("Segoe UI", 6.8f, FontStyle.Bold);
            using var fontSectionHdr = new Font("Segoe UI", 6f, FontStyle.Bold);

            // 1. Sol Kısım: Öğrenci ve İşletme Bilgileri
            int leftColsWidth = colDefs.Where(c => c.Key is "seq" or "no" or "name" or "teacher" or "enterprise").Sum(c => c.Width);
            if (leftColsWidth > 0)
            {
                var rectInfo = new Rectangle(left, curY, leftColsWidth, topBandH);
                g.FillRectangle(brushInfoBg, rectInfo);
                g.DrawRectangle(penBorder, rectInfo);
                g.DrawString("ÖĞRENCİ VE İŞLETME BİLGİLERİ", fontSectionHdr, brushDark, rectInfo, formatCenter);
            }

            // 2. Hafta Grupları
            var weekGroups = AttendanceWeekHelper.GetMonthWeekGroups(selectedMonth.Year, selectedMonth.Month);
            int weekX = left + leftColsWidth;
            foreach (var wg in weekGroups)
            {
                int wgWidth = colDefs
                    .Where(c => c.Key.StartsWith("day_") && int.TryParse(c.Key[4..], out int d) && d >= wg.StartDay && d <= wg.EndDay)
                    .Sum(c => c.Width);

                if (wgWidth > 0)
                {
                    var rectWeek = new Rectangle(weekX, curY, wgWidth, topBandH);
                    var bgBrush = wg.IsTransition ? brushInfoBg : brushAltRow;
                    g.FillRectangle(bgBrush, rectWeek);
                    g.DrawRectangle(penBorder, rectWeek);
                    g.DrawString(wg.Title, fontWeekHdr, brushHdrBlue, rectWeek, formatCenter);
                    weekX += wgWidth;
                }
            }

            // 3. Sağ Kısım: Devamsızlık Toplamları
            int rightColsWidth = colDefs.Where(c => c.Key is "total_d" or "total_i" or "total_days").Sum(c => c.Width);
            if (rightColsWidth > 0)
            {
                var rectTotals = new Rectangle(weekX, curY, rightColsWidth, topBandH);
                g.FillRectangle(brushInfoBg, rectTotals);
                g.DrawRectangle(penBorder, rectTotals);
                g.DrawString("TOPLAM", fontSectionHdr, brushDark, rectTotals, formatCenter);
            }

            // --- Alt Sütun Başlıkları Çizimi ---
            int cx = left;
            for (int i = 0; i < colDefs.Count; i++)
            {
                var col = colDefs[i];
                var hdrBrush = col.IsWeekend ? brushHdrWknd : brushHdrBlue;
                var rect = new Rectangle(cx, curY + topBandH, col.Width, subBandH);
                g.FillRectangle(hdrBrush, rect);
                g.DrawRectangle(penBorder, rect);

                var sfH = col.AlignLeft ? formatLeft : formatCenter;
                g.DrawString(col.Title, fontColHeader, brushWhite, new RectangleF(rect.X + 1, rect.Y, rect.Width - 2, rect.Height), sfH);
                cx += col.Width;
            }
            curY += totalHdrH;

            // 4. Öğrenci Satırlarını Yaz
            int rowHeight = 18;
            int pageBottomLimit = bounds.Bottom - 76; // Alttaki 3'lü imza ve lejant için 76px pay bırak
            int startTableY = curY - totalHdrH;
            int rowsDrawnOnThisPage = 0;

            while (studentIndexInGroup < currentGroup.Students.Count)
            {
                if (curY + rowHeight > pageBottomLimit)
                {
                    // Sayfa sonuna ulaşıldı, bu sınıfın sonraki sayfasına geç
                    pageInGroup++;
                    ev.HasMorePages = true;
                    return;
                }

                var sRow = currentGroup.Students[studentIndexInGroup];
                bool isAlt = rowsDrawnOnThisPage % 2 == 1;

                if (isAlt)
                {
                    g.FillRectangle(brushAltRow, left, curY, availableWidth, rowHeight);
                }

                cx = left;
                for (int i = 0; i < colDefs.Count; i++)
                {
                    var col = colDefs[i];
                    var cellRect = new Rectangle(cx, curY, col.Width, rowHeight);

                    if (col.IsWeekend)
                    {
                        g.FillRectangle(brushWknd, cellRect);
                    }

                    g.DrawRectangle(penGrid, cellRect);

                    string val = GetCellValue(col.Key, sRow, studentIndexInGroup + 1);

                    Brush textBrush = brushDark;
                    Font textFont = fontCell;

                    if (col.IsWeekend)
                    {
                        textBrush = brushMuted;
                        if (string.IsNullOrWhiteSpace(val) || val == "-") val = "-";
                    }
                    else if (col.Key.StartsWith("day_"))
                    {
                        var trimmed = val.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && trimmed != "-")
                        {
                            if (string.Equals(trimmed, "X", StringComparison.OrdinalIgnoreCase))
                            {
                                textBrush = brushDark;
                            }
                            else if (string.Equals(trimmed, "D", StringComparison.OrdinalIgnoreCase))
                            {
                                textBrush = brushRed;
                                textFont = fontCellBold;
                            }
                            else
                            {
                                textBrush = brushBlue;
                                textFont = fontCellBold;
                            }
                        }
                    }
                    else if (col.Key == "total_d")
                    {
                        if (double.TryParse(val, out var dVal) && dVal > 0)
                        {
                            textBrush = brushRed;
                            textFont = fontCellBold;
                        }
                    }
                    else if (col.Key == "total_i")
                    {
                        if (double.TryParse(val, out var iVal) && iVal > 0)
                        {
                            textBrush = brushBlue;
                            textFont = fontCellBold;
                        }
                    }
                    else if (col.Key == "total_days")
                    {
                        if (double.TryParse(val, out var tVal) && tVal > 0)
                        {
                            textFont = fontCellBold;
                        }
                    }
                    else if (col.Key == "no" || col.Key == "name")
                    {
                        textFont = fontCellBold;
                    }

                    var sfCell = col.AlignLeft ? formatLeft : formatCenter;
                    g.DrawString(val, textFont, textBrush, new RectangleF(cellRect.X + 2, cellRect.Y, cellRect.Width - 4, cellRect.Height), sfCell);

                    cx += col.Width;
                }

                curY += rowHeight;
                rowsDrawnOnThisPage++;
                studentIndexInGroup++;
            }

            // Tablo dış çerçevesi
            g.DrawRectangle(penBorder, left, startTableY, availableWidth, curY - startTableY);

            // 5. Grup Bitti: Sayfa Altı Lejantı ve 3'lü Resmi İmza Bloğu
            // A. Lejant ve Sınıf Toplam Özeti
            int lejantY = bounds.Bottom - 64;
            string lejantText = "Kodlar: [X] Devamlı  |  [D] Özürsüz Devamsız  |  [İ] İzinli  |  [R] Raporlu  |  [S] Sevkli  |  [N] Nöbetçi  |  [F] Faaliyet  |  [-] Tatil/Hafta Sonu";

            int studentCount = currentGroup.Students.Count;
            double classTotalD = currentGroup.Students.Sum(s => double.TryParse(s.TotalD, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0);
            double classTotalI = currentGroup.Students.Sum(s => double.TryParse(s.TotalI, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0);
            string summaryText = $"Sınıf Mevcudu: {studentCount}   |   Top. Özürsüz (D): {classTotalD:0.#} gün   |   Top. İzinli (İ): {classTotalI:0.#} gün";

            g.DrawString(lejantText, fontFooterText, brushGray, new RectangleF(left, lejantY, availableWidth - 360, 14), formatLeft);
            g.DrawString(summaryText, fontFooterBold, brushDark, new RectangleF(right - 360, lejantY, 360, 14), formatRight);

            // Ayırıcı çizgi
            using var penSep = new Pen(Color.FromArgb(226, 232, 240), 1);
            g.DrawLine(penSep, left, lejantY + 14, right, lejantY + 14);

            // B. 3'lü Resmi İmza Bloğu
            int signY = bounds.Bottom - 46;
            int signColW = availableWidth / 3;

            // 1. Sol Blok: Koordinatör Öğretmen(ler)
            var rectTeacher = new RectangleF(left, signY, signColW, 46);
            DrawTeacherSignatures(g, currentGroup.CoordinatorTeachers, rectTeacher, fontSignName, fontSignTitle, fontSignSub, brushDark, brushGray, brushMuted, formatCenter);

            // 2. Orta Blok: Koordinatör Müdür Yardımcısı
            var rectVice = new RectangleF(left + signColW, signY, signColW, 46);
            string viceName = string.IsNullOrWhiteSpace(settings.VicePrincipalName) ? "Müdür Yardımcısı" : settings.VicePrincipalName;
            g.DrawString(viceName, fontSignName, brushDark, new RectangleF(rectVice.X, rectVice.Y, rectVice.Width, 14), formatCenter);
            g.DrawString("Koordinatör Müdür Yardımcısı", fontSignTitle, brushGray, new RectangleF(rectVice.X, rectVice.Y + 13, rectVice.Width, 12), formatCenter);
            g.DrawString("İmza", fontSignSub, brushMuted, new RectangleF(rectVice.X, rectVice.Y + 26, rectVice.Width, 12), formatCenter);

            // 3. Sağ Blok: Okul Müdürü
            var rectPrin = new RectangleF(left + 2 * signColW, signY, signColW, 46);
            string prinName = string.IsNullOrWhiteSpace(settings.PrincipalName) ? "Mustafa KEMAL" : settings.PrincipalName;
            g.DrawString(prinName, fontSignName, brushDark, new RectangleF(rectPrin.X, rectPrin.Y, rectPrin.Width, 14), formatCenter);
            g.DrawString("Okul Müdürü", fontSignTitle, brushGray, new RectangleF(rectPrin.X, rectPrin.Y + 13, rectPrin.Width, 12), formatCenter);
            g.DrawString("Uygundur / Mühür - İmza", fontSignSub, brushMuted, new RectangleF(rectPrin.X, rectPrin.Y + 26, rectPrin.Width, 12), formatCenter);

            // Sonraki sınıfa geç
            groupIndex++;
            studentIndexInGroup = 0;
            pageInGroup = 1;

            ev.HasMorePages = groupIndex < classGroups.Count;
        };

        return pd;
    }

    private static void DrawTeacherSignatures(
        Graphics g,
        List<string> teachers,
        RectangleF rect,
        Font fontName,
        Font fontTitle,
        Font fontSub,
        Brush brushDark,
        Brush brushGray,
        Brush brushMuted,
        StringFormat sfCenter)
    {
        if (teachers.Count == 0)
        {
            g.DrawString("..................................................", fontName, brushDark, new RectangleF(rect.X, rect.Y, rect.Width, 14), sfCenter);
            g.DrawString("Koordinatör Öğretmen", fontTitle, brushGray, new RectangleF(rect.X, rect.Y + 13, rect.Width, 12), sfCenter);
            g.DrawString("İmza", fontSub, brushMuted, new RectangleF(rect.X, rect.Y + 26, rect.Width, 12), sfCenter);
        }
        else if (teachers.Count == 1)
        {
            g.DrawString(teachers[0], fontName, brushDark, new RectangleF(rect.X, rect.Y, rect.Width, 14), sfCenter);
            g.DrawString("Koordinatör Öğretmen", fontTitle, brushGray, new RectangleF(rect.X, rect.Y + 13, rect.Width, 12), sfCenter);
            g.DrawString("İmza", fontSub, brushMuted, new RectangleF(rect.X, rect.Y + 26, rect.Width, 12), sfCenter);
        }
        else if (teachers.Count == 2)
        {
            // İki öğretmen varsa yan yana göster
            string names = $"{teachers[0]}    —    {teachers[1]}";
            g.DrawString(names, fontName, brushDark, new RectangleF(rect.X, rect.Y, rect.Width, 14), sfCenter);
            g.DrawString("Koordinatör Öğretmenler", fontTitle, brushGray, new RectangleF(rect.X, rect.Y + 13, rect.Width, 12), sfCenter);
            g.DrawString("İmza                                 İmza", fontSub, brushMuted, new RectangleF(rect.X, rect.Y + 26, rect.Width, 12), sfCenter);
        }
        else
        {
            // 3 ve daha fazla öğretmen varsa
            string names = string.Join("  •  ", teachers);
            g.DrawString(names, fontName, brushDark, new RectangleF(rect.X, rect.Y, rect.Width, 14), sfCenter);
            g.DrawString("Koordinatör Öğretmenler", fontTitle, brushGray, new RectangleF(rect.X, rect.Y + 13, rect.Width, 12), sfCenter);
            g.DrawString("İmza", fontSub, brushMuted, new RectangleF(rect.X, rect.Y + 26, rect.Width, 12), sfCenter);
        }
    }

    private class ColumnDef
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public int Width { get; set; }
        public bool AlignLeft { get; set; }
        public bool IsWeekend { get; set; }
    }

    private static List<ColumnDef> BuildColumns(int daysInMonth, DateTime selectedMonth, int totalWidth)
    {
        var cols = new List<ColumnDef>
        {
            new() { Key = "seq", Title = "#", Width = 26, AlignLeft = false },
            new() { Key = "no", Title = "Okul No", Width = 48, AlignLeft = false },
            new() { Key = "name", Title = "Adı Soyadı", Width = 115, AlignLeft = true },
            new() { Key = "teacher", Title = "Koordinatör Öğretmen", Width = 105, AlignLeft = true },
            new() { Key = "enterprise", Title = "Staj İşletmesi", Width = 110, AlignLeft = true }
        };

        for (int d = 1; d <= daysInMonth; d++)
        {
            var dt = new DateTime(selectedMonth.Year, selectedMonth.Month, d);
            bool isWeekend = dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;
            string dayInitial = GetDayInitial(dt.DayOfWeek);

            cols.Add(new ColumnDef
            {
                Key = $"day_{d}",
                Title = $"{d}\n{dayInitial}",
                Width = 18,
                AlignLeft = false,
                IsWeekend = isWeekend
            });
        }

        cols.Add(new ColumnDef { Key = "total_d", Title = "D", Width = 24, AlignLeft = false });
        cols.Add(new ColumnDef { Key = "total_i", Title = "İ", Width = 24, AlignLeft = false });
        cols.Add(new ColumnDef { Key = "total_days", Title = "Top.", Width = 32, AlignLeft = false });

        // Genişliği tam olarak sayfaya ölçekle
        int baseSum = cols.Sum(c => c.Width);
        if (baseSum > 0 && baseSum != totalWidth)
        {
            float scale = (float)totalWidth / baseSum;
            for (int i = 0; i < cols.Count; i++)
            {
                cols[i].Width = (int)(cols[i].Width * scale);
            }
            int diff = totalWidth - cols.Sum(c => c.Width);
            cols[cols.Count - 1].Width += diff;
        }

        return cols;
    }

    private static string GetCellValue(string key, AttendancePrintStudentRow row, int seqNo)
    {
        if (key == "seq") return seqNo.ToString();
        if (key == "no") return row.StudentNumber;
        if (key == "name") return row.FullName;
        if (key == "teacher") return row.TeacherName;
        if (key == "enterprise") return row.EnterpriseName;
        if (key == "total_d") return row.TotalD;
        if (key == "total_i") return row.TotalI;
        if (key == "total_days") return row.TotalDays;

        if (key.StartsWith("day_") && int.TryParse(key.Substring(4), out int day))
        {
            if (day >= 1 && day < row.DayCodes.Length)
            {
                return row.DayCodes[day];
            }
        }

        return "";
    }

    private static string GetDayInitial(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => "Pzt",
        DayOfWeek.Tuesday => "Sal",
        DayOfWeek.Wednesday => "Çar",
        DayOfWeek.Thursday => "Per",
        DayOfWeek.Friday => "Cum",
        DayOfWeek.Saturday => "Cmt",
        DayOfWeek.Sunday => "Paz",
        _ => ""
    };

    private static string NaturalSortKey(string input)
    {
        return Regex.Replace(input ?? "", @"\d+", m => m.Value.PadLeft(10, '0'));
    }
}
