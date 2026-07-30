import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Eye,
  Heart,
  MessageCircle,
  MessageSquareText,
  Sparkles,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../../lib/helpers";
import {
  likeContent,
  unlikeContent,
} from "../../services/contentInteractionsService";
import { setDiscussionLike } from "../../services/forumService";
import { useAuthStore } from "../../stores/authStore";
import type { DiscussionListItemDTO } from "../../types/home";
import type { PostDTO } from "../../types/profile";
import { InteractionCommentsPanel } from "../interactions/InteractionCommentsPanel";
import { ContentReportButton } from "../reporting/ContentReportButton";
import { ForumContentMenu } from "./ForumContentMenu";
import { ForumMediaGallery } from "./ForumMediaGallery";

interface FeedCardProps {
  reason?: string;
  isAuthenticated: boolean;
}

export function DiscussionFeedCard({
  discussion,
  reason,
  isAuthenticated,
}: FeedCardProps & {
  discussion: DiscussionListItemDTO;
}) {
  const { language } = useI18n();
  const queryClient = useQueryClient();
  const [isSpoilerRevealed, setIsSpoilerRevealed] = useState(false);
  const currentUserId = useAuthStore((state) => state.user?.id);
  const shouldHideSpoiler =
    discussion.isSpoiler &&
    discussion.authorId !== currentUserId &&
    !isSpoilerRevealed;
  const avatarUrl = resolveAssetUrl(
    discussion.communityIconUrl ?? discussion.authorProfileImageUrl,
  );
  const displayName =
    discussion.communityName ?? discussion.authorUserName;
  const likeMutation = useMutation({
    mutationFn: () =>
      setDiscussionLike(
        discussion.id,
        !discussion.isLikedByCurrentUser,
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["forum"] });
    },
  });

  return (
    <article className="group relative rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 p-4 transition hover:border-teal-200/15 hover:bg-[#101c1b] sm:p-5">
      <ForumContentMenu
        kind="discussion"
        contentId={discussion.id}
        contentLabel={discussion.title}
        authorId={discussion.authorId}
        authorUserName={discussion.authorUserName}
        initialIsActive={discussion.isSavedByCurrentUser}
        className="absolute right-3 top-3 sm:right-4 sm:top-4"
      />

      {reason && (
        <div className="mb-3 flex items-center gap-2 pr-10 text-[11px] font-semibold text-violet-300/85">
          <Sparkles size={13} />
          <span>{reason}</span>
        </div>
      )}

      <div className="flex gap-3.5">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-gradient-to-br from-violet-500/25 to-teal-300/15 text-sm font-black text-teal-100">
          {avatarUrl ? (
            <img
              src={avatarUrl}
              alt=""
              className="h-full w-full object-cover"
            />
          ) : (
            displayName.charAt(0).toUpperCase()
          )}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 pr-9 text-xs">
            {discussion.communitySlug ? (
              <Link
                to={`/forum/communities/${discussion.communitySlug}`}
                className="font-semibold text-teal-200 transition hover:text-teal-100"
              >
                {displayName}
              </Link>
            ) : (
              <Link
                to={`/users/${encodeURIComponent(
                  discussion.authorUserName,
                )}`}
                className="font-semibold text-zinc-300 transition hover:text-white"
              >
                {displayName}
              </Link>
            )}
            <span className="text-zinc-700">•</span>
            <span className="text-zinc-600">
              {formatRelativeDate(discussion.createdAt)}
            </span>
            {discussion.isPinned && (
              <span className="rounded-full bg-teal-300/10 px-2 py-0.5 font-semibold text-teal-300">
                Bərkidilib
              </span>
            )}
          </div>

          <Link
            to={`/forum/discussions/${discussion.slug}`}
            data-i18n-ignore
            className="mt-2 block text-base font-bold leading-6 text-zinc-100 transition group-hover:text-white sm:text-lg"
          >
            {discussion.title}
          </Link>

          {shouldHideSpoiler && (
            <button
              type="button"
              onClick={() => setIsSpoilerRevealed(true)}
              className="mt-3 flex w-full items-center justify-center gap-2 rounded-2xl border border-amber-300/15 bg-amber-300/[0.05] px-4 py-5 text-xs font-bold text-amber-200 transition hover:bg-amber-300/[0.09]"
            >
              <Eye size={15} />
              Spoiler məzmununu göstər
            </button>
          )}

          {discussion.contentPreview && !shouldHideSpoiler && (
            <p data-i18n-ignore className="mt-2 line-clamp-3 text-sm leading-6 text-zinc-500">
              {discussion.contentPreview}
            </p>
          )}

          {!shouldHideSpoiler && (
            <ForumMediaGallery
              media={discussion.media}
              title={discussion.title}
              compact
            />
          )}

          <div className="mt-2.5">
            <span className="rounded-lg border border-white/[0.07] bg-white/[0.035] px-2.5 py-1 text-[11px] font-semibold text-zinc-500">
              {discussion.categoryName}
            </span>
          </div>

          <div className="mt-4 flex items-center gap-1">
            <button
              type="button"
              disabled={!isAuthenticated || likeMutation.isPending}
              aria-pressed={discussion.isLikedByCurrentUser}
              onClick={() => likeMutation.mutate()}
              className={`flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs transition ${
                discussion.isLikedByCurrentUser
                  ? "bg-rose-400/10 text-rose-300"
                  : "text-zinc-600 hover:bg-white/[0.045] hover:text-zinc-300"
              } disabled:cursor-not-allowed disabled:opacity-50`}
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
            </button>
            <Link
              to={`/forum/discussions/${discussion.slug}`}
              className="flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs text-zinc-600 transition hover:bg-white/[0.045] hover:text-zinc-300"
            >
              <MessageCircle size={14} />
              {language === "en"
                ? `${discussion.replyCount} ${discussion.replyCount === 1 ? "reply" : "replies"}`
                : `${discussion.replyCount} cavab`}
            </Link>
            <span className="ml-auto flex items-center gap-1 text-[11px] text-zinc-700">
              <Eye size={13} />
              {discussion.viewCount}
            </span>
          </div>
        </div>
      </div>
    </article>
  );
}

export function ForumPostCard({
  post,
  reason,
  isAuthenticated,
}: FeedCardProps & {
  post: PostDTO;
}) {
  const { language } = useI18n();
  const queryClient = useQueryClient();
  const [showComments, setShowComments] = useState(false);
  const avatarUrl = resolveAssetUrl(post.profileImageUrl);
  const imageUrl = resolveAssetUrl(post.imageUrl);
  const likeMutation = useMutation({
    mutationFn: async () => {
      if (post.isLikedByCurrentUser) {
        await unlikeContent("post", post.id);
      } else {
        await likeContent("post", post.id);
      }
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["forum"] });
    },
  });

  return (
    <article className="overflow-hidden rounded-[22px] border border-white/[0.075] bg-[#0d1716]/80 transition hover:border-violet-200/15 hover:bg-[#10191a]">
      <div className="p-4 sm:p-5">
        {reason && (
          <div className="mb-3 flex items-center gap-2 text-[11px] font-semibold text-violet-300/85">
            <Sparkles size={13} />
            <span>{reason}</span>
          </div>
        )}

        <div className="flex items-start gap-3.5">
          <Link
            to={`/users/${encodeURIComponent(post.userName)}`}
            className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-violet-500/15 text-sm font-black text-violet-200"
          >
            {avatarUrl ? (
              <img
                src={avatarUrl}
                alt=""
                className="h-full w-full object-cover"
              />
            ) : (
              post.userName.charAt(0).toUpperCase()
            )}
          </Link>

          <div className="min-w-0 flex-1">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <Link
                  to={`/users/${encodeURIComponent(post.userName)}`}
                  className="truncate text-sm font-bold text-zinc-200 transition hover:text-white"
                >
                  {post.userName}
                </Link>
                <p className="mt-0.5 text-[11px] text-zinc-600">
                  {formatRelativeDate(post.createdAt)} · İstifadəçi postu
                </p>
              </div>

              <ContentReportButton
                reportedUserId={post.userId}
                reportedUserName={post.userName}
                contentType="Post"
                contentId={post.id}
                contentLabel={post.content?.slice(0, 80) || "Şəkilli post"}
                isOwnContent={post.isMine}
              />
            </div>

            {post.content && (
              <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-zinc-400">
                {post.content}
              </p>
            )}
          </div>
        </div>

        {imageUrl && (
          <img
            src={imageUrl}
            alt={`${post.userName} tərəfindən paylaşılan şəkil`}
            className="mt-4 max-h-[34rem] w-full rounded-2xl border border-white/[0.07] object-contain"
          />
        )}

        <div className="mt-4 flex items-center gap-1 border-t border-white/[0.055] pt-3">
          <button
            type="button"
            disabled={!isAuthenticated || likeMutation.isPending}
            onClick={() => likeMutation.mutate()}
            className={`flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs transition ${
              post.isLikedByCurrentUser
                ? "bg-rose-400/10 text-rose-300"
                : "text-zinc-600 hover:bg-white/[0.045] hover:text-zinc-300"
            } disabled:cursor-not-allowed disabled:opacity-50`}
          >
            <Heart
              size={14}
              fill={post.isLikedByCurrentUser ? "currentColor" : "none"}
            />
            {post.likeCount}
          </button>
          <button
            type="button"
            onClick={() => setShowComments((current) => !current)}
            className={`flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs transition ${
              showComments
                ? "bg-violet-400/10 text-violet-300"
                : "text-zinc-600 hover:bg-white/[0.045] hover:text-zinc-300"
            }`}
          >
            <MessageCircle size={14} />
            {language === "en"
              ? `${post.commentCount} ${post.commentCount === 1 ? "comment" : "comments"}`
              : `${post.commentCount} şərh`}
          </button>
          <span className="ml-auto flex items-center gap-1.5 text-[11px] text-zinc-700">
            <MessageSquareText size={13} />
            Post
          </span>
        </div>
      </div>

      {showComments && (
        <InteractionCommentsPanel
          contentType="post"
          contentId={post.id}
          isAuthenticated={isAuthenticated}
          placeholder="Post haqqında şərh yaz..."
          emptyText="Bu posta hələ şərh yazılmayıb."
          invalidateQueryKeys={[["forum"]]}
        />
      )}
    </article>
  );
}

export function ForumFeedSkeleton() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 5 }).map((_, index) => (
        <div
          key={index}
          className="h-44 animate-pulse rounded-[22px] border border-white/[0.05] bg-white/[0.025]"
        />
      ))}
    </div>
  );
}
