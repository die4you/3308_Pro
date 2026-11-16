using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Entities;

public class MonthlyPayroll : BaseEntity
{
    public int AcademicYearId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; } // 1 - 12
    public PayrollStatus Status { get; set; } = PayrollStatus.Taslak;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedDate { get; set; }

    public string PeriodName => $"{Year} / {Month:D2}";
}
