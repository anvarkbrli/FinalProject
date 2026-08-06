namespace Ludora.Application.DTOs
{
    public record ForumFeedFilterDTO
    {
        public string? Search { get; init; }
        public string? CategorySlug { get; init; }
        public string? Period { get; init; } = "week";
        public string? InterestGameTitles { get; init; }
        public string? InterestGenres { get; init; }
        public string? InterestPlayStyles { get; init; }
        public string? InterestPlatform { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }

    public record ForumFeedItemDTO
    {
        public string ItemType { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public DateTime PublishedAt { get; init; }
        public PostDTO? Post { get; init; }
        public DiscussionListItemDTO? Discussion { get; init; }
    }

    public record ForumUserSuggestionDTO
    {
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public int SharedGameCount { get; init; }
        public int PostCount { get; init; }
        public int DiscussionCount { get; init; }
        public int FollowerCount { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public record ForumFeedResultDTO
    {
        public IReadOnlyList<ForumFeedItemDTO> Items { get; init; } = [];
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public int TotalPages { get; init; }
        public IReadOnlyList<ForumCommunitySummaryDTO> SuggestedCommunities { get; init; } = [];
        public IReadOnlyList<ForumUserSuggestionDTO> SuggestedUsers { get; init; } = [];
    }
}
