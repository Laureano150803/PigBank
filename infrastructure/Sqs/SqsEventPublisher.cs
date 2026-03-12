using DistributedSis.domain.interfaces;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace DistributedSis.infrastructure.Sqs
{
    public class SqsEventPublisher : IEventPublisher
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly string _queueUrl;
        public SqsEventPublisher(IAmazonSQS sqsClient, string queueUrl)
        {
            _sqsClient = sqsClient;
            _queueUrl = queueUrl;
        }
        public async Task PublishCardRequestAsync(string userId, string requestType)
        {
            var payload = new
            {
                userId = userId,
                request = requestType
            };

            var messageBody = JsonSerializer.Serialize(payload);

            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = messageBody
            };

           
            await _sqsClient.SendMessageAsync(request);
        }

        public async Task PublishNotificationAsync<T>(string type, T data)
        {
            // Obtenemos la URL de la cola de notificaciones desde el entorno
            var notificationQueueUrl = Environment.GetEnvironmentVariable("NOTIFICATION_QUEUE_URL");

            if (string.IsNullOrEmpty(notificationQueueUrl))
            {
                throw new InvalidOperationException("Falta la variable de entorno NOTIFICATION_QUEUE_URL.");
            }

            var payload = new
            {
                type = type,
                data = data
            };

            // Usamos CamelCase para asegurar que "type" y "data" queden en minúscula en el JSON
            var messageBody = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var request = new SendMessageRequest
            {
                QueueUrl = notificationQueueUrl,
                MessageBody = messageBody
            };

            await _sqsClient.SendMessageAsync(request);
        }
    }
    
}
