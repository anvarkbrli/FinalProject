using Ludora.Application.DTOs.Badge;

namespace Ludora.Application.DTOs;

public record UserProfileDTO
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? ProfileImageUrl { get; set; }

    public IReadOnlyList<UserSocialLinkDTO> SocialLinks { get; set; } = [];

    public bool IsPrivate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int FollowerCount { get; set; }

    public int FollowingCount { get; set; }

    public int PostCount { get; set; }

    public UserProfileStatisticsDTO Statistics { get; set; } = new();

    public IReadOnlyList<UserBadgeDTO> Badges { get; set; } = [];
}
