using DistributedSis.domain.entities;

namespace DistributedSis.domain.interfaces
{
    public interface ICardRepository
    {
        Task SaveAsync(Card card);
        Task<Card> GetByIdAsync(string uuid);
        Task UpdateAsync(Card card);
        Task<List<Card>> GetByUserIdAsync(string userId);

    }
}
