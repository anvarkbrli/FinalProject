namespace Ludora.Application.Models.Steam
{
    public sealed record SteamComingSoonGameModel
    {
        public int Rank { get; init; }
        public int AppId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? HeaderImageUrl { get; init; }
        public string? SmallCapsuleImageUrl { get; init; }
        public string? PortraitImageUrl { get; init; }
        public string? ReleaseDateText { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public string StoreUrl { get; init; } = string.Empty;
        public bool IsDiscounted { get; init; }
        public int DiscountPercent { get; init; }
        public long? OriginalPriceMinor { get; init; }
        public long? FinalPriceMinor { get; init; }
        public string? Currency { get; init; }
    }
}
