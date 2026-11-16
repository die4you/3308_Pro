using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Entities;

public class StudentPlacement : BaseEntity
{
    public int AcademicYearId { get; set; }
    public int StudentId { get; set; }
    public int EnterpriseId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? InternshipDays { get; set; } // örn: "Pzt,Sal,Çrş"
    public string? ContractNo { get; set; } // Sözleşme No
    public PlacementStatus Status { get; set; } = PlacementStatus.Aktif;

    // Display & Joins
    public string? StudentFullName { get; set; }
    public string? StudentNumber { get; set; }
    public string? EnterpriseName { get; set; }
    public string? AcademicYearName { get; set; }
    public string? DepartmentName { get; set; }
}
