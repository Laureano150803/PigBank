using Amazon.DynamoDBv2.DataModel;

namespace DistributedSis.domain.entities
{
    public class User
    {
        [DynamoDBHashKey]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string IDnumber { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Image { get; set; }
    }
}
