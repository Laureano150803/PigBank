using Amazon.S3;
using Amazon.S3.Model;
using DistributedSis.application.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace DistributedSis.application.UseCases
{
    public class CatalogQueryHandler
    {
        private readonly IConnectionMultiplexer _redis;
        private const string REDIS_CATALOG_KEY = "catalog:services";
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName = Environment.GetEnvironmentVariable("CATALOG_BUCKET_NAME") ?? "mi-bucket-catalogo-default";

        public CatalogQueryHandler(IConnectionMultiplexer redis, IAmazonS3 s3Client)
        {
            _redis = redis;
            _s3Client = s3Client;
        }

        public async Task<string?> GetCatalogQueryHandler()
        {
            var db = _redis.GetDatabase();
            var catalogJson = await db.StringGetAsync(REDIS_CATALOG_KEY);
            return catalogJson; // Retornará null si no existe
        }
        public async Task<int> UpdateCatalogCommandHandler(string csvContent)
        {
            // 1. Subir a S3 como respaldo
            var fileName = $"catalog_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                ContentBody = csvContent,
                ContentType = "text/csv"
            };
            await _s3Client.PutObjectAsync(putRequest);

            // 2. Parsear el CSV a Lista
            var catalogItems = ParseCsv(csvContent);

            // 3. Guardar en Redis reemplazando lo anterior
            var db = _redis.GetDatabase();
            var jsonToStore = JsonSerializer.Serialize(catalogItems);
            await db.StringSetAsync(REDIS_CATALOG_KEY, jsonToStore);

            return catalogItems.Count; // Retornamos cuántos items procesamos
        }
        private List<ServiceCatalogItem> ParseCsv(string csvContent)
        {
            var items = new List<ServiceCatalogItem>();
            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++)
            {
                var columns = lines[i].Split(',');
                if (columns.Length >= 7)
                {
                    items.Add(new ServiceCatalogItem
                    {
                        Id = i,
                        Categoria = columns[0].Trim(),
                        Proveedor = columns[1].Trim(),
                        Servicio = columns[2].Trim(),
                        Plan = columns[3].Trim(),
                        PrecioMensual = decimal.TryParse(columns[4], out var price) ? price : 0,
                        Detalles = columns[5].Trim(),
                        Estado = columns[6].Trim()
                    });
                }
            }
            return items;
        }
    }
}
