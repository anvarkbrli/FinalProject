using System.Text.Json.Serialization;
namespace Ludora.Infrastructure.IGDB.Models
{
    internal sealed class IgdbCoverResponse
    {
        [JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
    }
}
