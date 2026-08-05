namespace Ludora.Application.DTOs;

public sealed record PostCommentDTO
{
    public long Id { get; init; }
    public long PostId { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsMine { get; init; }
    public bool CanDelete { get; init; }
}
