namespace MebKoordinatorluk.Core.Entities;

public class Field : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Branches { get; set; }
    public bool IsActive { get; set; } = true;

    public List<string> GetBranchList()
    {
        if (string.IsNullOrWhiteSpace(Branches) && string.IsNullOrWhiteSpace(Description))
            return new List<string>();

        var raw = string.IsNullOrWhiteSpace(Branches) ? Description! : Branches;
        return raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(b => b.Trim())
                  .Where(b => !string.IsNullOrWhiteSpace(b))
                  .Distinct(StringComparer.CurrentCultureIgnoreCase)
                  .OrderBy(b => b)
                  .ToList();
    }
}
