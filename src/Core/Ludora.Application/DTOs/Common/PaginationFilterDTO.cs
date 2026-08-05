namespace Ludora.Application.DTOs;

public sealed record PaginationFilterDTO
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
