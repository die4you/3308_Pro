namespace MebKoordinatorluk.Core.Services;

public class AttendanceWeekGroup
{
    public string Title { get; set; } = "";
    public int StartDay { get; set; }
    public int EndDay { get; set; }
    public int StartWorkDay { get; set; }
    public int EndWorkDay { get; set; }
    public bool IsTransition { get; set; }
}

public static class AttendanceWeekHelper
{
    public static List<AttendanceWeekGroup> GetMonthWeekGroups(int year, int month)
    {
        var groups = new List<AttendanceWeekGroup>();
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int currentDay = 1;
        int weekNumber = 1;

        var firstDayDate = new DateTime(year, month, 1);
        if (firstDayDate.DayOfWeek != DayOfWeek.Monday)
        {
            // 1. gün Pazartesi değilse, ilk Pazartesiye kadar olan günler bir önceki ayın devamı olan geçiş grubudur
            int transitionEndDay = 1;
            while (transitionEndDay <= daysInMonth && new DateTime(year, month, transitionEndDay).DayOfWeek != DayOfWeek.Sunday)
            {
                transitionEndDay++;
            }
            transitionEndDay = Math.Min(transitionEndDay, daysInMonth);

            // İş günlerini bul (Pzt-Cum)
            int workStart = -1, workEnd = -1;
            for (int d = 1; d <= transitionEndDay; d++)
            {
                var dow = new DateTime(year, month, d).DayOfWeek;
                if (dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday)
                {
                    if (workStart == -1) workStart = d;
                    workEnd = d;
                }
            }

            string title = workStart != -1
                ? (workStart == workEnd ? $"Geçiş ({workStart})" : $"Geçiş ({workStart}-{workEnd})")
                : $"Geçiş ({1}-{transitionEndDay})";

            groups.Add(new AttendanceWeekGroup
            {
                Title = title,
                StartDay = 1,
                EndDay = transitionEndDay,
                StartWorkDay = workStart != -1 ? workStart : 1,
                EndWorkDay = workEnd != -1 ? workEnd : transitionEndDay,
                IsTransition = true
            });

            currentDay = transitionEndDay + 1;
        }

        // Şimdi Pazartesi ile başlayan tam/kalan haftaları oluştur
        while (currentDay <= daysInMonth)
        {
            int weekStart = currentDay;
            int weekEnd = currentDay;

            // Pazar gününe kadar veya ay sonuna kadar git
            while (weekEnd <= daysInMonth && new DateTime(year, month, weekEnd).DayOfWeek != DayOfWeek.Sunday)
            {
                weekEnd++;
            }
            weekEnd = Math.Min(weekEnd, daysInMonth);

            // İş günlerini bul (Pzt-Cum)
            int workStart = -1, workEnd = -1;
            for (int d = weekStart; d <= weekEnd; d++)
            {
                var dow = new DateTime(year, month, d).DayOfWeek;
                if (dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday)
                {
                    if (workStart == -1) workStart = d;
                    workEnd = d;
                }
            }

            string title;
            if (workStart != -1)
            {
                title = workStart == workEnd
                    ? $"{weekNumber}. Hafta ({workStart})"
                    : $"{weekNumber}. Hafta ({workStart}-{workEnd})";
            }
            else
            {
                title = $"{weekNumber}. Hafta ({weekStart}-{weekEnd})";
            }

            groups.Add(new AttendanceWeekGroup
            {
                Title = title,
                StartDay = weekStart,
                EndDay = weekEnd,
                StartWorkDay = workStart != -1 ? workStart : weekStart,
                EndWorkDay = workEnd != -1 ? workEnd : weekEnd,
                IsTransition = false
            });

            weekNumber++;
            currentDay = weekEnd + 1;
        }

        return groups;
    }

    /// <summary>
    /// Ayın en son iş gününü (Pazartesi-Cuma, hafta sonları ve resmi tatiller hariç) hesaplar.
    /// </summary>
    public static DateTime GetLastWorkingDayOfMonth(DateTime month)
    {
        int lastDay = DateTime.DaysInMonth(month.Year, month.Month);
        DateTime dt = new DateTime(month.Year, month.Month, lastDay);
        while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday || IsOfficialHoliday(dt))
        {
            dt = dt.AddDays(-1);
        }
        return dt;
    }

    public static bool IsOfficialHoliday(DateTime d)
    {
        if (d.Month == 1 && d.Day == 1) return true; // Yılbaşı
        if (d.Month == 4 && d.Day == 23) return true; // 23 Nisan Ulusal Egemenlik ve Çocuk Bayramı
        if (d.Month == 5 && d.Day == 1) return true; // 1 Mayıs Emek ve Dayanışma Günü
        if (d.Month == 5 && d.Day == 19) return true; // 19 Mayıs Atatürk'ü Anma, Gençlik ve Spor Bayramı
        if (d.Month == 7 && d.Day == 15) return true; // 15 Temmuz Demokrasi ve Milli Birlik Günü
        if (d.Month == 8 && d.Day == 30) return true; // 30 Ağustos Zafer Bayramı
        if (d.Month == 10 && d.Day == 29) return true; // 29 Ekim Cumhuriyet Bayramı
        return false;
    }
}
