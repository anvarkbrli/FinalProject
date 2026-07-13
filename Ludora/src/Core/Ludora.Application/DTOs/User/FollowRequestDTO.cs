namespace Ludora.Application.DTOs;

public record FollowRequestDTO
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? ProfileImageUrl { get; set; }

    public DateTime RequestedAt { get; set; }
}