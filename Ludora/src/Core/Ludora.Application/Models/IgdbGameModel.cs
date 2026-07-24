using Ludora.Domain.Enums;

namespace Ludora.Application.Models
{
    public sealed record IgdbGameModel
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public string? CoverImageUrl { get; init; }
        public double? Rating { get; init; }

        public GameType GameType { get; init; } = GameType.MainGame;
        public long? ParentGameId { get; init; }
        public long? VersionParentId { get; init; }
        public string? VersionTitle { get; init; }
        public IReadOnlyList<long> DlcIds { get; init; } = [];
        public IReadOnlyList<long> ExpansionIds { get; init; } = [];
        public IReadOnlyList<long> StandaloneExpansionIds { get; init; } = [];
        public IReadOnlyList<long> BundleIds { get; init; } = [];
        public IReadOnlyList<long> RemakeIds { get; init; } = [];
        public IReadOnlyList<long> RemasterIds { get; init; } = [];
        public IReadOnlyList<IgdbGenreModel> Genres { get; init; } = [];
        public IReadOnlyList<IgdbPlatformModel> Platforms { get; init; } = [];
        public IReadOnlyList<IgdbCompanyRelationModel> Companies { get; init; } = [];
    }
}


