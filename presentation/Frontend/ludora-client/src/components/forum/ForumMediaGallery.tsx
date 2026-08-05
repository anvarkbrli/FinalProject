import { Maximize2, Play, X } from "lucide-react";
import { useEffect, useState } from "react";
import { resolveAssetUrl } from "../../lib/helpers";
import type { DiscussionMediaDTO } from "../../types/home";
import "./forum.css";

interface ForumMediaGalleryProps {
  media: DiscussionMediaDTO[];
  title: string;
  compact?: boolean;
}

export function ForumMediaGallery({
  media,
  title,
  compact = false,
}: ForumMediaGalleryProps) {
  const [activeMedia, setActiveMedia] =
    useState<DiscussionMediaDTO | null>(null);

  useEffect(() => {
    if (!activeMedia) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setActiveMedia(null);
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [activeMedia]);

  if (media.length === 0) return null;

  const visibleMedia = compact ? media.slice(0, 4) : media;

  return (
    <>
      <div
        className={`mt-4 grid overflow-hidden rounded-2xl border border-white/[0.07] ${
          visibleMedia.length === 1
            ? "grid-cols-1"
            : "grid-cols-2"
        }`}
      >
        {visibleMedia.map((item, index) => {
          const url = resolveAssetUrl(item.url) ?? item.url;
          const remaining = media.length - visibleMedia.length;

          return (
            <button
              key={item.id}
              type="button"
              onClick={() => setActiveMedia(item)}
              className={`group relative overflow-hidden bg-black/25 ${
                compact ? "h-44 sm:h-56" : "min-h-52 max-h-[520px]"
              }`}
              aria-label={`${title} mediasını böyüt`}
            >
              {item.isVideo ||
              item.contentType.startsWith("video/") ? (
                <>
                  <video
                    src={url}
                    className="h-full w-full object-cover"
                    preload="metadata"
                    muted
                  />
                  <span className="absolute inset-0 flex items-center justify-center bg-black/20">
                    <span className="flex h-12 w-12 items-center justify-center rounded-full border border-white/20 bg-black/55 text-white backdrop-blur">
                      <Play size={20} fill="currentColor" />
                    </span>
                  </span>
                </>
              ) : (
                <img
                  src={url}
                  alt={`${title} müzakirəsinə əlavə edilmiş media`}
                  className="h-full w-full object-cover transition duration-500 group-hover:scale-[1.02]"
                  loading="lazy"
                />
              )}

              <span className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-xl bg-black/55 text-white opacity-0 backdrop-blur transition group-hover:opacity-100">
                <Maximize2 size={15} />
              </span>

              {remaining > 0 && index === visibleMedia.length - 1 && (
                <span className="absolute inset-0 flex items-center justify-center bg-black/60 text-2xl font-black text-white backdrop-blur-[2px]">
                  +{remaining}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {activeMedia && (
        <div
          className="forum-lightbox"
          role="dialog"
          aria-modal="true"
          aria-label="Böyüdülmüş media"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget)
              setActiveMedia(null);
          }}
        >
          <div className="forum-lightbox__content">
            <button
              type="button"
              onClick={() => setActiveMedia(null)}
              className="absolute -right-2 -top-12 z-10 flex h-10 w-10 items-center justify-center rounded-xl border border-white/15 bg-black/45 text-white backdrop-blur transition hover:bg-white/10"
              aria-label="Medianı bağla"
            >
              <X size={19} />
            </button>

            {activeMedia.isVideo ||
            activeMedia.contentType.startsWith("video/") ? (
              <video
                src={
                  resolveAssetUrl(activeMedia.url) ??
                  activeMedia.url
                }
                className="forum-lightbox__media"
                controls
                autoPlay
              />
            ) : (
              <img
                src={
                  resolveAssetUrl(activeMedia.url) ??
                  activeMedia.url
                }
                alt={`${title} müzakirəsinin böyüdülmüş mediası`}
                className="forum-lightbox__media"
              />
            )}
          </div>
        </div>
      )}
    </>
  );
}
