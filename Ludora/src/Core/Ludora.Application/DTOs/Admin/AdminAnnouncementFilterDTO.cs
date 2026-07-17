namespace Ludora.Application.DTOs.Admin;

public record AdminAnnouncementFilterDTO
{
    public string? Search { get; init; }
    public bool? IsPublished { get; init; }
    public bool? IsDeleted { get; init; }
    public string? SortBy { get; init; } = "newest";
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
