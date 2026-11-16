namespace MebKoordinatorluk.Core.Entities;

public class AttendanceEntry : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int StudentId { get; set; }
    public int DayOfMonth { get; set; } // 1..31
    public string Code { get; set; } = "X"; // X, D, İ, C, S, SY, ÖY, T
}
