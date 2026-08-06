namespace Ludora.Application.DTOs
{
    public record AdminPostFilterDTO
    {
        public string? Search { get; init; }
        public Guid? UserId { get; init; }
        public bool? IsDeleted { get; init; }
        public string? SortBy { get; init; } = "newest";
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }
}
