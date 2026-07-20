namespace Ludora.Application.DTOs;

public record AdminPostDTO
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public string? Content { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
    public Guid? DeletedByUserId { get; init; }
    public string? DeleteReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
