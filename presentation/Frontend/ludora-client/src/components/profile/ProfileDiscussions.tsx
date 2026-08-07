import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { formatRelativeDate, resolveAssetUrl } from "../../lib/helpers";
import type { DiscussionListItemDTO, PagedResultDTO } from "../../types/home";

interface ProfileDiscussionsProps {
  result: PagedResultDTO<DiscussionListItemDTO> | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  page: number;
  onPageChange: (page: number) => void;
}

export function ProfileDiscussions({
  result,
  isLoading,
  isError,
  error,
  page,
  onPageChange,
}: ProfileDiscussionsProps) {
  const { t } = useI18n();
  const discussions = result?.items ?? [];
  const totalPages = result?.totalPages ?? 0;

  if (isLoading) {
    return (
      <section className="mx-auto max-w-4xl space-y-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <div
            key={index}
            className="h-40 animate-pulse rounded-3xl border border-[var(--wp-border)] bg-[var(--wp-surface)]"
          />
        ))}
      </section>
    );
  }

  if (isError) {
    return (
      <section className="mx-auto max-w-4xl rounded-3xl border border-red-400/20 bg-red-500/[0.06] px-6 py-10 text-center">
        <h2 className="text-lg font-bold text-[var(--wp-text-strong)]">
          {t("profile:discussions.loadErrorTitle")}
        </h2>
        <p className="mx-auto mt-2 max-w-xl text-sm leading-6 text-[var(--wp-text-muted)]">
          {getApiErrorMessage(error, t("profile:discussions.loadError"))}
        </p>
      </section>
    );
  }

  if (discussions.length === 0) {
    return (
      <section className="mx-auto max-w-4xl rounded-3xl border border-[var(--wp-border)] bg-[var(--wp-surface)] px-6 py-12 text-center shadow-[var(--wp-shadow-card)]">
        <h2 className="text-lg font-bold text-[var(--wp-text-strong)]">
          {t("profile:discussions.emptyTitle")}
        </h2>
        <p className="mx-auto mt-2 max-w-xl text-sm leading-6 text-[var(--wp-text-muted)]">
          {t("profile:discussions.emptyDescription")}
        </p>
      </section>
    );
  }

  return (
    <section className="mx-auto max-w-4xl">
      <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold text-[var(--wp-text-strong)]">
            {t("profile:discussions.title")}
          </h2>
          <p className="mt-1 text-sm text-[var(--wp-text-muted)]">
            {t("profile:discussions.description")}
          </p>
        </div>
      </div>

      <div className="space-y-4">
        {discussions.map((discussion) => {
          const communityIcon = resolveAssetUrl(discussion.communityIconUrl);
          const locationLabel = discussion.communityName
            ? discussion.communityName
            : t("profile:discussions.mainForum");

          return (
            <Link
              key={discussion.id}
              to={`/forum/discussions/${discussion.slug}`}
              className="group block rounded-3xl border border-[var(--wp-border)] bg-[var(--wp-surface)] p-5 shadow-[var(--wp-shadow-card)] transition duration-300 hover:-translate-y-0.5 hover:border-[color-mix(in_srgb,var(--wp-accent)_35%,var(--wp-border))] hover:bg-[var(--wp-surface-hover)] hover:shadow-[var(--wp-shadow-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--wp-accent)] sm:p-6"
            >
              <div className="flex flex-wrap items-center gap-2 text-xs font-semibold">
                <span className="inline-flex items-center gap-2 rounded-full border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-3 py-1.5 text-[var(--wp-text)]">
                  {communityIcon && (
                    <img
                      src={communityIcon}
                      alt=""
                      className="h-4 w-4 rounded-full object-cover"
                      loading="lazy"
                    />
                  )}
                  {locationLabel}
                </span>
                <span className="rounded-full bg-[var(--wp-accent-soft)] px-3 py-1.5 text-[var(--wp-accent-strong)]">
                  {discussion.categoryName}
                </span>
                {discussion.isPinned && (
                  <span className="text-[var(--wp-text-muted)]">
                    {t("profile:discussions.pinned")}
                  </span>
                )}
              </div>

              <h3 className="mt-4 text-lg font-bold leading-7 text-[var(--wp-text-strong)] transition-colors duration-300 group-hover:text-[var(--wp-accent-strong)] sm:text-xl">
                {discussion.title}
              </h3>

              {discussion.contentPreview.trim().length > 0 && (
                <p className="mt-2 line-clamp-2 text-sm leading-6 text-[var(--wp-text-muted)]">
                  {discussion.contentPreview}
                </p>
              )}

              <div className="mt-5 flex flex-wrap items-center gap-x-5 gap-y-2 border-t border-[var(--wp-border)] pt-4 text-xs text-[var(--wp-text-muted)]">
                <span>{formatRelativeDate(discussion.createdAt)}</span>
                <span>{t("profile:discussions.likes", { count: discussion.likeCount })}</span>
                <span>{t("profile:discussions.replies", { count: discussion.replyCount })}</span>
                <span>{t("profile:discussions.views", { count: discussion.viewCount })}</span>
              </div>
            </Link>
          );
        })}
      </div>

      {totalPages > 1 && (
        <div className="mt-7 flex items-center justify-center gap-3">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => onPageChange(Math.max(1, page - 1))}
            className="rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface)] px-4 py-2 text-sm font-semibold text-[var(--wp-text)] transition hover:bg-[var(--wp-surface-hover)] disabled:cursor-not-allowed disabled:opacity-40"
          >
            {t("profile:discussions.previous")}
          </button>
          <span className="text-sm font-medium text-[var(--wp-text-muted)]">
            {t("profile:discussions.page", { page, totalPages })}
          </span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => onPageChange(Math.min(totalPages, page + 1))}
            className="rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface)] px-4 py-2 text-sm font-semibold text-[var(--wp-text)] transition hover:bg-[var(--wp-surface-hover)] disabled:cursor-not-allowed disabled:opacity-40"
          >
            {t("profile:discussions.next")}
          </button>
        </div>
      )}
    </section>
  );
}
