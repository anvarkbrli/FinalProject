interface BrandLogoProps {
  className?: string;
  alt?: string;
}

export function BrandLogo({
  className = "h-10 w-12",
  alt = "",
}: BrandLogoProps) {
  return (
    <img
      src="/wellplayd-logo.png"
      alt={alt}
      className={`shrink-0 object-contain ${className}`}
      draggable={false}
    />
  );
}
