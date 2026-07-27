import {
  Bell,
  BellOff,
  Bookmark,
  BookmarkCheck,
  Flag,
  LoaderCircle,
  MoreHorizontal,
  Pencil,
  Trash2,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { getApiErrorMessage } from "../../lib/api";
import {
  setDiscussionReplyFollowed,
  setDiscussionSaved,
} from "../../services/forumService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";
import type { UserReportContentType } from "../../types/userReports";
import { UserReportModal } from "../profile/UserReportModal";

interface ForumContentMenuProps {
  kind: "discussion" | "reply";
  contentId: number;
  contentLabel: string;
  authorId: string;
  authorUserName: string;
  initialIsActive: boolean;
  canEdit?: boolean;
  canDelete?: boolean;
  onEdit?: () => void;
  onDelete?: () => boolean | void | Promise<boolean | void>;
  className?: string;
}

export function ForumContentMenu({
  kind,
  contentId,
  contentLabel,
  authorId,
  authorUserName,
  initialIsActive,
  canEdit = false,
  canDelete = false,
  onEdit,
  onDelete,
  className = "",
}: ForumContentMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [isReportOpen, setIsReportOpen] = useState(false);
  const [isActive, setIsActive] = useState(initialIsActive);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const currentUserId = useAuthStore((state) => state.user?.id ?? null);
  const isAuthenticated = isAuthSessionValid(
    accessToken,
    expiresAtUtc,
  );
  const isOwnContent = currentUserId === authorId;
  const reportContentType: UserReportContentType =
    kind === "discussion" ? "Discussion" : "DiscussionReply";

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        menuRef.current &&
        !menuRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  if (!isAuthenticated) {
    return null;
  }

  const hasOwnActions =
    (canEdit && Boolean(onEdit)) ||
    (canDelete && Boolean(onDelete));

  if (isOwnContent && !hasOwnActions) {
    return null;
  }

  const toggleActiveState = async () => {
    const nextValue = !isActive;
    setError(null);
    setIsBusy(true);

    try {
      if (kind === "discussion") {
        await setDiscussionSaved(contentId, nextValue);
      } else {
        await setDiscussionReplyFollowed(contentId, nextValue);
      }

      setIsActive(nextValue);
      setIsOpen(false);
    } catch (requestError) {
      setError(
        getApiErrorMessage(
          requestError,
          kind === "discussion"
            ? "Müzakirənin yadda saxlanma vəziyyəti dəyişdirilmədi."
            : "Şərhin izlənmə vəziyyəti dəyişdirilmədi.",
        ),
      );
    } finally {
      setIsBusy(false);
    }
  };

  const handleDelete = async () => {
    if (!onDelete) return;

    setError(null);
    setIsBusy(true);
    try {
      const result = await onDelete();
      if (result !== false) {
        setIsOpen(false);
      }
    } catch (requestError) {
      setError(
        getApiErrorMessage(
          requestError,
          kind === "discussion"
            ? "Müzakirə silinə bilmədi."
            : "Şərh silinə bilmədi.",
        ),
      );
    } finally {
      setIsBusy(false);
    }
  };

  return (
    <>
      <div ref={menuRef} className={className}>
        <div className="relative">
          <button
            type="button"
            onClick={() => {
              setError(null);
              setIsOpen((current) => !current);
            }}
            className="flex h-8 w-8 items-center justify-center rounded-xl text-zinc-600 transition hover:bg-white/[0.055] hover:text-zinc-300"
            aria-label="Əlavə seçimlər"
            aria-haspopup="menu"
            aria-expanded={isOpen}
          >
            <MoreHorizontal size={18} />
          </button>

          {isOpen && (
            <div
              role="menu"
              className="absolute right-0 top-full z-50 mt-2 w-56 rounded-2xl border border-white/[0.09] bg-[#111c1b] p-1.5 shadow-2xl shadow-black/50"
            >
              {isOwnContent ? (
                <>
                  {canEdit && onEdit && (
                    <button
                      type="button"
                      role="menuitem"
                      disabled={isBusy}
                      onClick={() => {
                        setIsOpen(false);
                        onEdit();
                      }}
                      className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.055] disabled:opacity-50"
                    >
                      <Pencil size={16} />
                      Redaktə et
                    </button>
                  )}

                  {canDelete && onDelete && (
                    <button
                      type="button"
                      role="menuitem"
                      disabled={isBusy}
                      onClick={handleDelete}
                      className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-xs font-semibold text-rose-300 transition hover:bg-rose-400/[0.08] disabled:opacity-50"
                    >
                      {isBusy ? (
                        <LoaderCircle size={16} className="animate-spin" />
                      ) : (
                        <Trash2 size={16} />
                      )}
                      Sil
                    </button>
                  )}
                </>
              ) : (
                <>
                  <button
                    type="button"
                    role="menuitem"
                    disabled={isBusy}
                    onClick={toggleActiveState}
                    className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.055] disabled:opacity-50"
                  >
                    {isBusy ? (
                      <LoaderCircle size={16} className="animate-spin" />
                    ) : kind === "discussion" ? (
                      isActive ? (
                        <BookmarkCheck size={16} className="text-teal-300" />
                      ) : (
                        <Bookmark size={16} />
                      )
                    ) : isActive ? (
                      <BellOff size={16} className="text-teal-300" />
                    ) : (
                      <Bell size={16} />
                    )}
                    {kind === "discussion"
                      ? isActive
                        ? "Yadda saxlanılıb"
                        : "Yadda saxla"
                      : isActive
                        ? "İzləməni dayandır"
                        : "Şərhi izlə"}
                  </button>

                  {canDelete && onDelete && (
                    <button
                      type="button"
                      role="menuitem"
                      disabled={isBusy}
                      onClick={handleDelete}
                      className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-xs font-semibold text-rose-300 transition hover:bg-rose-400/[0.08] disabled:opacity-50"
                    >
                      {isBusy ? (
                        <LoaderCircle size={16} className="animate-spin" />
                      ) : (
                        <Trash2 size={16} />
                      )}
                      Sil
                    </button>
                  )}

                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => {
                      setIsOpen(false);
                      setIsReportOpen(true);
                    }}
                    className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-xs font-semibold text-rose-300 transition hover:bg-rose-400/[0.08]"
                  >
                    <Flag size={16} />
                    Şikayət et
                  </button>
                </>
              )}

              {error && (
                <p className="px-3 py-2 text-[11px] leading-4 text-rose-300">
                  {error}
                </p>
              )}
            </div>
          )}
        </div>
      </div>

      {!isOwnContent && (
        <UserReportModal
          isOpen={isReportOpen}
          reportedUserId={authorId}
          reportedUserName={authorUserName}
          reportedContentType={reportContentType}
          reportedContentId={contentId}
          reportedContentLabel={contentLabel}
          onClose={() => setIsReportOpen(false)}
          onSubmitted={() => setIsReportOpen(false)}
        />
      )}
    </>
  );
}
