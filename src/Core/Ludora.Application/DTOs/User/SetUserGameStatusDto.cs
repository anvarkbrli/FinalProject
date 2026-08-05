using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record SetUserGameStatusDTO
    {
        public UserGameStatus Status { get; set; }
    }
}
