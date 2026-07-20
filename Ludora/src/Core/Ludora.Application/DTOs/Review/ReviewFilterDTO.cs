using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record ReviewFilterDTO
{
    public ReviewSort SortBy { get; init; } = ReviewSort.Newest;
    public double? Rating { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
