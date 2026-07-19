using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserActivityDTO
{
    public long Id { get; init; }
    public UserActivityType Type { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? TargetUserId { get; init; }
    public string? TargetUserName { get; init; }
    public string? TargetUserProfileImageUrl { get; init; }
    public CatalogSource? GameSource { get; init; }
    public long? GameId { get; init; }
    public string? GameName { get; init; }
    public string? GameSlug { get; init; }
    public string? GameCoverImageUrl { get; init; }
    public long? ReviewId { get; init; }
    public UserGameStatus? Status { get; init; }
    public double? Rating { get; init; }
    public long? PostId { get; init; }
    public int? GameListId { get; init; }
    public string? SubjectTitle { get; init; }
}
