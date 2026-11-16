namespace MebKoordinatorluk.Core.Common;

public static class ValidationHelper
{
    /// <summary>
    /// TC Kimlik Numarasının 11 haneli ve resmi algoritma kurallarına uygunluğunu denetler.
    /// </summary>
    public static bool IsValidTcKimlikNo(string? tc)
    {
        if (string.IsNullOrWhiteSpace(tc))
            return false;

        tc = tc.Trim();

        if (tc.Length != 11)
            return false;

        if (!tc.All(char.IsDigit))
            return false;

        if (tc[0] == '0')
            return false;

        int[] digits = tc.Select(c => c - '0').ToArray();

        int sumOdd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        int sumEven = digits[1] + digits[3] + digits[5] + digits[7];

        int digit10 = ((sumOdd * 7) - sumEven) % 10;
        if (digit10 < 0) digit10 += 10;

        if (digits[9] != digit10)
            return false;

        int sumFirst10 = 0;
        for (int i = 0; i < 10; i++)
        {
            sumFirst10 += digits[i];
        }

        if (sumFirst10 % 10 != digits[10])
            return false;

        return true;
    }

    /// <summary>
    /// Telefon numarasının geçerliliğini denetler (en az 10 basamak).
    /// </summary>
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return true; // Opsiyonel ise geçerli sayılır

        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        return digitsOnly.Length >= 10 && digitsOnly.Length <= 11;
    }

    /// <summary>
    /// E-posta adresinin temel format doğrulaması.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return true; // Opsiyonel ise geçerli

        email = email.Trim();
        return email.Contains('@') && email.Contains('.') && email.Length >= 5;
    }
}
