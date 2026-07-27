import {
  Eye,
  Heart,
  LockKeyhole,
  MessageCircle,
  Pin,
} from "lucide-react";
import { Link } from "react-router-dom";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../../lib/helpers";
import type { DiscussionListItemDTO } from "../../types/home";

interface DiscussionCardProps {
  discussion: DiscussionListItemDTO;
}

export function DiscussionCard({
  discussion,
}: DiscussionCardProps) {
  const authorImageUrl = resolveAssetUrl(
    discussion.authorProfileImageUrl,
  );

  const authorInitial =
    discussion.authorUserName
      .trim()
      .charAt(0)
      .toUpperCase() || "?";

  return (
    <Link
      to={`/forum/discussions/${discussion.slug}`}
      aria-label={`${discussion.title} müzakirəsinə keç`}
      className="group block rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4 transition duration-300 hover:border-violet-400/20 hover:bg-white/[0.045] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-400/70"
    >
      <div className="flex items-start justify-between gap-3">
        <span className="rounded-full bg-violet-500/10 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wider text-violet-300">
          {discussion.categoryName}
        </span>

        <div className="flex items-center gap-2 text-zinc-500">
          {discussion.isPinned && (
            <span title="Sabitlənmiş müzakirə">
              <Pin size={14} />
            </span>
          )}

          {discussion.isLocked && (
            <span title="Bağlanmış müzakirə">
              <LockKeyhole size={14} />
            </span>
          )}
        </div>
      </div>

      <h3 className="mt-3 line-clamp-2 font-semibold leading-6 text-zinc-200 transition group-hover:text-white">
        {discussion.title}
      </h3>

      <div className="mt-4 flex items-center gap-2.5">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-full border border-white/10 bg-violet-500/10 text-xs font-semibold text-violet-300">
          {authorImageUrl ? (
            <img
              src={authorImageUrl}
              alt={`${discussion.authorUserName} profil şəkli`}
              loading="lazy"
              className="h-full w-full object-cover"
              onError={(event) => {
                event.currentTarget.style.display = "none";
              }}
            />
          ) : (
            authorInitial
          )}
        </div>

        <div className="min-w-0 text-xs">
          <p className="truncate font-medium text-zinc-400">
            {discussion.authorUserName}
          </p>

          <p className="mt-0.5 text-zinc-600">
            {formatRelativeDate(discussion.createdAt)}
          </p>
        </div>
      </div>

      <div className="mt-4 flex items-center gap-4 border-t border-white/5 pt-3 text-xs text-zinc-500">
        <span
          className={`flex items-center gap-1.5 ${
            discussion.isLikedByCurrentUser
              ? "text-rose-300"
              : ""
          }`}
          title="Bəyənmə sayı"
        >
          <Heart
            size={14}
            fill={
              discussion.isLikedByCurrentUser
                ? "currentColor"
                : "none"
            }
          />

          {discussion.likeCount}
        </span>

        <span
          className="flex items-center gap-1.5"
          title="Cavab sayı"
        >
          <MessageCircle size={14} />
          {discussion.replyCount}
        </span>

        <span
          className="flex items-center gap-1.5"
          title="Baxış sayı"
        >
          <Eye size={14} />
          {discussion.viewCount}
        </span>
      </div>
    </Link>
  );
}
