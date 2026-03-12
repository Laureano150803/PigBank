using Amazon.DynamoDBv2.DataModel;

namespace DistributedSis.domain.entities
{
    [DynamoDBTable("notification-table")]
    public class NotificationLog
    {
        [DynamoDBHashKey("uuid")]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        [DynamoDBProperty("type")]
        public string Type { get; set; }

        [DynamoDBProperty("content")]
        public string Content { get; set; } // Guardaremos el JSON serializado de la data

        [DynamoDBRangeKey("createdAt")]
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    }

    [DynamoDBTable("notification-error-table")]
    public class NotificationErrorLog
    {

        [DynamoDBHashKey("uuid")]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        [DynamoDBProperty("errorReason")]
        public string ErrorReason { get; set; }

        [DynamoDBProperty("failedMessage")]
        public string FailedMessage { get; set; }

        [DynamoDBRangeKey("createdAt")]
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    }
}
