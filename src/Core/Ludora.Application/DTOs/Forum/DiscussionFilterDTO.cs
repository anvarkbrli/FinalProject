namespace Ludora.Application.DTOs
{
    public record DiscussionFilterDTO
    {
        public string? Search { get; init; }
        public string? CategorySlug { get; init; }
        public string? CommunitySlug { get; init; }
        public string? SortBy { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }
}
