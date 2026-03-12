using DistributedSis.domain.entities;

namespace DistributedSis.domain.interfaces
{
    public interface IUserRepository
    {
        Task SaveUserAsync(User user);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByIdAsync(string userId);
        Task UpdateUserAsync(User user);
    }
}
