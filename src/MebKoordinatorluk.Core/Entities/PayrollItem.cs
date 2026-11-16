namespace MebKoordinatorluk.Core.Entities;

public class PayrollItem : BaseEntity
{
    public int PayrollId { get; set; }
    public int TeacherId { get; set; }
    public double Week1Hours { get; set; } = 0.0;
    public double Week2Hours { get; set; } = 0.0;
    public double Week3Hours { get; set; } = 0.0;
    public double Week4Hours { get; set; } = 0.0;
    public double Week5Hours { get; set; } = 0.0;
    public double TotalHours { get; set; } = 0.0;
    public decimal HourlyRate { get; set; } = 0.0m;
    public decimal TotalAmount { get; set; } = 0.0m;
    public string? Notes { get; set; }

    // Display & Joins
    public string? TeacherFullName { get; set; }
    public string? TeacherTc { get; set; }
    public string? DepartmentName { get; set; }
}
