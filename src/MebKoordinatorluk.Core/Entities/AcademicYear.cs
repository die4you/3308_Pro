namespace MebKoordinatorluk.Core.Entities;

public class AcademicYear : BaseEntity
{
    public string Name { get; set; } = string.Empty; // örn: "2024-2025"
    public int Term { get; set; } = 1; // 1: 1. Dönem (Güz), 2: 2. Dönem (Bahar), 3: Yaz Stajı
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public string DisplayName => $"{Name} - {Term}. Dönem";
}
