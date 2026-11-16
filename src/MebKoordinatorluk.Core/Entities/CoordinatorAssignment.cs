using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Entities;

public class CoordinatorAssignment : BaseEntity
{
    public int AcademicYearId { get; set; }
    public int TeacherId { get; set; }
    public int EnterpriseId { get; set; }
    public DayOfWeekTurkish DayOfWeek { get; set; } = DayOfWeekTurkish.Pazartesi;
    public int StudentCount { get; set; } = 0;
    public double CalculatedHours { get; set; } = 0.0; // MEB mevzuatına göre hesaplanan haftalık koordinatörlük ek ders saati
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Display & Joins
    public string? TeacherFullName { get; set; }
    public string? EnterpriseName { get; set; }
    public string? EnterpriseDistrict { get; set; }
    public DistanceType DistanceType { get; set; } = DistanceType.MucavirAlanIci;
    public string? AcademicYearName { get; set; }
}
