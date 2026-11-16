using System;
using System.Collections.Generic;
using System.Linq;

namespace MebKoordinatorluk.Core.Models;

public enum SgkAuditSeverity
{
    Error,   // SGK bildirge yüklemesini veya tahakkukunu kesinlikle engelleyen kritik hata
    Warning, // Muhtemel veri eksikliği veya şüpheli durum (dikkat gerektirir)
    Info     // Bilgilendirme amaçlı durum
}

public enum SgkAuditCategory
{
    TcKimlikValidation,     // TC Kimlik formatı, mükerrer TC veya eksik kimlik
    AttendanceCompleteness, // Ay içi hiç devamsızlık işlenmemiş olması veya eksik günler
    DayCalculationMismatch, // 30 Gün SGK kuralı veya prim + eksik gün toplamı uyuşmazlığı
    MissingReasonCode,      // Eksik gün olmasına rağmen eksik gün nedeni bulunmaması
    DateIntervalMismatch,   // İşe giriş ve çıkış tarihlerinin çelişkili veya geçersiz olması
    ZeroDayAudit,           // 0 gün prim bildirimi (tam ay devamsız / çıkış)
    WorkplaceInfo           // SGK Sicil No, Vergi No veya Kurum bilgisi eksikliği
}

public class SgkAuditItem
{
    public int StudentId { get; set; }
    public string TcNo { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string EnterpriseName { get; set; } = string.Empty;
    public SgkAuditCategory Category { get; set; }
    public SgkAuditSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Advice { get; set; } = string.Empty;
    public string CalculatedDaysInfo { get; set; } = string.Empty;

    public string CategoryDisplayName => Category switch
    {
        SgkAuditCategory.TcKimlikValidation => "TC Kimlik & Kimlik",
        SgkAuditCategory.AttendanceCompleteness => "Devamsızlık Eksikliği",
        SgkAuditCategory.DayCalculationMismatch => "30 Gün Kuralı",
        SgkAuditCategory.MissingReasonCode => "Eksik Gün Nedeni",
        SgkAuditCategory.DateIntervalMismatch => "Giriş / Çıkış Tarihi",
        SgkAuditCategory.ZeroDayAudit => "Sıfır Gün Prim",
        SgkAuditCategory.WorkplaceInfo => "İşyeri / Sicil Bilgisi",
        _ => "Genel Denetim"
    };

    public string SeverityDisplayName => Severity switch
    {
        SgkAuditSeverity.Error => "Kritik Hata",
        SgkAuditSeverity.Warning => "Uyarı",
        SgkAuditSeverity.Info => "Bilgi",
        _ => "Bilinmiyor"
    };
}

public class SgkAuditReport
{
    public int Year { get; set; }
    public int MonthIndex { get; set; }
    public int TotalStudentsAudited { get; set; }
    public List<SgkAuditItem> Items { get; set; } = new();

    public int ErrorCount => Items.Count(i => i.Severity == SgkAuditSeverity.Error);
    public int WarningCount => Items.Count(i => i.Severity == SgkAuditSeverity.Warning);
    public int InfoCount => Items.Count(i => i.Severity == SgkAuditSeverity.Info);
    public int ProblematicStudentCount => Items.Select(i => i.StudentId).Where(id => id > 0).Distinct().Count();

    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;
    public bool IsClean => Items.Count == 0;
}
