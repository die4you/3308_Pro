namespace MebKoordinatorluk.Core.Entities;

public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
}
