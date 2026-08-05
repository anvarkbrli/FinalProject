import type { ReactNode } from "react";
import type { SocialPlatform } from "../../types/profile";

interface SocialPlatformIconProps {
  platform: SocialPlatform;
  size?: number;
}

function BrandSvg({
  size,
  children,
  viewBox = "0 0 24 24",
}: {
  size: number;
  children: ReactNode;
  viewBox?: string;
}) {
  return (
    <svg
      aria-hidden="true"
      viewBox={viewBox}
      width={size}
      height={size}
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className="shrink-0"
    >
      {children}
    </svg>
  );
}

export function SocialPlatformIcon({
  platform,
  size = 18,
}: SocialPlatformIconProps) {
  switch (platform) {
    case "Steam":
      return (
        <BrandSvg size={size}>
          <circle cx="16.4" cy="7.6" r="4.1" stroke="currentColor" strokeWidth="1.8" />
          <circle cx="16.4" cy="7.6" r="1.8" fill="currentColor" />
          <circle cx="7" cy="16.5" r="2.5" stroke="currentColor" strokeWidth="1.8" />
          <path d="M9.2 15.3l4.2-3.1 2.1.9M2.6 14.2l2.2.9" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" />
        </BrandSvg>
      );

    case "Instagram":
      return (
        <BrandSvg size={size}>
          <rect x="3" y="3" width="18" height="18" rx="5" stroke="currentColor" strokeWidth="2" />
          <circle cx="12" cy="12" r="4.2" stroke="currentColor" strokeWidth="2" />
          <circle cx="17.5" cy="6.6" r="1.2" fill="currentColor" />
        </BrandSvg>
      );

    case "Spotify":
      return (
        <BrandSvg size={size}>
          <circle cx="12" cy="12" r="10" fill="currentColor" />
          <path d="M7 9.2c3.6-1 7.8-.7 10.8.9M7.8 12.5c3-0.8 6.4-.5 9 .8M8.5 15.5c2.4-.6 5-.4 7 .6" stroke="#0b0b10" strokeWidth="1.7" strokeLinecap="round" />
        </BrandSvg>
      );

    case "Letterboxd":
      return (
        <BrandSvg size={size}>
          <circle cx="8" cy="12" r="5" fill="#FF8000" />
          <circle cx="16" cy="12" r="5" fill="#40BCF4" />
          <path d="M12 7a5 5 0 0 1 0 10 5 5 0 0 1 0-10Z" fill="#00E054" />
        </BrandSvg>
      );

    case "EpicGames":
      return (
        <BrandSvg size={size} viewBox="0 0 28 32">
          <path d="M3 2h22v21.2L14 30 3 23.2V2Z" fill="currentColor" />
          <path d="M7 6.3h14v2.3H9.7v2h9.7v2.2H9.7v2.1h11v2.3H7V6.3Z" fill="currentColor" className="text-[#0b0b10]" />
          <path d="M8.1 20.1h11.8v2.3h-4.5v3.1h-2.8v-3.1H8.1v-2.3Z" fill="currentColor" className="text-[#0b0b10]" />
        </BrandSvg>
      );

    case "Discord":
      return (
        <BrandSvg size={size}>
          <path d="M7.2 6.3A16 16 0 0 1 12 5.5c1.7 0 3.3.3 4.8.8 1.6 2.3 2.4 4.7 2.2 7.3-1.7 1.3-3.4 2.1-5.2 2.5l-.7-1.1c.9-.3 1.8-.8 2.6-1.4-2.5 1.2-5 1.2-7.5 0 .8.6 1.7 1.1 2.6 1.4l-.7 1.1a13 13 0 0 1-5.2-2.5c-.2-2.6.6-5 2.3-7.3Z" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" />
          <circle cx="9.2" cy="11.1" r="1.2" fill="currentColor" />
          <circle cx="14.8" cy="11.1" r="1.2" fill="currentColor" />
        </BrandSvg>
      );

    case "X":
      return (
        <BrandSvg size={size}>
          <path d="M5 4l14 16M19 4 5 20" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
        </BrandSvg>
      );

    case "Twitch":
      return (
        <BrandSvg size={size}>
          <path d="M5 3h15v11l-4 4h-4l-2.4 2.4V18H5V3Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
          <path d="M10 7v5M15 7v5" stroke="currentColor" strokeWidth="1.8" />
        </BrandSvg>
      );

    case "YouTube":
      return (
        <BrandSvg size={size}>
          <path d="M3.4 8.2A3.2 3.2 0 0 1 6.6 5h10.8a3.2 3.2 0 0 1 3.2 3.2v7.6a3.2 3.2 0 0 1-3.2 3.2H6.6a3.2 3.2 0 0 1-3.2-3.2V8.2Z" fill="currentColor" />
          <path d="m10 9 5 3-5 3V9Z" fill="#0b0b10" />
        </BrandSvg>
      );

    case "GitHub":
      return (
        <BrandSvg size={size}>
          <path fill="currentColor" fillRule="evenodd" d="M12 2.3a9.9 9.9 0 0 0-3.1 19.3c.5.1.7-.2.7-.5v-1.9c-2.8.6-3.4-1.2-3.4-1.2-.5-1.2-1.1-1.5-1.1-1.5-.9-.6.1-.6.1-.6 1 0 1.6 1 1.6 1 .9 1.6 2.4 1.1 3 .9.1-.7.4-1.1.7-1.3-2.3-.3-4.7-1.1-4.7-5a3.9 3.9 0 0 1 1-2.7c-.1-.3-.4-1.3.1-2.7 0 0 .9-.3 2.8 1a9.6 9.6 0 0 1 5.1 0c1.9-1.3 2.8-1 2.8-1 .5 1.4.2 2.4.1 2.7a3.9 3.9 0 0 1 1 2.7c0 3.9-2.4 4.7-4.7 5 .4.3.7 1 .7 2v3c0 .3.2.6.7.5A9.9 9.9 0 0 0 12 2.3Z" clipRule="evenodd" />
        </BrandSvg>
      );

    case "TikTok":
      return (
        <BrandSvg size={size}>
          <path d="M14 4v10.2a4.2 4.2 0 1 1-3.1-4.1" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" />
          <path d="M14 4c.8 2.7 2.5 4.2 5 4.5" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" />
        </BrandSvg>
      );

    case "Reddit":
      return (
        <BrandSvg size={size}>
          <circle cx="12" cy="13" r="7" stroke="currentColor" strokeWidth="1.8" />
          <circle cx="9" cy="12" r="1" fill="currentColor" />
          <circle cx="15" cy="12" r="1" fill="currentColor" />
          <path d="M9 15c1.8 1.2 4.2 1.2 6 0M14 6l1.1-3 3 1" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
          <circle cx="18.8" cy="4.2" r="1.4" stroke="currentColor" strokeWidth="1.4" />
        </BrandSvg>
      );

    case "Xbox":
      return (
        <BrandSvg size={size}>
          <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.8" />
          <path d="M7 7c2.2.3 3.8 1.4 5 3.1C13.2 8.4 14.8 7.3 17 7M6.5 17.5c1.5-3 3.3-5.5 5.5-7.4 2.2 1.9 4 4.4 5.5 7.4" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
        </BrandSvg>
      );

    case "PlayStation":
      return (
        <BrandSvg size={size}>
          <path d="M10 3.5c4.4.6 6.4 2 6.4 4.6 0 2.4-1.5 3.8-4 4.2V6.7L10 6.1v11.8l-2.5-.8V4.8L10 3.5Z" fill="currentColor" />
          <path d="M13.2 15.1c2.9-1 5.4-.8 5.7.4.4 1.4-2.4 3.3-6.2 4.2-3.3.8-6.3.6-7.3-.3-.8-.8.3-1.9 2.7-2.8v1.5c-1 .4-1.4.8-1.1 1.1.5.5 2.5.4 4.5-.1 2.4-.6 4.2-1.6 4-2.2-.2-.5-1.1-.6-2.3-.3v-1.5Z" fill="currentColor" />
        </BrandSvg>
      );
  }
}
