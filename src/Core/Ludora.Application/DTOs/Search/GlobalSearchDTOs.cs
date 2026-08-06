using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public sealed record GlobalSearchFilterDTO
    {
        public string Query { get; init; } = string.Empty;
        public int Limit { get; init; } = 20;
    }


    public sealed record SearchPageFilterDTO
    {
        public string Query { get; init; } = string.Empty;
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }

    public sealed record GlobalSearchCountsDTO
    {
        public int Games { get; init; }
        public int Users { get; init; }
        public int Discussions { get; init; }
        public int Reviews { get; init; }
        public int Lists { get; init; }
        public int Total => checked(Games + Users + Discussions + Reviews + Lists);
    }

    public sealed record GlobalSearchResultDTO
    {
        public string Query { get; init; } = string.Empty;
        public GlobalSearchCountsDTO Counts { get; init; } = new();
        public IReadOnlyList<SearchGameResultDTO> Games { get; init; } = [];
        public IReadOnlyList<SearchUserResultDTO> Users { get; init; } = [];
        public IReadOnlyList<SearchDiscussionResultDTO> Discussions { get; init; } = [];
        public IReadOnlyList<SearchReviewResultDTO> Reviews { get; init; } = [];
        public IReadOnlyList<SearchGameListResultDTO> Lists { get; init; } = [];
    }

    public sealed record SearchGameResultDTO
    {
        public CatalogSource Source { get; init; }
        public long Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? CoverImageUrl { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public double? AverageRating { get; init; }
        public int Score { get; init; }
    }

    public sealed record SearchUserResultDTO
    {
        public Guid Id { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public string? Bio { get; init; }
        public int Score { get; init; }
    }

    public sealed record SearchDiscussionResultDTO
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string AuthorUserName { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public int Score { get; init; }
    }

    public sealed record SearchReviewResultDTO
    {
        public long Id { get; init; }
        public Guid UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public CatalogSource Source { get; init; }
        public long GameId { get; init; }
        public string GameName { get; init; } = string.Empty;
        public string? GameCoverImageUrl { get; init; }
        public string ContentPreview { get; init; } = string.Empty;
        public bool ContainsSpoilers { get; init; }
        public bool IsMine { get; init; }
        public DateTime CreatedAt { get; init; }
        public int Score { get; init; }
    }

    public sealed record SearchGameListResultDTO
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public ListVisibility Visibility { get; init; }
        public bool IsRanked { get; init; }
        public Guid OwnerId { get; init; }
        public string OwnerUserName { get; init; } = string.Empty;
        public int GameCount { get; init; }
        public DateTime CreatedAt { get; init; }
        public int Score { get; init; }
    }
}
