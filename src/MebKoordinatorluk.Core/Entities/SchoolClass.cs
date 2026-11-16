namespace MebKoordinatorluk.Core.Entities;

public class SchoolClass : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int? FieldId { get; set; }
    public string? FieldName { get; set; }
    public string InstitutionType { get; set; } = "Anadolu Meslek Programı"; // Anadolu Meslek Programı, MESEM
    public string EducationType { get; set; } = "Örgün Eğitim"; // Örgün Eğitim, MESEM
    public string InternshipDays { get; set; } = "Pazartesi,Salı,Çarşamba";
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IgnoreHalfDayDeduction { get; set; } = false;
    public bool HasInternship { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public List<string> GetDaysList()
    {
        if (string.IsNullOrWhiteSpace(InternshipDays)) return new List<string>();
        return InternshipDays.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(d => d.Trim())
                             .Where(d => !string.IsNullOrWhiteSpace(d))
                             .ToList();
    }
}
