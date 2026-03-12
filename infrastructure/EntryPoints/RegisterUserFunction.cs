using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.DependencyInjection;
using Amazon.DynamoDBv2;
using Amazon.SQS;
using DistributedSis.application.UseCases;
using DistributedSis.application.DTOs;
using DistributedSis.domain.interfaces;
using DistributedSis.infrastructure.Repository;
using DistributedSis.infrastructure.Sqs;
using System.Net;

namespace DistributedSis.infrastructure.EntryPoints
{
    public class RegisterUserFunction : BaseFunction
    {
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation("Iniciando registro de usuario.");

                

                var registerRequest = JsonSerializer.Deserialize<CreateUserRequest>(
                    request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (registerRequest == null)
                {
                    return this.CreateResponse(HttpStatusCode.BadRequest, new { error = "El cuerpo de la petición es inválido." });
                }

                // 2. Resolver el caso de uso desde el contenedor
                var commandHandler = this.ServiceProvider.GetRequiredService<RegisterUserCommandHandler>();

                // 3. Ejecutar la orquestación (encripta, guarda en DynamoDB y envía a SQS)
                await commandHandler.Handle(registerRequest);

                // 4. Retornar un HTTP 201 Created si todo salió bien
                return this.CreateResponse(HttpStatusCode.Created, new { message = "Usuario registrado exitosamente." });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error fatal durante el registro: {ex.Message}");
                return this.CreateResponse(HttpStatusCode.InternalServerError, new { error = "Ocurrió un error procesando la solicitud." });
            }
        }
        public async Task<APIGatewayProxyResponse> GetProfileUserFunction(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation("Iniciando OBTENER de usuario.");
                if (!request.PathParameters.TryGetValue("user_id", out var userId))
                    return CreateResponse(HttpStatusCode.BadRequest, new { error = "User ID es requerido" });

                var handler = ServiceProvider.GetRequiredService<RegisterUserCommandHandler>();
                var user = await handler.GetUserProfileHandler(userId);

                return CreateResponse(HttpStatusCode.OK, user);
            }
            catch (KeyNotFoundException)
            {
                return CreateResponse(HttpStatusCode.NotFound, new { error = "Usuario no encontrado" });
            }
            catch (Exception ex)
            {
                return CreateResponse(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }
        public async Task<APIGatewayProxyResponse> UpdateUserFunction(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                if (!request.PathParameters.TryGetValue("user_id", out var userId))
                    return CreateResponse(HttpStatusCode.BadRequest, new { error = "User ID requerido" });

                var updateRequest = JsonSerializer.Deserialize<UpdateUserRequest>(request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var handler = ServiceProvider.GetRequiredService<RegisterUserCommandHandler>();
                await handler.UpdateUserProfileHandler(userId, updateRequest);

                return CreateResponse(HttpStatusCode.OK, new { message = "Perfil actualizado exitosamente" });
            }
            catch (Exception ex)
            {
                return CreateResponse(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }
        public async Task<APIGatewayProxyResponse> UploadAvatarUserFunction(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                if (!request.PathParameters.TryGetValue("user_id", out var userId))
                    return CreateResponse(HttpStatusCode.BadRequest, new { error = "User ID requerido" });

                var imageRequest = JsonSerializer.Deserialize<uploadImageRequest>(request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var handler = ServiceProvider.GetRequiredService<RegisterUserCommandHandler>();
                string fileName = await handler.UploadProfileImage(userId, imageRequest);

                return CreateResponse(HttpStatusCode.OK, new { message = "Imagen subida", path = fileName });
            }
            catch (Exception ex)
            {
                return CreateResponse(HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

    }
}
