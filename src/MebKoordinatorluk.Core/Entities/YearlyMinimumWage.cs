namespace MebKoordinatorluk.Core.Entities;

public class YearlyMinimumWage
{
    public int Year { get; set; }
    public decimal NetMinimumWage { get; set; }
    public decimal GrossMinimumWage { get; set; }
    public string? UpdatedDate { get; set; }

    /// <summary>
    /// Brüt asgari ücreti Net Asgari Ücret / 0.85 olarak 2 ondalık basamağa yuvarlayarak hesaplar.
    /// </summary>
    public static decimal CalculateGrossFromNet(decimal net)
    {
        if (net <= 0) return 0m;
        return Math.Round(net / 0.85m, 2);
    }
}
