import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowBigDown,
  ArrowBigUp,
  ArrowLeft,
  FileVideo2,
  Heart,
  Eye,
  ImagePlus,
  LoaderCircle,
  Lock,
  MessageCircle,
  Reply,
  Search,
  Send,
  Trash2,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ForumMediaGallery } from "../components/forum/ForumMediaGallery";
import { ForumContentMenu } from "../components/forum/ForumContentMenu";
import { DiscussionPollCard } from "../components/forum/DiscussionPollCard";
import "../components/forum/forum.css";
import { Navbar } from "../components/layout/Navbar";
import { ApiErrorStatusPage } from "../components/status/ApiErrorStatusPage";
import { useI18n } from "../i18n/useI18n";
import { getApiErrorMessage } from "../lib/api";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../lib/helpers";
import {
  createDiscussionReply,
  deleteDiscussion,
  deleteDiscussionReply,
  getDiscussionBySlug,
  getDiscussionReplies,
  setDiscussionLike,
  updateDiscussion,
  updateDiscussionReply,
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
  onDelete: (replyId: number) => Promise<boolean>;
  onUpdate: (replyId: number, content: string) => Promise<void>;
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
  onUpdate,
  onReply,
}: CommentItemProps) {
  const [isReplying, setIsReplying] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [replyText, setReplyText] = useState("");
  const [editText, setEditText] = useState(reply.content);
  const currentUserId = useAuthStore((state) => state.user?.id ?? null);
  const isOwnReply = currentUserId === reply.authorId;
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
            contentLabel={`${reply.authorUserName} şərhi`}
            authorId={reply.authorId}
            authorUserName={reply.authorUserName}
            initialIsActive={reply.isFollowedByCurrentUser}
            canEdit={reply.canEdit}
            canDelete={reply.canDelete}
            onEdit={() => {
              setEditText(reply.content);
              setIsEditing(true);
              setIsReplying(false);
            }}
            onDelete={() => onDelete(reply.id)}
            className="absolute right-3 top-3"
          />
        )}
        <div className="flex gap-3">
          <Link
            to={`/users/${encodeURIComponent(reply.authorUserName)}`}
            aria-label={`${reply.authorUserName} profilinə keç`}
            className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-teal-300/10 text-xs font-black text-teal-200 transition hover:ring-2 hover:ring-teal-300/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-300/50"
          >
            {avatarUrl ? (
              <img
                src={avatarUrl}
                alt={`${reply.authorUserName} profil şəkli`}
                className="h-full w-full object-cover"
              />
            ) : (
              reply.authorUserName.charAt(0).toUpperCase()
            )}
          </Link>

          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2 pr-9 text-xs">
              <Link
                to={`/users/${encodeURIComponent(reply.authorUserName)}`}
                className="font-bold text-zinc-200 transition hover:text-teal-200 hover:underline focus-visible:outline-none focus-visible:text-teal-200 focus-visible:underline"
              >
                {reply.authorUserName}
              </Link>
              {reply.authorCommunityRole === "Owner" && (
                <span className="rounded-full border border-amber-400/20 bg-amber-400/[0.08] px-1.5 py-0.5 text-[9px] font-bold text-amber-300">
                  Sahib
                </span>
              )}
              {reply.authorCommunityRole === "Moderator" && (
                <span className="rounded-full border border-violet-400/20 bg-violet-400/[0.08] px-1.5 py-0.5 text-[9px] font-bold text-violet-300">
                  Moderator
                </span>
              )}
              <span className="text-zinc-700">•</span>
              <time className="text-zinc-600">
                {formatRelativeDate(reply.createdAt)}
              </time>
              {!reply.isDeleted && reply.updatedAt && (
                <span className="text-zinc-600">
                  · Redaktə edilib
                </span>
              )}
            </div>

            {reply.isDeleted ? (
              <p className="mt-3 text-sm italic text-zinc-600">
                Bu şərh silinib.
              </p>
            ) : isEditing ? (
              <div className="mt-3">
                <textarea
                  value={editText}
                  onChange={(event) => setEditText(event.target.value)}
                  rows={4}
                  maxLength={5000}
                  className="w-full resize-y rounded-xl border border-white/[0.08] bg-black/20 p-3 text-sm leading-6 text-white outline-none focus:border-teal-300/30"
                  autoFocus
                />
                <div className="mt-2 flex justify-end gap-2">
                  <button
                    type="button"
                    disabled={busyReplyId === reply.id}
                    onClick={() => {
                      setIsEditing(false);
                      setEditText(reply.content);
                    }}
                    className="px-3 py-2 text-xs font-semibold text-zinc-600"
                  >
                    Ləğv et
                  </button>
                  <button
                    type="button"
                    disabled={
                      !editText.trim() ||
                      editText.trim() === reply.content ||
                      busyReplyId === reply.id
                    }
                    onClick={async () => {
                      try {
                        await onUpdate(reply.id, editText.trim());
                        setIsEditing(false);
                      } catch {
                        // Xəta səhifənin ümumi xəbərdarlıq sahəsində göstərilir.
                      }
                    }}
                    className="rounded-xl bg-teal-300 px-3 py-2 text-xs font-black text-[#07100f] disabled:opacity-40"
                  >
                    Redaktəni saxla
                  </button>
                </div>
              </div>
            ) : (
              <p data-i18n-ignore className="mt-3 whitespace-pre-wrap text-sm leading-6 text-zinc-300">
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

              {reply.canDelete && !isOwnReply && (
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
                onUpdate={onUpdate}
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
  useI18n();
  const { slug = "" } = useParams();
  const navigate = useNavigate();
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
  const [isSpoilerRevealed, setIsSpoilerRevealed] = useState(false);
  const [isEditingDiscussion, setIsEditingDiscussion] = useState(false);
  const [editDiscussionTitle, setEditDiscussionTitle] = useState("");
  const [editDiscussionContent, setEditDiscussionContent] = useState("");
  const [editKeptMediaIds, setEditKeptMediaIds] = useState<number[]>([]);
  const [editDiscussionFiles, setEditDiscussionFiles] = useState<File[]>([]);
  const [isDiscussionBusy, setIsDiscussionBusy] = useState(false);

  const editDiscussionPreviews = useMemo(
    () =>
      editDiscussionFiles.map((file) => ({
        file,
        url: URL.createObjectURL(file),
      })),
    [editDiscussionFiles],
  );

  useEffect(
    () => () =>
      editDiscussionPreviews.forEach((item) =>
        URL.revokeObjectURL(item.url),
      ),
    [editDiscussionPreviews],
  );

  const discussionQuery = useQuery({
    queryKey: ["forum", "discussion", slug],
    queryFn: ({ signal }) => getDiscussionBySlug(slug, signal),
    enabled: Boolean(slug),
  });

  const discussion = discussionQuery.data;
  const shouldHideSpoiler =
    Boolean(discussion?.isSpoiler) &&
    discussion?.authorId !== user?.id &&
    !isSpoilerRevealed;
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

  async function handleUpdateReply(
    replyId: number,
    content: string,
  ) {
    setBusyReplyId(replyId);
    setError("");
    try {
      await updateDiscussionReply(replyId, content);
      await refreshReplies();
    } catch (updateError) {
      setError(
        getApiErrorMessage(updateError, "Şərh redaktə edilə bilmədi."),
      );
      throw updateError;
    } finally {
      setBusyReplyId(null);
    }
  }

  async function handleDelete(replyId: number): Promise<boolean> {
    if (!window.confirm("Bu şərhi silmək istədiyinizə əminsiniz?"))
      return false;

    setBusyReplyId(replyId);
    setError("");
    try {
      await deleteDiscussionReply(replyId);
      await refreshReplies();
      return true;
    } catch (deleteError) {
      setError(
        getApiErrorMessage(deleteError, "Şərh silinə bilmədi."),
      );
      throw deleteError;
    } finally {
      setBusyReplyId(null);
    }
  }

  function beginDiscussionEdit() {
    if (!discussion) return;

    setEditDiscussionTitle(discussion.title);
    setEditDiscussionContent(discussion.content);
    setEditKeptMediaIds(
      discussion.media
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder)
        .map((media) => media.id),
    );
    setEditDiscussionFiles([]);
    setError("");
    setIsEditingDiscussion(true);
  }

  function cancelDiscussionEdit() {
    if (isDiscussionBusy) return;

    setIsEditingDiscussion(false);
    setEditDiscussionTitle("");
    setEditDiscussionContent("");
    setEditKeptMediaIds([]);
    setEditDiscussionFiles([]);
    setError("");
  }

  function addEditDiscussionFiles(selected: FileList | null) {
    if (!selected) return;

    const acceptedTypes = new Set([
      "image/jpeg",
      "image/png",
      "image/webp",
      "video/mp4",
      "video/webm",
    ]);
    const currentCount =
      editKeptMediaIds.length + editDiscussionFiles.length;
    const availableSlots = Math.max(0, 8 - currentCount);

    if (availableSlots === 0) {
      setError("Bir müzakirədə maksimum 8 media ola bilər.");
      return;
    }

    const acceptedFiles: File[] = [];

    for (const file of Array.from(selected)) {
      if (!acceptedTypes.has(file.type)) {
        setError(
          "Yalnız JPG, PNG, WEBP şəkilləri və MP4, WEBM videoları əlavə edilə bilər.",
        );
        continue;
      }

      const isVideo = file.type.startsWith("video/");
      const maxSize = isVideo
        ? 100 * 1024 * 1024
        : 10 * 1024 * 1024;

      if (file.size > maxSize) {
        setError(
          isVideo
            ? "Hər video maksimum 100 MB ola bilər."
            : "Hər şəkil maksimum 10 MB ola bilər.",
        );
        continue;
      }

      acceptedFiles.push(file);
      if (acceptedFiles.length >= availableSlots) break;
    }

    if (acceptedFiles.length > 0) {
      setEditDiscussionFiles((current) => [
        ...current,
        ...acceptedFiles,
      ]);
      setError("");
    }
  }

  async function handleDiscussionUpdate() {
    if (!discussion || !editDiscussionTitle.trim()) return;

    const normalizedContent = editDiscussionContent.trim();
    const resultingMediaCount =
      editKeptMediaIds.length + editDiscussionFiles.length;

    if (!normalizedContent && resultingMediaCount === 0) {
      setError("Müzakirə mətni və ya ən azı bir media saxlanmalıdır.");
      return;
    }

    setError("");
    setIsDiscussionBusy(true);
    try {
      await updateDiscussion({
        discussionId: discussion.id,
        title: editDiscussionTitle.trim(),
        content: normalizedContent,
        categoryId: discussion.categoryId,
        isSpoiler: discussion.isSpoiler,
        keptMediaIds: editKeptMediaIds,
        mediaFiles: editDiscussionFiles,
      });
      await discussionQuery.refetch();
      setIsEditingDiscussion(false);
      setEditDiscussionTitle("");
      setEditDiscussionContent("");
      setEditKeptMediaIds([]);
      setEditDiscussionFiles([]);
    } catch (updateError) {
      setError(
        getApiErrorMessage(
          updateError,
          "Müzakirə redaktə edilə bilmədi.",
        ),
      );
    } finally {
      setIsDiscussionBusy(false);
    }
  }

  async function handleDiscussionDelete(): Promise<boolean> {
    if (!discussion) return false;
    if (!window.confirm("Bu müzakirəni silmək istədiyinizə əminsiniz?"))
      return false;

    setError("");
    setIsDiscussionBusy(true);
    try {
      await deleteDiscussion(discussion.id);
      navigate("/forum");
      return true;
    } catch (deleteError) {
      setError(
        getApiErrorMessage(deleteError, "Müzakirə silinə bilmədi."),
      );
      throw deleteError;
    } finally {
      setIsDiscussionBusy(false);
    }
  }

  if (discussionQuery.isLoading) {
    return (
      <div className="forum-theme min-h-screen">
        <Navbar />
        <main className="mx-auto max-w-4xl px-5 py-16">
          <div className="h-80 animate-pulse rounded-3xl bg-white/[0.03]" />
        </main>
      </div>
    );
  }

  if (!discussion || discussionQuery.isError) {
    return (
      <ApiErrorStatusPage
        error={discussionQuery.error}
        entityName="Müzakirə"
      />
    );
  }

  const avatarUrl = resolveAssetUrl(
    discussion.communityIconUrl ??
      discussion.authorProfileImageUrl,
  );
  const displayName =
    discussion.communityName ?? discussion.authorUserName;
  const originalMediaIds = discussion.media
    .slice()
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((media) => media.id);
  const keptExistingMedia = editKeptMediaIds
    .map((mediaId) =>
      discussion.media.find((media) => media.id === mediaId),
    )
    .filter((media) => media !== undefined);
  const editMediaCount =
    keptExistingMedia.length + editDiscussionFiles.length;
  const isEditMediaUnchanged =
    editDiscussionFiles.length === 0 &&
    originalMediaIds.length === editKeptMediaIds.length &&
    originalMediaIds.every(
      (mediaId, index) => mediaId === editKeptMediaIds[index],
    );
  const isDiscussionEditUnchanged =
    editDiscussionTitle.trim() === discussion.title &&
    editDiscussionContent.trim() === discussion.content &&
    isEditMediaUnchanged;

  return (
    <div className="forum-theme min-h-screen text-zinc-100">
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
                {formatRelativeDate(discussion.createdAt)}
                {discussion.updatedAt && " · Redaktə edilib"} ·{" "}
                {discussion.categoryName}
              </p>
              {discussion.communityId && (
                <p className="mt-1 text-[11px] text-zinc-500">
                  Paylaşan:{" "}
                  <Link
                    to={`/users/${encodeURIComponent(
                      discussion.authorUserName,
                    )}`}
                    className="font-semibold text-zinc-400 transition hover:text-teal-300"
                  >
                    {discussion.authorUserName}
                  </Link>
                  {discussion.authorCommunityRole === "Owner" && (
                    <span className="ml-1 rounded-full border border-amber-400/20 bg-amber-400/[0.08] px-1.5 py-0.5 text-[9px] font-bold text-amber-300">
                      Sahib
                    </span>
                  )}
                  {discussion.authorCommunityRole === "Moderator" && (
                    <span className="ml-1 rounded-full border border-violet-400/20 bg-violet-400/[0.08] px-1.5 py-0.5 text-[9px] font-bold text-violet-300">
                      Moderator
                    </span>
                  )}
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
                canEdit={discussion.canEdit}
                canDelete={discussion.canDelete}
                onEdit={beginDiscussionEdit}
                onDelete={handleDiscussionDelete}
              />
            </div>
          </div>

          {isEditingDiscussion ? (
            <div className="mt-6 space-y-4">
              <input
                type="text"
                value={editDiscussionTitle}
                onChange={(event) =>
                  setEditDiscussionTitle(event.target.value)
                }
                maxLength={200}
                className="w-full rounded-xl border border-white/[0.08] bg-black/20 px-4 py-3 text-base font-bold text-white outline-none focus:border-teal-300/30"
                aria-label="Müzakirə başlığı"
              />
              <textarea
                value={editDiscussionContent}
                onChange={(event) =>
                  setEditDiscussionContent(event.target.value)
                }
                maxLength={10000}
                rows={7}
                placeholder="Müzakirə mətni"
                className="w-full resize-y rounded-xl border border-white/[0.08] bg-black/20 p-4 text-sm leading-7 text-white outline-none focus:border-teal-300/30"
                aria-label="Müzakirə mətni"
              />

              <div>
                <div className="flex items-center justify-between gap-3">
                  <p className="text-xs font-bold text-zinc-300">
                    Şəkil və videolar
                  </p>
                  <span className="text-[11px] text-zinc-600">
                    {editMediaCount}/8
                  </span>
                </div>

                <label className="mt-2 flex cursor-pointer items-center justify-center gap-3 rounded-2xl border border-dashed border-white/10 bg-white/[0.02] px-4 py-5 text-sm text-zinc-500 transition hover:border-teal-300/25 hover:text-teal-200">
                  <ImagePlus size={18} />
                  Şəkil və ya video əlavə et
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp,video/mp4,video/webm"
                    multiple
                    className="sr-only"
                    disabled={editMediaCount >= 8 || isDiscussionBusy}
                    onChange={(event) => {
                      addEditDiscussionFiles(event.target.files);
                      event.currentTarget.value = "";
                    }}
                  />
                </label>

                {editMediaCount > 0 && (
                  <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
                    {keptExistingMedia.map((media) => {
                      const mediaUrl =
                        resolveAssetUrl(media.url) ?? media.url;

                      return (
                        <div
                          key={`existing-${media.id}`}
                          className="relative h-32 overflow-hidden rounded-2xl border border-white/[0.08] bg-black/25"
                        >
                          {media.isVideo ? (
                            <video
                              src={mediaUrl}
                              controls
                              preload="metadata"
                              className="h-full w-full object-contain"
                            />
                          ) : (
                            <img
                              src={mediaUrl}
                              alt={media.originalFileName}
                              className="h-full w-full object-cover"
                            />
                          )}
                          <button
                            type="button"
                            onClick={() =>
                              setEditKeptMediaIds((current) =>
                                current.filter(
                                  (mediaId) => mediaId !== media.id,
                                ),
                              )
                            }
                            disabled={isDiscussionBusy}
                            className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-xl border border-white/10 bg-black/75 text-white backdrop-blur transition hover:bg-rose-500/85 disabled:opacity-50"
                            aria-label={`${media.originalFileName} faylını sil`}
                          >
                            <X size={15} />
                          </button>
                        </div>
                      );
                    })}

                    {editDiscussionPreviews.map(
                      ({ file, url }, index) => (
                        <div
                          key={`${file.name}-${file.lastModified}-${index}`}
                          className="relative h-32 overflow-hidden rounded-2xl border border-teal-300/15 bg-black/25"
                        >
                          {file.type.startsWith("video/") ? (
                            <div className="flex h-full flex-col items-center justify-center gap-2 px-3 text-center text-zinc-500">
                              <FileVideo2 size={28} />
                              <span className="max-w-full truncate text-[10px]">
                                {file.name}
                              </span>
                            </div>
                          ) : (
                            <img
                              src={url}
                              alt={file.name}
                              className="h-full w-full object-cover"
                            />
                          )}
                          <button
                            type="button"
                            onClick={() =>
                              setEditDiscussionFiles((current) =>
                                current.filter(
                                  (_, currentIndex) =>
                                    currentIndex !== index,
                                ),
                              )
                            }
                            disabled={isDiscussionBusy}
                            className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-xl border border-white/10 bg-black/75 text-white backdrop-blur transition hover:bg-rose-500/85 disabled:opacity-50"
                            aria-label={`${file.name} faylını sil`}
                          >
                            <X size={15} />
                          </button>
                        </div>
                      ),
                    )}
                  </div>
                )}

                <p className="mt-2 text-[11px] leading-5 text-zinc-600">
                  Şəkillər maksimum 10 MB, videolar maksimum 100 MB ola bilər.
                </p>
              </div>

              <div className="flex justify-end gap-2 border-t border-white/[0.06] pt-4">
                <button
                  type="button"
                  disabled={isDiscussionBusy}
                  onClick={cancelDiscussionEdit}
                  className="rounded-xl px-4 py-2.5 text-xs font-semibold text-zinc-500 transition hover:text-zinc-300"
                >
                  Ləğv et
                </button>
                <button
                  type="button"
                  disabled={
                    isDiscussionBusy ||
                    !editDiscussionTitle.trim() ||
                    (!editDiscussionContent.trim() &&
                      editMediaCount === 0) ||
                    isDiscussionEditUnchanged
                  }
                  onClick={handleDiscussionUpdate}
                  className="flex items-center gap-2 rounded-xl bg-teal-300 px-4 py-2.5 text-xs font-black text-[#07100f] disabled:opacity-40"
                >
                  {isDiscussionBusy && (
                    <LoaderCircle size={14} className="animate-spin" />
                  )}
                  Redaktəni saxla
                </button>
              </div>
            </div>
          ) : (
            <>
              <h1 data-i18n-ignore className="mt-6 text-2xl font-bold leading-tight text-white sm:text-3xl">
                {discussion.title}
              </h1>
              {shouldHideSpoiler && (
                <button
                  type="button"
                  onClick={() => setIsSpoilerRevealed(true)}
                  className="mt-5 flex w-full items-center justify-center gap-2 rounded-2xl border border-amber-300/15 bg-amber-300/[0.05] px-5 py-8 text-sm font-bold text-amber-200 transition hover:bg-amber-300/[0.09]"
                >
                  <Eye size={18} />
                  Spoiler məzmununu göstər
                </button>
              )}
              {!shouldHideSpoiler && (
                <ForumMediaGallery
                  media={discussion.media}
                  title={discussion.title}
                />
              )}
              {discussion.content && !shouldHideSpoiler && (
                <p data-i18n-ignore className="mt-5 whitespace-pre-wrap text-sm leading-7 text-zinc-300">
                  {discussion.content}
                </p>
              )}
              {discussion.poll && !shouldHideSpoiler && (
                <DiscussionPollCard
                  discussionId={discussion.id}
                  poll={discussion.poll}
                  isAuthenticated={isAuthenticated}
                  onChanged={(poll) =>
                    queryClient.setQueryData(
                      ["forum", "discussion", slug],
                      { ...discussion, poll },
                    )
                  }
                />
              )}
            </>
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
                  onUpdate={handleUpdateReply}
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
