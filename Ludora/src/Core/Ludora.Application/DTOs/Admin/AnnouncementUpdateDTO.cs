namespace Ludora.Application.DTOs.Admin;

public record AnnouncementUpdateDTO
{
    public string? Title { get; init; }
    public string? Content { get; init; }
    public bool IsPublished { get; init; }
}
