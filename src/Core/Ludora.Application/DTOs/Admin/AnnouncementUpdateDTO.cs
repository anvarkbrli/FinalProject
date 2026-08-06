using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Admin
{
    public record AnnouncementUpdateDTO
    {
        public string? Title { get; init; }
        public string? Content { get; init; }
        public AnnouncementAudience Audience { get; init; } = AnnouncementAudience.Everyone;
        public bool IsPublished { get; init; }
    }
}
