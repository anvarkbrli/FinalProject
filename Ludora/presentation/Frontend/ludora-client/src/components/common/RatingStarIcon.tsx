import { Star } from "lucide-react";

interface RatingStarIconProps {
  fillPercent: number;
  size: number;
  strokeWidth?: number;
  className?: string;
  emptyClassName?: string;
  filledClassName?: string;
}

export function RatingStarIcon({
  fillPercent,
  size,
  strokeWidth = 2,
  className = "",
  emptyClassName = "text-zinc-700",
  filledClassName = "fill-amber-300 text-amber-300",
}: RatingStarIconProps) {
  const normalizedFill = Math.min(
    100,
    Math.max(0, fillPercent),
  );

  const hiddenPercent =
    100 - normalizedFill;

  return (
    <span
      aria-hidden="true"
      className={`relative block shrink-0 ${className}`}
      style={{
        width: size,
        height: size,
      }}
    >
      <Star
        size={size}
        strokeWidth={strokeWidth}
        className={`absolute inset-0 h-full w-full ${emptyClassName}`}
      />

      <Star
        size={size}
        strokeWidth={strokeWidth}
        className={`pointer-events-none absolute inset-0 h-full w-full ${filledClassName}`}
        style={{
          clipPath: `inset(0 ${hiddenPercent}% 0 0)`,
          WebkitClipPath: `inset(0 ${hiddenPercent}% 0 0)`,
        }}
      />
    </span>
  );
}
