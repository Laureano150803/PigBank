using Amazon.DynamoDBv2.DataModel;

namespace DistributedSis.domain.entities
{
    [DynamoDBTable("Transactions")]
    public class Transaction
    {
        [DynamoDBHashKey("uuid")]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        [DynamoDBGlobalSecondaryIndexHashKey("CardIdIndex")]
        [DynamoDBProperty("cardId")]
        public string CardId { get; set; }

        [DynamoDBProperty("amount")]
        public decimal Amount { get; set; }

        [DynamoDBProperty("merchant")]
        public string Merchant { get; set; }

        [DynamoDBProperty("type")]
        public string Type { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("CardIdIndex")]
        [DynamoDBRangeKey("createdAt")]
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    }
}
