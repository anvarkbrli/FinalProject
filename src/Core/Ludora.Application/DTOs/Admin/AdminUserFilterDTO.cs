namespace Ludora.Application.DTOs
{
    public record AdminUserFilterDTO
    {
        public string? Search { get; init; }
        public string? Role { get; init; }
        public bool? IsBanned { get; init; }
        public string? SortBy { get; init; } = "newest";
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }
}
