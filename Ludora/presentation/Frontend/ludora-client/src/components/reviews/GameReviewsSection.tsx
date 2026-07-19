import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  Clock3,
  Eye,
  Heart,
  LoaderCircle,
  MessageCircle,
  Monitor,
  PenLine,
  Star,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  getGameReviews,
  likeReview,
  unlikeReview,
} from "../../services/reviewsService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";
import type { GameDetailDTO } from "../../types/games";
import type {
  ReviewDTO,
  ReviewSort,
} from "../../types/reviews";
import { LogGameModal } from "./LogGameModal";

const sortOptions: Array<{
  value: ReviewSort;
  label: string;
}> = [
  {
    value: "Newest",
    label: "Ən yenilər",
  },
  {
    value: "Oldest",
    label: "Ən köhnələr",
  },
  {
    value: "HighestRated",
    label: "Yüksək puan",
  },
  {
    value: "LowestRated",
    label: "Aşağı puan",
  },
];

function formatDateTime(value: string): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("az-AZ", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(date);
}

function formatPlaytime(
  totalMinutes: number | null,
): string | null {
  if (
    totalMinutes === null ||
    totalMinutes < 1
  ) {
    return null;
  }

  const hours = Math.floor(
    totalMinutes / 60,
  );
  const minutes = totalMinutes % 60;

  if (hours === 0) {
    return `${minutes} dəqiqə`;
  }

  if (minutes === 0) {
    return `${hours} saat`;
  }

  return `${hours} saat ${minutes} dəqiqə`;
}

function ReviewAvatar({
  review,
}: {
  review: ReviewDTO;
}) {
  const imageUrl = resolveAssetUrl(
    review.profileImageUrl,
  );

  return (
    <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.08] bg-violet-500/10 text-sm font-black text-violet-200">
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={`${review.userName} profil şəkli`}
          className="h-full w-full object-cover"
        />
      ) : (
        review.userName
          .trim()
          .charAt(0)
          .toUpperCase() || "?"
      )}
    </span>
  );
}

export function GameReviewsSection({
  game,
}: {
  game: GameDetailDTO;
}) {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [sort, setSort] =
    useState<ReviewSort>("Newest");
  const [isLogModalOpen, setIsLogModalOpen] =
    useState(false);
  const [revealedSpoilers, setRevealedSpoilers] =
    useState<Set<number>>(() => new Set());

  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );
  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );

  const isAuthenticated =
    isAuthSessionValid(
      accessToken,
      expiresAtUtc,
    );

  const reviewsQuery = useQuery({
    queryKey: [
      "game-reviews",
      game.source,
      game.id,
      sort,
      page,
    ],
    queryFn: () =>
      getGameReviews(
        game.source,
        game.id,
        sort,
        page,
      ),
  });

  const likeMutation = useMutation({
    mutationFn: async (review: ReviewDTO) => {
      if (review.isLikedByCurrentUser) {
        await unlikeReview(review.id);
      } else {
        await likeReview(review.id);
      }
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: [
          "game-reviews",
          game.source,
          game.id,
        ],
      });
    },
  });

  const result = reviewsQuery.data;
  const reviews = result?.items ?? [];
  const totalCount = result?.totalCount ?? 0;
  const totalPages = result?.totalPages ?? 0;

  return (
    <section className="border-t border-white/[0.07] bg-[#08090d]">
      <div className="mx-auto max-w-7xl px-5 py-14 lg:px-8 lg:py-18">
        <div className="flex flex-col gap-5 border-b border-white/[0.09] pb-6 md:flex-row md:items-end md:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <MessageCircle
                size={21}
                className="text-violet-300"
              />
              <h2 className="text-2xl font-black tracking-tight text-white">
                İstifadəçi rəyləri
              </h2>
            </div>
            <p className="mt-2 text-sm leading-6 text-zinc-600">
              Oyunu oynayan istifadəçilərin puanları, platformaları, oyun müddətləri və fikirləri.
            </p>
          </div>

          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <select
              value={sort}
              onChange={(event) => {
                setSort(
                  event.target.value as ReviewSort,
                );
                setPage(1);
              }}
              className="h-11 rounded-xl border border-white/[0.09] bg-[#15161c] px-4 text-sm font-semibold text-zinc-300 outline-none transition focus:border-violet-400/40"
            >
              {sortOptions.map((option) => (
                <option
                  key={option.value}
                  value={option.value}
                >
                  {option.label}
                </option>
              ))}
            </select>

            {isAuthenticated ? (
              <button
                type="button"
                onClick={() => {
                  setIsLogModalOpen(true);
                }}
                className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500"
              >
                <PenLine size={17} />
                Rəy yaz / Log et
              </button>
            ) : (
              <Link
                to="/login"
                className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500"
              >
                <PenLine size={17} />
                Rəy yazmaq üçün daxil ol
              </Link>
            )}
          </div>
        </div>

        <div className="mt-7 flex items-center justify-between gap-4">
          <p className="text-sm font-semibold text-zinc-500">
            {totalCount} rəy
          </p>
          {reviewsQuery.isFetching &&
            !reviewsQuery.isLoading && (
              <LoaderCircle
                size={17}
                className="animate-spin text-violet-300"
              />
            )}
        </div>

        {reviewsQuery.isLoading ? (
          <div className="flex min-h-64 items-center justify-center gap-3 text-sm text-zinc-500">
            <LoaderCircle
              size={21}
              className="animate-spin"
            />
            Rəylər yüklənir...
          </div>
        ) : reviewsQuery.isError ? (
          <div className="mt-6 rounded-2xl border border-red-400/15 bg-red-500/[0.05] p-7 text-center text-sm text-red-200">
            {getApiErrorMessage(
              reviewsQuery.error,
              "Rəyləri yükləmək mümkün olmadı.",
            )}
          </div>
        ) : reviews.length === 0 ? (
          <div className="mt-6 rounded-3xl border border-dashed border-white/[0.09] bg-white/[0.015] px-6 py-14 text-center">
            <MessageCircle
              size={34}
              className="mx-auto text-zinc-700"
            />
            <p className="mt-4 font-bold text-zinc-300">
              Bu oyun üçün hələ rəy yoxdur
            </p>
            <p className="mt-2 text-sm text-zinc-600">
              İlk rəyi sən əlavə edə bilərsən.
            </p>
          </div>
        ) : (
          <div className="mt-6 grid gap-4 lg:grid-cols-2">
            {reviews.map((review) => {
              const isSpoilerRevealed =
                revealedSpoilers.has(review.id);
              const playtime = formatPlaytime(
                review.playtimeMinutes,
              );

              return (
                <article
                  key={review.id}
                  className="flex min-h-[290px] flex-col overflow-hidden rounded-3xl border border-white/[0.08] bg-[#111218]"
                >
                  <div className="flex items-start justify-between gap-4 border-b border-white/[0.06] p-5">
                    <Link
                      to={`/users/${review.userName}`}
                      className="flex min-w-0 items-center gap-3"
                    >
                      <ReviewAvatar
                        review={review}
                      />
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-bold text-zinc-100 transition hover:text-violet-200">
                          {review.userName}
                        </span>
                        <span className="mt-1 block text-xs text-zinc-600">
                          {formatDateTime(
                            review.createdAt,
                          )}
                        </span>
                      </span>
                    </Link>

                    {review.rating !== null && (
                      <span className="inline-flex shrink-0 items-center gap-1.5 rounded-xl border border-amber-300/15 bg-amber-400/10 px-3 py-2 text-sm font-black text-amber-300">
                        <Star
                          size={15}
                          fill="currentColor"
                        />
                        {review.rating.toFixed(1)}
                      </span>
                    )}
                  </div>

                  <div className="flex flex-wrap gap-2 px-5 pt-4">
                    {review.platformName && (
                      <span className="inline-flex items-center gap-1.5 rounded-lg border border-sky-300/10 bg-sky-500/[0.07] px-2.5 py-1.5 text-xs font-semibold text-sky-200/80">
                        <Monitor size={13} />
                        {review.platformName}
                      </span>
                    )}

                    {playtime && (
                      <span className="inline-flex items-center gap-1.5 rounded-lg border border-white/[0.07] bg-white/[0.035] px-2.5 py-1.5 text-xs font-semibold text-zinc-400">
                        <Clock3 size={13} />
                        {playtime}
                      </span>
                    )}
                  </div>

                  <div className="flex flex-1 p-5 pt-4">
                    {review.containsSpoilers &&
                    !isSpoilerRevealed ? (
                      <button
                        type="button"
                        onClick={() => {
                          setRevealedSpoilers(
                            (currentValue) => {
                              const nextValue =
                                new Set(
                                  currentValue,
                                );
                              nextValue.add(
                                review.id,
                              );
                              return nextValue;
                            },
                          );
                        }}
                        className="flex min-h-32 w-full flex-col items-center justify-center rounded-2xl border border-amber-300/15 bg-amber-500/[0.055] px-5 text-center transition hover:bg-amber-500/[0.09]"
                      >
                        <Eye
                          size={24}
                          className="text-amber-300"
                        />
                        <span className="mt-3 text-sm font-bold text-amber-200">
                          Bu rəy spoiler ehtiva edir
                        </span>
                        <span className="mt-1 text-xs text-amber-100/45">
                          Rəyi açmaq üçün bir dəfə kliklə
                        </span>
                      </button>
                    ) : (
                      <p className="whitespace-pre-wrap text-sm leading-7 text-zinc-300">
                        {review.content}
                      </p>
                    )}
                  </div>

                  <div className="flex items-center gap-5 border-t border-white/[0.06] px-5 py-4 text-xs text-zinc-600">
                    <button
                      type="button"
                      disabled={
                        !isAuthenticated ||
                        review.isMine ||
                        likeMutation.isPending
                      }
                      onClick={() => {
                        likeMutation.mutate(
                          review,
                        );
                      }}
                      className={`inline-flex items-center gap-2 font-semibold transition disabled:cursor-not-allowed disabled:opacity-50 ${
                        review.isLikedByCurrentUser
                          ? "text-rose-300"
                          : "hover:text-rose-300"
                      }`}
                    >
                      <Heart
                        size={15}
                        fill={
                          review.isLikedByCurrentUser
                            ? "currentColor"
                            : "none"
                        }
                      />
                      {review.likeCount}
                    </button>

                    <span className="inline-flex items-center gap-2">
                      <MessageCircle size={15} />
                      {review.commentCount}
                    </span>

                    {review.isMine && (
                      <button
                        type="button"
                        onClick={() => {
                          setIsLogModalOpen(true);
                        }}
                        className="ml-auto font-semibold text-violet-300 transition hover:text-violet-200"
                      >
                        Redaktə et
                      </button>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        )}

        {totalPages > 1 && (
          <div className="mt-8 flex items-center justify-center gap-3">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => {
                setPage((currentPage) =>
                  Math.max(1, currentPage - 1),
                );
              }}
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.025] text-zinc-400 transition hover:text-white disabled:cursor-not-allowed disabled:opacity-35"
              aria-label="Əvvəlki səhifə"
            >
              <ChevronLeft size={18} />
            </button>

            <span className="min-w-24 text-center text-sm font-semibold text-zinc-500">
              {page} / {totalPages}
            </span>

            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => {
                setPage((currentPage) =>
                  Math.min(
                    totalPages,
                    currentPage + 1,
                  ),
                );
              }}
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.025] text-zinc-400 transition hover:text-white disabled:cursor-not-allowed disabled:opacity-35"
              aria-label="Növbəti səhifə"
            >
              <ChevronRight size={18} />
            </button>
          </div>
        )}
      </div>

      <LogGameModal
        open={isLogModalOpen}
        game={game}
        onClose={() => {
          setIsLogModalOpen(false);
        }}
      />
    </section>
  );
}
