using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DistributedSis.application.DTOs;
using DistributedSis.application.UseCases;
using DistributedSis.infrastructure.Repository;
using DistributedSis.domain.interfaces;
using System.Net;
using System.Text.Json;

// Asegúrate de incluir el serializador
namespace DistributedSis.infrastructure.EntryPoints;

public class LoginUserFunction : BaseFunction
{ 
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation("Iniciando proceso de login.");

            // 1. Validar que el cuerpo de la petición no esté vacío
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

            // 3. Resolver el caso de uso desde el contenedor DI
            var commandHandler = this.ServiceProvider.GetRequiredService<LoginUserCommandHandler>();

            var result = await commandHandler.ExecuteAsync(loginRequest);

            // 5. Retornar HTTP 200 OK con el Token
            return this.CreateResponse(HttpStatusCode.OK, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Capturamos la excepción específica de credenciales inválidas que lanzamos en el CommandHandler
            context.Logger.LogWarning($"Intento de login fallido: {ex.Message}");
            return this.CreateResponse(HttpStatusCode.Unauthorized, new { error = "Usuario o contraseña incorrectos." });
        }
        catch (Exception ex)
        {
            // Capturamos cualquier otro error inesperado (ej. caída de DynamoDB)
            context.Logger.LogError($"Error interno en login: {ex.Message}");
            return this.CreateResponse(HttpStatusCode.InternalServerError, new { error = "Ocurrió un error procesando la solicitud." });
        }
    }

    
}