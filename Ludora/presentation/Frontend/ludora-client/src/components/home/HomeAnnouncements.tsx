import { useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  Megaphone,
} from "lucide-react";
import { useState } from "react";
import { formatRelativeDate } from "../../lib/helpers";
import { getHomeAnnouncements } from "../../services/homeService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";

export function HomeAnnouncements() {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );
  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );
  const hasValidSession =
    user !== null &&
    isAuthSessionValid(accessToken, expiresAtUtc);

  const announcementsQuery = useQuery({
    queryKey: ["homepage", "announcements"],
    queryFn: ({ signal }) => getHomeAnnouncements(signal),
    enabled: hasValidSession,
    staleTime: 5 * 60 * 1000,
  });

  const announcements = announcementsQuery.data?.items ?? [];

  if (
    !hasValidSession ||
    announcementsQuery.isLoading ||
    announcementsQuery.isError ||
    announcements.length === 0
  ) {
    return null;
  }

  const activeIndex = Math.min(
    selectedIndex,
    announcements.length - 1,
  );
  const announcement = announcements[activeIndex];
  const publishedAt =
    announcement.publishedAtUtc ?? announcement.createdAt;
  const hasMultipleAnnouncements = announcements.length > 1;

  function showPrevious() {
    setSelectedIndex((currentIndex) =>
      currentIndex <= 0
        ? announcements.length - 1
        : currentIndex - 1,
    );
  }

  function showNext() {
    setSelectedIndex((currentIndex) =>
      currentIndex >= announcements.length - 1
        ? 0
        : currentIndex + 1,
    );
  }

  return (
    <section
      aria-label="Ludora elanları"
      className="border-b border-white/5"
    >
      <div className="mx-auto max-w-7xl px-5 py-3 lg:px-8">
        <div className="relative overflow-hidden rounded-2xl border border-violet-400/15 bg-white/[0.025] shadow-lg shadow-black/10">
          <div className="pointer-events-none absolute inset-0 bg-[linear-gradient(100deg,rgba(124,58,237,0.10),transparent_42%,rgba(217,70,239,0.035))]" />

          <div className="relative flex items-start gap-3 px-4 py-3.5 sm:items-center">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-violet-300/10 bg-violet-500/10 text-violet-300">
              <Megaphone size={17} aria-hidden="true" />
            </span>

            <div
              aria-live="polite"
              className="min-w-0 flex-1"
            >
              <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-violet-300">
                  Ludora elanı
                </p>

                <span className="text-[11px] text-zinc-600">
                  {formatRelativeDate(publishedAt)}
                </span>
              </div>

              <div className="mt-1 flex min-w-0 flex-col gap-0.5 sm:flex-row sm:items-baseline sm:gap-2">
                <h2 className="truncate text-sm font-semibold text-zinc-100">
                  {announcement.title}
                </h2>

                <span
                  aria-hidden="true"
                  className="hidden text-zinc-700 sm:inline"
                >
                  —
                </span>

                <p className="line-clamp-2 text-xs leading-5 text-zinc-500 sm:line-clamp-1">
                  {announcement.content}
                </p>
              </div>
            </div>

            {hasMultipleAnnouncements && (
              <div className="flex shrink-0 items-center gap-1">
                <span className="mr-1 text-[11px] tabular-nums text-zinc-600">
                  {activeIndex + 1}/{announcements.length}
                </span>

                <button
                  type="button"
                  onClick={showPrevious}
                  aria-label="Əvvəlki elanı göstər"
                  className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/[0.07] bg-white/[0.025] text-zinc-500 transition hover:border-violet-400/20 hover:bg-violet-500/10 hover:text-violet-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60"
                >
                  <ChevronLeft size={16} aria-hidden="true" />
                </button>

                <button
                  type="button"
                  onClick={showNext}
                  aria-label="Növbəti elanı göstər"
                  className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/[0.07] bg-white/[0.025] text-zinc-500 transition hover:border-violet-400/20 hover:bg-violet-500/10 hover:text-violet-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/60"
                >
                  <ChevronRight size={16} aria-hidden="true" />
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
