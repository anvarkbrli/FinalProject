

using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Admin
{
    public record AdminUserBanHistoryItemDTO
    {
        public int Id { get; init; }
        public BanType Type { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DateTime BannedAtUtc { get; init; }
        public DateTime? BannedUntilUtc { get; init; }
        public Guid BannedByUserId { get; init; }
        public string BannedByUserName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool IsExpired { get; init; }
        public bool WasManuallyUnbanned { get; init; }
        public DateTime? UnbannedAtUtc { get; init; }
        public Guid? UnbannedByUserId { get; init; }
        public string? UnbannedByUserName { get; init; }
    }
}
