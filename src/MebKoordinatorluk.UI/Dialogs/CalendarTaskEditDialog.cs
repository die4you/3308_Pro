using System;
using System.Drawing;
using System.Windows.Forms;
using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.UI.Dialogs;

public class CalendarTaskEditDialog : Form
{
    public CalendarTask TaskItem { get; private set; }

    private readonly TextBox _txtTitle;
    private readonly TextBox _txtSubtitle;
    private readonly NumericUpDown _numStartDay;
    private readonly NumericUpDown _numEndDay;
    private readonly TextBox _txtRangeText;
    private readonly ComboBox _cmbFrequency;
    private readonly Panel _pnlMonthPicker;
    private readonly ComboBox _cmbTargetMonth;
    private readonly Panel _pnlYearMonthPicker;
    private readonly DateTimePicker _dtpYearMonth;
    private readonly ComboBox _cmbNavTarget;
    private readonly TextBox _txtNavBtnText;
    private readonly CheckBox _chkIsActive;
    private readonly Label _lblInfo;

    public CalendarTaskEditDialog(CalendarTask? existing = null)
    {
        TaskItem = existing ?? new CalendarTask
        {
            StartDay = 1,
            EndDay = 5,
            RangeText = "1 – 5 Arası",
            FrequencyType = TaskFrequencyType.RecurringMonthly,
            IsActive = true
        };

        Text = existing == null ? "➕ Yeni İdari Görev Ekle" : "✏️ İdari Görevi Düzenle";
        Size = new Size(580, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(79, 70, 229), // Indigo-600
            Padding = new Padding(16, 12, 16, 12)
        };
        var lblHeadTitle = new Label
        {
            Text = existing == null ? "📌 Yeni İdari İşlem / Görev Tanımı" : "✏️ İdari İşlem ve Görev Güncelleme",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill
        };
        pnlHeader.Controls.Add(lblHeadTitle);
        Controls.Add(pnlHeader);

        int y = 70;
        const int labelX = 24;
        const int inputX = 160;
        const int inputW = 370;

        // 1. Görev Başlığı
        var lblTitle = new Label { Text = "Görev Başlığı *:", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtTitle = new TextBox { Location = new Point(inputX, y), Width = inputW, Text = TaskItem.Title };
        Controls.AddRange([lblTitle, _txtTitle]);
        y += 38;

        // 2. Açıklama / Alt Başlık
        var lblSub = new Label { Text = "Açıklama / İş Akışı:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtSubtitle = new TextBox { Location = new Point(inputX, y), Width = inputW, Text = TaskItem.Subtitle ?? "" };
        Controls.AddRange([lblSub, _txtSubtitle]);
        y += 38;

        // 3. Gün Aralığı
        var lblDays = new Label { Text = "Ayın Gün Aralığı *:", Location = new Point(labelX, y + 3), AutoSize = true };
        _numStartDay = new NumericUpDown { Location = new Point(inputX, y), Width = 65, Minimum = 1, Maximum = 31, Value = Math.Clamp(TaskItem.StartDay, 1, 31) };
        var lblTo = new Label { Text = "ile", Location = new Point(inputX + 75, y + 3), AutoSize = true };
        _numEndDay = new NumericUpDown { Location = new Point(inputX + 105, y), Width = 65, Minimum = 1, Maximum = 31, Value = Math.Clamp(TaskItem.EndDay, 1, 31) };
        var lblArasi = new Label { Text = "arası", Location = new Point(inputX + 180, y + 3), AutoSize = true };

        _txtRangeText = new TextBox { Location = new Point(inputX + 225, y), Width = 145, Text = TaskItem.RangeText ?? $"{TaskItem.StartDay} – {TaskItem.EndDay} Arası" };

        _numStartDay.ValueChanged += (s, e) => AutoUpdateRangeText();
        _numEndDay.ValueChanged += (s, e) => AutoUpdateRangeText();

        Controls.AddRange([lblDays, _numStartDay, lblTo, _numEndDay, lblArasi, _txtRangeText]);
        y += 42;

        // 4. Tekrarlama Sıklığı
        var lblFreq = new Label { Text = "Tekrarlama Sıklığı *:", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbFrequency = new ComboBox
        {
            Location = new Point(inputX, y),
            Width = inputW,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbFrequency.Items.Add("🔁 Aylık Yenileme (Her Ay Rutin Göster)");
        _cmbFrequency.Items.Add("📅 Yıllık Yenileme (Her Yıl Belirli Bir Ayda Göster)");
        _cmbFrequency.Items.Add("📌 Belirlenen Tarihte Gösterme (Tek Seferlik / Özel Ay)");
        _cmbFrequency.SelectedIndex = (int)TaskItem.FrequencyType;
        Controls.AddRange([lblFreq, _cmbFrequency]);
        y += 38;

        // 4.a Yıllık Yenileme için Ay Seçici Paneli
        _pnlMonthPicker = new Panel { Location = new Point(inputX, y), Size = new Size(inputW, 32), Visible = false };
        var lblMonthPick = new Label { Text = "Geçerli Ay:", Location = new Point(0, 5), AutoSize = true };
        _cmbTargetMonth = new ComboBox { Location = new Point(90, 2), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        for (int m = 1; m <= 12; m++)
        {
            _cmbTargetMonth.Items.Add($"{m} - {CalendarTask.GetMonthName(m)}");
        }
        int selMonth = TaskItem.TargetMonth.HasValue && TaskItem.TargetMonth.Value >= 1 && TaskItem.TargetMonth.Value <= 12
            ? TaskItem.TargetMonth.Value
            : DateTime.Today.Month;
        _cmbTargetMonth.SelectedIndex = selMonth - 1;
        _pnlMonthPicker.Controls.AddRange([lblMonthPick, _cmbTargetMonth]);
        Controls.Add(_pnlMonthPicker);

        // 4.b Tek Seferlik Tarih için Yıl-Ay Seçici Paneli
        _pnlYearMonthPicker = new Panel { Location = new Point(inputX, y), Size = new Size(inputW, 32), Visible = false };
        var lblYm = new Label { Text = "Hedef Yıl / Ay:", Location = new Point(0, 5), AutoSize = true };
        _dtpYearMonth = new DateTimePicker
        {
            Location = new Point(90, 2),
            Width = 140,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy - MMMM"
        };
        if (!string.IsNullOrWhiteSpace(TaskItem.TargetYearMonth) && DateTime.TryParseExact(TaskItem.TargetYearMonth + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsedYm))
        {
            _dtpYearMonth.Value = parsedYm;
        }
        else
        {
            _dtpYearMonth.Value = DateTime.Today;
        }
        _pnlYearMonthPicker.Controls.AddRange([lblYm, _dtpYearMonth]);
        Controls.Add(_pnlYearMonthPicker);

        _cmbFrequency.SelectedIndexChanged += (s, e) => UpdateFrequencyVisibility();
        UpdateFrequencyVisibility();
        y += 38;

        // 5. Hızlı Erişim Yönlendirmesi
        var lblNav = new Label { Text = "Hızlı Buton Hedefi:", Location = new Point(labelX, y + 3), AutoSize = true };
        _cmbNavTarget = new ComboBox
        {
            Location = new Point(inputX, y),
            Width = inputW,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbNavTarget.Items.Add(new NavItem("", "(Yönlendirme Butonu Olmasın)"));
        _cmbNavTarget.Items.Add(new NavItem("toplu-isegiris-cikis", "⚡ Sigorta İşlemleri (İşe Giriş / Çıkış)"));
        _cmbNavTarget.Items.Add(new NavItem("xml-ebildirge", "📑 SGK e-Bildirge & Primler"));
        _cmbNavTarget.Items.Add(new NavItem("fees", "💳 Ücret Dekontları & Devlet Desteği"));
        _cmbNavTarget.Items.Add(new NavItem("attendance", "📝 Devamsızlık Takibi"));
        _cmbNavTarget.Items.Add(new NavItem("coordinator-assignments", "📋 Koordinatörlük Görev Dağılımı"));
        _cmbNavTarget.Items.Add(new NavItem("forms", "📑 Görev Onay ve Evrak Formları"));
        _cmbNavTarget.Items.Add(new NavItem("students", "🎓 Öğrenci Listesi"));

        int navIndex = 0;
        for (int i = 0; i < _cmbNavTarget.Items.Count; i++)
        {
            if (_cmbNavTarget.Items[i] is NavItem item && item.Key == TaskItem.NavTarget)
            {
                navIndex = i;
                break;
            }
        }
        _cmbNavTarget.SelectedIndex = navIndex;
        Controls.AddRange([lblNav, _cmbNavTarget]);
        y += 38;

        // 6. Buton Üzeri Metin
        var lblBtnTxt = new Label { Text = "Buton Üzeri Yazı:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtNavBtnText = new TextBox { Location = new Point(inputX, y), Width = inputW, Text = TaskItem.NavBtnText ?? "" };
        _cmbNavTarget.SelectedIndexChanged += (s, e) =>
        {
            if (_cmbNavTarget.SelectedItem is NavItem item && !string.IsNullOrEmpty(item.Key))
            {
                if (string.IsNullOrWhiteSpace(_txtNavBtnText.Text))
                {
                    _txtNavBtnText.Text = item.Key switch
                    {
                        "toplu-isegiris-cikis" => "⚡ Sigorta İşlemleri",
                        "xml-ebildirge" => "📑 e-Bildirge",
                        "fees" => "💳 Ücret & Dekont",
                        "attendance" => "📝 Devamsızlık",
                        "coordinator-assignments" => "📋 Görevlendirme",
                        "forms" => "📑 Görev Formları",
                        "students" => "🎓 Öğrenciler",
                        _ => "Aç"
                    };
                }
            }
            else
            {
                _txtNavBtnText.Text = "";
            }
        };
        Controls.AddRange([lblBtnTxt, _txtNavBtnText]);
        y += 42;

        // 7. Aktiflik / Gizleme Durumu
        _chkIsActive = new CheckBox
        {
            Location = new Point(inputX, y),
            Width = inputW,
            Text = "✓ Bu Görev Aktif (İlgili ayda Dashboard takviminde gösterilsin)",
            Checked = TaskItem.IsActive,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(21, 128, 61), // green-700
            Cursor = Cursors.Hand
        };
        _chkIsActive.CheckedChanged += (s, e) =>
        {
            if (_chkIsActive.Checked)
            {
                _chkIsActive.Text = "✓ Bu Görev Aktif (İlgili ayda Dashboard takviminde gösterilsin)";
                _chkIsActive.ForeColor = Color.FromArgb(21, 128, 61);
            }
            else
            {
                _chkIsActive.Text = "🚫 Bu Görev Pasif / Gizli (Dashboard takviminde gösterilmez)";
                _chkIsActive.ForeColor = Color.FromArgb(185, 28, 28);
            }
        };
        Controls.Add(_chkIsActive);
        y += 40;

        // Bilgi Notu
        _lblInfo = new Label
        {
            Location = new Point(labelX, y),
            Size = new Size(inputW + 130, 40),
            Text = "💡 İpucu: Görevi silmek yerine 'Pasif' yaparak takvimde gizleyebilirsiniz. İleride dilediğinizde tek tıkla tekrar aktif hale getirebilirsiniz.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        Controls.Add(_lblInfo);
        y += 50;

        // Butonlar Panel
        var pnlButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(16, 10, 16, 10)
        };

        var btnCancel = new Button
        {
            Text = "Vazgeç",
            Size = new Size(95, 34),
            Dock = DockStyle.Right,
            BackColor = Color.FromArgb(243, 244, 246),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var btnSave = new Button
        {
            Text = "💾 Kaydet",
            Size = new Size(110, 34),
            Dock = DockStyle.Right,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += BtnSave_Click;

        pnlButtons.Controls.Add(btnSave);
        pnlButtons.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
        pnlButtons.Controls.Add(btnCancel);
        Controls.Add(pnlButtons);
    }

    private void AutoUpdateRangeText()
    {
        if (_numStartDay.Value > _numEndDay.Value)
        {
            _numEndDay.Value = _numStartDay.Value;
        }
        _txtRangeText.Text = $"{_numStartDay.Value} – {_numEndDay.Value} Arası";
    }

    private void UpdateFrequencyVisibility()
    {
        var freq = (TaskFrequencyType)_cmbFrequency.SelectedIndex;
        _pnlMonthPicker.Visible = freq == TaskFrequencyType.SpecificMonthEveryYear;
        _pnlYearMonthPicker.Visible = freq == TaskFrequencyType.OneTimeSpecificMonth;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var title = _txtTitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Lütfen görev başlığını giriniz!", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtTitle.Focus();
            return;
        }

        int start = (int)_numStartDay.Value;
        int end = (int)_numEndDay.Value;
        if (start > end)
        {
            MessageBox.Show("Başlangıç günü bitiş gününden büyük olamaz!", "Hatalı Gün Aralığı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _numStartDay.Focus();
            return;
        }

        var freq = (TaskFrequencyType)_cmbFrequency.SelectedIndex;

        TaskItem.Title = title;
        TaskItem.Subtitle = string.IsNullOrWhiteSpace(_txtSubtitle.Text) ? null : _txtSubtitle.Text.Trim();
        TaskItem.StartDay = start;
        TaskItem.EndDay = end;
        TaskItem.RangeText = string.IsNullOrWhiteSpace(_txtRangeText.Text) ? $"{start} – {end} Arası" : _txtRangeText.Text.Trim();
        TaskItem.FrequencyType = freq;

        if (freq == TaskFrequencyType.SpecificMonthEveryYear)
        {
            TaskItem.TargetMonth = _cmbTargetMonth.SelectedIndex + 1;
            TaskItem.TargetYearMonth = null;
        }
        else if (freq == TaskFrequencyType.OneTimeSpecificMonth)
        {
            TaskItem.TargetMonth = null;
            TaskItem.TargetYearMonth = _dtpYearMonth.Value.ToString("yyyy-MM");
        }
        else
        {
            TaskItem.TargetMonth = null;
            TaskItem.TargetYearMonth = null;
        }

        var nav = _cmbNavTarget.SelectedItem as NavItem;
        TaskItem.NavTarget = string.IsNullOrEmpty(nav?.Key) ? null : nav.Key;
        TaskItem.NavBtnText = string.IsNullOrEmpty(TaskItem.NavTarget) ? null : (string.IsNullOrWhiteSpace(_txtNavBtnText.Text) ? "Aç" : _txtNavBtnText.Text.Trim());
        TaskItem.IsActive = _chkIsActive.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }

    private class NavItem
    {
        public string Key { get; }
        public string Title { get; }
        public NavItem(string key, string title) { Key = key; Title = title; }
        public override string ToString() => Title;
    }
}
