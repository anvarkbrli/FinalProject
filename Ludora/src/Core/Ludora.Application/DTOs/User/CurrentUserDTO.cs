namespace Ludora.Application.DTOs;

public record CurrentUserDTO
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public bool EmailConfirmed { get; set; }

    public string? Bio { get; set; }

    public string? ProfileImageUrl { get; set; }

    public IReadOnlyList<UserSocialLinkDTO> SocialLinks { get; set; } = [];

    public bool IsPrivate { get; set; }

    public string PreferredLanguage { get; set; } = "az";

    public DateTime CreatedAt { get; set; }

    public int FollowerCount { get; set; }

    public int FollowingCount { get; set; }

    public int PostCount { get; set; }

    public UserProfileStatisticsDTO Statistics { get; set; } = new();
}
