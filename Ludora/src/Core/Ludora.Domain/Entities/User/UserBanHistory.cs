using Ludora.Domain.Enums;


namespace Ludora.Domain.Entities.User
{
    public class UserBanHistory
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;
        public Guid BannedByUserId { get; set; }
        public AppUser BannedByUser { get; set; } = null!;
        public BanType BanType { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime BannedAtUtc { get; set; }
        public DateTime? BannedUntilUtc { get; set; }
        public DateTime? UnbannedAtUtc { get; set; }
        public Guid? UnbannedByUserId { get; set; }
        public AppUser? UnbannedByUser { get; set; }
    }
}
