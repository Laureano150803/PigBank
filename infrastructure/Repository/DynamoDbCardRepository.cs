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
            // Inicializamos el contexto de alto nivel
            _context = new DynamoDBContext(dynamoDbClient);
        }

        public async Task<Card?> GetByIdAsync(string uuid)
        {
            var config = new DynamoDBOperationConfig { OverrideTableName = _tableName };

            // QueryAsync de alto nivel maneja automáticamente los Reserved Keywords
            var search = _context.QueryAsync<Card>(uuid, config);
            var results = await search.GetNextSetAsync();

            return results.FirstOrDefault();
        }
        public async Task<List<Card>> GetByUserIdAsync(string userId)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = _tableName,
                IndexName = "UserIndex" // Usamos el GSI de forma limpia
            };

            // En el modelo de objetos, el Query se hace sobre la Partition Key del índice
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
            // SaveAsync hace el PutItem completo mapeando el objeto automáticamente
            await _context.SaveAsync(card, config);
        }
    }
}
