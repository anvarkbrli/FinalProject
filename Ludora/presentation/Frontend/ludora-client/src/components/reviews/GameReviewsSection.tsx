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
  Star,
} from "lucide-react";
import { useState } from "react";
import { useI18n } from "../../i18n/useI18n";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
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
import { ReviewCommentsPanel } from "./ReviewCommentsPanel";
import { ReviewHardwareSummary } from "./ReviewHardwareSummary";
import { ReviewLikesModal } from "./ReviewLikesModal";

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

function formatPlaytime(
  totalMinutes: number | null,
  language: "az" | "en",
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
    return language === "en"
      ? `${minutes} ${minutes === 1 ? "minute" : "minutes"}`
      : `${minutes} dəqiqə`;
  }

  if (minutes === 0) {
    return language === "en"
      ? `${hours} ${hours === 1 ? "hour" : "hours"}`
      : `${hours} saat`;
  }

  return language === "en"
    ? `${hours} ${hours === 1 ? "hour" : "hours"} ${minutes} ${minutes === 1 ? "minute" : "minutes"}`
    : `${hours} saat ${minutes} dəqiqə`;
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
  const { language } = useI18n();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [sort, setSort] =
    useState<ReviewSort>("Newest");
  const [revealedSpoilers, setRevealedSpoilers] =
    useState<Set<number>>(() => new Set());
  const [likesReview, setLikesReview] = useState<{
    id: number;
    count: number;
  } | null>(null);
  const [openCommentReviewIds, setOpenCommentReviewIds] =
    useState<Set<number>>(() => new Set());
  const [interactionError, setInteractionError] =
    useState<string | null>(null);

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
      setInteractionError(null);
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: [
            "game-reviews",
            game.source,
            game.id,
          ],
        }),
        queryClient.invalidateQueries({
          queryKey: ["user-reviews"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["review-likes"],
        }),
      ]);
    },
    onError: (error) => {
      setInteractionError(
        getApiErrorMessage(
          error,
          "Rəy bəyənmə əməliyyatı tamamlanmadı.",
        ),
      );
    },
  });

  const result = reviewsQuery.data;
  const reviews = result?.items ?? [];
  const totalCount = result?.totalCount ?? 0;
  const totalPages = result?.totalPages ?? 0;

  return (
    <section id="reviews" className="scroll-mt-24 border-t border-white/[0.07] bg-[#08090d]">
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

        {interactionError && (
          <div className="mt-4 rounded-2xl border border-red-400/15 bg-red-500/[0.05] px-4 py-3 text-sm text-red-200">
            {interactionError}
          </div>
        )}

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
                language,
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

                  <div className="flex-1 p-5 pt-4">
                    {review.containsSpoilers &&
                    !review.isMine &&
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
                          {language === "en" ? "This review contains spoilers" : "Bu rəy spoiler ehtiva edir"}
                        </span>
                        <span className="mt-1 text-xs text-amber-100/45">
                          {language === "en" ? "Click once to reveal the review" : "Rəyi açmaq üçün bir dəfə kliklə"}
                        </span>
                      </button>
                    ) : (
                      <p data-user-content className="whitespace-pre-wrap text-sm leading-7 text-zinc-300">
                        {review.content}
                      </p>
                    )}

                    {review.showHardwareProfile && (
                      <ReviewHardwareSummary
                        gpu={review.hardwareGpu}
                        cpu={review.hardwareCpu}
                        ramGb={review.hardwareRamGb}
                        vramGb={review.hardwareVramGb}
                      />
                    )}
                  </div>

                  <div className="flex items-center gap-5 border-t border-white/[0.06] px-5 py-4 text-xs text-zinc-600">
                    <span className="inline-flex items-center gap-2">
                      <button
                        type="button"
                        disabled={
                          !isAuthenticated ||
                          review.isMine ||
                          likeMutation.isPending
                        }
                        onClick={() => {
                          likeMutation.mutate(review);
                        }}
                        className={`group inline-flex items-center font-semibold transition disabled:cursor-not-allowed disabled:opacity-50 ${
                          review.isLikedByCurrentUser
                            ? "text-rose-300"
                            : "hover:text-rose-300"
                        }`}
                        aria-label="Rəyi bəyən"
                      >
                        <Heart
                          size={15}
                          className={`transition ${
                            review.isLikedByCurrentUser
                              ? "fill-current"
                              : "fill-transparent group-hover:fill-current"
                          }`}
                        />
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setLikesReview({
                            id: review.id,
                            count: review.likeCount,
                          });
                        }}
                        className="font-semibold transition hover:text-rose-300"
                      >
                        {review.likeCount} bəyəni
                      </button>
                    </span>

                    <button
                      type="button"
                      onClick={() => {
                        setOpenCommentReviewIds((currentValue) => {
                          const nextValue = new Set(currentValue);
                          if (nextValue.has(review.id)) {
                            nextValue.delete(review.id);
                          } else {
                            nextValue.add(review.id);
                          }
                          return nextValue;
                        });
                      }}
                      className={`inline-flex items-center gap-2 font-semibold transition hover:text-violet-300 ${
                        openCommentReviewIds.has(review.id)
                          ? "text-violet-300"
                          : ""
                      }`}
                    >
                      <MessageCircle size={15} />
                      {review.commentCount}
                      <span className="sr-only">Şərhləri göstər</span>
                    </button>

                  </div>

                  {openCommentReviewIds.has(review.id) && (
                    <ReviewCommentsPanel
                      reviewId={review.id}
                      isAuthenticated={isAuthenticated}
                    />
                  )}
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

      <ReviewLikesModal
        reviewId={likesReview?.id ?? null}
        likeCount={likesReview?.count ?? 0}
        onClose={() => setLikesReview(null)}
      />

    </section>
  );
}
