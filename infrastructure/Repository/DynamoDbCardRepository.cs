using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;

namespace DistributedSis.infrastructure.Repository
{
    public class DynamoDbCardRepository : ICardRepository
    {
        private readonly DynamoDBContext _context;
        private readonly string _tableName = Environment.GetEnvironmentVariable("CARD_TABLE") ?? "Cards";
        public DynamoDbCardRepository(IAmazonDynamoDB dynamoDbClient)
        {

            _context = new DynamoDBContext(dynamoDbClient);
        }

        public async Task<Card?> GetByIdAsync(string uuid)
        {
            var config = new DynamoDBOperationConfig { OverrideTableName = _tableName };


            var search = _context.QueryAsync<Card>(uuid, config);
            var results = await search.GetNextSetAsync();

            return results.FirstOrDefault();
        }
        public async Task<List<Card>> GetByUserIdAsync(string userId)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = _tableName,
                IndexName = "UserIndex" 
            };


            var search = _context.QueryAsync<Card>(userId, config);
            return await search.GetNextSetAsync();
        }

        public async Task SaveAsync(Card card)
        {
            await UpdateAsync(card);
        }

        public async Task UpdateAsync(Card card)
        {
            var config = new DynamoDBOperationConfig { OverrideTableName = _tableName };

            await _context.SaveAsync(card, config);
        }
    }
}
