using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Home
{
    public sealed record HomeSocialDashboardDTO
    {
        public int FollowedUserCount { get; init; }
        public IReadOnlyList<UserActivityDTO> Activities { get; init; } = [];
        public IReadOnlyList<HomeFriendsPlayingGameDTO> FriendsPlaying { get; init; } = [];
        public IReadOnlyList<HomeFriendRatingDTO> FriendRatings { get; init; } = [];
        public IReadOnlyList<HomeFriendReviewDTO> FriendReviews { get; init; } = [];
        public HomeTasteMatchDTO? TasteMatch { get; init; }
        public IReadOnlyList<HomeFriendSuggestionDTO> FriendSuggestions { get; init; } = [];
    }

    public sealed record HomeSocialGameDTO
    {
        public CatalogSource Source { get; init; }
        public long Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? CoverImageUrl { get; init; }
    }

    public sealed record HomeSocialUserDTO
    {
        public Guid Id { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
    }

    public sealed record HomeFriendsPlayingGameDTO
    {
        public HomeSocialGameDTO Game { get; init; } = new();
        public IReadOnlyList<HomeSocialUserDTO> Friends { get; init; } = [];
        public int FriendCount { get; init; }
        public DateTime LastActivityAtUtc { get; init; }
    }

    public sealed record HomeFriendRatingDTO
    {
        public HomeSocialUserDTO User { get; init; } = new();
        public HomeSocialGameDTO Game { get; init; } = new();
        public double Rating { get; init; }
        public DateTime RatedAtUtc { get; init; }
    }


    public sealed record HomeFriendReviewDTO
    {
        public long ReviewId { get; init; }
        public HomeSocialUserDTO User { get; init; } = new();
        public HomeSocialGameDTO Game { get; init; } = new();
        public double? Rating { get; init; }
        public string Content { get; init; } = string.Empty;
        public bool ContainsSpoilers { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }

    public sealed record HomeTasteMatchDTO
    {
        public HomeSocialUserDTO User { get; init; } = new();
        public int MatchPercent { get; init; }
        public int SharedGameCount { get; init; }
        public int SharedFavoriteCount { get; init; }
        public IReadOnlyList<HomeSocialGameDTO> SharedGames { get; init; } = [];
    }

    public sealed record HomeFriendSuggestionDTO
    {
        public HomeSocialUserDTO User { get; init; } = new();
        public bool IsPrivate { get; init; }
        public int MutualFollowCount { get; init; }
        public int SharedGameCount { get; init; }
        public int SharedFavoriteCount { get; init; }
    }
}
