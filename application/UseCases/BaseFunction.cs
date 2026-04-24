using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.SimpleEmailV2;
using Amazon.SQS;
using DistributedSis.domain.interfaces;
using DistributedSis.infrastructure.EntryPoints;
using DistributedSis.infrastructure.Repository;
using DistributedSis.infrastructure.Sqs;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
namespace DistributedSis.application.UseCases
{
    public abstract class BaseFunction
    {
        protected readonly IServiceProvider ServiceProvider;
        protected BaseFunction() {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient());
            services.AddSingleton<IAmazonSQS>(new AmazonSQSClient());
            services.AddTransient<IDynamoDBContext, DynamoDBContext>();

            //redis
            var redisEndpoint = Environment.GetEnvironmentVariable("REDIS_ENDPOINT") ?? "localhost";
            var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";

            var connectionString = $"{redisEndpoint}:{redisPort},abortConnect=false,ssl=false";
            services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(connectionString));
            // 2. Repositorios
            services.AddTransient<IUserRepository, DynamoDbUserRepository>();
            services.AddTransient<ICardRepository, DynamoDbCardRepository>();
            services.AddTransient<ITransactionRepository, DynamoDbTransactionRepository>();
            services.AddTransient<INotificationRepository, DynamoDbNotificationRepository>();
            services.AddTransient<ITemplateRepository, S3TemplateRepository>();
            services.AddTransient<IEmailSender, SesEmailSender>();
            services.AddAWSService<IAmazonS3>();
            services.AddAWSService<IAmazonSimpleEmailServiceV2>();
            services.AddTransient<IEventPublisher>(sp =>
            {
                var sqsClient = sp.GetRequiredService<IAmazonSQS>();
                var queueUrl = Environment.GetEnvironmentVariable("CREATE_REQUEST_CARD_SQS_URL") ?? "";
                return new SqsEventPublisher(sqsClient, queueUrl);
            });

            services.AddTransient<RegisterUserCommandHandler>();
            services.AddTransient<LoginUserCommandHandler>();
            services.AddTransient<CreateCardCommandHandler>();
            services.AddTransient<PurchaseCommandHandler>();
            services.AddTransient<NotificationCommandHandler>();
            services.AddTransient<CatalogQueryHandler>();
        }
        protected APIGatewayProxyResponse CreateResponse(HttpStatusCode statusCode, object body)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)statusCode,
                Body = JsonSerializer.Serialize(body),
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" } 
                }
            };
        }
    }
}
