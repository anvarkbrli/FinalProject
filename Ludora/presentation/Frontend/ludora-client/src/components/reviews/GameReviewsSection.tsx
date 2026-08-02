import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  Eye,
  Heart,
  LoaderCircle,
  MessageCircle,
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
    label: "Yüksək reytinq",
  },
  {
    value: "LowestRated",
    label: "Aşağı reytinq",
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
    <section id="reviews" className="scroll-mt-24 border-t border-[color:var(--wp-border)] bg-transparent">
      <div className="mx-auto max-w-7xl px-5 py-14 lg:px-8 lg:py-18">
        <div className="flex flex-col gap-5 border-b border-[color:var(--wp-border)] pb-6 md:flex-row md:items-end md:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <MessageCircle
                size={21}
                className="text-violet-300"
              />
              <h2 className="text-2xl font-black tracking-tight text-[var(--wp-text-strong)]">
                İstifadəçi rəyləri
              </h2>
            </div>
            <p className="mt-2 text-sm leading-6 text-[color:var(--wp-text-muted)]">
              Oyunu oynayan istifadəçilərin reytinqləri, platformaları, oyun müddətləri və fikirləri.
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
            className="h-11 rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] px-4 text-sm font-semibold text-[var(--wp-text)] outline-none transition focus:border-violet-400/40"
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
          <p className="text-sm font-semibold text-[color:var(--wp-text-muted)]">
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
          <div className="flex min-h-64 items-center justify-center gap-3 text-sm text-[color:var(--wp-text-muted)]">
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
          <div className="mt-6 rounded-3xl border border-dashed border-[color:var(--wp-border)] bg-[color:var(--wp-surface)] px-6 py-14 text-center">
            <MessageCircle
              size={34}
              className="mx-auto text-[color:var(--wp-text-muted)]"
            />
            <p className="mt-4 font-bold text-[var(--wp-text-strong)]">
              Bu oyun üçün hələ rəy yoxdur
            </p>
            <p className="mt-2 text-sm text-[color:var(--wp-text-muted)]">
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
                  className="wp-review-card flex min-h-[320px] flex-col overflow-hidden rounded-3xl"
                >
                  <div className="flex items-start justify-between gap-4 border-b border-[color:var(--wp-border)] p-5">
                    <Link
                      to={`/users/${review.userName}`}
                      className="flex min-w-0 items-center gap-3"
                    >
                      <ReviewAvatar
                        review={review}
                      />
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-bold text-[var(--wp-text-strong)] transition hover:text-[var(--wp-accent-strong)]">
                          {review.userName}
                        </span>
                        <span className="mt-1 block text-xs text-[color:var(--wp-text-muted)]">
                          {formatDateTime(
                            review.createdAt,
                          )}
                        </span>
                      </span>
                    </Link>

                    {review.rating !== null && (
                      <span className="inline-flex shrink-0 items-center gap-1.5 rounded-xl border border-amber-300/20 bg-amber-400/10 px-3 py-2 text-sm font-black text-amber-300">
                        <Star
                          size={15}
                          fill="currentColor"
                        />
                        {review.rating.toFixed(1)}
                      </span>
                    )}
                  </div>

                  <div className="grid flex-1 gap-4 p-5 lg:grid-cols-[minmax(0,1fr)_240px]">
                    <div className="flex min-h-[12rem] flex-col">
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
                          className="wp-review-content-box flex w-full flex-1 flex-col items-center justify-center rounded-2xl px-5 text-center transition hover:bg-amber-500/[0.08]"
                        >
                          <Eye
                            size={24}
                            className="text-amber-300"
                          />
                          <span className="mt-3 text-sm font-bold text-amber-300">
                            {language === "en" ? "This review contains spoilers" : "Bu rəy spoiler ehtiva edir"}
                          </span>
                          <span className="mt-1 text-xs text-[color:var(--wp-text-muted)]">
                            {language === "en" ? "Click once to reveal the review" : "Rəyi açmaq üçün bir dəfə kliklə"}
                          </span>
                        </button>
                      ) : (
                        <div className="wp-review-content-box flex flex-1 rounded-2xl p-4">
                          <p data-user-content className="whitespace-pre-wrap text-sm leading-7 text-[var(--wp-text)]">
                            {review.content && review.content.trim().length > 0
                              ? review.content
                              : language === "en"
                                ? "The player logged this game without adding written notes."
                                : "İstifadəçi bu oyunu yazılı qeyd əlavə etmədən loglayıb."}
                          </p>
                        </div>
                      )}
                    </div>

                    <aside className="space-y-3">
                      <div className="wp-review-subcard rounded-2xl p-4">
                        <div className="text-[11px] font-black uppercase tracking-[0.16em] text-[var(--wp-accent-strong)]">
                          {language === "en" ? "Review snapshot" : "Rəy xülasəsi"}
                        </div>
                        <dl className="mt-3 space-y-3 text-sm">
                          <div className="flex items-start justify-between gap-3">
                            <dt className="text-[color:var(--wp-text-muted)]">
                              {language === "en" ? "Platform" : "Platforma"}
                            </dt>
                            <dd className="text-right font-semibold text-[var(--wp-text-strong)]">
                              {review.platformName ?? (language === "en" ? "Not specified" : "Qeyd edilməyib")}
                            </dd>
                          </div>
                          <div className="flex items-start justify-between gap-3">
                            <dt className="text-[color:var(--wp-text-muted)]">
                              {language === "en" ? "Playtime" : "Oyun müddəti"}
                            </dt>
                            <dd className="text-right font-semibold text-[var(--wp-text-strong)]">
                              {playtime ?? (language === "en" ? "Not shared" : "Paylaşılmayıb")}
                            </dd>
                          </div>
                          <div className="flex items-start justify-between gap-3">
                            <dt className="text-[color:var(--wp-text-muted)]">
                              {language === "en" ? "Comments" : "Şərhlər"}
                            </dt>
                            <dd className="text-right font-semibold text-[var(--wp-text-strong)]">
                              {review.commentCount}
                            </dd>
                          </div>
                        </dl>
                      </div>

                      {review.showHardwareProfile ? (
                        <ReviewHardwareSummary
                          gpu={review.hardwareGpu}
                          cpu={review.hardwareCpu}
                          ramGb={review.hardwareRamGb}
                          vramGb={review.hardwareVramGb}
                        />
                      ) : (
                        <div className="wp-review-subcard rounded-2xl p-4">
                          <div className="text-[11px] font-black uppercase tracking-[0.16em] text-[var(--wp-accent-strong)]">
                            {language === "en" ? "System details" : "Sistem detalları"}
                          </div>
                          <p className="mt-3 text-sm leading-6 text-[color:var(--wp-text-muted)]">
                            {language === "en"
                              ? "No hardware details were shared for this review."
                              : "Bu rəy üçün sistem detalları paylaşılmayıb."}
                          </p>
                        </div>
                      )}
                    </aside>
                  </div>

                  <div className="flex items-center gap-5 border-t border-[color:var(--wp-border)] px-5 py-4 text-xs text-[color:var(--wp-text-muted)]">
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
                            ? "text-rose-400"
                            : "hover:text-rose-400"
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
                        className="font-semibold transition hover:text-rose-400"
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
                      className={`inline-flex items-center gap-2 font-semibold transition hover:text-violet-400 ${
                        openCommentReviewIds.has(review.id)
                          ? "text-violet-400"
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
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] text-[color:var(--wp-text-muted)] transition hover:text-[var(--wp-text-strong)] disabled:cursor-not-allowed disabled:opacity-35"
              aria-label="Əvvəlki səhifə"
            >
              <ChevronLeft size={18} />
            </button>

            <span className="min-w-24 text-center text-sm font-semibold text-[color:var(--wp-text-muted)]">
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
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] text-[color:var(--wp-text-muted)] transition hover:text-[var(--wp-text-strong)] disabled:cursor-not-allowed disabled:opacity-35"
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
