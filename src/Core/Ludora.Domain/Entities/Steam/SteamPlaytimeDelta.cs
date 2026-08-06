namespace Ludora.Domain.Entities.Steam
{
    public sealed class SteamPlaytimeDelta
    {
        public long Id { get; set; }
        public long UserGamePlayLogId { get; set; }
        public int DeltaMinutes { get; set; }
        public int TotalPlaytimeMinutes { get; set; }
        public DateOnly OccurredOn { get; set; }
        public DateTime SyncedAtUtc { get; set; }

        public UserGamePlayLog UserGamePlayLog { get; set; } = null!;
    }
}
