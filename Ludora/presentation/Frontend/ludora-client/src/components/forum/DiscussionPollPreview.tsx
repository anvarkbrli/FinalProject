import { BarChart3, Clock3, LockKeyhole } from "lucide-react";
import { useI18n } from "../../i18n/useI18n";
import { formatDateTime } from "../../lib/helpers";
import type { DiscussionPollPreviewDTO } from "../../types/home";

interface DiscussionPollPreviewProps {
  poll: DiscussionPollPreviewDTO;
  compact?: boolean;
}

export function DiscussionPollPreview({
  poll,
  compact = false,
}: DiscussionPollPreviewProps) {
  const { language } = useI18n();
  const endsAt = new Date(poll.endsAtUtc);
  const hasEnded =
    poll.isClosed ||
    (!Number.isNaN(endsAt.getTime()) && endsAt.getTime() <= Date.now());

  return (
    <section
      className={`rounded-2xl border border-teal-400/20 bg-[color:color-mix(in_srgb,var(--wp-surface-strong)_88%,transparent)] ${
        compact ? "mt-3 p-3" : "mt-4 p-4"
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="flex items-center gap-2 text-[10px] font-black uppercase tracking-[0.16em] text-teal-400">
            <BarChart3 size={13} />
            {language === "en" ? "Poll" : "Sorğu"}
          </p>
          <h3
            data-i18n-ignore
            className="mt-1.5 line-clamp-2 text-sm font-bold leading-5 text-[var(--wp-text-strong)]"
          >
            {poll.question}
          </h3>
        </div>

        <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-soft)] px-2.5 py-1 text-[10px] font-semibold text-[color:var(--wp-text-muted)]">
          {hasEnded ? <LockKeyhole size={12} /> : <Clock3 size={12} />}
          {hasEnded
            ? language === "en"
              ? "Ended"
              : "Bitib"
            : formatDateTime(poll.endsAtUtc)}
        </span>
      </div>

      <div className="mt-3 grid gap-2 sm:grid-cols-2">
        {poll.options.map((option) => (
          <div
            key={option.id}
            data-i18n-ignore
            className="min-w-0 rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-soft)] px-3 py-2.5 text-sm font-semibold text-[var(--wp-text)]"
          >
            <span className="line-clamp-2">{option.text}</span>
          </div>
        ))}
      </div>

      <p className="mt-3 text-[11px] font-semibold text-[color:var(--wp-text-muted)]">
        {language === "en"
          ? "Open the discussion to vote and view results."
          : "Səs vermək və nəticələrə baxmaq üçün müzakirəni aç."}
      </p>
    </section>
  );
}
