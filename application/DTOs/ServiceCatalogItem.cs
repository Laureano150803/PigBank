using System.Text.Json.Serialization;

namespace DistributedSis.application.DTOs
{
    public class ServiceCatalogItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("categoria")]
        public string Categoria { get; set; }

        [JsonPropertyName("proveedor")]
        public string Proveedor { get; set; }

        [JsonPropertyName("servicio")]
        public string Servicio { get; set; }

        [JsonPropertyName("plan")]
        public string Plan { get; set; }

        [JsonPropertyName("precio_mensual")]
        public decimal PrecioMensual { get; set; }

        [JsonPropertyName("detalles")]
        public string Detalles { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; }
    }
}
