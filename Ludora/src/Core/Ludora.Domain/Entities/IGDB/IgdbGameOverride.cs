namespace Ludora.Domain.Entities.IGDB;

public class IgdbGameOverride
{
    public int Id { get; set; }
    public long IgdbGameId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? CoverImageUrl { get; set; }
    public double? Rating { get; set; }
    public bool ReplaceGenres { get; set; }
    public bool ReplacePlatforms { get; set; }
    public bool ReplaceCompanies { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<IgdbGameGenreOverride> Genres { get; set; } = new List<IgdbGameGenreOverride>();
    public ICollection<IgdbGamePlatformOverride> Platforms { get; set; } = new List<IgdbGamePlatformOverride>();
    public ICollection<IgdbGameCompanyOverride> Companies { get; set; } = new List<IgdbGameCompanyOverride>();
}
