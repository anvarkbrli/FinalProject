using System.Text.Json.Serialization;

namespace Ludora.Infrastructure.IGDB.Models
{
    internal sealed class IgdbPopularityPrimitiveResponse
    {
        [JsonPropertyName("game_id")]
        public long GameId { get; set; }

        [JsonPropertyName("popularity_type")]
        public long PopularityTypeId { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }
    }
}
