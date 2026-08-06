using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class Review
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public CatalogSource Source { get; set; }
        public long GameId { get; set; }
        public string Content { get; set; } = null!;
        public bool ContainsSpoilers { get; set; }
        public DateOnly? PlayedOn { get; set; }
        public CatalogSource? PlatformSource { get; set; }
        public long? PlatformId { get; set; }
        public int? PlaytimeMinutes { get; set; }
        public bool ShowHardwareProfile { get; set; }
        public string? HardwareGpuName { get; set; }
        public string? HardwareCpuName { get; set; }
        public int? HardwareRamGb { get; set; }
        public int? HardwareVramGb { get; set; }
        public bool IsSteamAutoLogged { get; set; }
        public bool IsSteamPlaytimeOverridden { get; set; }
        public bool IsSteamPlayedOnOverridden { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }

        public AppUser User { get; set; } = null!;
        public ICollection<ReviewLike> Likes { get; set; } = new List<ReviewLike>();
        public ICollection<ReviewComment> Comments { get; set; } = new List<ReviewComment>();
    }
}
