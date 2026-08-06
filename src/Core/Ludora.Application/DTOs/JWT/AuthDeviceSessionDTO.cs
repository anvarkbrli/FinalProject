namespace Ludora.Application.DTOs.JWT
{
    public sealed record AuthDeviceSessionDTO
    {
        public Guid Id { get; init; }
        public string DeviceName { get; init; } = string.Empty;
        public string Browser { get; init; } = string.Empty;
        public string OperatingSystem { get; init; } = string.Empty;
        public string? IpAddress { get; init; }
        public DateTime SignedInAtUtc { get; init; }
        public DateTime LastActiveAtUtc { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
        public bool IsCurrent { get; init; }
    }

    public sealed record RevokeAuthDeviceSessionResultDTO
    {
        public bool CurrentSessionRevoked { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
