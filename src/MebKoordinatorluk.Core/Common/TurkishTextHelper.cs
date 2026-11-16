using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MebKoordinatorluk.Core.Common;

public static class TurkishTextHelper
{
    public static readonly CultureInfo TrCulture = new("tr-TR");

    /// <summary>
    /// Türkçe kültür kurallarına uygun olarak metni büyük harfe dönüştürür (ı -> I, i -> İ).
    /// </summary>
    public static string ToUpperTr(this string? s)
    {
        return string.IsNullOrWhiteSpace(s) ? string.Empty : s.ToUpper(TrCulture);
    }

    /// <summary>
    /// Türkçe kültür kurallarına uygun olarak metni küçük harfe dönüştürür (I -> ı, İ -> i).
    /// </summary>
    public static string ToLowerTr(this string? s)
    {
        return string.IsNullOrWhiteSpace(s) ? string.Empty : s.ToLower(TrCulture);
    }

    /// <summary>
    /// Gün adını Türkçe karakterli ve ilk harfi büyük olacak şekilde düzeltir (örn: "carsamba" -> "Çarşamba", "sali" -> "Salı").
    /// </summary>
    public static string NormalizeDayName(string? day)
    {
        if (string.IsNullOrWhiteSpace(day)) return string.Empty;

        string clean = day.Trim().ToLowerInvariant();
        clean = clean.Replace("ı", "i").Replace("ç", "c").Replace("ş", "s").Replace("ğ", "g").Replace("ö", "o").Replace("ü", "u");

        return clean switch
        {
            "pazartesi" or "pzt" => "Pazartesi",
            "sali" or "sal" => "Salı",
            "carsamba" or "car" or "crs" => "Çarşamba",
            "persembe" or "per" or "prs" => "Perşembe",
            "cuma" or "cum" => "Cuma",
            "cumartesi" or "cmt" => "Cumartesi",
            "pazar" or "paz" => "Pazar",
            _ => CapitalizeFirst(day.Trim())
        };
    }

    /// <summary>
    /// Virgül, noktalı virgül veya yeni satırla ayrılmış gün listesini Türkçe karakterli ve ilk harfi büyük formatta birleştirir.
    /// </summary>
    public static string FormatDays(string? days, string separator = ", ")
    {
        if (string.IsNullOrWhiteSpace(days)) return "-";

        var parts = days.Split(new[] { ',', ';', '/', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "-";

        var normalized = parts
            .Select(p => NormalizeDayName(p))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        return normalized.Count > 0 ? string.Join(separator, normalized) : "-";
    }

    /// <summary>
    /// Gün adından haftanın sıra numarasını döndürür (Pazartesi=1, ..., Pazar=7).
    /// Bilinmeyen günler için 99 döndürür.
    /// </summary>
    public static int DayOrder(string? day)
    {
        string normalized = NormalizeDayName(day);
        return normalized switch
        {
            "Pazartesi" => 1,
            "Salı"      => 2,
            "Çarşamba"  => 3,
            "Perşembe"  => 4,
            "Cuma"      => 5,
            "Cumartesi" => 6,
            "Pazar"     => 7,
            _           => 99
        };
    }

    /// <summary>
    /// Virgül, noktalı virgül veya yeni satırla ayrılmış gün listesindeki ilk günün sıra numarasını döndürür.
    /// </summary>
    public static int FirstDayOrder(string? days)
    {
        if (string.IsNullOrWhiteSpace(days)) return 99;
        var firstDay = days.Split(new[] { ',', ';', '/', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                           .FirstOrDefault();
        return DayOrder(firstDay);
    }

    /// <summary>
    /// Türkçe kurallarına göre ilk harfi büyük, kalan harfleri küçük yapar.
    /// </summary>
    public static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        if (s.Length == 1) return s.ToUpper(TrCulture);
        return char.ToUpper(s[0], TrCulture) + s.Substring(1).ToLower(TrCulture);
    }
}
