using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record BanUserDTO
    {
        public BanType Type { get; init; }
        public int? DurationMinutes { get; init; }
        public string? Reason { get; init; }
    }
}


