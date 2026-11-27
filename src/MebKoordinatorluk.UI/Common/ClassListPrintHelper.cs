using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Text.RegularExpressions;
using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.UI.Common;

public class ClassListPrintOptions
{
    public string SelectedClassroom { get; set; } = "Tüm Sınıflar";
    public bool NewPagePerClass { get; set; } = true;
    public bool ShowTcNo { get; set; } = true;
    public bool ShowField { get; set; } = true;
    public bool ShowEnterprise { get; set; } = true;
    public bool ShowGender { get; set; } = true;
    public bool ShowPhone { get; set; } = false;
    public bool ShowParentContact { get; set; } = false;
    public bool ShowSignatureColumn { get; set; } = false;
    public bool Landscape { get; set; } = false;

    // Özel Sütun Seçenekleri (Veli Adı Soyadı vb.)
    public bool AddCustomColumn { get; set; } = false;
    public string CustomColumnTitle { get; set; } = "Veli Adı Soyadı";
    public string CustomColumnPlacement { get; set; } = "after-name";
    public int CustomColumnWidth { get; set; } = 140;
}

public static class ClassListPrintHelper
{
    public static PrintDocument CreatePrintDocument(
        List<Student> students,
        AppSettings settings,
        ClassListPrintOptions options)
    {
        var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = options.Landscape;
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

        // Filter and group students
        var filteredStudents = students.Where(s => s.IsActive).AsEnumerable();
        if (options.SelectedClassroom != "Tüm Sınıflar")
        {
            filteredStudents = filteredStudents.Where(s =>
                string.Equals(s.Classroom, options.SelectedClassroom, StringComparison.OrdinalIgnoreCase));
        }

        var classGroups = filteredStudents
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Classroom) ? "Sınıfsız" : s.Classroom.Trim())
            .OrderBy(g => NaturalSortKey(g.Key))
            .Select(g => new
            {
                ClassName = g.Key,
                Students = g.OrderBy(s => NaturalSortKey(s.StudentNumber ?? "0")).ThenBy(s => s.FullName).ToList()
            })
            .ToList();

        int groupIndex = 0;
        int studentIndexInGroup = 0;

        pd.BeginPrint += (sender, e) =>
        {
            groupIndex = 0;
            studentIndexInGroup = 0;
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
            int bottom = bounds.Bottom;
            int availableWidth = bounds.Width;

            using var fontHeaderMain = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var fontHeaderSub = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var fontClassTitle = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var fontColHeader = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fontCell = new Font("Segoe UI", 8.5f);
            using var fontFooter = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fontSmall = new Font("Segoe UI", 8f, FontStyle.Italic);

            using var penBorder = new Pen(Color.FromArgb(203, 213, 225), 1);
            using var penGrid = new Pen(Color.FromArgb(226, 232, 240), 1);
            using var brushHeaderBg = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var brushAltRow = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var brushText = new SolidBrush(Color.FromArgb(15, 23, 42));

            var formatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            var formatLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

            if (groupIndex >= classGroups.Count)
            {
                e.HasMorePages = false;
                return;
            }

            var currentGroup = classGroups[groupIndex];

            // 1. Draw Page Header
            int currentY = top;

            string valilik = string.IsNullOrWhiteSpace(settings.Valilik) ? "İSTANBUL VALİLİĞİ" : settings.Valilik.ToUpper(new CultureInfo("tr-TR"));
            string mem = !string.IsNullOrWhiteSpace(settings.DistrictMem) ? settings.DistrictMem.ToUpper(new CultureInfo("tr-TR")) : "";
            string schoolName = string.IsNullOrWhiteSpace(settings.SchoolName) ? "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ" : settings.SchoolName;
            string year = string.IsNullOrWhiteSpace(settings.AcademicYear) ? "2025-2026" : settings.AcademicYear;

            g.DrawString("T.C.", fontHeaderSub, brushText, new RectangleF(left, currentY, availableWidth, 18), formatCenter);
            currentY += 18;
            g.DrawString(valilik, fontHeaderSub, brushText, new RectangleF(left, currentY, availableWidth, 18), formatCenter);
            currentY += 18;
            if (!string.IsNullOrWhiteSpace(mem))
            {
                g.DrawString(mem, fontHeaderSub, brushText, new RectangleF(left, currentY, availableWidth, 18), formatCenter);
                currentY += 18;
            }
            g.DrawString(schoolName, fontHeaderMain, brushText, new RectangleF(left, currentY, availableWidth, 24), formatCenter);
            currentY += 26;

            string titleText = $"{year} EĞİTİM ÖĞRETİM YILI {currentGroup.ClassName.ToUpper(new CultureInfo("tr-TR"))} ÖĞRENCİ LİSTESİ";
            g.DrawString(titleText, fontClassTitle, brushText, new RectangleF(left, currentY, availableWidth, 22), formatCenter);
            currentY += 30;

            // 2. Define Table Columns
            var colDefs = BuildColumns(options, availableWidth);
            int tableHeaderHeight = 24;

            // Draw Table Header
            int curX = left;
            foreach (var col in colDefs)
            {
                var rect = new Rectangle(curX, currentY, col.Width, tableHeaderHeight);
                g.FillRectangle(brushHeaderBg, rect);
                g.DrawRectangle(penBorder, rect);

                var sf = col.AlignLeft ? formatLeft : formatCenter;
                g.DrawString(col.Title, fontColHeader, brushText, new RectangleF(rect.X + 4, rect.Y, rect.Width - 8, rect.Height), sf);
                curX += col.Width;
            }
            currentY += tableHeaderHeight;

            // 3. Draw Table Rows
            int rowHeight = options.ShowParentContact ? 30 : 22;
            int rowIndex = 0;

            while (studentIndexInGroup < currentGroup.Students.Count)
            {
                if (currentY + rowHeight + 105 > bottom)
                {
                    // Not enough space for more rows and footer -> page break
                    e.HasMorePages = true;
                    return;
                }

                var student = currentGroup.Students[studentIndexInGroup];
                rowIndex++;

                bool isAlt = rowIndex % 2 == 0;
                curX = left;

                for (int i = 0; i < colDefs.Count; i++)
                {
                    var col = colDefs[i];
                    var rect = new Rectangle(curX, currentY, col.Width, rowHeight);

                    if (isAlt)
                    {
                        g.FillRectangle(brushAltRow, rect);
                    }
                    g.DrawRectangle(penGrid, rect);

                    string val = GetColumnValue(col.Key, student, studentIndexInGroup + 1);
                    var sf = col.AlignLeft ? formatLeft : formatCenter;

                    g.DrawString(val, fontCell, brushText, new RectangleF(rect.X + 4, rect.Y, rect.Width - 8, rect.Height), sf);
                    curX += col.Width;
                }

                currentY += rowHeight;
                studentIndexInGroup++;
            }

            // Group finished! Draw Footer Summary & Signatures
            currentY += 12;

            int femaleCount = currentGroup.Students.Count(s => string.Equals(s.Gender, "Kız", StringComparison.OrdinalIgnoreCase));
            int maleCount = currentGroup.Students.Count - femaleCount;
            string summaryText = $"Toplam Öğrenci: {currentGroup.Students.Count}  (Kız: {femaleCount}, Erkek: {maleCount})";
            string dateText = $"Tarih: {DateTime.Now:dd.MM.yyyy}";

            g.DrawString(summaryText, fontFooter, brushText, new PointF(left, currentY));
            g.DrawString(dateText, fontFooter, brushText, new PointF(right - 120, currentY));

            // Signatures block - Liste sonundan aşağıya, sayfanın alt kısmına çekildi
            int sigY = Math.Max(currentY + 45, bottom - 65);

            if (sigY + 55 <= bottom + 25)
            {
                int sigWidth = 220;
                var rectTeacher = new RectangleF(left + 20, sigY, sigWidth, 55);
                var rectPrincipal = new RectangleF(right - sigWidth - 20, sigY, sigWidth, 55);

                g.DrawString("Sınıf Rehber Öğretmeni\n\n[ Adı Soyadı / İmza ]", fontCell, brushText, rectTeacher, formatCenter);

                string principal = string.IsNullOrWhiteSpace(settings.PrincipalName) ? "Mustafa KEMAL" : settings.PrincipalName;
                g.DrawString($"{principal}\nOkul Müdürü\n[ İmza ]", fontCell, brushText, rectPrincipal, formatCenter);
            }

            // Move to next group
            groupIndex++;
            studentIndexInGroup = 0;

            if (groupIndex < classGroups.Count)
            {
                e.HasMorePages = true;
            }
            else
            {
                e.HasMorePages = false;
            }
        };

        return pd;
    }

    private static string GetColumnValue(string key, Student s, int seqNo)
    {
        return key switch
        {
            "seq" => seqNo.ToString(),
            "no" => s.StudentNumber ?? "-",
            "tc" => string.IsNullOrWhiteSpace(s.TcKimlikNo) ? "-" : s.TcKimlikNo,
            "name" => s.FullName,
            "gender" => string.IsNullOrWhiteSpace(s.Gender) ? "-" : s.Gender,
            "field" => string.IsNullOrWhiteSpace(s.FieldName) ? "-" : s.FieldName,
            "enterprise" => string.IsNullOrWhiteSpace(s.EnterpriseName) ? "-" : s.EnterpriseName,
            "phone" => string.IsNullOrWhiteSpace(s.Phone) ? "-" : s.Phone,
            "parent" => FormatParentContact(s),
            "signature" => "",
            "custom" => "",
            _ => ""
        };
    }

    private static string FormatParentContact(Student s)
    {
        bool hasName = !string.IsNullOrWhiteSpace(s.ParentName);
        bool hasPhone = !string.IsNullOrWhiteSpace(s.ParentPhone);

        if (hasName && hasPhone)
        {
            return $"{s.ParentName.Trim()}\n{s.ParentPhone.Trim()}";
        }
        if (hasName) return s.ParentName.Trim();
        if (hasPhone) return s.ParentPhone.Trim();
        return "-";
    }

    private class ColumnDef
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public int Width { get; set; }
        public bool AlignLeft { get; set; }
    }

    private static List<ColumnDef> BuildColumns(ClassListPrintOptions opts, int totalWidth)
    {
        var cols = new List<ColumnDef>
        {
            new() { Key = "seq", Title = "Sıra", Width = 38, AlignLeft = false },
            new() { Key = "no", Title = "Okul No", Width = 62, AlignLeft = false }
        };

        if (opts.ShowTcNo)
        {
            cols.Add(new ColumnDef { Key = "tc", Title = "T.C. Kimlik No", Width = 95, AlignLeft = false });
        }

        // Adı Soyadı başlangıçta eklenir, genişliği diğer tüm sütunlara göre AUTO hesaplanacaktır
        cols.Add(new ColumnDef { Key = "name", Title = "Adı Soyadı", Width = 150, AlignLeft = true });

        if (opts.ShowGender)
        {
            cols.Add(new ColumnDef { Key = "gender", Title = "Cinsiyet", Width = 65, AlignLeft = false });
        }

        if (opts.ShowField)
        {
            cols.Add(new ColumnDef { Key = "field", Title = "Alan / Dal", Width = 120, AlignLeft = true });
        }

        if (opts.ShowEnterprise)
        {
            cols.Add(new ColumnDef { Key = "enterprise", Title = "Staj İşletmesi", Width = 135, AlignLeft = true });
        }

        if (opts.ShowPhone)
        {
            cols.Add(new ColumnDef { Key = "phone", Title = "Öğrenci Tel", Width = 95, AlignLeft = false });
        }

        if (opts.ShowParentContact)
        {
            cols.Add(new ColumnDef { Key = "parent", Title = "Veli (Ad & Tel)", Width = 145, AlignLeft = true });
        }

        if (opts.ShowSignatureColumn)
        {
            cols.Add(new ColumnDef { Key = "signature", Title = "İmza / Yoklama", Width = 85, AlignLeft = false });
        }

        // Özel Sütun Ekleme (Veli Adı Soyadı vb. - genişlik kullanıcı tanımlı)
        if (opts.AddCustomColumn && !string.IsNullOrWhiteSpace(opts.CustomColumnTitle))
        {
            int customW = opts.CustomColumnWidth > 0 ? opts.CustomColumnWidth : 140;
            var customCol = new ColumnDef
            {
                Key = "custom",
                Title = opts.CustomColumnTitle.Trim(),
                Width = customW,
                AlignLeft = true
            };

            int targetIdx = -1;
            switch (opts.CustomColumnPlacement?.ToLowerInvariant())
            {
                case "after-no":
                    targetIdx = cols.FindIndex(c => c.Key == "no");
                    break;
                case "after-tc":
                    targetIdx = cols.FindIndex(c => c.Key == "tc");
                    if (targetIdx == -1) targetIdx = cols.FindIndex(c => c.Key == "no");
                    break;
                case "after-name":
                    targetIdx = cols.FindIndex(c => c.Key == "name");
                    break;
                case "after-gender":
                    targetIdx = cols.FindIndex(c => c.Key == "gender");
                    if (targetIdx == -1) targetIdx = cols.FindIndex(c => c.Key == "name");
                    break;
                case "after-field":
                    targetIdx = cols.FindIndex(c => c.Key == "field");
                    if (targetIdx == -1) targetIdx = cols.FindIndex(c => c.Key == "name");
                    break;
                case "after-enterprise":
                    targetIdx = cols.FindIndex(c => c.Key == "enterprise");
                    break;
                case "end":
                default:
                    targetIdx = -1;
                    break;
            }

            if (targetIdx >= 0)
            {
                cols.Insert(targetIdx + 1, customCol);
            }
            else
            {
                int sigIdx = cols.FindIndex(c => c.Key == "signature");
                if (sigIdx >= 0)
                {
                    cols.Insert(sigIdx, customCol);
                }
                else
                {
                    cols.Add(customCol);
                }
            }
        }

        // Adı Soyadı sütununu AUTO yap: Diğer tüm sütunlar çıkarıldıktan sonra kalan alanın tamamını Adı Soyadı alsın
        int otherColsWidth = cols.Where(c => c.Key != "name").Sum(c => c.Width);
        int availableForName = totalWidth - otherColsWidth;

        var nameCol = cols.FirstOrDefault(c => c.Key == "name");
        if (nameCol != null)
        {
            if (availableForName >= 100)
            {
                nameCol.Width = availableForName;
            }
            else
            {
                // Çok fazla sütun varsa Adı Soyadı en az 110 kalsın, işletme veya özel sütundan dengelensin
                nameCol.Width = 110;
                int overflow = (otherColsWidth + 110) - totalWidth;
                if (overflow > 0)
                {
                    var flexibleCols = cols.Where(c => c.Key is "enterprise" or "field" or "custom" or "parent" or "phone").ToList();
                    int perColReduce = overflow / Math.Max(1, flexibleCols.Count);
                    foreach (var fc in flexibleCols)
                    {
                        fc.Width = Math.Max(70, fc.Width - perColReduce);
                    }
                }
            }
        }

        return cols;
    }

    private static string NaturalSortKey(string input)
    {
        return Regex.Replace(input ?? "", @"\d+", m => m.Value.PadLeft(10, '0'));
    }
}
