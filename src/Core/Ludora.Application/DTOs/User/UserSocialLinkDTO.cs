using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record UserSocialLinkDTO
    {
        public SocialPlatform Platform { get; init; }
        public string Url { get; init; } = string.Empty;
    }
}
