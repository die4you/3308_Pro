namespace MebKoordinatorluk.Core.Services;

public record AttendanceCodeInfo(
    string Code,
    string Description,
    string Category,
    double AbsenceDays,
    bool IsHealthReport,
    bool DeductsFee)
{
    public override string ToString() => $"{Code} — {Description}";
}

public static class AttendanceCodeHelper
{
    public static readonly List<AttendanceCodeInfo> AllCodes = new()
    {
        new("X", "Geldi / Stajda (Mevcut)", "Mevcut", 0.0, false, false),
        new("D", "Tam Gün (Özürsüz)", "Özürsüz", 1.0, false, true),
        new("ÖY", "Öğleden Sonra Yarım Gün (Özürsüz)", "Özürsüz", 0.5, false, true),
        new("SY", "Sabahtan Yarım Gün (Özürsüz)", "Özürsüz", 0.5, false, true),
        new("Y", "Yarım Gün (Özürsüz)", "Özürsüz", 0.5, false, true),
        new("İ", "İzinli (Özürlü)", "Özürlü", 1.0, false, true),
        new("S", "Raporlu (Özürlü)", "Özürlü", 1.0, true, true),
        new("SH", "Sağlık Kurulu (Özürlü)", "Özürlü", 1.0, true, true),
        new("K", "Karantina / Temaslı Raporu (Özürlü)", "Özürlü", 1.0, true, true),
        new("SV", "Sevkli (Özürlü)", "Özürlü", 1.0, false, true),
        new("V", "1. Derece Yakını Vefatı (Özürlü)", "Özürlü", 1.0, false, true),
        new("DA", "Doğal Afet (Özürlü)", "Özürlü", 1.0, false, true),
        new("YA", "Yangın (Özürlü)", "Özürlü", 1.0, false, true),
        new("T", "Tedbir Kararı 175/1 (Özürlü)", "Özürlü", 1.0, false, true),
        new("C", "Okuldan Uzaklaştırma (Özürlü)", "Özürlü", 1.0, false, true),
        new("GÖ", "Göz Altına Alınma (Özürlü)", "Özürlü", 1.0, false, true),
        new("TU", "Tutuklanma (Özürlü)", "Özürlü", 1.0, false, true),
        new("F", "Faaliyet (Görevli / Kesintisiz)", "Görevli", 0.0, false, false),
        new("N", "Nöbetçi (Görevli / Kesintisiz)", "Görevli", 0.0, false, false)
    };

    private static readonly Dictionary<string, AttendanceCodeInfo> _codeMap =
        AllCodes.ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);

    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var trimmed = code.Trim().ToUpperInvariant();
        if (trimmed == "I") return "İ";
        if (trimmed == "OY") return "ÖY";
        if (trimmed == "GO") return "GÖ";
        return trimmed;
    }

    public static AttendanceCodeInfo? GetInfo(string? code)
    {
        var norm = NormalizeCode(code);
        if (_codeMap.TryGetValue(norm, out var info))
            return info;
        return null;
    }

    /// <summary>
    /// Devamsızlık gün sayısı (1.0, 0.5 veya 0.0).
    /// N (Nöbetçi) ve F (Faaliyet) günleri görevli sayıldığından 0.0 gün döner, kesinti yapılmaz.
    /// </summary>
    public static double GetAbsenceDays(string? code)
    {
        var info = GetInfo(code);
        if (info != null)
            return info.AbsenceDays;

        var norm = NormalizeCode(code);
        // Eski 'R' rapor kodu gelirse 1 gün say
        if (norm == "R") return 1.0;
        return 0.0;
    }

    /// <summary>
    /// SGK e-Bildirge ve İşten Çıkış XML eksik gün hesabı için devamsızlık gün sayısı.
    /// Günün bölünmezliği ilkesi ve iş kazası sigortası güvencesi gereği,
    /// yarım günlük devamsızlıklar (ÖY, SY, Y) SGK prim gününü kesintiye uğratmaz (0.0 döner).
    /// Yalnızca tam günlük devamsızlıklar (1.0) SGK eksik gün hesabına dahil edilir.
    /// </summary>
    public static double GetSgkAbsenceDays(string? code)
    {
        var info = GetInfo(code);
        if (info != null)
        {
            // Yarım gün devamsızlıklar SGK'da kesintiye yol açmaz
            return info.AbsenceDays >= 1.0 ? info.AbsenceDays : 0.0;
        }

        var norm = NormalizeCode(code);
        if (norm == "R") return 1.0;
        return 0.0;
    }

    /// <summary>
    /// Özürsüz devamsızlık mı? (D, ÖY, SY, Y)
    /// </summary>
    public static bool IsUnexcused(string? code)
    {
        var info = GetInfo(code);
        return info != null && info.Category == "Özürsüz";
    }

    /// <summary>
    /// Özürlü devamsızlık mı? (İ, S, SH, K, SV, V, DA, YA, T, C, GÖ, TU)
    /// </summary>
    public static bool IsExcused(string? code)
    {
        var info = GetInfo(code);
        return info != null && info.Category == "Özürlü";
    }

    /// <summary>
    /// Görevli gün mü? (N, F)
    /// </summary>
    public static bool IsDutyOrActivity(string? code)
    {
        var info = GetInfo(code);
        return info != null && info.Category == "Görevli";
    }

    /// <summary>
    /// SGK e-Bildirge'de İstirahat (rapor) bildirimi gerektirir mi? (S, SH, K, R)
    /// </summary>
    public static bool IsHealthReport(string? code)
    {
        var norm = NormalizeCode(code);
        if (norm == "R") return true;
        var info = GetInfo(code);
        return info != null && info.IsHealthReport;
    }

    /// <summary>
    /// Verilen devamsızlık kodunun tekil SGK Eksik Gün Neden Kodunu döner.
    /// </summary>
    public static string GetSgkMissingReasonCode(string? code)
    {
        var norm = NormalizeCode(code);
        if (norm is "S" or "SH" or "K" or "R") return "01"; // İstirahat
        if (norm is "D") return "15";                       // Devamsızlık
        if (norm is "C") return "03";                       // Disiplin Cezası
        if (norm is "GÖ") return "04";                      // Gözaltı
        if (norm is "TU") return "05";                      // Tutukluluk
        if (norm is "İ" or "SV" or "V" or "DA" or "YA" or "T") return "13"; // Diğer Nedenler

        var info = GetInfo(code);
        if (info != null)
        {
            if (info.IsHealthReport) return "01";
            if (info.Category == "Özürsüz") return "15";
            return "13";
        }

        return "13";
    }

    /// <summary>
    /// Öğrencinin ay içindeki devamsızlık kayıtlarına göre SGK Eksik Gün Nedenini belirler:
    /// - Kural A (Tekil Neden): Tüm eksik günler aynı gerekçeye/koda dayanıyorsa o standart kod (örn: 01, 15, 13) döner.
    /// - Kural B (Birden Fazla Kuralı): Ay içindeki eksik günler en az iki farklı SGK gerekçesinden kaynaklanıyorsa '12' (Birden Fazla) döner.
    /// </summary>
    public static string DetermineSgkMissingReasonCode(IEnumerable<string?> codes)
    {
        var distinctReasons = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(GetSgkMissingReasonCode)
            .Distinct()
            .ToList();

        if (distinctReasons.Count == 0)
        {
            return "13"; // Varsayılan genel eksik gün nedeni: Diğer Nedenler
        }

        if (distinctReasons.Count == 1)
        {
            return distinctReasons[0];
        }

        // En az iki farklı nedenden kaynaklanıyorsa SGK Kuralı: 12 (Birden Fazla)
        return "12";
    }

    /// <summary>
    /// SGK eksik gün kodu açıklamasını döner (Arayüz ve çapraz denetim raporları için).
    /// </summary>
    public static string GetSgkMissingReasonDescription(string code) => code switch
    {
        "01" => "01 - İstirahat (Rapor)",
        "03" => "03 - Disiplin Cezası",
        "04" => "04 - Gözaltına Alınma",
        "05" => "05 - Tutukluluk",
        "12" => "12 - Birden Fazla Neden",
        "13" => "13 - Diğer Nedenler",
        "15" => "15 - Devamsızlık",
        "21" => "21 - Diğer Ücretsiz İzin",
        _ => $"{code} - Eksik Gün"
    };
}
