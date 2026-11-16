namespace MebKoordinatorluk.Core.Entities;

public class Department : BaseEntity
{
    public string Code { get; set; } = string.Empty; // örn: "BIL"
    public string Name { get; set; } = string.Empty; // örn: "Bilişim Teknolojileri"
    public string? SubBranch { get; set; } // örn: "Yazılım Geliştirme", "Ağ İşletmenliği"
    public bool IsActive { get; set; } = true;

    public string FullBranchName => string.IsNullOrWhiteSpace(SubBranch) ? Name : $"{Name} / {SubBranch}";
}
