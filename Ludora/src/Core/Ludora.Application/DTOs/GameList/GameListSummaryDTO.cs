using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameListSummaryDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public ListVisibility Visibility { get; set; }

        public Guid OwnerId { get; set; }

        public string OwnerUserName { get; set; } = null!;

        public int GameCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}