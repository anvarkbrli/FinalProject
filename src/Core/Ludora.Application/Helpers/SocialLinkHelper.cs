using System.Text.Json;

using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Domain.Enums;

namespace Ludora.Application.Helpers
{
    public static class SocialLinkHelper
    {
        private const int MaxLinks = 8;
        private const int MaxUrlLength = 2048;

        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public static IReadOnlyList<UserSocialLinkDTO> Normalize(IEnumerable<string>? urls)
        {
            if (urls is null)
                return [];

            var normalizedUrls = urls
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            if (normalizedUrls.Count > MaxLinks)
                throw new BadRequestException($"Maksimum {MaxLinks} sosial link əlavə etmək olar.");

            var result = new List<UserSocialLinkDTO>(normalizedUrls.Count);
            var usedPlatforms = new HashSet<SocialPlatform>();

            foreach (var value in normalizedUrls)
            {
                if (value.Length > MaxUrlLength)
                    throw new BadRequestException("Sosial link maksimum 2048 simvol ola bilər.");

                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                {
                    throw new BadRequestException("Sosial link düzgün HTTP/HTTPS URL olmalıdır.");
                }

                var platform = DetectPlatform(uri)
                    ?? throw new BadRequestException(
                        "Dəstəklənməyən sosial linkdir. Steam, Discord, X/Twitter, Twitch, YouTube, Instagram, Spotify, Letterboxd, GitHub, TikTok, Reddit, Xbox, PlayStation və Epic Games linkləri qəbul edilir.");

                if (!usedPlatforms.Add(platform))
                    throw new ConflictException($"{platform} üçün yalnız bir sosial link əlavə etmək olar.");

                var builder = new UriBuilder(uri)
                {
                    Fragment = string.Empty,
                    Scheme = Uri.UriSchemeHttps,
                    Port = -1
                };

                result.Add(new UserSocialLinkDTO
                {
                    Platform = platform,
                    Url = builder.Uri.AbsoluteUri.TrimEnd('/')
                });
            }

            return result;
        }

        public static string? Serialize(IReadOnlyList<UserSocialLinkDTO> links)
        {
            return links.Count == 0
                ? null
                : JsonSerializer.Serialize(links, SerializerOptions);
        }

        public static IReadOnlyList<UserSocialLinkDTO> Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<UserSocialLinkDTO>>(json, SerializerOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        public static IReadOnlyList<UserSocialLinkDTO> DeserializeWithAutomaticSteam(
            string? json,
            string? steamId,
            string? steamProfileUrl)
        {
            var links = Deserialize(json).ToList();

            if (string.IsNullOrWhiteSpace(steamId) ||
                links.Any(x => x.Platform == SocialPlatform.Steam))
            {
                return links;
            }

            var profileUrl = string.IsNullOrWhiteSpace(steamProfileUrl)
                ? $"https://steamcommunity.com/profiles/{steamId.Trim()}"
                : steamProfileUrl.Trim();

            links.Add(new UserSocialLinkDTO
            {
                Platform = SocialPlatform.Steam,
                Url = profileUrl
            });

            return links;
        }

        private static SocialPlatform? DetectPlatform(Uri uri)
        {
            var host = uri.Host.ToLowerInvariant();
            if (MatchesHost(host, "steamcommunity.com") || MatchesHost(host, "store.steampowered.com"))
                return SocialPlatform.Steam;

            if (MatchesHost(host, "discord.com") || MatchesHost(host, "discord.gg") || MatchesHost(host, "discordapp.com"))
                return SocialPlatform.Discord;

            if (MatchesHost(host, "x.com") || MatchesHost(host, "twitter.com"))
                return SocialPlatform.X;

            if (MatchesHost(host, "twitch.tv"))
                return SocialPlatform.Twitch;

            if (MatchesHost(host, "youtube.com") || MatchesHost(host, "youtu.be"))
                return SocialPlatform.YouTube;

            if (MatchesHost(host, "instagram.com"))
                return SocialPlatform.Instagram;

            if (MatchesHost(host, "spotify.com"))
                return SocialPlatform.Spotify;

            if (MatchesHost(host, "letterboxd.com"))
                return SocialPlatform.Letterboxd;

            if (MatchesHost(host, "github.com"))
                return SocialPlatform.GitHub;

            if (MatchesHost(host, "tiktok.com"))
                return SocialPlatform.TikTok;

            if (MatchesHost(host, "reddit.com"))
                return SocialPlatform.Reddit;

            if (MatchesHost(host, "xbox.com") || MatchesHost(host, "account.xbox.com"))
                return SocialPlatform.Xbox;

            if (MatchesHost(host, "playstation.com") || MatchesHost(host, "my.playstation.com"))
                return SocialPlatform.PlayStation;

            if (MatchesHost(host, "epicgames.com") || MatchesHost(host, "store.epicgames.com"))
                return SocialPlatform.EpicGames;

            return null;
        }

        private static bool MatchesHost(string host, string expected)
        {
            return host.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith($".{expected}", StringComparison.OrdinalIgnoreCase);
        }
    }
}
