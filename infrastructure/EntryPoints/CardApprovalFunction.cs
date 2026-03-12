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
            
            var commandHandler = ServiceProvider.GetRequiredService<CreateCardCommandHandler>();

            foreach (var record in sqsEvent.Records)
            {
                try
                {
                    context.Logger.LogInformation($"Procesando mensaje SQS ID: {record.MessageId}");


                    var cardRequest = JsonSerializer.Deserialize<CardRequestMessage>(
                        record.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (cardRequest != null)
                    {

                        await commandHandler.ExecuteAsync(cardRequest);
                        context.Logger.LogInformation($"Solicitud de tarjeta {cardRequest.request} procesada para usuario {cardRequest.UserId}");
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Error procesando mensaje {record.MessageId}: {ex.Message}");

                    throw;
                }
            }
        }
    }
}
