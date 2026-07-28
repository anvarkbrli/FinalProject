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
    <div className="mt-4 rounded-2xl border border-violet-300/10 bg-violet-500/[0.045] p-4">
      <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.14em] text-violet-200/75">
        <MonitorCog size={15} />
        Oynanılan sistem
      </div>

      <dl className="mt-3 grid gap-2 text-xs sm:grid-cols-2">
        <div className="min-w-0 rounded-xl border border-white/[0.06] bg-black/10 px-3 py-2.5">
          <dt className="flex items-center gap-1.5 text-zinc-600">
            <MonitorCog size={13} />
            GPU
          </dt>
          <dd className="mt-1 break-words font-semibold text-zinc-300">
            {gpu}
          </dd>
        </div>

        <div className="min-w-0 rounded-xl border border-white/[0.06] bg-black/10 px-3 py-2.5">
          <dt className="flex items-center gap-1.5 text-zinc-600">
            <Cpu size={13} />
            CPU
          </dt>
          <dd className="mt-1 break-words font-semibold text-zinc-300">
            {cpu}
          </dd>
        </div>

        <div className="rounded-xl border border-white/[0.06] bg-black/10 px-3 py-2.5">
          <dt className="flex items-center gap-1.5 text-zinc-600">
            <MemoryStick size={13} />
            RAM
          </dt>
          <dd className="mt-1 font-semibold text-zinc-300">
            {ramGb} GB
          </dd>
        </div>

        {vramGb !== null && (
          <div className="rounded-xl border border-white/[0.06] bg-black/10 px-3 py-2.5">
            <dt className="flex items-center gap-1.5 text-zinc-600">
              <MemoryStick size={13} />
              VRAM
            </dt>
            <dd className="mt-1 font-semibold text-zinc-300">
              {vramGb} GB
            </dd>
          </div>
        )}
      </dl>
    </div>
  );
}
