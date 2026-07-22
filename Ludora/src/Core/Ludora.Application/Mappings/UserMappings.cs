namespace Ludora.Application.Mappings;

using Ludora.Application.DTOs;
using Ludora.Application.Helpers;
using Ludora.Domain.Entities.User;

public static class UserMappings
{
    public static CurrentUserDTO ToCurrentUserDTO(
        this AppUser user
    )
    {
        return new CurrentUserDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            SocialLinks = SocialLinkHelper.Deserialize(user.SocialLinksJson),
            IsPrivate = user.IsPrivate,
            CreatedAt = user.CreatedAt
        };
    }

    public static UserProfileDTO ToUserProfileDTO(
        this AppUser user
    )
    {
        return new UserProfileDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            SocialLinks = SocialLinkHelper.DeserializeWithAutomaticSteam(
                user.SocialLinksJson,
                user.SteamId,
                user.SteamProfileUrl),
            IsPrivate = user.IsPrivate,
            CreatedAt = user.CreatedAt,
            FollowerCount = 0,
            FollowingCount = 0,
            PostCount = 0
        };
    }
}