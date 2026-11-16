using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using MebKoordinatorluk.Core.Models;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public class SgkAuditDialog : Form
{
    private readonly SgkAuditReport _report;
    private readonly Action<string>? _onNavigate;

    private DataGridView _grid = null!;
    private ComboBox _cmbFilter = null!;
    private TextBox _txtSearch = null!;
    private Label _lblSummaryStatus = null!;
    private Button _btnProceed = null!;

    public SgkAuditDialog(SgkAuditReport report, Action<string>? onNavigate = null)
    {
        _report = report;
        _onNavigate = onNavigate;

        InitializeComponent();
        ApplyFilter();
    }

    private void InitializeComponent()
    {
        Text = "SGK e-Bildirge & Devamsızlık Çapraz Denetim Raporu — 3308 Pro";
        Size = new Size(1100, 680);
        MinimumSize = new Size(950, 550);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(248, 250, 252); // slate-50
        Font = new Font("Segoe UI", 9f);

        // 1. Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblIcon = new Label
        {
            Text = _report.HasErrors ? "❌" : (_report.HasWarnings ? "⚠️" : "✅"),
            Font = new Font("Segoe UI Emoji", 24f),
            Size = new Size(48, 48),
            Location = new Point(18, 12),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblTitle = new Label
        {
            Text = "SGK e-Bildirge & Devamsızlık Çapraz Denetim Raporu",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(70, 12),
            AutoSize = true
        };

        _lblSummaryStatus = new Label
        {
            Text = $"{_report.Year}/{_report.MonthIndex:D2} Dönemi | İncelenen Sigortalı: {_report.TotalStudentsAudited} | " +
                   $"Kritik Hata: {_report.ErrorCount} | Uyarı: {_report.WarningCount} | Bilgi: {_report.InfoCount}",
            Font = new Font("Segoe UI", 9f),
            ForeColor = _report.HasErrors ? Color.FromArgb(220, 38, 38) : (_report.HasWarnings ? Color.FromArgb(217, 119, 6) : Color.FromArgb(22, 163, 74)),
            Location = new Point(72, 38),
            AutoSize = true
        };

        pnlHeader.Controls.AddRange([lblIcon, lblTitle, _lblSummaryStatus]);

        // 2. Summary Badge Panel & Filter Bar
        var pnlTopControls = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 10, 20, 10)
        };

        // Badges (Left)
        int badgeX = 20;
        var pnlBadges = new FlowLayoutPanel
        {
            Location = new Point(badgeX, 10),
            Size = new Size(520, 36),
            BackColor = Color.Transparent,
            WrapContents = false
        };

        var badgeErrors = CreateBadge($"❌ {_report.ErrorCount} Kritik Hata", Color.FromArgb(254, 226, 226), Color.FromArgb(185, 28, 28));
        var badgeWarnings = CreateBadge($"⚠️ {_report.WarningCount} Uyarı", Color.FromArgb(254, 243, 199), Color.FromArgb(180, 83, 9));
        var badgeInfo = CreateBadge($"ℹ️ {_report.InfoCount} Bilgi", Color.FromArgb(224, 242, 254), Color.FromArgb(3, 105, 161));
        int cleanCount = Math.Max(0, _report.TotalStudentsAudited - _report.ProblematicStudentCount);
        var badgeClean = CreateBadge($"✅ {cleanCount} Sorunsuz", Color.FromArgb(220, 252, 231), Color.FromArgb(21, 128, 61));

        pnlBadges.Controls.AddRange([badgeErrors, badgeWarnings, badgeInfo, badgeClean]);

        // Filters (Right)
        var pnlFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 20, 0)
        };

        var lblFilt = new Label { Text = "Filtre:", AutoSize = true, Margin = new Padding(0, 6, 6, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Font = new Font("Segoe UI", 9f)
        };
        _cmbFilter.Items.AddRange(["Tüm Kayıtlar", "Yalnızca Kritik Hatalar", "Yalnızca Uyarılar", "Yalnızca Bilgilendirmeler"]);
        _cmbFilter.SelectedIndex = _report.HasErrors ? 1 : 0;
        _cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        _txtSearch = new TextBox
        {
            Width = 190,
            PlaceholderText = "İsim, TC veya Sınıf Ara...",
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(10, 0, 0, 0)
        };
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        pnlFilters.Controls.AddRange([lblFilt, _cmbFilter, _txtSearch]);

        pnlTopControls.Controls.AddRange([pnlBadges, pnlFilters]);

        // 3. Grid Panel
        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 10, 20, 10),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(30, 41, 59),
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 32
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSeverity", HeaderText = "Durum", Width = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTc", HeaderText = "TC Kimlik No", Width = 115 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Öğrenci Adı Soyadı", Width = 180 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClass", HeaderText = "Sınıf", Width = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCategory", HeaderText = "Kategori", Width = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMessage", HeaderText = "Tespit Edilen Hata / Uyarı", Width = 260 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAdvice", HeaderText = "Önerilen Çözüm", Width = 230 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDays", HeaderText = "Hesaplanan Günler", Width = 180 });

        _grid.RowPrePaint += Grid_RowPrePaint;
        pnlGrid.Controls.Add(_grid);

        // 4. Bottom Action Bar
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var btnExportExcel = new Button
        {
            Text = "📊 Excel Raporu Al",
            Size = new Size(160, 36),
            Location = new Point(20, 12),
            BackColor = Color.FromArgb(16, 185, 129), // emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnExportExcel.FlatAppearance.BorderSize = 0;
        btnExportExcel.Click += BtnExportExcel_Click;

        var btnGotoAttendance = new Button
        {
            Text = "📑 Devamsızlık Çizelgesine Git",
            Size = new Size(200, 36),
            Location = new Point(190, 12),
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(31, 41, 55),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = _onNavigate != null
        };
        btnGotoAttendance.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        btnGotoAttendance.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
            _onNavigate?.Invoke("attendance");
        };

        var btnCancel = new Button
        {
            Text = "Durdur / Düzeltmeye Dön",
            Size = new Size(180, 36),
            Location = new Point(700, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(75, 85, 99),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnProceed = new Button
        {
            Text = _report.HasErrors ? "⚠️ Yine de XML Oluştur" : "✅ XML e-Bildirgeyi Oluştur",
            Size = new Size(200, 36),
            Location = new Point(890, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = _report.HasErrors ? Color.FromArgb(239, 68, 68) : Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnProceed.FlatAppearance.BorderSize = 0;
        _btnProceed.Click += BtnProceed_Click;

        pnlBottom.Controls.AddRange([btnExportExcel, btnGotoAttendance, btnCancel, _btnProceed]);

        Controls.Add(pnlGrid);
        Controls.Add(pnlTopControls);
        Controls.Add(pnlHeader);
        Controls.Add(pnlBottom);
    }

    private static Label CreateBadge(string text, Color bg, Color fg)
    {
        return new Label
        {
            Text = text,
            BackColor = bg,
            ForeColor = fg,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 0, 8, 0)
        };
    }

    private void ApplyFilter()
    {
        _grid.Rows.Clear();

        var query = _report.Items.AsEnumerable();

        if (_cmbFilter.SelectedIndex == 1)
        {
            query = query.Where(x => x.Severity == SgkAuditSeverity.Error);
        }
        else if (_cmbFilter.SelectedIndex == 2)
        {
            query = query.Where(x => x.Severity == SgkAuditSeverity.Warning);
        }
        else if (_cmbFilter.SelectedIndex == 3)
        {
            query = query.Where(x => x.Severity == SgkAuditSeverity.Info);
        }

        var search = _txtSearch.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(x =>
                x.StudentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.TcNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.ClassName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Message.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            string icon = item.Severity switch
            {
                SgkAuditSeverity.Error => "❌ Hata",
                SgkAuditSeverity.Warning => "⚠️ Uyarı",
                _ => "ℹ️ Bilgi"
            };

            int rowIdx = _grid.Rows.Add(
                icon,
                item.TcNo,
                item.StudentName,
                item.ClassName,
                item.CategoryDisplayName,
                item.Message,
                item.Advice,
                item.CalculatedDaysInfo
            );

            _grid.Rows[rowIdx].Tag = item;
        }
    }

    private void Grid_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is SgkAuditItem item)
        {
            if (item.Severity == SgkAuditSeverity.Error)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242); // light red
                row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
            }
            else if (item.Severity == SgkAuditSeverity.Warning)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235); // light amber
                row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(240, 249, 255); // light blue
                row.DefaultCellStyle.ForeColor = Color.FromArgb(30, 64, 175);
            }
        }
    }

    private void BtnProceed_Click(object? sender, EventArgs e)
    {
        if (_report.HasErrors)
        {
            var confirm = MessageBox.Show(
                $"Raporda {_report.ErrorCount} adet KRİTİK HATA bulunmaktadır!\n\n" +
                "Bu hatalar düzeltilmeden oluşturulan XML dosyası SGK e-Bildirge sistemine yüklendiğinde HATA VERİP REDDEDİLEBİLİR veya hatalı gün bildiriminden dolayı idari para cezası doğurabilir.\n\n" +
                "Yine de XML bildirge dosyasını oluşturmak istiyor musunuz?",
                "Kritik Hata Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (_report.Items.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak denetim kaydı bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx",
            FileName = $"SGK_Capraz_Denetim_Raporu_{_report.Year}_{_report.MonthIndex:D2}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            Title = "SGK Çapraz Denetim Raporunu Excel Olarak Kaydet"
        };

        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("SGK Denetim Raporu");

            // Başlık
            ws.Cell(1, 1).Value = $"SGK e-BİLDİRGE & DEVAMSIZLIK ÇAPRAZ DENETİM RAPORU ({_report.Year}/{_report.MonthIndex:D2})";
            ws.Range(1, 1, 1, 8).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 13;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 28;

            // Özet Satırı
            ws.Cell(2, 1).Value = $"İncelenen Sigortalı: {_report.TotalStudentsAudited} | Kritik Hata: {_report.ErrorCount} | Uyarı: {_report.WarningCount} | Bilgi: {_report.InfoCount} | Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}";
            ws.Range(2, 1, 2, 8).Merge();
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(2).Height = 20;

            // Kolon Başlıkları
            string[] headers = ["Seviye", "TC Kimlik No", "Öğrenci Adı Soyadı", "Sınıf", "Kategori", "Tespit Edilen Durum / Hata", "Önerilen Çözüm", "Hesaplanan Günler"];
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(4, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); // slate-800
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            ws.Row(4).Height = 24;

            int row = 5;
            foreach (var item in _report.Items)
            {
                ws.Cell(row, 1).Value = item.SeverityDisplayName;
                ws.Cell(row, 2).Value = item.TcNo;
                ws.Cell(row, 3).Value = item.StudentName;
                ws.Cell(row, 4).Value = item.ClassName;
                ws.Cell(row, 5).Value = item.CategoryDisplayName;
                ws.Cell(row, 6).Value = item.Message;
                ws.Cell(row, 7).Value = item.Advice;
                ws.Cell(row, 8).Value = item.CalculatedDaysInfo;

                if (item.Severity == SgkAuditSeverity.Error)
                {
                    ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226);
                }
                else if (item.Severity == SgkAuditSeverity.Warning)
                {
                    ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                }

                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(sfd.FileName);

            var open = MessageBox.Show(
                "SGK Çapraz Denetim Raporu başarıyla Excel dosyasına aktarıldı.\nDosyayı şimdi açmak ister misiniz?",
                "Excel Raporu Oluşturuldu",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (open == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel raporu oluşturulurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
