using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class UserSecurityAndRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ISqliteConnectionFactory _factory;

    public UserSecurityAndRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"meb_user_test_{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig(_tempDbPath);
        _factory = new SqliteConnectionFactory(config);

        var initializer = new DatabaseInitializer(_factory);
        initializer.Initialize();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public void PasswordSecurity_HashAndVerify_ShouldSucceedForCorrectPassword()
    {
        var password = "SuperSecretPassword123!";
        var (hash, salt) = PasswordSecurity.HashPassword(password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.False(string.IsNullOrWhiteSpace(salt));

        bool isValid = PasswordSecurity.VerifyPassword(password, hash, salt);
        Assert.True(isValid);
    }

    [Fact]
    public void PasswordSecurity_Verify_ShouldFailForWrongPassword()
    {
        var (hash, salt) = PasswordSecurity.HashPassword("CorrectPassword1");
        bool isValid = PasswordSecurity.VerifyPassword("WrongPassword2", hash, salt);
        Assert.False(isValid);
    }

    [Fact]
    public void PasswordSecurity_Verify_ShouldReturnFalseForNullOrEmpty()
    {
        var (hash, salt) = PasswordSecurity.HashPassword("ValidPassword");
        Assert.False(PasswordSecurity.VerifyPassword("", hash, salt));
        Assert.False(PasswordSecurity.VerifyPassword("ValidPassword", "", salt));
        Assert.False(PasswordSecurity.VerifyPassword("ValidPassword", hash, ""));
    }

    [Fact]
    public async Task UserRepository_InitialSeededAdmin_ShouldExistAndAuthenticate()
    {
        var repo = new UserRepository(_factory);
        var users = (await repo.GetAllAsync()).ToList();

        Assert.NotEmpty(users);
        var admin = users.FirstOrDefault(u => u.Username.Equals("admin", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(admin);
        Assert.True(admin.IsAdmin);
        Assert.True(admin.IsActive);

        // Seeded password from Settings is "admin123"
        var authUser = await repo.AuthenticateAsync("admin", "admin123");
        Assert.NotNull(authUser);
        Assert.Equal(admin.Id, authUser.Id);
    }

    [Fact]
    public async Task UserRepository_Add_Authenticate_ChangePassword_Delete_ShouldWorkCorrectly()
    {
        var repo = new UserRepository(_factory);

        var (hash, salt) = PasswordSecurity.HashPassword("TeacherPass2026");
        var newUser = new User
        {
            Username = "ogretmen1",
            FullName = "Ali Veli",
            Role = "Öğretmen",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true
        };

        // 1. Add user
        int newId = await repo.AddAsync(newUser);
        Assert.True(newId > 0);

        // 2. Authenticate
        var authSuccess = await repo.AuthenticateAsync("ogretmen1", "TeacherPass2026");
        Assert.NotNull(authSuccess);
        Assert.Equal("Ali Veli", authSuccess.FullName);

        var authWrongPass = await repo.AuthenticateAsync("ogretmen1", "WrongPass");
        Assert.Null(authWrongPass);

        // 3. Change password
        await repo.ChangePasswordAsync(newId, "NewPassword999");
        var authWithOldPass = await repo.AuthenticateAsync("ogretmen1", "TeacherPass2026");
        Assert.Null(authWithOldPass);

        var authWithNewPass = await repo.AuthenticateAsync("ogretmen1", "NewPassword999");
        Assert.NotNull(authWithNewPass);

        // 4. Update
        authWithNewPass.FullName = "Ali Veli Güncel";
        await repo.UpdateAsync(authWithNewPass);
        var updated = await repo.GetByIdAsync(newId);
        Assert.NotNull(updated);
        Assert.Equal("Ali Veli Güncel", updated.FullName);

        // 5. Delete
        await repo.DeleteAsync(newId);
        var deleted = await repo.GetByIdAsync(newId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task UserRepository_InactiveUser_ShouldNotAuthenticate()
    {
        var repo = new UserRepository(_factory);

        var (hash, salt) = PasswordSecurity.HashPassword("Password123");
        var user = new User
        {
            Username = "pasif_kullanici",
            FullName = "Pasif Hesap",
            Role = "Öğretmen",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = false
        };

        int id = await repo.AddAsync(user);
        Assert.True(id > 0);

        var authResult = await repo.AuthenticateAsync("pasif_kullanici", "Password123");
        Assert.Null(authResult); // Inactive accounts must not authenticate
    }
}
