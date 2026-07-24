using System.Text.Json.Serialization;

namespace Ludora.Infrastructure.IGDB.Models;

internal sealed class IgdbExternalGameResponse
{
    [JsonPropertyName("game")]
    public long GameId { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("external_game_source")]
    public int ExternalGameSource { get; set; }
}
