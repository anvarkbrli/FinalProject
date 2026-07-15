namespace Ludora.Application.Models;

public sealed record IgdbGameModel
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? CoverImageUrl { get; init; }
    public double? Rating { get; init; }
    public IReadOnlyList<IgdbGenreModel> Genres { get; init; } = Array.Empty<IgdbGenreModel>();
    public IReadOnlyList<IgdbPlatformModel> Platforms { get; init; } = Array.Empty<IgdbPlatformModel>();
    public IReadOnlyList<IgdbCompanyRelationModel> Companies { get; init; } = Array.Empty<IgdbCompanyRelationModel>();
}
