namespace Ludora.Application.DTOs;

public record DiscussionReplyDTO
{
    public int Id { get; init; }
    public int DiscussionId { get; init; }

    public Guid AuthorId { get; init; }
    public string AuthorUserName { get; init; } = string.Empty;
    public string? AuthorProfileImageUrl { get; init; }

    public string Content { get; init; } = string.Empty;
    public int LikeCount { get; init; }
    public bool IsLikedByCurrentUser { get; init; }
    public bool IsDeleted { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
