using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DistributedSis.application.DTOs;
using DistributedSis.application.UseCases;
using System.Net;
using System.Text.Json;

namespace DistributedSis.infrastructure.EntryPoints
{
    public class TransactionFunctions : BaseFunction
    {

        public async Task<APIGatewayProxyResponse> PurchaseHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogInformation("Procesando compra con APIGatewayProxyRequest (V1)...");

            try
            {
                var commandHandler = ServiceProvider.GetRequiredService<PurchaseCommandHandler>();

                var purchaseRequest = JsonSerializer.Deserialize<PurchaseRequest>(request.Body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (purchaseRequest == null)
                {
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "Cuerpo de la solicitud vacío o inválido." });
                }

                await commandHandler.ExecuteAsync(purchaseRequest);

                return CreateResponse(HttpStatusCode.OK, new { message = "Compra realizada con éxito." });
            }
            catch (InvalidOperationException ex)
            {

                return CreateResponse(HttpStatusCode.BadRequest, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return CreateResponse(HttpStatusCode.NotFound, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error crítico: {ex.Message}");
                return CreateResponse(HttpStatusCode.InternalServerError, new { message = "Error interno del sistema." });
            }
        }
        public async Task<APIGatewayProxyResponse> SaveBalanceHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {

                if (!request.PathParameters.TryGetValue("card_id", out var cardId))
                {
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "Falta el ID de la tarjeta en la URL." });
                }

                var commandHandler = ServiceProvider.GetRequiredService<PurchaseCommandHandler>();



                var saveRequest = JsonSerializer.Deserialize<SaveBalanceRequest>(request.Body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                context.Logger.LogInformation("Antes de llegar");

                await commandHandler.SaveBalanceCommandHandler(cardId, saveRequest);
                context.Logger.LogInformation("Si llegó");


                return CreateResponse(HttpStatusCode.OK, new { message = "Saldo cargado exitosamente." });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error en SaveBalance: {ex.Message}");
                return CreateResponse(HttpStatusCode.InternalServerError, new { message = ex.Message });
            }
        }


        public async Task<APIGatewayProxyResponse> PaidCreditCardHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogInformation("Iniciando pago de tarjeta de crédito...");

            try
            {
                if (request.PathParameters == null || !request.PathParameters.TryGetValue("card_id", out var cardId))
                {
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "El parámetro card_id es requerido en la URL." });
                }

                var paymentRequest = JsonSerializer.Deserialize<PaidCreditardRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (paymentRequest == null || paymentRequest.Amount <= 0)
                {
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "Monto de pago inválido." });
                }

                var commandHandler = ServiceProvider.GetRequiredService<PurchaseCommandHandler>();
                await commandHandler.PaidCreditCardCommandHandler(cardId, paymentRequest.Amount);

                return CreateResponse(HttpStatusCode.OK, new { message = $"Pago de {paymentRequest.Amount} aplicado exitosamente a la tarjeta de crédito." });
            }
            catch (InvalidOperationException ex)
            {
                return CreateResponse(HttpStatusCode.BadRequest, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return CreateResponse(HttpStatusCode.NotFound, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error en PaidCreditCardHandler: {ex.Message}");
                return CreateResponse(HttpStatusCode.InternalServerError, new { message = "Error procesando el pago." });
            }
        }

        public async Task<APIGatewayProxyResponse> ActivateCardHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogInformation("Iniciando activación de tarjeta...");

            try
            {
                var activateRequest = JsonSerializer.Deserialize<ActivateCardRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (string.IsNullOrEmpty(activateRequest?.UserId))
                {
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "El campo userId es requerido en el cuerpo de la petición." });
                }

                var commandHandler = ServiceProvider.GetRequiredService<PurchaseCommandHandler>();

                await commandHandler.ActivateCardCommandHandler(activateRequest.UserId);

                return CreateResponse(HttpStatusCode.OK, new { message = "Tarjeta activada exitosamente tras validar las 10 transacciones." });
            }
            catch (InvalidOperationException ex)
            {

                return CreateResponse(HttpStatusCode.BadRequest, new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return CreateResponse(HttpStatusCode.NotFound, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error en ActivateCardHandler: {ex.Message}");
                return CreateResponse(HttpStatusCode.InternalServerError, new { message = "Error procesando la activación." });
            }
        }
        public async Task<APIGatewayProxyResponse> GetReportHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogInformation("Iniciando generación de reporte de transacciones...");

            try
            {

                if (request.PathParameters == null || !request.PathParameters.TryGetValue("card_id", out var cardId))
                {
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "El parámetro card_id es requerido en la URL." });
                }

     
                string startDate = request.QueryStringParameters?.ContainsKey("start") == true ? request.QueryStringParameters["start"] : null;
                string endDate = request.QueryStringParameters?.ContainsKey("end") == true ? request.QueryStringParameters["end"] : null;

                if (string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(request.Body))
                {
                    var body = JsonSerializer.Deserialize<JsonElement>(request.Body);
                    if (body.TryGetProperty("start", out var startProp)) startDate = startProp.GetString();
                    if (body.TryGetProperty("end", out var endProp)) endDate = endProp.GetString();
                }

                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                {
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "Se requieren las fechas 'start' y 'end'." });
                }


                var commandHandler = ServiceProvider.GetRequiredService<PurchaseCommandHandler>();


                string reportUrl = await commandHandler.GenerateReportAsync(cardId, startDate, endDate);


                return CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Reporte generado con éxito",
                    url = reportUrl
                });
            }
            catch (KeyNotFoundException ex)
            {
                return CreateResponse(HttpStatusCode.NotFound, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error en GetReportHandler: {ex.Message}");
                return CreateResponse(HttpStatusCode.InternalServerError, new { message = "Error interno generando el reporte." });
            }
        }


    }
}
