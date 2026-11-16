using System;

namespace MebKoordinatorluk.Core.Entities;

public enum TaskFrequencyType
{
    RecurringMonthly = 0,       // Her Ay Tekrarla
    SpecificMonthEveryYear = 1, // Her Yıl Belirli Bir Ayda (Dönemsel: Eylül, Ocak, Haziran vb.)
    OneTimeSpecificMonth = 2    // Tek Seferlik Özel Bir Ay (Örn: 2026-11)
}

public class CalendarTask : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public int StartDay { get; set; } = 1;
    public int EndDay { get; set; } = 5;
    public string? RangeText { get; set; }

    public TaskFrequencyType FrequencyType { get; set; } = TaskFrequencyType.RecurringMonthly;
    public int? TargetMonth { get; set; } // 1..12 (SpecificMonthEveryYear için)
    public string? TargetYearMonth { get; set; } // "yyyy-MM" (OneTimeSpecificMonth için)

    public string? NavTarget { get; set; }
    public string? NavBtnText { get; set; }

    public bool IsSystemDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int OrderIndex { get; set; } = 0;

    public string DisplayRange => !string.IsNullOrWhiteSpace(RangeText)
        ? RangeText
        : $"{StartDay} – {EndDay} Arası";

    public string FrequencyDisplayText
    {
        get
        {
            return FrequencyType switch
            {
                TaskFrequencyType.RecurringMonthly => "🔁 Her Ay",
                TaskFrequencyType.SpecificMonthEveryYear => TargetMonth.HasValue && TargetMonth.Value >= 1 && TargetMonth.Value <= 12
                    ? $"📅 Her {GetMonthName(TargetMonth.Value)}"
                    : "📅 Belirli Ayda",
                TaskFrequencyType.OneTimeSpecificMonth => !string.IsNullOrWhiteSpace(TargetYearMonth)
                    ? $"📌 {TargetYearMonth}"
                    : "📌 Tek Seferlik",
                _ => "Bilinmiyor"
            };
        }
    }

    public static string GetMonthName(int month)
    {
        return month switch
        {
            1 => "Ocak",
            2 => "Şubat",
            3 => "Mart",
            4 => "Nisan",
            5 => "Mayıs",
            6 => "Haziran",
            7 => "Temmuz",
            8 => "Ağustos",
            9 => "Eylül",
            10 => "Ekim",
            11 => "Kasım",
            12 => "Aralık",
            _ => month.ToString()
        };
    }

    public bool IsApplicableForMonth(DateTime date)
    {
        if (!IsActive) return false;

        return FrequencyType switch
        {
            TaskFrequencyType.RecurringMonthly => true,
            TaskFrequencyType.SpecificMonthEveryYear => TargetMonth.HasValue && TargetMonth.Value == date.Month,
            TaskFrequencyType.OneTimeSpecificMonth => !string.IsNullOrWhiteSpace(TargetYearMonth) && TargetYearMonth == date.ToString("yyyy-MM"),
            _ => false
        };
    }
}
