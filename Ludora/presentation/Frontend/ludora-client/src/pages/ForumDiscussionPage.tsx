import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowBigDown,
  ArrowBigUp,
  ArrowLeft,
  Heart,
  LoaderCircle,
  Lock,
  MessageCircle,
  Reply,
  Search,
  Send,
  Trash2,
  X,
} from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ForumMediaGallery } from "../components/forum/ForumMediaGallery";
import { ForumContentMenu } from "../components/forum/ForumContentMenu";
import "../components/forum/forum.css";
import { Navbar } from "../components/layout/Navbar";
import { getApiErrorMessage } from "../lib/api";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../lib/helpers";
import {
  createDiscussionReply,
  deleteDiscussionReply,
  getDiscussionBySlug,
  getDiscussionReplies,
  setDiscussionLike,
  voteDiscussionReply,
} from "../services/forumService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../stores/authStore";
import type { DiscussionReplyDTO } from "../types/forum";

function filterReplies(
  replies: DiscussionReplyDTO[],
  query: string,
): DiscussionReplyDTO[] {
  if (!query) return replies;

  return replies.flatMap((reply) => {
    const children = filterReplies(reply.children, query);
    const matches =
      reply.content.toLocaleLowerCase("az-AZ").includes(query) ||
      reply.authorUserName
        .toLocaleLowerCase("az-AZ")
        .includes(query);

    return matches || children.length > 0
      ? [{ ...reply, children }]
      : [];
  });
}

interface CommentItemProps {
  reply: DiscussionReplyDTO;
  depth: number;
  isAuthenticated: boolean;
  searchQuery: string;
  busyReplyId: number | null;
  onVote: (reply: DiscussionReplyDTO, value: -1 | 1) => void;
  onDelete: (replyId: number) => void;
  onReply: (parentReplyId: number, content: string) => Promise<void>;
}

function CommentItem({
  reply,
  depth,
  isAuthenticated,
  searchQuery,
  busyReplyId,
  onVote,
  onDelete,
  onReply,
}: CommentItemProps) {
  const [isReplying, setIsReplying] = useState(false);
  const [replyText, setReplyText] = useState("");
  const avatarUrl = resolveAssetUrl(reply.authorProfileImageUrl);
  const isMatch =
    searchQuery.length > 0 &&
    (reply.content.toLocaleLowerCase("az-AZ").includes(searchQuery) ||
      reply.authorUserName
        .toLocaleLowerCase("az-AZ")
        .includes(searchQuery));

  return (
    <div className="forum-comment-node">
      <article
        id={`reply-${reply.id}`}
        className={`forum-comment-card relative rounded-2xl border border-white/[0.07] bg-[#0d1716]/70 p-4 ${
          isMatch ? "forum-comment-match" : ""
        }`}
      >
        {!reply.isDeleted && (
          <ForumContentMenu
            kind="reply"
            contentId={reply.id}
            contentLabel={`@${reply.authorUserName} şərhi`}
            authorId={reply.authorId}
            authorUserName={reply.authorUserName}
            initialIsActive={reply.isFollowedByCurrentUser}
            className="absolute right-3 top-3"
          />
        )}
        <div className="flex gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-teal-300/10 text-xs font-black text-teal-200">
            {avatarUrl ? (
              <img
                src={avatarUrl}
                alt=""
                className="h-full w-full object-cover"
              />
            ) : (
              reply.authorUserName.charAt(0).toUpperCase()
            )}
          </div>

          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2 pr-9 text-xs">
              <span className="font-bold text-zinc-200">
                {reply.isDeleted
                  ? "Silinmiş istifadəçi"
                  : reply.authorUserName}
              </span>
              <span className="text-zinc-700">•</span>
              <time className="text-zinc-600">
                {formatRelativeDate(reply.createdAt)}
              </time>
            </div>

            {reply.isDeleted ? (
              <p className="mt-3 text-sm italic text-zinc-600">
                Bu şərh silinib.
              </p>
            ) : (
              <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-zinc-300">
                {reply.content}
              </p>
            )}

            <div className="mt-3 flex flex-wrap items-center gap-1">
              <div className="flex items-center rounded-xl border border-white/[0.07] bg-black/15">
                <button
                  type="button"
                  disabled={!isAuthenticated || busyReplyId === reply.id}
                  onClick={() => onVote(reply, 1)}
                  className={`flex h-8 w-8 items-center justify-center rounded-xl transition ${
                    reply.currentUserVote === 1
                      ? "bg-teal-300/15 text-teal-200"
                      : "text-zinc-600 hover:text-teal-200"
                  }`}
                  aria-label="Müsbət səs"
                >
                  <ArrowBigUp
                    size={17}
                    fill={
                      reply.currentUserVote === 1
                        ? "currentColor"
                        : "none"
                    }
                  />
                </button>
                <span
                  className={`min-w-7 text-center text-xs font-black ${
                    reply.netScore > 0
                      ? "text-teal-200"
                      : reply.netScore < 0
                        ? "text-rose-300"
                        : "text-zinc-500"
                  }`}
                >
                  {reply.netScore}
                </span>
                <button
                  type="button"
                  disabled={!isAuthenticated || busyReplyId === reply.id}
                  onClick={() => onVote(reply, -1)}
                  className={`flex h-8 w-8 items-center justify-center rounded-xl transition ${
                    reply.currentUserVote === -1
                      ? "bg-rose-400/15 text-rose-300"
                      : "text-zinc-600 hover:text-rose-300"
                  }`}
                  aria-label="Mənfi səs"
                >
                  <ArrowBigDown
                    size={17}
                    fill={
                      reply.currentUserVote === -1
                        ? "currentColor"
                        : "none"
                    }
                  />
                </button>
              </div>

              {isAuthenticated && !reply.isDeleted && depth < 7 && (
                <button
                  type="button"
                  onClick={() => setIsReplying((current) => !current)}
                  className="flex h-8 items-center gap-1.5 rounded-xl px-2.5 text-xs font-semibold text-zinc-600 transition hover:bg-white/[0.04] hover:text-zinc-300"
                >
                  <Reply size={14} />
                  Cavabla
                </button>
              )}

              {reply.canDelete && (
                <button
                  type="button"
                  onClick={() => onDelete(reply.id)}
                  className="ml-auto flex h-8 items-center gap-1.5 rounded-xl px-2.5 text-xs text-zinc-600 transition hover:bg-rose-400/[0.07] hover:text-rose-300"
                >
                  <Trash2 size={13} />
                  Sil
                </button>
              )}
            </div>

            {isReplying && (
              <div className="mt-3">
                <textarea
                  value={replyText}
                  onChange={(event) => setReplyText(event.target.value)}
                  rows={3}
                  maxLength={5000}
                  placeholder={`@${reply.authorUserName} istifadəçisinə cavab yaz...`}
                  className="w-full resize-y rounded-xl border border-white/[0.08] bg-black/20 p-3 text-sm text-white outline-none focus:border-teal-300/30"
                  autoFocus
                />
                <div className="mt-2 flex justify-end gap-2">
                  <button
                    type="button"
                    onClick={() => {
                      setIsReplying(false);
                      setReplyText("");
                    }}
                    className="px-3 py-2 text-xs font-semibold text-zinc-600"
                  >
                    Ləğv et
                  </button>
                  <button
                    type="button"
                    disabled={
                      !replyText.trim() || busyReplyId === reply.id
                    }
                    onClick={async () => {
                      await onReply(reply.id, replyText.trim());
                      setReplyText("");
                      setIsReplying(false);
                    }}
                    className="rounded-xl bg-teal-300 px-3 py-2 text-xs font-black text-[#07100f] disabled:opacity-40"
                  >
                    Cavabı paylaş
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </article>

      {reply.children.length > 0 && (
        <div className="forum-comment-children mt-3 space-y-3">
          {reply.children.map((child) => (
            <div className="forum-comment-branch" key={child.id}>
              <CommentItem
                reply={child}
                depth={depth + 1}
                isAuthenticated={isAuthenticated}
                searchQuery={searchQuery}
                busyReplyId={busyReplyId}
                onVote={onVote}
                onDelete={onDelete}
                onReply={onReply}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function ForumDiscussionPage() {
  const { slug = "" } = useParams();
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );
  const isAuthenticated =
    user !== null &&
    isAuthSessionValid(accessToken, expiresAtUtc);

  const [commentText, setCommentText] = useState("");
  const [commentSearch, setCommentSearch] = useState("");
  const [error, setError] = useState("");
  const [busyReplyId, setBusyReplyId] = useState<number | null>(
    null,
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLikeBusy, setIsLikeBusy] = useState(false);

  const discussionQuery = useQuery({
    queryKey: ["forum", "discussion", slug],
    queryFn: ({ signal }) => getDiscussionBySlug(slug, signal),
    enabled: Boolean(slug),
  });

  const discussion = discussionQuery.data;
  const repliesQuery = useQuery({
    queryKey: ["forum", "replies", discussion?.id],
    queryFn: ({ signal }) =>
      getDiscussionReplies(discussion!.id, signal),
    enabled: Boolean(discussion?.id),
  });

  const normalizedSearch = commentSearch
    .trim()
    .toLocaleLowerCase("az-AZ");
  const visibleReplies = useMemo(
    () =>
      filterReplies(
        repliesQuery.data?.items ?? [],
        normalizedSearch,
      ),
    [normalizedSearch, repliesQuery.data?.items],
  );

  async function refreshReplies() {
    await queryClient.invalidateQueries({
      queryKey: ["forum", "replies", discussion?.id],
    });
  }

  async function submitReply(
    parentReplyId: number | undefined,
    content: string,
  ) {
    if (!discussion || !content.trim()) return;
    setError("");
    setIsSubmitting(true);
    if (parentReplyId) setBusyReplyId(parentReplyId);
    try {
      await createDiscussionReply(
        discussion.id,
        content.trim(),
        parentReplyId,
      );
      await refreshReplies();
      setCommentText("");
    } catch (submitError) {
      setError(
        getApiErrorMessage(
          submitError,
          "Şərh paylaşıla bilmədi.",
        ),
      );
    } finally {
      setIsSubmitting(false);
      setBusyReplyId(null);
    }
  }

  async function handleVote(
    reply: DiscussionReplyDTO,
    requestedValue: -1 | 1,
  ) {
    setBusyReplyId(reply.id);
    setError("");
    try {
      const value =
        reply.currentUserVote === requestedValue
          ? 0
          : requestedValue;
      await voteDiscussionReply(reply.id, value);
      await refreshReplies();
    } catch (voteError) {
      setError(
        getApiErrorMessage(voteError, "Səs qeydə alınmadı."),
      );
    } finally {
      setBusyReplyId(null);
    }
  }

  async function handleDelete(replyId: number) {
    if (!window.confirm("Bu şərhi silmək istədiyinizə əminsiniz?"))
      return;
    setBusyReplyId(replyId);
    try {
      await deleteDiscussionReply(replyId);
      await refreshReplies();
    } catch (deleteError) {
      setError(
        getApiErrorMessage(deleteError, "Şərh silinə bilmədi."),
      );
    } finally {
      setBusyReplyId(null);
    }
  }

  if (discussionQuery.isLoading) {
    return (
      <div className="min-h-screen bg-[#07100f]">
        <Navbar />
        <main className="mx-auto max-w-4xl px-5 py-16">
          <div className="h-80 animate-pulse rounded-3xl bg-white/[0.03]" />
        </main>
      </div>
    );
  }

  if (!discussion || discussionQuery.isError) {
    return (
      <div className="min-h-screen bg-[#07100f]">
        <Navbar />
        <main className="mx-auto max-w-3xl px-5 py-20 text-center">
          <h1 className="text-xl font-bold text-white">
            Müzakirə tapılmadı
          </h1>
          <Link
            to="/forum"
            className="mt-5 inline-flex items-center gap-2 text-sm font-bold text-teal-300"
          >
            <ArrowLeft size={16} />
            Foruma qayıt
          </Link>
        </main>
      </div>
    );
  }

  const avatarUrl = resolveAssetUrl(
    discussion.communityIconUrl ??
      discussion.authorProfileImageUrl,
  );
  const displayName =
    discussion.communityName ?? discussion.authorUserName;

  return (
    <div className="min-h-screen bg-[#07100f] text-zinc-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-4 py-6 sm:px-6 lg:py-9">
        <Link
          to="/forum"
          className="inline-flex items-center gap-2 text-xs font-bold text-zinc-500 transition hover:text-teal-300"
        >
          <ArrowLeft size={15} />
          Forum lentinə qayıt
        </Link>

        <article className="mt-5 rounded-[26px] border border-white/[0.08] bg-[#0c1716]/90 p-5 sm:p-7">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center overflow-hidden rounded-xl bg-teal-300/10 text-sm font-black text-teal-200">
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
            <div>
              {discussion.communitySlug ? (
                <Link
                  to={`/forum/communities/${discussion.communitySlug}`}
                  className="text-sm font-bold text-teal-200 transition hover:text-teal-100"
                >
                  {displayName}
                </Link>
              ) : (
                <p className="text-sm font-bold text-zinc-200">
                  {displayName}
                </p>
              )}
              <p className="mt-0.5 text-xs text-zinc-600">
                {formatRelativeDate(discussion.createdAt)} ·{" "}
                {discussion.categoryName}
              </p>
              {discussion.communityId && (
                <p className="mt-1 text-[11px] text-zinc-500">
                  Paylaşan: @{discussion.authorUserName}
                </p>
              )}
            </div>
            <div className="ml-auto flex items-center gap-2">
              {discussion.isLocked && (
                <span className="flex items-center gap-1.5 rounded-full bg-amber-300/10 px-3 py-1 text-[11px] font-bold text-amber-200">
                  <Lock size={12} />
                  Kilidli
                </span>
              )}
              <ForumContentMenu
                kind="discussion"
                contentId={discussion.id}
                contentLabel={discussion.title}
                authorId={discussion.authorId}
                authorUserName={discussion.authorUserName}
                initialIsActive={discussion.isSavedByCurrentUser}
              />
            </div>
          </div>

          <h1 className="mt-6 text-2xl font-bold leading-tight text-white sm:text-3xl">
            {discussion.title}
          </h1>

          <ForumMediaGallery
            media={discussion.media}
            title={discussion.title}
          />

          {discussion.content && (
            <p className="mt-5 whitespace-pre-wrap text-sm leading-7 text-zinc-300">
              {discussion.content}
            </p>
          )}

          <div className="mt-6 flex items-center gap-2 border-t border-white/[0.06] pt-4">
            <button
              type="button"
              disabled={!isAuthenticated || isLikeBusy}
              onClick={async () => {
                setIsLikeBusy(true);
                try {
                  await setDiscussionLike(
                    discussion.id,
                    !discussion.isLikedByCurrentUser,
                  );
                  await discussionQuery.refetch();
                } finally {
                  setIsLikeBusy(false);
                }
              }}
              className={`flex h-9 items-center gap-2 rounded-xl px-3 text-xs font-bold ${
                discussion.isLikedByCurrentUser
                  ? "bg-rose-400/10 text-rose-300"
                  : "bg-white/[0.035] text-zinc-500"
              }`}
            >
              <Heart
                size={15}
                fill={
                  discussion.isLikedByCurrentUser
                    ? "currentColor"
                    : "none"
                }
              />
              {discussion.likeCount}
            </button>
            <span className="flex h-9 items-center gap-2 rounded-xl bg-white/[0.035] px-3 text-xs text-zinc-500">
              <MessageCircle size={15} />
              {discussion.replyCount} şərh
            </span>
          </div>
        </article>

        <section className="mt-6">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-teal-300">
                Söhbət
              </p>
              <h2 className="mt-1 text-xl font-bold text-white">
                Şərhlər
              </h2>
            </div>
            <div className="relative">
              <Search
                size={15}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
              />
              <input
                type="search"
                value={commentSearch}
                onChange={(event) =>
                  setCommentSearch(event.target.value)
                }
                placeholder="Şərhlərdə axtar..."
                className="h-10 w-full rounded-xl border border-white/[0.08] bg-white/[0.025] pl-9 pr-9 text-xs text-white outline-none focus:border-teal-300/25 sm:w-64"
              />
              {commentSearch && (
                <button
                  type="button"
                  onClick={() => setCommentSearch("")}
                  className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-zinc-600"
                  aria-label="Axtarışı təmizlə"
                >
                  <X size={14} />
                </button>
              )}
            </div>
          </div>

          {isAuthenticated && !discussion.isLocked && (
            <div className="mt-4 rounded-2xl border border-white/[0.07] bg-[#0c1716]/70 p-4">
              <textarea
                value={commentText}
                onChange={(event) =>
                  setCommentText(event.target.value)
                }
                maxLength={5000}
                rows={4}
                placeholder="Müzakirəyə şərh yaz..."
                className="forum-comment-editor w-full resize-y bg-transparent text-sm leading-6 text-white placeholder:text-zinc-700"
              />
              <div className="mt-3 flex justify-end pt-1">
                <button
                  type="button"
                  disabled={!commentText.trim() || isSubmitting}
                  onClick={() =>
                    submitReply(undefined, commentText)
                  }
                  className="flex h-10 items-center gap-2 rounded-xl bg-teal-300 px-4 text-xs font-black text-[#07100f] disabled:opacity-40"
                >
                  {isSubmitting ? (
                    <LoaderCircle
                      size={15}
                      className="animate-spin"
                    />
                  ) : (
                    <Send size={15} />
                  )}
                  Şərhi paylaş
                </button>
              </div>
            </div>
          )}

          {error && (
            <p className="mt-4 rounded-xl border border-rose-400/15 bg-rose-400/[0.05] px-4 py-3 text-sm text-rose-200">
              {error}
            </p>
          )}

          <div className="mt-4 space-y-3">
            {repliesQuery.isLoading ? (
              Array.from({ length: 3 }).map((_, index) => (
                <div
                  key={index}
                  className="h-32 animate-pulse rounded-2xl bg-white/[0.025]"
                />
              ))
            ) : visibleReplies.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-white/10 p-9 text-center text-sm text-zinc-600">
                {normalizedSearch
                  ? "Axtarışa uyğun şərh tapılmadı."
                  : "Hələ şərh yoxdur. İlk fikri sən paylaş."}
              </div>
            ) : (
              visibleReplies.map((reply) => (
                <CommentItem
                  key={reply.id}
                  reply={reply}
                  depth={0}
                  isAuthenticated={isAuthenticated}
                  searchQuery={normalizedSearch}
                  busyReplyId={busyReplyId}
                  onVote={handleVote}
                  onDelete={handleDelete}
                  onReply={(parentReplyId, content) =>
                    submitReply(parentReplyId, content)
                  }
                />
              ))
            )}
          </div>
        </section>
      </main>
    </div>
  );
}
