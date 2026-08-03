import {
  Cpu,
  MemoryStick,
  MonitorCog,
} from "lucide-react";
import type { ReactNode } from "react";

interface ReviewHardwareSummaryProps {
  gpu: string | null;
  cpu: string | null;
  ramGb: number | null;
  vramGb: number | null;
  language: "az" | "en";
}

export function ReviewHardwareSummary({
  gpu,
  cpu,
  ramGb,
  vramGb,
  language,
}: ReviewHardwareSummaryProps) {
  if (!gpu || !cpu || ramGb === null) {
    return null;
  }

  return (
    <div className="wp-review-hardware rounded-2xl px-4 py-4 sm:px-5">
      <div className="flex items-center gap-2 text-xs font-bold text-[var(--wp-text-strong)]">
        <MonitorCog
          size={16}
          className="text-[var(--wp-accent-strong)]"
        />
        {language === "en" ? "Played on" : "Oynanılan sistem"}
      </div>

      <dl className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <HardwareItem icon={<MonitorCog size={14} />} label="GPU" value={gpu} />
        <HardwareItem icon={<Cpu size={14} />} label="CPU" value={cpu} />
        <HardwareItem
          icon={<MemoryStick size={14} />}
          label="RAM"
          value={`${ramGb} GB`}
        />
        {vramGb !== null && (
          <HardwareItem
            icon={<MemoryStick size={14} />}
            label="VRAM"
            value={`${vramGb} GB`}
          />
        )}
      </dl>
    </div>
  );
}

function HardwareItem({
  icon,
  label,
  value,
}: {
  icon: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="min-w-0">
      <dt className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.1em] text-[color:var(--wp-text-muted)]">
        {icon}
        {label}
      </dt>
      <dd className="mt-1.5 break-words text-sm font-semibold text-[var(--wp-text-strong)]">
        {value}
      </dd>
    </div>
  );
}
