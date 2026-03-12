using DistributedSis.domain.entities;
using DistributedSis.domain.interfaces;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace DistributedSis.infrastructure.Repository
{
    public class DynamoDbUserRepository : IUserRepository
    {
        private readonly DynamoDBContext _context;
        public DynamoDbUserRepository(IAmazonDynamoDB dynamoDbClient)
        {
            _context = new DynamoDBContext(dynamoDbClient);
        }
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = "Users"
            };
            var conditions = new List<ScanCondition>
            {
                new ScanCondition("Email", ScanOperator.Equal, email)
            };
            var search = _context.ScanAsync<User>(conditions, config);
            var result = await search.GetNextSetAsync();
            return result.FirstOrDefault();
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = "Users"
            };
            var conditions = new List<ScanCondition>
            {
                new ScanCondition("Id", ScanOperator.Equal, userId)
            };
            var search = _context.ScanAsync<User>(conditions, config);
            var result = await search.GetNextSetAsync();
            return result.FirstOrDefault();
        }

        public async Task SaveUserAsync(User user)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = "Users"
            };
            await _context.SaveAsync(user, config);
        }

        public async Task UpdateUserAsync(User user)
        {
            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = "Users"
            };
            await _context.ScanAsync<User>(new List<ScanCondition> { new ScanCondition("Id", ScanOperator.Equal, user.Id) }, config)
                .GetNextSetAsync()
                .ContinueWith(async searchResult =>
                {
                    var existingUser = searchResult.Result.FirstOrDefault();
                    if (existingUser != null)
                    {
                        existingUser.Name = user.Name;
                        existingUser.Email = user.Email;
                        existingUser.Password = user.Password;
                        existingUser.IDnumber = user.IDnumber;
                        existingUser.Address = user.Address;
                        existingUser.Phone = user.Phone;
                        existingUser.Image = user.Image;
                        await _context.SaveAsync(existingUser, config);
                    }
                });
        }
    }
}
