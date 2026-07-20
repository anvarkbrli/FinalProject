namespace Ludora.Application.DTOs;

public sealed record ReviewLikeUserDTO
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
    public DateTime LikedAt { get; init; }
}
