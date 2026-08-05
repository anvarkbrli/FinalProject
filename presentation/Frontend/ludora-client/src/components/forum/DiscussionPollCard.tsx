import { BarChart3, LoaderCircle, LockKeyhole } from "lucide-react";
import { useState } from "react";
import { getApiErrorMessage } from "../../lib/api";
import {
  closeDiscussionPoll,
  voteDiscussionPoll,
} from "../../services/forumService";
import type { DiscussionPollDTO } from "../../types/home";

interface DiscussionPollCardProps {
  discussionId: number;
  poll: DiscussionPollDTO;
  isAuthenticated: boolean;
  onChanged: (poll: DiscussionPollDTO) => void;
}

export function DiscussionPollCard({
  discussionId,
  poll,
  isAuthenticated,
  onChanged,
}: DiscussionPollCardProps) {
  const [busyOptionId, setBusyOptionId] = useState<number | null>(null);
  const [isClosing, setIsClosing] = useState(false);
  const [error, setError] = useState("");
  const cannotVote = poll.hasEnded || poll.isClosed;

  async function vote(optionId: number) {
    if (!isAuthenticated || cannotVote || busyOptionId !== null) return;
    setError("");
    setBusyOptionId(optionId);
    try {
      onChanged(await voteDiscussionPoll(discussionId, optionId));
    } catch (voteError) {
      setError(getApiErrorMessage(voteError, "Səs qeydə alına bilmədi."));
    } finally {
      setBusyOptionId(null);
    }
  }

  async function close() {
    setError("");
    setIsClosing(true);
    try {
      onChanged(await closeDiscussionPoll(discussionId));
    } catch (closeError) {
      setError(getApiErrorMessage(closeError, "Sorğu bağlana bilmədi."));
    } finally {
      setIsClosing(false);
    }
  }

  return (
    <section className="mt-5 rounded-2xl border border-teal-300/15 bg-teal-300/[0.04] p-4 sm:p-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="flex items-center gap-2 text-[10px] font-black uppercase tracking-[0.18em] text-teal-300">
            <BarChart3 size={14} />
            Sorğu
          </p>
          <h2 data-i18n-ignore className="mt-2 text-base font-bold text-white">
            {poll.question}
          </h2>
        </div>
        {poll.canEdit && !cannotVote && (
          <button
            type="button"
            onClick={close}
            disabled={isClosing}
            className="flex shrink-0 items-center gap-1.5 rounded-xl border border-white/10 px-3 py-2 text-[11px] font-bold text-zinc-400 hover:text-white disabled:opacity-50"
          >
            {isClosing ? (
              <LoaderCircle size={13} className="animate-spin" />
            ) : (
              <LockKeyhole size={13} />
            )}
            Sorğunu bağla
          </button>
        )}
      </div>

      <div className="mt-4 space-y-2.5">
        {poll.options.map((option) => {
          const isSelected = poll.selectedOptionId === option.id;
          const percentage = option.percentage ?? 0;
          return (
            <button
              key={option.id}
              type="button"
              disabled={
                cannotVote ||
                !isAuthenticated ||
                busyOptionId !== null ||
                (poll.hasVoted && !poll.allowVoteChange)
              }
              onClick={() => vote(option.id)}
              className={`relative w-full overflow-hidden rounded-xl border px-4 py-3 text-left transition ${
                isSelected
                  ? "border-teal-300/45 bg-teal-300/10"
                  : "border-white/[0.08] bg-black/15 hover:border-teal-300/25"
              } disabled:cursor-default`}
            >
              {poll.canViewResults && (
                <span
                  className="absolute inset-y-0 left-0 bg-teal-300/[0.09] transition-[width]"
                  style={{ width: `${percentage}%` }}
                />
              )}
              <span className="relative flex items-center justify-between gap-3">
                <span data-i18n-ignore className="text-sm font-semibold text-zinc-200">
                  {option.text}
                </span>
                <span className="flex items-center gap-2 text-xs font-bold text-teal-200">
                  {busyOptionId === option.id && (
                    <LoaderCircle size={13} className="animate-spin" />
                  )}
                  {poll.canViewResults &&
                    `${percentage}% · ${option.voteCount ?? 0}`}
                </span>
              </span>
            </button>
          );
        })}
      </div>

      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-[11px] text-zinc-500">
        <span>
          {cannotVote
            ? "Sorğu bitib"
            : isAuthenticated
              ? poll.hasVoted
                ? "Səsiniz qeydə alınıb"
                : "Bir seçimə səs verin"
              : "Səs vermək üçün daxil olun"}
        </span>
        <span>
          {poll.canViewResults
            ? `${poll.totalVotes ?? 0} səs`
            : "Nəticələr hələ gizlidir"}
        </span>
      </div>

      {error && (
        <p className="mt-3 rounded-xl border border-rose-400/15 bg-rose-400/[0.06] px-3 py-2 text-xs text-rose-200">
          {error}
        </p>
      )}
    </section>
  );
}
