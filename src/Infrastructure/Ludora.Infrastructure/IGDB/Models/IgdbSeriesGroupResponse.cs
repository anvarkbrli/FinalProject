using System.Text.Json.Serialization;
namespace Ludora.Infrastructure.IGDB.Models
{
    internal sealed class IgdbSeriesGroupResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("games")]
        public List<long>? GameIds { get; set; }
    }
}
