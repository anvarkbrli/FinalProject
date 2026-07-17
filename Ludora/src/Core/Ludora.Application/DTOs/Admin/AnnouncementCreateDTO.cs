namespace Ludora.Application.DTOs.Admin;

public record AnnouncementCreateDTO
{
    public string? Title { get; init; }
    public string? Content { get; init; }
    public bool IsPublished { get; init; } = true;
}
