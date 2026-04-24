using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DistributedSis.application.UseCases;
using System.Net;
using System.Text;

namespace DistributedSis.infrastructure.EntryPoints
{
    public class CatalogFunctions : BaseFunction
    {

        public async Task<APIGatewayProxyResponse> GetCatalogFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation("Iniciando obtención del catálogo desde Redis.");

                var queryHandler = this.ServiceProvider.GetRequiredService<CatalogQueryHandler>();
                var catalogJson = await queryHandler.GetCatalogQueryHandler();

                if (string.IsNullOrEmpty(catalogJson))
                {
                    return this.CreateResponse(HttpStatusCode.NotFound, new { message = "Catálogo no encontrado." });
                }

                // Aquí construimos la respuesta manual porque ya tenemos el JSON como string desde Redis,
                // usar CreateResponse volvería a serializar el string y escaparía las comillas.
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = catalogJson,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "Access-Control-Allow-Origin", "*" }
                    }
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error obteniendo catálogo: {ex.Message}");
                return this.CreateResponse(HttpStatusCode.InternalServerError, new { error = "Ocurrió un error procesando la solicitud." });
            }
        }

        public async Task<APIGatewayProxyResponse> UpdateCatalogFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation("Iniciando actualización de catálogo.");

                if (string.IsNullOrEmpty(request.Body))
                {
                    return this.CreateResponse(HttpStatusCode.BadRequest, new { error = "El cuerpo de la petición está vacío." });
                }

                // Manejo seguro del body por si API Gateway lo convierte a Base64
                string csvContent = request.IsBase64Encoded
                    ? Encoding.UTF8.GetString(Convert.FromBase64String(request.Body))
                    : request.Body;

                var commandHandler = this.ServiceProvider.GetRequiredService<CatalogQueryHandler>();
                var totalProcessed = await commandHandler.UpdateCatalogCommandHandler(csvContent);

                return this.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Catálogo actualizado exitosamente",
                    totalItems = totalProcessed
                });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error actualizando catálogo: {ex.Message}");
                return this.CreateResponse(HttpStatusCode.InternalServerError, new { error = "Ocurrió un error procesando el archivo CSV." });
            }
        }
    }
}