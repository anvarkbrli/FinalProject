namespace Ludora.Application.DTOs.JWT
{
    public sealed record AuthSessionResultDTO
    {
        public AuthResponseDTO Response { get; init; } = null!;
        public string RefreshToken { get; init; } = string.Empty;
        public DateTime RefreshTokenExpiresAtUtc { get; init; }
    }
}
