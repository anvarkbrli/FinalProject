import type { SocialPlatform } from "../types/profile";

export const socialPlatformLabels: Record<SocialPlatform, string> = {
  Steam: "Steam",
  Discord: "Discord",
  X: "X / Twitter",
  Twitch: "Twitch",
  YouTube: "YouTube",
  Instagram: "Instagram",
  Spotify: "Spotify",
  Letterboxd: "Letterboxd",
  GitHub: "GitHub",
  TikTok: "TikTok",
  Reddit: "Reddit",
  Xbox: "Xbox",
  PlayStation: "PlayStation",
  EpicGames: "Epic Games",
};

export function detectSocialPlatform(value: string): SocialPlatform | null {
  try {
    const url = new URL(value.trim());
    const host = url.hostname.toLowerCase();
    const matches = (domain: string) =>
      host === domain || host.endsWith(`.${domain}`);

    if (matches("steamcommunity.com") || matches("store.steampowered.com")) return "Steam";
    if (matches("discord.com") || matches("discord.gg") || matches("discordapp.com")) return "Discord";
    if (matches("x.com") || matches("twitter.com")) return "X";
    if (matches("twitch.tv")) return "Twitch";
    if (matches("youtube.com") || matches("youtu.be")) return "YouTube";
    if (matches("instagram.com")) return "Instagram";
    if (matches("spotify.com")) return "Spotify";
    if (matches("letterboxd.com")) return "Letterboxd";
    if (matches("github.com")) return "GitHub";
    if (matches("tiktok.com")) return "TikTok";
    if (matches("reddit.com")) return "Reddit";
    if (matches("xbox.com")) return "Xbox";
    if (matches("playstation.com") || matches("my.playstation.com")) return "PlayStation";
    if (matches("epicgames.com") || matches("store.epicgames.com")) return "EpicGames";

    return null;
  } catch {
    return null;
  }
}
