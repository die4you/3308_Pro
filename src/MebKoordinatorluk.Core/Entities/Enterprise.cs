using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Entities;

public class Enterprise : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int? AddressGroupId { get; set; }
    public string? AddressGroupName { get; set; }
    public string? Sector { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public DistanceType DistanceType { get; set; } = DistanceType.MucavirAlanIci;
    public string? DistanceText { get; set; } // e.g. "Mücavir Alan İçi" or "28.5 km"
    public int Capacity { get; set; } = 4;
    public string ActiveDays { get; set; } = "Pazartesi,Salı,Çarşamba";
    public int? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public string? TaxNumber { get; set; }
    public string? Iban { get; set; }
    public string? SgkSicilNo { get; set; }
    public string? MasterTrainerName { get; set; }
    public bool OptOutStateContribution { get; set; } = false;
    public int EmployeeCount { get; set; } = 1;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Display helper
    public string LocationSummary => !string.IsNullOrWhiteSpace(District)
        ? $"{District}/{City ?? "İSTANBUL"}"
        : (Address ?? "");

    public List<string> GetDaysList()
    {
        if (string.IsNullOrWhiteSpace(ActiveDays)) return new List<string>();
        return ActiveDays.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(d => d.Trim())
                         .Where(d => !string.IsNullOrWhiteSpace(d))
                         .ToList();
    }
}
