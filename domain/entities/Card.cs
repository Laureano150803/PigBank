using Amazon.DynamoDBv2.DataModel;

namespace DistributedSis.domain.entities
{
    [DynamoDBTable("Cards")]
    public class Card
    {
        [DynamoDBHashKey("uuid")]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        [DynamoDBProperty("user_id")]
        public string UserId { get; set; }

        [DynamoDBProperty("type")]
        public string Type { get; set; } // "DEBIT" o "CREDIT"

        [DynamoDBProperty("status")]
        public string Status { get; set; } // "ACTIVATED" o "PENDING"

        [DynamoDBProperty("balance")]
        public decimal Balance { get; set; }

        [DynamoDBRangeKey("createdAt")]
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }
}
