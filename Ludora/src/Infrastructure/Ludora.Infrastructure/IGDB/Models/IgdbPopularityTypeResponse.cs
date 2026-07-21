using System.Text.Json.Serialization;

namespace Ludora.Infrastructure.IGDB.Models;

internal sealed class IgdbPopularityTypeResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
