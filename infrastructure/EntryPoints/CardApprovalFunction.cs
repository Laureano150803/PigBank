using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using DistributedSis.application.DTOs;
using DistributedSis.application.UseCases;
using System.Text.Json;
namespace DistributedSis.infrastructure.EntryPoints
{
    public class CardApprovalFunction : BaseFunction
    {
        public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
        {
            // 1. Resolver el CommandHandler desde el contenedor de dependencias
            var commandHandler = ServiceProvider.GetRequiredService<CreateCardCommandHandler>();

            foreach (var record in sqsEvent.Records)
            {
                try
                {
                    context.Logger.LogInformation($"Procesando mensaje SQS ID: {record.MessageId}");

                    // 2. Deserializar el cuerpo del mensaje
                    var cardRequest = JsonSerializer.Deserialize<CardRequestMessage>(
                        record.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (cardRequest != null)
                    {
                        // 3. Ejecutar el caso de uso (el que acabamos de refactorizar)
                        await commandHandler.ExecuteAsync(cardRequest);
                        context.Logger.LogInformation($"Solicitud de tarjeta {cardRequest.request} procesada para usuario {cardRequest.UserId}");
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Error procesando mensaje {record.MessageId}: {ex.Message}");

                    // IMPORTANTE: Relanzamos la excepción para que SQS sepa que falló 
                    // y lo mande a la DLQ (Dead Letter Queue) configurada en Terraform.
                    throw;
                }
            }
        }
    }
}
