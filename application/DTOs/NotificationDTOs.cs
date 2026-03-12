using System.Text.Json;
using System.Text.Json.Serialization;

namespace DistributedSis.application.DTOs
{
    public record NotificationMessageDto(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("data")] JsonElement Data
        );
}
