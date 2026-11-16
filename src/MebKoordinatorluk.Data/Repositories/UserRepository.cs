using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Security;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public UserRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT u.*, (t.FirstName || ' ' || t.LastName) AS TeacherName
            FROM Users u
            LEFT JOIN Teachers t ON u.TeacherId = t.Id
            WHERE u.Id = @Id;";
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT u.*, (t.FirstName || ' ' || t.LastName) AS TeacherName
            FROM Users u
            LEFT JOIN Teachers t ON u.TeacherId = t.Id
            WHERE LOWER(u.Username) = LOWER(@Username);";
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Username = username.Trim() });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT u.*, (t.FirstName || ' ' || t.LastName) AS TeacherName
            FROM Users u
            LEFT JOIN Teachers t ON u.TeacherId = t.Id
            ORDER BY CASE WHEN u.Role = 'Admin' THEN 0 ELSE 1 END, u.FullName ASC;";
        return await conn.QueryAsync<User>(sql);
    }

    public async Task<int> AddAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO Users (Username, PasswordHash, PasswordSalt, FullName, Role, TeacherId, IsActive, CreatedAt, LastLoginAt)
            VALUES (@Username, @PasswordHash, @PasswordSalt, @FullName, @Role, @TeacherId, @IsActive, @CreatedAt, @LastLoginAt);
            SELECT last_insert_rowid();";

        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            Username = user.Username.Trim(),
            user.PasswordHash,
            user.PasswordSalt,
            FullName = user.FullName.Trim(),
            Role = string.IsNullOrWhiteSpace(user.Role) ? "Admin" : user.Role.Trim(),
            user.TeacherId,
            IsActive = user.IsActive ? 1 : 0,
            CreatedAt = string.IsNullOrWhiteSpace(user.CreatedAt) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : user.CreatedAt,
            user.LastLoginAt
        });
    }

    public async Task UpdateAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE Users
            SET Username = @Username,
                FullName = @FullName,
                Role = @Role,
                TeacherId = @TeacherId,
                IsActive = @IsActive
            WHERE Id = @Id;";

        await conn.ExecuteAsync(sql, new
        {
            user.Id,
            Username = user.Username.Trim(),
            FullName = user.FullName.Trim(),
            Role = string.IsNullOrWhiteSpace(user.Role) ? "Admin" : user.Role.Trim(),
            user.TeacherId,
            IsActive = user.IsActive ? 1 : 0
        });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Users WHERE Id = @Id;", new { Id = id });
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await GetByUsernameAsync(username);
        if (user == null || !user.IsActive)
            return null;

        bool isValid = PasswordSecurity.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
        return isValid ? user : null;
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE Users
            SET LastLoginAt = @LastLoginAt
            WHERE Id = @Id;";

        await conn.ExecuteAsync(sql, new
        {
            Id = userId,
            LastLoginAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    public async Task ChangePasswordAsync(int userId, string newPassword)
    {
        var (hash, salt) = PasswordSecurity.HashPassword(newPassword);
        using var conn = _factory.CreateConnection();
        const string sql = @"
            UPDATE Users
            SET PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt
            WHERE Id = @Id;";

        await conn.ExecuteAsync(sql, new
        {
            Id = userId,
            PasswordHash = hash,
            PasswordSalt = salt
        });
    }
}
