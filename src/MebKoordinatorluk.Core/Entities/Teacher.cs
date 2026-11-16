using MebKoordinatorluk.Core.Common;
using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Entities;

public class Teacher : BaseEntity
{
    public string? TcKimlikNo { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string? Branch { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string AvailableDays { get; set; } = string.Empty; // e.g. "Salı" or "Pazartesi,Salı"
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Kadrolu;
    public int MaxCoordinationHours { get; set; } = 16;
    public int TargetHours { get; set; } = 16;
    public int CoordinatorFeeHours { get; set; } = 16;
    public bool IsActive { get; set; } = true;

    // Compatibility Alias
    public int MaxWeeklyHours { get => MaxCoordinationHours; set => MaxCoordinationHours = value; }

    // Navigation / Display Helpers
    public string? DepartmentName { get; set; }
    public string FullName
    {
        get
        {
            var combined = $"{FirstName} {LastName}".Trim();
            return string.IsNullOrWhiteSpace(combined) ? (Branch ?? "Öğretmen") : combined;
        }
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parts = value.Trim().Split(' ', 2);
                FirstName = parts[0];
                LastName = parts.Length > 1 ? parts[1] : "";
            }
        }
    }

    public List<string> GetDaysList()
    {
        if (string.IsNullOrWhiteSpace(AvailableDays)) return new List<string>();
        return AvailableDays.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(d => TurkishTextHelper.NormalizeDayName(d.Trim()))
                            .Where(d => !string.IsNullOrWhiteSpace(d))
                            .ToList();
    }
}
