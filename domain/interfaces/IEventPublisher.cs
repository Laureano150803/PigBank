namespace DistributedSis.domain.interfaces
{
    public interface IEventPublisher
    {
        Task PublishCardRequestAsync(string userId, string requestType);
        Task PublishNotificationAsync<T>(string type, T data);
    }
}
