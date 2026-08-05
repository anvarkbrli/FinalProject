namespace Ludora.Application.DTOs;

public record ReviewCommentFilterDTO
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
