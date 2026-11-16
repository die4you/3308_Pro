namespace MebKoordinatorluk.Core.Entities;

public class AddressGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; } = "blue";
    public bool IsActive { get; set; } = true;

    // Compatibility Alias
    public string? District { get => Region; set => Region = value; }
}
