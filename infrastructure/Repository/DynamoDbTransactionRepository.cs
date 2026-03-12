using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;

namespace DistributedSis.infrastructure.Repository
{
    public class DynamoDbTransactionRepository : ITransactionRepository
    {
        private readonly DynamoDBContext _dynamoDb;
        private readonly string _tableName = Environment.GetEnvironmentVariable("TRANSACTION_TABLE") ?? "Transactions";

        public DynamoDbTransactionRepository(IAmazonDynamoDB dynamoDb)
        {
            _dynamoDb = new DynamoDBContext(dynamoDb); ;
        }
        public async Task<List<Transaction>> GetTransactionsByCardIdAsync(string cardId)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = _tableName,
                IndexName = "CardIdIndex" // <--- Importante mencionar el índice
            };

            var search = _dynamoDb.QueryAsync<Transaction>(cardId, config);
            return await search.GetNextSetAsync();
        }

        public async Task SaveTransactionAsync(Transaction transaction)
        {
            var config = new DynamoDBOperationConfig { OverrideTableName = _tableName };

            // SaveAsync mapea todo automáticamente y maneja palabras reservadas
            await _dynamoDb.SaveAsync(transaction, config);
        }
        public async Task<List<Transaction>> GetTransactionsReportAsync(string cardId, string startDate, string endDate)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = _tableName,
                IndexName = "CardIdIndex"
            };

            // DynamoDB busca la tarjeta y filtra solo las transacciones en ese rango de fechas
            var search = _dynamoDb.QueryAsync<Transaction>(
                cardId,
                QueryOperator.Between,
                new object[] { startDate, endDate },
                config
            );

            return await search.GetNextSetAsync();
        }
    }
}
