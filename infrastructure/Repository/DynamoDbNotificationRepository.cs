using Amazon.DynamoDBv2.DataModel;
using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;

namespace DistributedSis.infrastructure.Repository
{
    public class DynamoDbNotificationRepository : INotificationRepository
    {
        private readonly IDynamoDBContext _context;
        private readonly string _tableName = Environment.GetEnvironmentVariable("NOTIFICATION_TABLE") ?? "notification-table";
        private readonly string _errorTableName = Environment.GetEnvironmentVariable("ERROR_TABLE") ?? "notification-error-table";

        public DynamoDbNotificationRepository(IDynamoDBContext context)
        {
            _context = context;
        }
        public async Task SaveErrorAsync(NotificationErrorLog errorLog)
        {
            var config = new DynamoDBOperationConfig { OverrideTableName = _errorTableName };
            await _context.SaveAsync(errorLog, config);
        }

        public async  Task SaveNotificationAsync(NotificationLog notification)
        {
            var config = new DynamoDBOperationConfig { OverrideTableName = _tableName };
            await _context.SaveAsync(notification, config); ;
        }
    }
}
