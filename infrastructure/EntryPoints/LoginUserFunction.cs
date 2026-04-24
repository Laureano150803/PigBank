using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DistributedSis.application.DTOs;
using DistributedSis.application.UseCases;
using DistributedSis.infrastructure.Repository;
using DistributedSis.domain.interfaces;
using System.Net;
using System.Text.Json;

namespace DistributedSis.infrastructure.EntryPoints;

public class LoginUserFunction : BaseFunction
{ 
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation("Iniciando proceso de login.");

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return this.CreateResponse(HttpStatusCode.BadRequest, new { error = "El cuerpo de la petición es requerido." });
            }

            var loginRequest = JsonSerializer.Deserialize<LoginRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (loginRequest == null || string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                return this.CreateResponse(HttpStatusCode.BadRequest, new { error = "Email y password son obligatorios." });
            }

            var commandHandler = this.ServiceProvider.GetRequiredService<LoginUserCommandHandler>();

            var result = await commandHandler.ExecuteAsync(loginRequest);

            return this.CreateResponse(HttpStatusCode.OK, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Logger.LogWarning($"Intento de login fallido: {ex.Message}");
            return this.CreateResponse(HttpStatusCode.Unauthorized, new { error = "Usuario o contraseña incorrectos." });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error interno en login: {ex.Message}");
            return this.CreateResponse(HttpStatusCode.InternalServerError, new { error = "Ocurrió un error procesando la solicitud." });
        }
    }

    
}