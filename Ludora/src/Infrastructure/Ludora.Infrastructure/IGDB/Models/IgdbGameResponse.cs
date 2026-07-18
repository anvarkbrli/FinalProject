using Ludora.Domain.Enums;
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
    [JsonPropertyName("game_type")]
    public GameType? GameType { get; set; }

    [JsonPropertyName("parent_game")]
    public long? ParentGameId { get; set; }

    [JsonPropertyName("dlcs")]
    public List<long>? DlcIds { get; set; }

    [JsonPropertyName("expansions")]
    public List<long>? ExpansionIds { get; set; }

    [JsonPropertyName("standalone_expansions")]
    public List<long>? StandaloneExpansionIds { get; set; }

    [JsonPropertyName("bundles")]
    public List<long>? BundleIds { get; set; }

    [JsonPropertyName("remakes")]
    public List<long>? RemakeIds { get; set; }

    [JsonPropertyName("remasters")]
    public List<long>? RemasterIds { get; set; }

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
