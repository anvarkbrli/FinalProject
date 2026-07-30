import { ArrowRight, CalendarRange, X } from "lucide-react";
import { useI18n } from "../../i18n/useI18n";

interface AdminDateRangeFilterProps {
  from: string;
  to: string;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
  onClear: () => void;
  className?: string;
}

export function AdminDateRangeFilter({
  from,
  to,
  onFromChange,
  onToChange,
  onClear,
  className = "",
}: AdminDateRangeFilterProps) {
  const { language } = useI18n();
  const isEnglish = language === "en";
  const hasValue = Boolean(from || to);

  return (
    <div className={className}>
      <div className="mb-2 flex h-5 items-center justify-between gap-3">
        <span className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.12em] text-zinc-500">
          <CalendarRange size={14} className="text-violet-300" />
          {isEnglish ? "Date range" : "Tarix aralığı"}
        </span>
        {hasValue && (
          <button
            type="button"
            onClick={onClear}
            className="inline-flex items-center gap-1 text-[11px] font-medium text-zinc-500 transition hover:text-zinc-200"
          >
            <X size={12} />
            {isEnglish ? "Clear" : "Təmizlə"}
          </button>
        )}
      </div>

      <div className="grid min-h-14 grid-cols-[minmax(0,1fr)_2.25rem_minmax(0,1fr)] items-stretch overflow-hidden rounded-xl border border-white/[0.08] bg-[#0a0a0e] transition focus-within:border-violet-500/50 focus-within:ring-2 focus-within:ring-violet-500/10">
        <label className="relative min-w-0 px-3 pb-1.5 pt-5">
          <span className="pointer-events-none absolute left-3 top-1.5 text-[9px] font-bold uppercase tracking-[0.12em] text-zinc-600">
            {isEnglish ? "Start" : "Başlanğıc"}
          </span>
          <input
            type="date"
            value={from}
            max={to || undefined}
            aria-label={
              isEnglish ? "Start date" : "Başlanğıc tarixi"
            }
            onChange={(event) => onFromChange(event.target.value)}
            className="relative z-10 h-7 w-full min-w-0 bg-transparent text-xs font-medium outline-none"
            style={{ color: "transparent" }}
          />
          <span className="pointer-events-none absolute bottom-2 left-3 text-xs font-medium text-zinc-300">
            {formatDateValue(from, language)}
          </span>
        </label>

        <span className="flex items-center justify-center border-x border-white/[0.06] bg-white/[0.02] text-zinc-600">
          <ArrowRight size={14} />
        </span>

        <label className="relative min-w-0 px-3 pb-1.5 pt-5">
          <span className="pointer-events-none absolute left-3 top-1.5 text-[9px] font-bold uppercase tracking-[0.12em] text-zinc-600">
            {isEnglish ? "End date" : "Son tarix"}
          </span>
          <input
            type="date"
            value={to}
            min={from || undefined}
            aria-label={isEnglish ? "End date" : "Son tarix"}
            onChange={(event) => onToChange(event.target.value)}
            className="relative z-10 h-7 w-full min-w-0 bg-transparent text-xs font-medium outline-none"
            style={{ color: "transparent" }}
          />
          <span className="pointer-events-none absolute bottom-2 left-3 text-xs font-medium text-zinc-300">
            {formatDateValue(to, language)}
          </span>
        </label>
      </div>
    </div>
  );
}

function formatDateValue(
  value: string,
  language: "az" | "en",
): string {
  if (!value) return language === "en" ? "Select date" : "Tarix seç";

  const [year, month, day] = value.split("-");
  return `${day}.${month}.${year}`;
}
