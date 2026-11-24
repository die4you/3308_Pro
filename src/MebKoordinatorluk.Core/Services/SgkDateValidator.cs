namespace MebKoordinatorluk.Core.Services;

public static class SgkDateValidator
{
    /// <summary>
    /// SGK İşe Giriş tarihi için varsayılan tarihi hesaplar.
    /// Kurallar:
    /// - Pazartesi günü ise bugün (referenceDate) kullanılabilir.
    /// - Pazartesi değilse ertesi gün (veya haftasonuna denk geliyorsa takip eden ilk Pazartesi) atanır.
    /// </summary>
    public static DateTime GetDefaultSgkEntryDate(DateTime referenceDate)
    {
        var today = referenceDate.Date;
        if (today.DayOfWeek == DayOfWeek.Monday)
        {
            return today;
        }

        var nextDay = today.AddDays(1);
        while (nextDay.DayOfWeek == DayOfWeek.Saturday || nextDay.DayOfWeek == DayOfWeek.Sunday)
        {
            nextDay = nextDay.AddDays(1);
        }

        return nextDay;
    }

    /// <summary>
    /// SGK İşe Giriş tarihini doğrular.
    /// Kurallar:
    /// 1. Geçmiş günler olamaz (entryDate < referenceDate.Date).
    /// 2. Haftasonu olamaz (Cumartesi veya Pazar).
    /// 3. Pazartesi günü aynı gün olabilir.
    /// 4. Pazartesi değilse işe giriş tarihi yapılan günün ertesi günü (veya takip eden ilk iş günü) olmalıdır.
    /// </summary>
    public static bool ValidateSgkEntryDate(DateTime entryDate, DateTime referenceDate, out string errorMessage)
    {
        var date = entryDate.Date;
        var today = referenceDate.Date;

        if (date < today)
        {
            errorMessage = "SGK işe giriş tarihi geçmiş bir tarih olamaz!";
            return false;
        }

        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            errorMessage = "SGK işe giriş tarihi hafta sonu (Cumartesi veya Pazar) olamaz!";
            return false;
        }

        if (today.DayOfWeek == DayOfWeek.Monday)
        {
            if (date < today)
            {
                errorMessage = "Pazartesi günü yapılan işlemlerde SGK işe giriş tarihi en erken bugün (Pazartesi) olabilir!";
                return false;
            }
        }
        else
        {
            var minAllowedDate = GetDefaultSgkEntryDate(today);
            if (date < minAllowedDate)
            {
                errorMessage = $"İşlem yapılan gün Pazartesi olmadığı için SGK işe giriş tarihi en erken ertesi iş günü ({minAllowedDate:dd.MM.yyyy dddd}) olabilir!";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// SGK İşten Çıkış tarihini doğrular.
    /// Kurallar:
    /// 1. SGK işe giriş tarihi olmayan bir öğrenciye çıkış tarihi verilemez.
    /// 2. SGK işten çıkış tarihi, SGK işe giriş tarihinden sonraki bir tarih olmalıdır.
    /// 3. SGK mevzuatına göre işten ayrılış bildirgesi en fazla 10 gün geriye dönük verilebilir (exitDate >= referenceDate - 10 gün).
    /// </summary>
    public static bool ValidateSgkExitDate(DateTime exitDate, DateTime? entryDate, DateTime referenceDate, out string errorMessage)
    {
        var exit = exitDate.Date;
        var today = referenceDate.Date;

        if (!entryDate.HasValue)
        {
            errorMessage = "SGK işe giriş tarihi bulunmayan bir öğrenciye SGK işten çıkış tarihi verilemez!";
            return false;
        }

        var entry = entryDate.Value.Date;

        if (exit <= entry)
        {
            errorMessage = $"SGK işten çıkış tarihi ({exit:dd.MM.yyyy}), SGK işe giriş tarihinden ({entry:dd.MM.yyyy}) sonraki bir tarih olmalıdır!";
            return false;
        }

        var minRetroactiveDate = today.AddDays(-10);
        if (exit < minRetroactiveDate)
        {
            errorMessage = $"SGK mevzuatına göre işten çıkış tarihi en fazla 10 gün geriye dönük uygulanabilir ({minRetroactiveDate:dd.MM.yyyy} tarihinden önce olamaz)!";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Bir öğrencinin SGK İşe Giriş XML bildirimine uygun olup olmadığını denetler.
    /// Kural: İşe giriş tarihi tanımlı olmalı, ancak öğrencinin işten çıkış tarihi (staj ilişiği sonlandırması) OLMAMALIDIR.
    /// </summary>
    public static bool IsEligibleForEntryXml(DateTime? entryDate, DateTime? exitDate, out string? errorMessage)
    {
        if (!entryDate.HasValue)
        {
            errorMessage = "İşe giriş tarihi tanımlı değil.";
            return false;
        }

        if (exitDate.HasValue)
        {
            errorMessage = $"Öğrencinin işten çıkış tarihi ({exitDate.Value:dd.MM.yyyy}) bulunduğu için (staj ilişiği sonlandırıldığından) işe giriş bildirgesi üretilemez.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Bir öğrencinin SGK İşten Çıkış XML bildirimine uygun olup olmadığını denetler.
    /// Kural: İşten çıkış tarihi tanımlı olmalı ve eğer giriş tarihi varsa çıkış tarihi giriş tarihinden önce olmamalıdır.
    /// </summary>
    public static bool IsEligibleForExitXml(DateTime? entryDate, DateTime? exitDate, out string? errorMessage)
    {
        if (!exitDate.HasValue)
        {
            errorMessage = "İşten çıkış tarihi tanımlı değil.";
            return false;
        }

        if (entryDate.HasValue && exitDate.Value.Date < entryDate.Value.Date)
        {
            errorMessage = $"İşten çıkış tarihi ({exitDate.Value:dd.MM.yyyy}), işe giriş tarihinden ({entryDate.Value:dd.MM.yyyy}) önce olamaz.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
