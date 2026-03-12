namespace DistributedSis.domain.interfaces
{
    public interface ITemplateRepository
    {
        Task<string> GetTemplateAsync(string eventType);
    }
}
