namespace Ludora.Application.DTOs;

public record WishlistedFriendDTO
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
}
