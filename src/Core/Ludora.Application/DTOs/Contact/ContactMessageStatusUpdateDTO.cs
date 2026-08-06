using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Contact
{
    public sealed record ContactMessageStatusUpdateDTO
    {
        public ContactMessageStatus Status { get; init; }
    }
}
