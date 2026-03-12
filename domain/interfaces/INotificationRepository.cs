using DistributedSis.domain.entities;
namespace DistributedSis.domain.interfaces
{
    public interface INotificationRepository
    {
        Task SaveNotificationAsync(NotificationLog notification);
        Task SaveErrorAsync(NotificationErrorLog errorLog);
    }
}
