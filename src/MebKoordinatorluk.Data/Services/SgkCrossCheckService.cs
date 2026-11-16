using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Models;
using MebKoordinatorluk.Core.Services;

namespace MebKoordinatorluk.Data.Services;

public class SgkCrossCheckService : ISgkCrossCheckService
{
    private readonly IAttendanceRepository? _attendanceRepo;

    public SgkCrossCheckService(IAttendanceRepository? attendanceRepo = null)
    {
        _attendanceRepo = attendanceRepo;
    }

    public async Task<SgkAuditReport> RunAuditAsync(
        int year,
        int monthIndex,
        List<Student> students,
        AppSettings settings,
        List<AttendanceEntry>? attendanceEntries = null)
    {
        return await Task.Run(async () =>
        {
            var report = new SgkAuditReport
            {
                Year = year,
                MonthIndex = monthIndex,
                TotalStudentsAudited = students.Count
            };

            // 1. Kurum / SGK Sicil ve Vergi No Kontrolü
            if (string.IsNullOrWhiteSpace(settings.SgkSicilNo) || settings.SgkSicilNo.StartsWith("10532010110044"))
            {
                report.Items.Add(new SgkAuditItem
                {
                    StudentId = 0,
                    TcNo = "-",
                    StudentName = "KURUM GENELİ",
                    ClassName = "-",
                    EnterpriseName = settings.SchoolName ?? "Okul Bilgisi",
                    Category = SgkAuditCategory.WorkplaceInfo,
                    Severity = string.IsNullOrWhiteSpace(settings.SgkSicilNo) ? SgkAuditSeverity.Error : SgkAuditSeverity.Warning,
                    Message = string.IsNullOrWhiteSpace(settings.SgkSicilNo)
                        ? "SGK İşyeri Sicil Numarası tanımlanmamış!"
                        : "SGK İşyeri Sicil Numarası örnek/varsayılan değerde görünüyor.",
                    Advice = "Ayarlar > Okul Bilgileri ekranından okulunuza ait gerçek 26 haneli SGK İşyeri Sicil Numarasını kaydediniz."
                });
            }

            if (string.IsNullOrWhiteSpace(settings.VergiNo))
            {
                report.Items.Add(new SgkAuditItem
                {
                    StudentId = 0,
                    TcNo = "-",
                    StudentName = "KURUM GENELİ",
                    ClassName = "-",
                    EnterpriseName = settings.SchoolName ?? "Okul Bilgisi",
                    Category = SgkAuditCategory.WorkplaceInfo,
                    Severity = SgkAuditSeverity.Warning,
                    Message = "Okul Vergi Kimlik Numarası boş bırakılmış.",
                    Advice = "SGK e-Bildirge XML şemasında Vergi No gereklidir. Ayarlar ekranından vergi numaranızı giriniz."
                });
            }

            // 2. Devamsızlık Verilerini Al
            var monthAttendance = attendanceEntries;
            if (monthAttendance == null && _attendanceRepo != null)
            {
                try
                {
                    monthAttendance = await _attendanceRepo.GetForMonthAsync(year, monthIndex);
                }
                catch
                {
                    monthAttendance = new List<AttendanceEntry>();
                }
            }
            monthAttendance ??= new List<AttendanceEntry>();

            if (monthAttendance.Count == 0 && students.Count > 0)
            {
                report.Items.Add(new SgkAuditItem
                {
                    StudentId = 0,
                    TcNo = "-",
                    StudentName = "TÜM ÖĞRENCİLER",
                    ClassName = "-",
                    EnterpriseName = "-",
                    Category = SgkAuditCategory.AttendanceCompleteness,
                    Severity = SgkAuditSeverity.Warning,
                    Message = $"{year}/{monthIndex:D2} dönemi için sisteme hiç devamsızlık işlenmemiş.",
                    Advice = "Devamsızlığı olan stajyer öğrenciler varsa öncelikle Devamsızlık Girişi ekranından girişleri tamamlayınız. Aksi halde tüm öğrenciler için 30 gün tam prim bildirilecektir."
                });
            }

            // 3. TC Kimlik No Mükerrerlik Kontrolü
            var tcGroups = students
                .Where(s => !string.IsNullOrWhiteSpace(s.TcKimlikNo))
                .GroupBy(s => s.TcKimlikNo!.Trim())
                .Where(g => g.Count() > 1);

            foreach (var grp in tcGroups)
            {
                var names = string.Join(", ", grp.Select(x => $"{x.FirstName} {x.LastName}".Trim()));
                report.Items.Add(new SgkAuditItem
                {
                    StudentId = grp.First().Id,
                    TcNo = grp.Key,
                    StudentName = names,
                    ClassName = !string.IsNullOrWhiteSpace(grp.First().Classroom) ? grp.First().Classroom : "-",
                    EnterpriseName = grp.First().EnterpriseName ?? "-",
                    Category = SgkAuditCategory.TcKimlikValidation,
                    Severity = SgkAuditSeverity.Error,
                    Message = $"Mükerrer TC Kimlik No tespit edildi! ({grp.Key})",
                    Advice = "SGK sistemi aynı TC Kimlik numarasına sahip mükerrer sigortalı bildirimlerini reddeder. Öğrenci kartlarını kontrol edip mükerrer kaydı kaldırınız."
                });
            }

            // 4. Öğrenci Bazlı Bireysel Çapraz Denetimler
            int daysInMonth = DateTime.DaysInMonth(year, monthIndex);

            foreach (var s in students)
            {
                string sName = $"{s.FirstName} {s.LastName}".Trim();
                string cName = !string.IsNullOrWhiteSpace(s.Classroom) ? s.Classroom : "-";
                string eName = s.EnterpriseName ?? "-";
                string tc = s.TcKimlikNo?.Trim() ?? "";

                // TC Doğrulama
                if (string.IsNullOrWhiteSpace(tc))
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = "-",
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.TcKimlikValidation,
                        Severity = SgkAuditSeverity.Error,
                        Message = "TC Kimlik No boş!",
                        Advice = "Öğrencinin 11 haneli TC kimlik numarasını giriniz."
                    });
                }
                else if (tc.Length != 11 || !tc.All(char.IsDigit))
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.TcKimlikValidation,
                        Severity = SgkAuditSeverity.Error,
                        Message = $"Geçersiz TC Kimlik No ({tc}). 11 haneli rakam olmalıdır.",
                        Advice = "Öğrencinin TC Kimlik numarasını kontrol edip düzeltiniz."
                    });
                }

                // Ad / Soyad Eksikliği
                if (string.IsNullOrWhiteSpace(s.FirstName) || string.IsNullOrWhiteSpace(s.LastName))
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.TcKimlikValidation,
                        Severity = SgkAuditSeverity.Error,
                        Message = "Öğrencinin Adı veya Soyadı eksik!",
                        Advice = "Öğrenci kartından adı ve soyadını eksiksiz giriniz."
                    });
                }

                // Tarih Tutarlılığı (Giriş / Çıkış)
                bool entryInMonth = s.SgkEntryDate.HasValue && s.SgkEntryDate.Value.Year == year && s.SgkEntryDate.Value.Month == monthIndex;
                bool exitInMonth = s.SgkExitDate.HasValue && s.SgkExitDate.Value.Year == year && s.SgkExitDate.Value.Month == monthIndex;

                if (s.SgkEntryDate.HasValue && s.SgkExitDate.HasValue && s.SgkEntryDate.Value.Date > s.SgkExitDate.Value.Date)
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.DateIntervalMismatch,
                        Severity = SgkAuditSeverity.Error,
                        Message = $"İşe giriş tarihi ({s.SgkEntryDate:dd.MM.yyyy}), işten çıkış tarihinden ({s.SgkExitDate:dd.MM.yyyy}) sonra olamaz!",
                        Advice = "Öğrenci sigorta tarihlerini düzeltiniz."
                    });
                }

                // Devamsızlık Kontrolü
                var studentAtt = monthAttendance.Where(a => a.StudentId == s.Id).ToList();
                var activeAtt = studentAtt.Where(a =>
                    (!entryInMonth || a.DayOfMonth >= s.SgkEntryDate!.Value.Day) &&
                    (!exitInMonth || a.DayOfMonth <= s.SgkExitDate!.Value.Day)
                ).ToList();

                double absentCount = 0;
                bool hasHealthReport = false;
                foreach (var a in activeAtt)
                {
                    absentCount += AttendanceCodeHelper.GetSgkAbsenceDays(a.Code);
                    if (AttendanceCodeHelper.IsHealthReport(a.Code))
                    {
                        hasHealthReport = true;
                    }
                }
                int absentDays = (int)Math.Round(absentCount);

                // SGK 2020/20 Genelgesi Parmak Hesabı (31 çeken aylarda 1 gün devamsızlıkta 30 gün prim, 0 gün eksik gün)
                var (paidDays, missingDays) = SgkCalculationHelper.CalculateSgkDays(
                    year,
                    monthIndex,
                    s.SgkEntryDate,
                    s.SgkExitDate,
                    absentDays);

                var absentCodes = activeAtt
                    .Where(a => AttendanceCodeHelper.GetSgkAbsenceDays(a.Code) > 0)
                    .Select(a => a.Code);
                string missingReasonCode = missingDays > 0
                    ? AttendanceCodeHelper.DetermineSgkMissingReasonCode(absentCodes)
                    : "00";
                string missingReasonDesc = missingDays > 0
                    ? AttendanceCodeHelper.GetSgkMissingReasonDescription(missingReasonCode)
                    : "-";

                string daysDetail = missingDays > 0
                    ? $"Ay Gün: {daysInMonth}g | Devamsız: {absentDays}g | Prim: {paidDays}g | Eksik: {missingDays}g ({missingReasonDesc})"
                    : $"Ay Gün: {daysInMonth}g | Devamsız: {absentDays}g | Prim: {paidDays}g | Eksik: 0g";

                // Ay içi işe giriş durumunda bilgilendirme
                if (entryInMonth && s.SgkEntryDate.HasValue)
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.DayCalculationMismatch,
                        Severity = SgkAuditSeverity.Info,
                        Message = $"Ay içi işe giriş ({s.SgkEntryDate:dd.MM.yyyy}): Başlama gününden ay sonuna kadar {paidDays} gün prim hesaplandı. Giriş öncesi süreler için eksik gün yazılmadı (Eksik Gün: {missingDays}).",
                        Advice = "SGK kuralı: Ay içinde işe başlayan sigortalının işe giriş öncesindeki günleri eksik gün olarak bildirilmez.",
                        CalculatedDaysInfo = daysDetail
                    });
                }

                // Devamsızlık girilmemiş uyarısı (Öğrenci aktif olmasına rağmen hiç satır yoksa)
                if (studentAtt.Count == 0 && paidDays > 0 && monthAttendance.Count > 0)
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.AttendanceCompleteness,
                        Severity = SgkAuditSeverity.Info,
                        Message = "Bu ay hiç devamsızlık işlenmemiş (30 Gün Tam Devam kabul edildi).",
                        Advice = "Eğer öğrencinin staj devamsızlığı varsa bildirge oluşturmadan önce işleyiniz.",
                        CalculatedDaysInfo = daysDetail
                    });
                }

                // Ay içi işten çıkış durumunda bilgilendirme
                if (exitInMonth && s.SgkExitDate.HasValue && !entryInMonth)
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.DayCalculationMismatch,
                        Severity = SgkAuditSeverity.Info,
                        Message = $"Ay içi işten çıkış ({s.SgkExitDate:dd.MM.yyyy}): Ay başından çıkış gününe kadar {paidDays} gün prim hesaplandı. Çıkış sonrası için eksik gün bildirilmedi (Eksik Gün: {missingDays}).",
                        Advice = "SGK kuralı: Ay içinde işten ayrılan sigortalının çıkış sonrasındaki günleri eksik gün olarak bildirilmez.",
                        CalculatedDaysInfo = daysDetail
                    });
                }

                // SGK Gün Tutarlılığı ve Kural Kontrolleri:
                // Tam ay çalışanlar için kural uygunluk denetimi
                if (!entryInMonth && !exitInMonth)
                {
                    if (absentDays == 0 && (paidDays != 30 || missingDays != 0))
                    {
                        report.Items.Add(new SgkAuditItem
                        {
                            StudentId = s.Id,
                            TcNo = tc,
                            StudentName = sName,
                            ClassName = cName,
                            EnterpriseName = eName,
                            Category = SgkAuditCategory.DayCalculationMismatch,
                            Severity = SgkAuditSeverity.Error,
                            Message = $"Tam Çalışma Kuralı Uyuşmazlığı! Devamsızlığı olmayan sigortalının prim günü 30, eksik günü 0 olmalıdır (Mevcut: Prim {paidDays}, Eksik {missingDays}).",
                            Advice = "Ay kaç çekerse çeksin eksiksiz çalışan sigortalılar için 30 gün tam prim bildirilir.",
                            CalculatedDaysInfo = daysDetail
                        });
                    }
                    else if (daysInMonth == 31 && absentDays == 1 && (paidDays != 30 || missingDays != 0))
                    {
                        report.Items.Add(new SgkAuditItem
                        {
                            StudentId = s.Id,
                            TcNo = tc,
                            StudentName = sName,
                            ClassName = cName,
                            EnterpriseName = eName,
                            Category = SgkAuditCategory.DayCalculationMismatch,
                            Severity = SgkAuditSeverity.Error,
                            Message = $"31 Gün Kuralı Uyuşmazlığı! 31 çeken ayda 1 gün devamsızlıkta prim 30, eksik gün 0 olmalıdır (Mevcut: Prim {paidDays}, Eksik {missingDays}).",
                            Advice = "SGK portalı 30 gün prim bildirilen sigortalılarda eksik gün kabul etmez ve bildirimi reddeder.",
                            CalculatedDaysInfo = daysDetail
                        });
                    }
                    else if (paidDays + missingDays > daysInMonth)
                    {
                        report.Items.Add(new SgkAuditItem
                        {
                            StudentId = s.Id,
                            TcNo = tc,
                            StudentName = sName,
                            ClassName = cName,
                            EnterpriseName = eName,
                            Category = SgkAuditCategory.DayCalculationMismatch,
                            Severity = SgkAuditSeverity.Error,
                            Message = $"Gün Sayısı Aşımı! Prim Günü ({paidDays}) + Eksik Gün ({missingDays}) toplamı ayın gün sayısını ({daysInMonth}) aşıyor.",
                            Advice = "Devamsızlık ve prim gün sayılarını kontrol ediniz.",
                            CalculatedDaysInfo = daysDetail
                        });
                    }
                }

                // Sıfır Gün Kontrolü
                if (paidDays == 0)
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.ZeroDayAudit,
                        Severity = SgkAuditSeverity.Warning,
                        Message = "Öğrencinin prim günü 0 (sıfır) olarak bildirilmektedir.",
                        Advice = "Öğrenci tüm ay devamsız mı veya sözleşmesi feshedildi mi teyit ediniz. Fesih ise SGK çıkış işlemi yapılmalıdır.",
                        CalculatedDaysInfo = daysDetail
                    });
                }

                // Sağlık Raporu (İstirahat) Bilgilendirmesi
                if (hasHealthReport)
                {
                    report.Items.Add(new SgkAuditItem
                    {
                        StudentId = s.Id,
                        TcNo = tc,
                        StudentName = sName,
                        ClassName = cName,
                        EnterpriseName = eName,
                        Category = SgkAuditCategory.MissingReasonCode,
                        Severity = SgkAuditSeverity.Info,
                        Message = "Öğrencinin ay içinde Sağlık Raporu (R) bulunmaktadır (SGK RAPCALISTI=2 etiketi eklenecektir).",
                        Advice = "SGK İşveren portalından sağlık raporu onayını teyit ediniz.",
                        CalculatedDaysInfo = daysDetail
                    });
                }
            }

            return report;
        });
    }
}
