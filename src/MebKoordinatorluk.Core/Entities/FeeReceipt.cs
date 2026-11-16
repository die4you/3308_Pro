namespace MebKoordinatorluk.Core.Entities;

public class FeeReceipt : BaseEntity
{
    public int StudentId { get; set; }
    public string Period { get; set; } = string.Empty; // Format: "yyyy-MM" e.g. "2026-10"
    public string FileName { get; set; } = string.Empty;
    public string ArchivePath { get; set; } = string.Empty;
    public DateTime AuditDate { get; set; } = DateTime.Now;
    public string? MatchReason { get; set; }
}
