namespace Ludora.Application.DTOs.JWT
{
    public record UsernameAvailabilityDTO
    {
        public string UserName { get; init; } = string.Empty;
        public bool IsAvailable { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
