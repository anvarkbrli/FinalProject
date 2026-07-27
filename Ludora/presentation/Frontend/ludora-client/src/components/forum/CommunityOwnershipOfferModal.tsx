import { Crown, LoaderCircle, X } from "lucide-react";
import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { getApiErrorMessage } from "../../lib/api";
import { useConfirmation } from "../../lib/confirmation";
import {
  acceptCommunityOwnershipOffer,
  declineCommunityOwnershipOffer,
} from "../../services/forumCommunityService";

interface CommunityOwnershipOfferModalProps {
  communityId: number;
  communityName: string;
  onAccepted: () => Promise<void>;
  onDeclined: () => Promise<void>;
}

export function CommunityOwnershipOfferModal({
  communityId,
  communityName,
  onAccepted,
  onDeclined,
}: CommunityOwnershipOfferModalProps) {
  const requestConfirmation = useConfirmation();
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, []);

  async function run(action: () => Promise<void>) {
    setError("");
    setIsBusy(true);
    try {
      await action();
    } catch (actionError) {
      setError(
        getApiErrorMessage(
          actionError,
          "Sahiblik təklifinə cavab vermək mümkün olmadı.",
        ),
      );
    } finally {
      setIsBusy(false);
    }
  }

  async function handleDecline() {
    const confirmed = await requestConfirmation({
      title: "İcma sahibliyi təklifi rədd edilsin?",
      description: `${communityName} icmasına sahiblik etmək üçün göndərilən təklifi rədd edəcəksən.`,
      detail:
        "Təklif bağlanacaq və icmanın hazırkı sahibi başqa namizəd seçməli olacaq.",
      confirmLabel: "Təklifi rədd et",
      kind: "moderation",
      tone: "warning",
    });

    if (!confirmed) return;

    await run(async () => {
      await declineCommunityOwnershipOffer(communityId);
      await onDeclined();
    });
  }

  return createPortal(
    <div
      className="fixed inset-0 z-[1120] grid place-items-center bg-black/75 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="community-ownership-offer-title"
    >
      <section className="w-full max-w-lg rounded-[28px] border border-[var(--wp-border)] bg-[var(--wp-surface-strong)] p-6 text-center text-[var(--wp-text)] shadow-[var(--wp-shadow-floating)] sm:p-7">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl border border-amber-400/20 bg-amber-400/[0.09] text-amber-300">
          <Crown size={28} />
        </div>
        <h2
          id="community-ownership-offer-title"
          className="mt-5 text-xl font-black text-[var(--wp-text-strong)]"
        >
          İcma sahibliyi təklifi
        </h2>
        <p className="mt-3 text-sm leading-7 text-[var(--wp-text-muted)]">
          Sizə <strong className="text-[var(--wp-text-strong)]">{communityName}</strong>{" "}
          icmasının sahibi olmaq təklif edilir. Qəbul etdikdə icmanın idarəsi,
          üzvləri çıxarmaq, moderator təyin etmək və icmanı silmək daxil bütün
          sahib səlahiyyətləri sizə keçəcək.
        </p>

        {error && (
          <p className="mt-4 rounded-2xl border border-rose-400/15 bg-rose-400/[0.06] px-4 py-3 text-sm text-rose-300">
            {error}
          </p>
        )}

        <div className="mt-7 grid grid-cols-2 gap-3">
          <button
            type="button"
            disabled={isBusy}
            onClick={() => void handleDecline()}
            className="flex h-11 items-center justify-center gap-2 rounded-2xl border border-[var(--wp-border)] text-sm font-bold text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-hover)] hover:text-[var(--wp-text-strong)] disabled:opacity-50"
          >
            <X size={16} />
            Rədd et
          </button>
          <button
            type="button"
            disabled={isBusy}
            onClick={() =>
              void run(async () => {
                await acceptCommunityOwnershipOffer(communityId);
                await onAccepted();
              })
            }
            className="flex h-11 items-center justify-center gap-2 rounded-2xl bg-amber-500 text-sm font-black text-white transition hover:bg-amber-400 disabled:opacity-50"
          >
            {isBusy ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <Crown size={16} />
            )}
            Qəbul et
          </button>
        </div>
      </section>
    </div>,
    document.body,
  );
}
