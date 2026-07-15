using System.Text.Json.Serialization;

namespace Ludora.Infrastructure.IGDB.Models;

internal sealed class IgdbGameResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDate { get; set; }

    [JsonPropertyName("total_rating")]
    public double? TotalRating { get; set; }

    [JsonPropertyName("cover")]
    public IgdbCoverResponse? Cover { get; set; }

    [JsonPropertyName("genres")]
    public List<IgdbGenreResponse>? Genres { get; set; }

    [JsonPropertyName("platforms")]
    public List<IgdbPlatformResponse>? Platforms { get; set; }

    [JsonPropertyName("involved_companies")]
    public List<IgdbInvolvedCompanyResponse>? InvolvedCompanies { get; set; }
}

internal sealed class IgdbCoverResponse
{
    [JsonPropertyName("image_id")]
    public string? ImageId { get; set; }
}

internal sealed class IgdbInvolvedCompanyResponse
{
    [JsonPropertyName("company")]
    public IgdbCompanyResponse? Company { get; set; }

    [JsonPropertyName("developer")]
    public bool Developer { get; set; }

    [JsonPropertyName("publisher")]
    public bool Publisher { get; set; }
}
