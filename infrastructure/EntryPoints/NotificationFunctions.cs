using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using DistributedSis.application.DTOs;
using DistributedSis.application.UseCases;
using System.Text.Json;

namespace DistributedSis.infrastructure.EntryPoints
{
    public class NotificationFunctions : BaseFunction
    {
        public async Task SendNotificationHandler(SQSEvent sqsEvent, ILambdaContext context)
        {
            context.Logger.LogInformation($"Procesando lote de {sqsEvent.Records.Count} notificaciones...");

            var commandHandler = ServiceProvider.GetRequiredService<NotificationCommandHandler>();

            foreach (var record in sqsEvent.Records)
            {
                try
                {
                    context.Logger.LogInformation($"Mensaje SQS recibido: {record.Body}");


                    var notificationEvent = JsonSerializer.Deserialize<NotificationMessageDto>(record.Body, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });


                    await commandHandler.ProcessNotificationAsync(notificationEvent);

                    context.Logger.LogInformation($"Notificación procesada exitosamente.");
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Error procesando mensaje SQS {record.MessageId}: {ex.Message}");

                    throw;
                }
            }
        }

        public async Task ErrorHandler(SQSEvent sqsEvent, ILambdaContext context)
        {
            context.Logger.LogInformation($"Procesando {sqsEvent.Records.Count} mensajes fallidos en la DLQ...");

            var commandHandler = ServiceProvider.GetRequiredService<NotificationCommandHandler>();

            foreach (var record in sqsEvent.Records)
            {
                try
                {
                    await commandHandler.ProcessErrorAsync(record.Body);
                    context.Logger.LogInformation($"Error guardado en base de datos para el mensaje {record.MessageId}.");
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Fallo crítico en DLQ: {ex.Message}");
                    throw;
                }
            }
        }
    }
}
