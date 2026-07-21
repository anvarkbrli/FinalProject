using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record ReviewDTO
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public CatalogSource Source { get; init; }
    public long GameId { get; init; }
    public string GameName { get; init; } = string.Empty;
    public string? GameSlug { get; init; }
    public string? GameCoverImageUrl { get; init; }
    public string Content { get; init; } = string.Empty;
    public double? Rating { get; init; }
    public UserGameStatus? Status { get; init; }
    public bool ContainsSpoilers { get; init; }
    public DateOnly? PlayedOn { get; init; }
    public CatalogSource? PlatformSource { get; init; }
    public long? PlatformId { get; init; }
    public string? PlatformName { get; init; }
    public int? PlaytimeMinutes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsMine { get; init; }
    public int LikeCount { get; init; }
    public int CommentCount { get; init; }
    public bool IsLikedByCurrentUser { get; init; }
}
