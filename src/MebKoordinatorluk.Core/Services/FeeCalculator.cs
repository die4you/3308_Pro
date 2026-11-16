using System.Globalization;
using System.Text.RegularExpressions;
using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.Core.Services;

public class FeeCalculationRow
{
    public Student Student { get; set; } = null!;
    public string EnterpriseName { get; set; } = "Atanmadı";
    public string EducationType { get; set; } = "Örgün Eğitim";
    public string Category { get; set; } = "%30";
    public string RateText { get; set; } = "%30";
    public decimal MinWage { get; set; }
    public decimal FullWage { get; set; }
    public decimal DailyWage { get; set; }
    public decimal KesintiTutari { get; set; }
    public decimal OdenecekTutar { get; set; }
    public decimal DevletKatkisi { get; set; }
    public decimal IsletmeMaliyeti { get; set; }
    public decimal DevamGunSayisi { get; set; }
    public decimal DevamsizGunSayisi { get; set; }
    public decimal BosGunSayisi { get; set; }
    public decimal ToplamKesintiGunSayisi => DevamsizGunSayisi + BosGunSayisi;
    public string Note { get; set; } = string.Empty;
}

public class MonthlyFeeSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int StudentCount { get; set; }
    public int Kat1Count { get; set; } // %30 alan öğrenci sayısı
    public int Kat2Count { get; set; } // 1/2 alan öğrenci sayısı
    public decimal TotalGross { get; set; }
    public decimal TotalDeduction { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalDevletKatkisi { get; set; }
    public decimal TotalIsletmeMaliyeti { get; set; }
    public List<FeeCalculationRow> Rows { get; set; } = new();
}

public static class FeeCalculator
{
    private static bool TryParseDate(string? str, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(str)) return false;
        return DateTime.TryParse(str, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out dt)
            || DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
    }

    public static (int Grade, string Branch) ParseClassAndBranch(string? classroom)
    {
        if (string.IsNullOrWhiteSpace(classroom))
            return (0, string.Empty);

        var match = Regex.Match(classroom.Trim(), @"(?:MESEM\s*)?(\d{1,2})\s*[-/]?\s*([A-Za-zÇĞİÖŞÜçğıöşü]*)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            int.TryParse(match.Groups[1].Value, out var grade);
            string branch = match.Groups[2].Value.ToUpperInvariant();
            return (grade, branch);
        }

        return (0, string.Empty);
    }

    public static int GetGradeLevel(Student s, SchoolClass? cls)
    {
        string classroom = (s.Classroom ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(classroom) && cls != null)
            classroom = (cls.Name ?? string.Empty).Trim();

        var (gradeFromClass, _) = ParseClassAndBranch(classroom);
        if (gradeFromClass > 0)
            return gradeFromClass;

        if (cls != null && !string.IsNullOrWhiteSpace(cls.Name))
        {
            var (clsGrade, _) = ParseClassAndBranch(cls.Name);
            if (clsGrade > 0)
                return clsGrade;
        }

        if (!string.IsNullOrWhiteSpace(s.Grade) && int.TryParse(s.Grade.Trim(), out var g))
            return g;

        return 0; // Tanımlanamayan veya geçersiz sınıf
    }

    public static string GetEducationType(Student s, SchoolClass? cls)
    {
        if (!string.IsNullOrWhiteSpace(s.EducationType))
            return s.EducationType.Trim();

        if (cls != null && !string.IsNullOrWhiteSpace(cls.EducationType))
            return cls.EducationType.Trim();

        return IsMesem(s, cls) ? "MESEM" : "Örgün Eğitim";
    }

    public static bool IsMesem(Student s, SchoolClass? cls)
    {
        if (!string.IsNullOrWhiteSpace(s.EducationType) && s.EducationType.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (cls != null)
        {
            if (!string.IsNullOrWhiteSpace(cls.EducationType) && cls.EducationType.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrWhiteSpace(cls.InstitutionType) && cls.InstitutionType.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        if (!string.IsNullOrWhiteSpace(s.Classroom) && s.Classroom.IndexOf("MESEM", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public static bool IsEligibleForFeeCalculation(Student s, SchoolClass? cls)
    {
        // 1. İdarecinin sınıf tanımındaki "İşletmeye Gidiyor" (HasInternship) seçimi esastır:
        if (cls != null)
        {
            return cls.HasInternship;
        }

        // 2. Sınıf nesnesi bulunamazsa kanuni varsayılan yedek kural:
        int grade = GetGradeLevel(s, cls);
        bool isMesem = IsMesem(s, cls);

        if (isMesem)
        {
            // MESEM 9-10-11-12. sınıf öğrencileri
            return grade is 9 or 10 or 11 or 12;
        }

        // Örgün eğitim sadece 12. sınıf öğrencileri
        return grade == 12;
    }

    public static bool IsHalfWage(Student s, SchoolClass? cls)
    {
        // MEB 3308 kuralı:
        // - Örgün Eğitim 12. sınıf öğrencileri: %30
        // - MESEM 9, 10, 11. sınıf öğrencileri: %30
        // - SADECE MESEM 12. sınıf öğrencileri: 1/2 (%50)
        return IsMesem(s, cls) && GetGradeLevel(s, cls) == 12;
    }

    public static int ParseStudentNumber(string? num)
    {
        if (string.IsNullOrWhiteSpace(num)) return int.MaxValue;
        var trimmed = num.Trim();
        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\d+");
        if (match.Success && int.TryParse(match.Value, out int val))
            return val;
        var anyMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"\d+");
        if (anyMatch.Success && int.TryParse(anyMatch.Value, out int anyVal))
            return anyVal;
        return int.MaxValue;
    }

    private static string GetTurkishMonthName(int month) => month switch
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
        _ => $"{month}. Ay"
    };

    public static (DateTime EffectiveStart, DateTime EffectiveEnd, bool HasNotStarted, bool HasExited) GetStudentEffectivePeriod(
        Student student,
        int year,
        int month,
        AppSettings? settings = null,
        SchoolClass? cls = null)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = new DateTime(year, month, daysInMonth);

        bool isMesem = IsMesem(student, cls);

        DateTime termStart;
        DateTime termEnd;

        if (isMesem)
        {
            // MESEM dönem takvimine bağlı değildir, yıl boyu stajdadır
            termStart = monthStart;
            termEnd = monthEnd;
        }
        else
        {
            int schoolYearStart = month >= 9 ? year : year - 1;
            int schoolYearEnd = schoolYearStart + 1;

            DateTime defaultStart = new DateTime(schoolYearStart, 9, 9);
            DateTime defaultEnd = new DateTime(schoolYearEnd, 6, 20);

            if (settings != null)
            {
                if (TryParseDate(settings.OrgunStartDate, out var sDate))
                    defaultStart = sDate;
                if (TryParseDate(settings.OrgunEndDate, out var eDate))
                    defaultEnd = eDate;
            }

            if (cls != null)
            {
                if (TryParseDate(cls.StartDate, out var csDate))
                    defaultStart = csDate;
                if (TryParseDate(cls.EndDate, out var ceDate))
                    defaultEnd = ceDate;
            }

            termStart = defaultStart;
            termEnd = defaultEnd;
        }

        // Bireysel İşe Giriş Tarihi (SgkEntryDate)
        DateTime effectiveStart;
        if (student.SgkEntryDate.HasValue)
        {
            var entryDate = student.SgkEntryDate.Value.Date;
            if (isMesem)
            {
                effectiveStart = entryDate;
            }
            else
            {
                // Örgün eğitimde Eylül ayında okul açılışından önce staj başlamaz
                effectiveStart = (month == 9 && termStart.Month == 9 && entryDate < termStart)
                    ? termStart
                    : entryDate;
            }
        }
        else
        {
            effectiveStart = isMesem ? monthStart : (month == 9 && termStart.Month == 9 ? termStart : monthStart);
        }

        // Bireysel İşten Çıkış Tarihi (SgkExitDate)
        DateTime effectiveEnd;
        if (student.SgkExitDate.HasValue)
        {
            var exitDate = student.SgkExitDate.Value.Date;
            if (isMesem)
            {
                effectiveEnd = exitDate;
            }
            else
            {
                // Örgün eğitimde Haziran ayında okul kapanışından sonra staj devam etmez
                effectiveEnd = (month == 6 && termEnd.Month == 6 && exitDate > termEnd)
                    ? termEnd
                    : exitDate;
            }
        }
        else
        {
            effectiveEnd = isMesem ? monthEnd : (month == 6 && termEnd.Month == 6 ? termEnd : monthEnd);
        }

        bool hasNotStarted = effectiveStart > monthEnd;
        bool hasExited = !hasNotStarted && (effectiveEnd < monthStart || effectiveStart > effectiveEnd);

        return (effectiveStart, effectiveEnd, hasNotStarted, hasExited);
    }

    private static string GetDefaultDayCode(
        Student s,
        int year,
        int month,
        int day,
        AppSettings? settings,
        List<SchoolClass>? classes)
    {
        var date = new DateTime(year, month, day);
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return "-";

        if (s.SgkEntryDate.HasValue && date < s.SgkEntryDate.Value.Date)
            return "";

        if (s.SgkExitDate.HasValue && date > s.SgkExitDate.Value.Date)
            return "";

        SchoolClass? cls = null;
        if (classes != null && !string.IsNullOrWhiteSpace(s.Classroom))
        {
            cls = classes.FirstOrDefault(c => string.Equals(c.Name, s.Classroom, StringComparison.OrdinalIgnoreCase));
        }

        // MESEM öğrencileri dönem takvimine bağlı değildir, yıl boyu stajdadır
        if (IsMesem(s, cls))
        {
            return "X";
        }

        int schoolYearStart = month >= 9 ? year : year - 1;
        int schoolYearEnd = schoolYearStart + 1;

        DateTime defaultStart = new DateTime(schoolYearStart, 9, 9);
        DateTime defaultEnd = new DateTime(schoolYearEnd, 6, 20);

        if (settings != null)
        {
            if (TryParseDate(settings.OrgunStartDate, out var sDate))
                defaultStart = sDate;
            if (TryParseDate(settings.OrgunEndDate, out var eDate))
                defaultEnd = eDate;
        }

        if (cls != null)
        {
            if (TryParseDate(cls.StartDate, out var csDate))
                defaultStart = csDate;
            if (TryParseDate(cls.EndDate, out var ceDate))
                defaultEnd = ceDate;
        }

        // Eylül ayında okul başlangıcından önceki günler boş ("") olmalıdır
        if (month == 9 && defaultStart.Month == 9 && date < defaultStart.Date)
            return "";

        // Haziran ayında okul bitişinden sonraki günler boş ("") olmalıdır
        if (month == 6 && defaultEnd.Month == 6 && date > defaultEnd.Date)
            return "";

        return "X";
    }

    public static MonthlyFeeSummary CalculateMonthlyFees(
        int year,
        int month,
        decimal minimumWage,
        List<Student> students,
        List<Enterprise> enterprises,
        List<AttendanceEntry> attendanceEntries,
        AppSettings? settings = null,
        List<SchoolClass>? classes = null)
    {
        var summary = new MonthlyFeeSummary
        {
            Year = year,
            Month = month
        };

        var enterpriseMap = enterprises.ToDictionary(e => e.Id, e => e);
        var studentAttendanceMap = attendanceEntries
            .Where(a => a.Year == year && a.Month == month)
            .GroupBy(a => a.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        int daysInMonth = DateTime.DaysInMonth(year, month);
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = new DateTime(year, month, daysInMonth);

        foreach (var student in students.Where(s => s.IsActive).OrderBy(s => s.Classroom).ThenBy(s => ParseStudentNumber(s.StudentNumber)))
        {
            SchoolClass? cls = null;
            if (classes != null && !string.IsNullOrWhiteSpace(student.Classroom))
            {
                cls = classes.FirstOrDefault(c => string.Equals(c.Name, student.Classroom, StringComparison.OrdinalIgnoreCase));
            }

            // Sadece MESEM 9-10-11-12 ve Örgün Eğitim 12. sınıf öğrencileri listelenir
            if (!IsEligibleForFeeCalculation(student, cls))
                continue;

            string eduType = GetEducationType(student, cls);
            bool isMesemStudent = IsMesem(student, cls);

            // Kategori kuralı:
            // Örgün eğitim 12. sınıf -> %30
            // MESEM 9, 10, 11 -> %30
            // MESEM 12. sınıf -> 1/2
            bool isHalf = IsHalfWage(student, cls);
            var rate = isHalf ? 0.50m : 0.30m;
            var rateText = isHalf ? "1/2" : "%30";

            var fullMonthlyWage = Math.Round(minimumWage * rate, 2);
            var dailyWage = fullMonthlyWage / 30m;

            var dayEntryMap = new Dictionary<int, string>();
            if (studentAttendanceMap.TryGetValue(student.Id, out var entries))
            {
                foreach (var entry in entries)
                {
                    dayEntryMap[entry.DayOfMonth] = entry.Code ?? string.Empty;
                }
            }

            Enterprise? ent = null;
            string entName = "Atanmadı";
            if (student.EnterpriseId.HasValue && enterpriseMap.TryGetValue(student.EnterpriseId.Value, out var foundEnt))
            {
                ent = foundEnt;
                entName = foundEnt.Name;
            }

            // Öğrencinin aktif staj dönemi (İşe giriş, işten çıkış ve okul takvimi entegrasyonu):
            var (effectiveStart, effectiveEnd, hasNotStarted, hasExited) =
                GetStudentEffectivePeriod(student, year, month, settings, cls);

            if (hasNotStarted)
            {
                var rowNotStarted = new FeeCalculationRow
                {
                    Student = student,
                    EnterpriseName = entName,
                    EducationType = eduType,
                    Category = rateText,
                    RateText = rateText,
                    MinWage = minimumWage,
                    FullWage = 0m,
                    DailyWage = dailyWage,
                    KesintiTutari = 0m,
                    OdenecekTutar = 0m,
                    DevamGunSayisi = 0m,
                    DevamsizGunSayisi = 0m,
                    BosGunSayisi = 0m,
                    Note = $"Öğrenci henüz işe başlamamıştır (İşe giriş: {effectiveStart:dd.MM.yyyy})."
                };
                summary.Rows.Add(rowNotStarted);
                if (isHalf) summary.Kat2Count++;
                else summary.Kat1Count++;
                continue;
            }

            if (hasExited)
            {
                var rowExited = new FeeCalculationRow
                {
                    Student = student,
                    EnterpriseName = entName,
                    EducationType = eduType,
                    Category = rateText,
                    RateText = rateText,
                    MinWage = minimumWage,
                    FullWage = 0m,
                    DailyWage = dailyWage,
                    KesintiTutari = 0m,
                    OdenecekTutar = 0m,
                    DevamGunSayisi = 0m,
                    DevamsizGunSayisi = 0m,
                    BosGunSayisi = 0m,
                    Note = $"Öğrenci işten ayrılmıştır (İşten çıkış: {effectiveEnd:dd.MM.yyyy})."
                };
                summary.Rows.Add(rowExited);
                if (isHalf) summary.Kat2Count++;
                else summary.Kat1Count++;
                continue;
            }

            int activeStartDay = effectiveStart > monthStart ? effectiveStart.Day : 1;
            int activeEndDay = effectiveEnd < monthEnd ? effectiveEnd.Day : daysInMonth;

            int activeDaysCount;
            decimal fullWage;
            bool isPartialMonth = false;

            if (activeStartDay == 1 && activeEndDay == daysInMonth)
            {
                // Tam ay (30 gün) esası
                activeDaysCount = 30;
                fullWage = fullMonthlyWage;
            }
            else
            {
                // Kıst dönem hesabı (Takvim gün sayısı)
                int calendarDays = activeEndDay - activeStartDay + 1;
                activeDaysCount = Math.Max(0, Math.Min(30, calendarDays));
                fullWage = Math.Round(activeDaysCount * dailyWage, 2);
                isPartialMonth = true;
            }

            decimal devamsizGun = 0;
            decimal bosGun = 0;

            // Sadece aktif staj dönemi içerisindeki günlerde devamsızlık aranır
            for (int d = activeStartDay; d <= activeEndDay; d++)
            {
                var date = new DateTime(year, month, d);
                // Hafta sonları (Cumartesi ve Pazar) öğrencinin resmi tatilidir, devamsızlık kesintisi yapılamaz
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    continue;

                string code;
                if (dayEntryMap.TryGetValue(d, out var savedCode))
                {
                    code = savedCode.Trim().ToUpperInvariant();
                }
                else
                {
                    code = GetDefaultDayCode(student, year, month, d, settings, classes);
                }

                if (string.IsNullOrWhiteSpace(code))
                {
                    bosGun += 1.0m;
                }
                else
                {
                    decimal absenceDays = (decimal)AttendanceCodeHelper.GetAbsenceDays(code);
                    if (absenceDays > 0)
                    {
                        if (absenceDays == 0.5m && cls != null && cls.IgnoreHalfDayDeduction)
                        {
                            // Yarım gün kesinti muafiyeti varsa kesinti yapma
                        }
                        else
                        {
                            devamsizGun += absenceDays;
                        }
                    }
                }
            }

            decimal totalDeductionDays = devamsizGun + bosGun;
            var kesinti = Math.Round(totalDeductionDays * dailyWage, 2);
            var odenecek = Math.Max(0m, fullWage - kesinti);
            var devamGun = Math.Max(0m, activeDaysCount - totalDeductionDays);

            string note;
            if (isPartialMonth)
            {
                string monthName = GetTurkishMonthName(month);
                string rangeStr = $"{activeStartDay}-{activeEndDay} {monthName}";
                if (student.SgkEntryDate.HasValue && student.SgkEntryDate.Value.Year == year && student.SgkEntryDate.Value.Month == month)
                {
                    rangeStr += $" (İşe Giriş: {student.SgkEntryDate.Value:dd.MM.yyyy})";
                }
                if (student.SgkExitDate.HasValue && student.SgkExitDate.Value.Year == year && student.SgkExitDate.Value.Month == month)
                {
                    rangeStr += $" (İşten Çıkış: {student.SgkExitDate.Value:dd.MM.yyyy})";
                }

                if (totalDeductionDays > 0)
                {
                    note = $"{rangeStr} dönemi ({activeDaysCount} gün) üzerinden hesaplandı. {totalDeductionDays:0.#} gün devamsızlık kesintisi yapılmıştır.";
                }
                else
                {
                    note = $"{rangeStr} dönemi ({activeDaysCount} gün) üzerinden hesaplandı. Tam devam.";
                }
            }
            else if (bosGun > 0 && devamsizGun > 0)
            {
                note = $"{devamsizGun:0.#} gün devamsızlık ve {bosGun:0.#} gün boş gün kesintisi yapılmıştır.";
            }
            else if (bosGun > 0)
            {
                note = $"{bosGun:0.#} gün boş gün kesintisi yapılmıştır.";
            }
            else if (devamsizGun > 0)
            {
                note = $"{devamsizGun:0.#} gün devamsızlık kesintisi yapılmıştır.";
            }
            else
            {
                note = "Tam devam";
            }

            decimal devletKatkisi;
            decimal isletmeMaliyeti;

            if (ent != null && ent.OptOutStateContribution)
            {
                // İşletme Devlet Katkısı İstemiyorsa: Devlet katkısı 0 TL, tamamı İşletme Maliyetidir.
                devletKatkisi = 0m;
                isletmeMaliyeti = odenecek;
            }
            else if (isMesemStudent)
            {
                // MESEM öğrencileri: Devlet katkısı reddedilmediği sürece maaşın %100'ü devlet katkısıdır.
                devletKatkisi = odenecek;
                isletmeMaliyeti = 0m;
            }
            else
            {
                // Örgün Eğitim stajyerleri: Personel sayısına göre (< 20 ise 2/3, >= 20 ise 1/3) Devlet Katkısı
                int empCount = ent?.EmployeeCount ?? 1;
                decimal stateRatio = (empCount < 20) ? (2m / 3m) : (1m / 3m);
                devletKatkisi = Math.Round(odenecek * stateRatio, 2);
                isletmeMaliyeti = odenecek - devletKatkisi;
            }

            var row = new FeeCalculationRow
            {
                Student = student,
                EnterpriseName = entName,
                EducationType = eduType,
                Category = rateText,
                RateText = rateText,
                MinWage = minimumWage,
                FullWage = fullWage,
                DailyWage = dailyWage,
                KesintiTutari = kesinti,
                OdenecekTutar = odenecek,
                DevletKatkisi = devletKatkisi,
                IsletmeMaliyeti = isletmeMaliyeti,
                DevamGunSayisi = devamGun,
                DevamsizGunSayisi = devamsizGun,
                BosGunSayisi = bosGun,
                Note = note
            };

            summary.Rows.Add(row);
            summary.TotalGross += fullWage;
            summary.TotalDeduction += kesinti;
            summary.TotalNet += odenecek;
            summary.TotalDevletKatkisi += devletKatkisi;
            summary.TotalIsletmeMaliyeti += isletmeMaliyeti;

            if (isHalf) summary.Kat2Count++;
            else summary.Kat1Count++;
        }

        summary.StudentCount = summary.Rows.Count;
        return summary;
    }
}
