import {
  Cpu,
  MemoryStick,
  MonitorCog,
} from "lucide-react";

interface ReviewHardwareSummaryProps {
  gpu: string | null;
  cpu: string | null;
  ramGb: number | null;
  vramGb: number | null;
}

export function ReviewHardwareSummary({
  gpu,
  cpu,
  ramGb,
  vramGb,
}: ReviewHardwareSummaryProps) {
  if (!gpu || !cpu || ramGb === null) {
    return null;
  }

  return (
    <div className="wp-review-subcard rounded-2xl p-4">
      <div className="flex items-center gap-2 text-[11px] font-black uppercase tracking-[0.16em] text-[var(--wp-accent-strong)]">
        <MonitorCog size={15} />
        Oynanılan sistem
      </div>

      <dl className="mt-3 grid gap-2 text-xs sm:grid-cols-2">
        <div className="min-w-0 rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] px-3 py-2.5">
          <dt className="flex items-center gap-1.5 text-[color:var(--wp-text-muted)]">
            <MonitorCog size={13} />
            GPU
          </dt>
          <dd className="mt-1 break-words font-semibold text-[var(--wp-text-strong)]">
            {gpu}
          </dd>
        </div>

        <div className="min-w-0 rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] px-3 py-2.5">
          <dt className="flex items-center gap-1.5 text-[color:var(--wp-text-muted)]">
            <Cpu size={13} />
            CPU
          </dt>
          <dd className="mt-1 break-words font-semibold text-[var(--wp-text-strong)]">
            {cpu}
          </dd>
        </div>

        <div className="rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] px-3 py-2.5">
          <dt className="flex items-center gap-1.5 text-[color:var(--wp-text-muted)]">
            <MemoryStick size={13} />
            RAM
          </dt>
          <dd className="mt-1 font-semibold text-[var(--wp-text-strong)]">
            {ramGb} GB
          </dd>
        </div>

        {vramGb !== null && (
          <div className="rounded-xl border border-[color:var(--wp-border)] bg-[color:var(--wp-surface-strong)] px-3 py-2.5">
            <dt className="flex items-center gap-1.5 text-[color:var(--wp-text-muted)]">
              <MemoryStick size={13} />
              VRAM
            </dt>
            <dd className="mt-1 font-semibold text-[var(--wp-text-strong)]">
              {vramGb} GB
            </dd>
          </div>
        )}
      </dl>
    </div>
  );
}
