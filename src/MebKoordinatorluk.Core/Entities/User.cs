namespace MebKoordinatorluk.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin"; // "Admin", "Öğretmen", "Kullanıcı"
    public int? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? LastLoginAt { get; set; }

    public bool IsAdmin => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
}
