using System;
using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.UI.Common;

namespace MebKoordinatorluk.UI.Dialogs;

public enum IsgPrintChoice
{
    Cancel,
    Preview,
    DirectPrint
}

public class IsgPrintOptionDialog : Form
{
    public IsgPrintChoice Choice { get; private set; } = IsgPrintChoice.Cancel;

    public IsgPrintOptionDialog(int studentCount)
    {
        InitializeComponent(studentCount);
    }

    private void InitializeComponent(int studentCount)
    {
        Text = "İSG Katılım Belgesi Yazdırma";
        Size = new Size(460, 220);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;

        var lblIcon = new Label
        {
            Text = "🖨️",
            Font = new Font("Segoe UI", 28f),
            Location = new Point(24, 20),
            Size = new Size(50, 50),
            TextAlign = ContentAlignment.MiddleCenter
        };

        string studentText = studentCount == 1 ? "1 öğrenci" : $"{studentCount} öğrenci";
        var lblTitle = new Label
        {
            Text = $"{studentText} için İSG Katılım Belgesi Basımı",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(80, 20),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Lütfen yapmak istediğiniz işlemi seçiniz. Önizleme ekranı üzerinden de belgeyi kontrol edip yazdırabilirsiniz.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(80, 48),
            Size = new Size(340, 40)
        };

        var btnPreview = new Button
        {
            Text = "🔍 Baskı Önizleme",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235), // Blue-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(24, 115),
            Size = new Size(135, 38),
            Cursor = Cursors.Hand
        };
        btnPreview.FlatAppearance.BorderSize = 0;
        btnPreview.Click += (s, e) =>
        {
            Choice = IsgPrintChoice.Preview;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnPrint = new Button
        {
            Text = "🖨️ Doğrudan Yazdır",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(16, 185, 129), // Emerald-600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(167, 115),
            Size = new Size(145, 38),
            Cursor = Cursors.Hand
        };
        btnPrint.FlatAppearance.BorderSize = 0;
        btnPrint.Click += (s, e) =>
        {
            Choice = IsgPrintChoice.DirectPrint;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "İptal",
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(320, 115),
            Size = new Size(100, 38),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) =>
        {
            Choice = IsgPrintChoice.Cancel;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.AddRange(new Control[] { lblIcon, lblTitle, lblSub, btnPreview, btnPrint, btnCancel });
    }
}
