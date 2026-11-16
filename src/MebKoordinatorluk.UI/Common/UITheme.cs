using System.Drawing;
using System.Windows.Forms;

namespace MebKoordinatorluk.UI.Common;

public static class UITheme
{
    // Modern Renk Paleti (Slate & MEB Mavi)
    public static readonly Color Background = Color.FromArgb(248, 250, 252);     // #F8FAFC
    public static readonly Color Surface = Color.White;                          // #FFFFFF
    public static readonly Color Border = Color.FromArgb(226, 232, 240);          // #E2E8F0
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);           // #2563EB (Tailwind Blue 600)
    public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);      // #1D4ED8
    public static readonly Color Secondary = Color.FromArgb(241, 245, 249);       // #F1F5F9
    public static readonly Color SecondaryHover = Color.FromArgb(226, 232, 240);  // #E2E8F0
    public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);        // #0F172A
    public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);   // #64748B
    public static readonly Color Success = Color.FromArgb(22, 163, 74);          // #16A34A
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);            // #DC2626
    public static readonly Color DangerHover = Color.FromArgb(185, 28, 28);       // #B91C1C

    public static void ApplyPrimaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Primary;
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
        btn.Height = 36;
        btn.Padding = new Padding(12, 0, 12, 0);
    }

    public static void ApplySecondaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = Border;
        btn.FlatAppearance.BorderSize = 1;
        btn.BackColor = Secondary;
        btn.ForeColor = TextPrimary;
        btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        btn.Cursor = Cursors.Hand;
        btn.Height = 36;
        btn.Padding = new Padding(10, 0, 10, 0);
    }

    public static void ApplyDangerButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Danger;
        btn.ForeColor = Color.White;
        btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
        btn.Height = 36;
        btn.Padding = new Padding(12, 0, 12, 0);
    }

    public static void ApplyGridStyle(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Border;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        // Başlık (Header) Stili
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        // Satır Stilleri
        grid.RowsDefaultCellStyle.BackColor = Surface;
        grid.RowsDefaultCellStyle.ForeColor = TextPrimary;
        grid.RowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
        grid.RowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 58, 138);
        grid.RowsDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        grid.RowTemplate.Height = 36;
    }

    public static void ApplyModernGridStyle(DataGridView grid) => ApplyGridStyle(grid);
}
