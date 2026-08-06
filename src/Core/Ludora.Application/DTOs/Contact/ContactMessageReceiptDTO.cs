using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Contact
{
    public sealed record ContactMessageReceiptDTO
    {
        public int Id { get; init; }
        public ContactMessageStatus Status { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
