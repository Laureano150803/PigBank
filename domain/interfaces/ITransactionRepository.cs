using DistributedSis.domain.entities;
namespace DistributedSis.domain.interfaces
{
    public interface ITransactionRepository
    {
        Task SaveTransactionAsync(Transaction transaction);
        Task<List<Transaction>> GetTransactionsByCardIdAsync(string cardId);
        Task<List<Transaction>> GetTransactionsReportAsync(string cardId, string startDate, string endDate);
    }
}
