import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  LoaderCircle,
  MessageCircle,
  Pencil,
  Send,
  Trash2,
  X,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
import {
  createReviewComment,
  deleteReviewComment,
  getReviewComments,
  updateReviewComment,
} from "../../services/reviewsService";
import type { ReviewCommentDTO } from "../../types/reviews";

interface ReviewCommentsPanelProps {
  reviewId: number;
  isAuthenticated: boolean;
}

function CommentAvatar({
  comment,
}: {
  comment: ReviewCommentDTO;
}) {
  const imageUrl = resolveAssetUrl(
    comment.profileImageUrl,
  );

  return (
    <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.07] bg-violet-500/10 text-xs font-black text-violet-200">
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={`${comment.userName} profil şəkli`}
          className="h-full w-full object-cover"
        />
      ) : (
        comment.userName.trim().charAt(0).toUpperCase() || "?"
      )}
    </span>
  );
}

export function ReviewCommentsPanel({
  reviewId,
  isAuthenticated,
}: ReviewCommentsPanelProps) {
  const { language } = useI18n();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [content, setContent] = useState("");
  const [editingCommentId, setEditingCommentId] =
    useState<number | null>(null);
  const [editingContent, setEditingContent] = useState("");
  const [errorMessage, setErrorMessage] =
    useState<string | null>(null);

  const commentsQuery = useQuery({
    queryKey: ["review-comments", reviewId, page],
    queryFn: () => getReviewComments(reviewId, page, 8),
  });

  const refreshComments = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["review-comments", reviewId],
      }),
      queryClient.invalidateQueries({
        queryKey: ["game-reviews"],
      }),
      queryClient.invalidateQueries({
        queryKey: ["user-reviews"],
      }),
    ]);
  };

  const createMutation = useMutation({
    mutationFn: () =>
      createReviewComment(reviewId, content.trim()),
    onSuccess: async () => {
      setContent("");
      setErrorMessage(null);
      await refreshComments();
    },
    onError: (error) => {
      setErrorMessage(
        getApiErrorMessage(
          error,
          "Şərhi əlavə etmək mümkün olmadı.",
        ),
      );
    },
  });

  const updateMutation = useMutation({
    mutationFn: () =>
      updateReviewComment(
        editingCommentId!,
        editingContent.trim(),
      ),
    onSuccess: async () => {
      setEditingCommentId(null);
      setEditingContent("");
      setErrorMessage(null);
      await refreshComments();
    },
    onError: (error) => {
      setErrorMessage(
        getApiErrorMessage(
          error,
          "Şərhi yeniləmək mümkün olmadı.",
        ),
      );
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteReviewComment,
    onSuccess: async () => {
      if (
        page > 1 &&
        (commentsQuery.data?.items.length ?? 0) === 1
      ) {
        setPage((currentPage) => currentPage - 1);
      }
      setErrorMessage(null);
      await refreshComments();
    },
    onError: (error) => {
      setErrorMessage(
        getApiErrorMessage(
          error,
          "Şərhi silmək mümkün olmadı.",
        ),
      );
    },
  });

  const result = commentsQuery.data;
  const comments = result?.items ?? [];
  const totalPages = result?.totalPages ?? 0;

  return (
    <div className="border-t border-white/[0.06] bg-black/10 px-5 py-5">
      <div className="flex items-center gap-2 text-sm font-bold text-zinc-300">
        <MessageCircle size={16} className="text-violet-300" />
        Şərhlər
      </div>

      {isAuthenticated ? (
        <div className="mt-4">
          <textarea
            value={content}
            onChange={(event) => {
              setContent(event.target.value);
            }}
            rows={3}
            maxLength={2000}
            placeholder="Rəyə şərh yaz..."
            className="w-full resize-none rounded-2xl border border-white/[0.08] bg-white/[0.03] px-4 py-3 text-sm leading-6 text-zinc-200 outline-none transition placeholder:text-zinc-700 focus:border-violet-400/40 focus:ring-4 focus:ring-violet-500/10"
          />
          <div className="mt-2 flex items-center justify-between gap-3">
            <span className="text-[11px] text-zinc-700">
              {content.length}/2000
            </span>
            <button
              type="button"
              disabled={
                createMutation.isPending ||
                content.trim().length === 0
              }
              onClick={() => {
                createMutation.mutate();
              }}
              className="inline-flex h-9 items-center gap-2 rounded-xl bg-violet-600 px-3.5 text-xs font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-45"
            >
              {createMutation.isPending ? (
                <LoaderCircle size={14} className="animate-spin" />
              ) : (
                <Send size={14} />
              )}
              Göndər
            </button>
          </div>
        </div>
      ) : (
        <div className="mt-4 rounded-2xl border border-white/[0.07] bg-white/[0.025] px-4 py-3 text-sm text-zinc-500">
          {language === "en" ? (
            <>
              <Link
                to="/login"
                className="font-semibold text-violet-300 transition hover:text-violet-200"
              >
                Sign in
              </Link>{" "}
              to write a comment.
            </>
          ) : (
            <>
              Şərh yazmaq üçün{" "}
              <Link
                to="/login"
                className="font-semibold text-violet-300 transition hover:text-violet-200"
              >
                hesabına daxil ol
              </Link>
              .
            </>
          )}
        </div>
      )}

      {errorMessage && (
        <div className="mt-3 flex items-start gap-2 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-3 py-2 text-xs leading-5 text-red-200">
          <X size={14} className="mt-0.5 shrink-0" />
          {errorMessage}
        </div>
      )}

      {commentsQuery.isLoading ? (
        <div className="flex items-center justify-center gap-2 py-8 text-xs text-zinc-600">
          <LoaderCircle size={15} className="animate-spin" />
          Şərhlər yüklənir...
        </div>
      ) : commentsQuery.isError ? (
        <div className="mt-4 rounded-xl border border-red-400/15 bg-red-500/[0.05] px-3 py-3 text-xs text-red-200">
          {getApiErrorMessage(
            commentsQuery.error,
            "Şərhləri yükləmək mümkün olmadı.",
          )}
        </div>
      ) : comments.length === 0 ? (
        <p className="py-7 text-center text-xs text-zinc-700">
          Bu rəyə hələ şərh yazılmayıb.
        </p>
      ) : (
        <div className="mt-5 space-y-3">
          {comments.map((comment) => (
            <div
              key={comment.id}
              className="rounded-2xl border border-white/[0.06] bg-white/[0.025] p-3.5"
            >
              <div className="flex items-start gap-3">
                <Link to={`/users/${comment.userName}`}>
                  <CommentAvatar comment={comment} />
                </Link>

                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <Link
                        to={`/users/${comment.userName}`}
                        className="text-xs font-bold text-zinc-200 transition hover:text-violet-200"
                      >
                        {comment.userName}
                      </Link>
                      <p className="mt-1 text-[11px] text-zinc-700">
                        {formatDateTime(comment.createdAt)}
                        {comment.updatedAt
                          ? " · redaktə edilib"
                          : ""}
                      </p>
                    </div>

                    {comment.isMine && (
                      <div className="flex items-center gap-1">
                        <button
                          type="button"
                          onClick={() => {
                            setEditingCommentId(comment.id);
                            setEditingContent(comment.content);
                          }}
                          className="flex h-8 w-8 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-white/[0.06] hover:text-zinc-300"
                          aria-label="Şərhi redaktə et"
                        >
                          <Pencil size={14} />
                        </button>
                        <button
                          type="button"
                          disabled={deleteMutation.isPending}
                          onClick={() => {
                            if (
                              window.confirm(
                                "Bu şərhi silmək istəyirsən?",
                              )
                            ) {
                              deleteMutation.mutate(comment.id);
                            }
                          }}
                          className="flex h-8 w-8 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-red-500/[0.08] hover:text-red-300 disabled:opacity-40"
                          aria-label="Şərhi sil"
                        >
                          <Trash2 size={14} />
                        </button>
                      </div>
                    )}
                  </div>

                  {editingCommentId === comment.id ? (
                    <div className="mt-3">
                      <textarea
                        value={editingContent}
                        onChange={(event) => {
                          setEditingContent(event.target.value);
                        }}
                        rows={3}
                        maxLength={2000}
                        className="w-full resize-none rounded-xl border border-white/[0.08] bg-black/20 px-3 py-2.5 text-xs leading-6 text-zinc-200 outline-none focus:border-violet-400/40"
                      />
                      <div className="mt-2 flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            setEditingCommentId(null);
                            setEditingContent("");
                          }}
                          className="rounded-lg px-3 py-2 text-xs font-semibold text-zinc-600 transition hover:bg-white/[0.05] hover:text-zinc-300"
                        >
                          Ləğv et
                        </button>
                        <button
                          type="button"
                          disabled={
                            updateMutation.isPending ||
                            editingContent.trim().length === 0
                          }
                          onClick={() => {
                            updateMutation.mutate();
                          }}
                          className="inline-flex items-center gap-2 rounded-lg bg-violet-600 px-3 py-2 text-xs font-bold text-white transition hover:bg-violet-500 disabled:opacity-45"
                        >
                          {updateMutation.isPending && (
                            <LoaderCircle
                              size={13}
                              className="animate-spin"
                            />
                          )}
                          Yadda saxla
                        </button>
                      </div>
                    </div>
                  ) : (
                    <p data-user-content className="mt-3 whitespace-pre-wrap break-words text-xs leading-6 text-zinc-400">
                      {comment.content}
                    </p>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-2">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => {
              setPage((currentPage) => Math.max(1, currentPage - 1));
            }}
            className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/[0.07] text-zinc-500 transition hover:text-white disabled:opacity-30"
            aria-label="Əvvəlki şərh səhifəsi"
          >
            <ChevronLeft size={15} />
          </button>
          <span className="min-w-16 text-center text-[11px] font-semibold text-zinc-600">
            {page} / {totalPages}
          </span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => {
              setPage((currentPage) =>
                Math.min(totalPages, currentPage + 1),
              );
            }}
            className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/[0.07] text-zinc-500 transition hover:text-white disabled:opacity-30"
            aria-label="Növbəti şərh səhifəsi"
          >
            <ChevronRight size={15} />
          </button>
        </div>
      )}
    </div>
  );
}
