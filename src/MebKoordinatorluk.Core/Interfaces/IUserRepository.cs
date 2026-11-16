using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task<int> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);
    Task<User?> AuthenticateAsync(string username, string password);
    Task UpdateLastLoginAsync(int userId);
    Task ChangePasswordAsync(int userId, string newPassword);
}
