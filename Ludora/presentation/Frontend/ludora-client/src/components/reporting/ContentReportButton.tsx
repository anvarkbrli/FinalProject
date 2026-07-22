import { Check, Flag } from "lucide-react";
import { useEffect, useState } from "react";
import { isAuthSessionValid, useAuthStore } from "../../stores/authStore";
import type { UserReportContentType } from "../../types/userReports";
import { UserReportModal } from "../profile/UserReportModal";

interface ContentReportButtonProps {
  reportedUserId: string;
  reportedUserName: string;
  contentType: UserReportContentType;
  contentId: number;
  contentLabel: string;
  isOwnContent?: boolean;
  className?: string;
}

export function ContentReportButton({
  reportedUserId,
  reportedUserName,
  contentType,
  contentId,
  contentLabel,
  isOwnContent = false,
  className = "flex h-8 w-8 items-center justify-center rounded-lg text-zinc-600 transition hover:bg-red-500/[0.08] hover:text-red-300",
}: ContentReportButtonProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [wasSubmitted, setWasSubmitted] = useState(false);

  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const currentUserId = useAuthStore((state) => state.user?.id ?? null);

  const isAuthenticated = isAuthSessionValid(accessToken, expiresAtUtc);
  const isCurrentUsersContent =
    isOwnContent || currentUserId === reportedUserId;

  useEffect(() => {
    if (!wasSubmitted) {
      return;
    }

    const timeoutId = window.setTimeout(() => {
      setWasSubmitted(false);
    }, 4000);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [wasSubmitted]);

  if (!isAuthenticated || isCurrentUsersContent) {
    return null;
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        className={className}
        aria-label={`${contentLabel} üçün şikayət göndər`}
        title={
          wasSubmitted
            ? "Şikayət göndərildi"
            : "Bu məzmunu şikayət et"
        }
      >
        {wasSubmitted ? <Check size={14} /> : <Flag size={14} />}
      </button>

      <UserReportModal
        isOpen={isOpen}
        reportedUserId={reportedUserId}
        reportedUserName={reportedUserName}
        reportedContentType={contentType}
        reportedContentId={contentId}
        reportedContentLabel={contentLabel}
        onClose={() => setIsOpen(false)}
        onSubmitted={() => {
          setIsOpen(false);
          setWasSubmitted(true);
        }}
      />
    </>
  );
}
