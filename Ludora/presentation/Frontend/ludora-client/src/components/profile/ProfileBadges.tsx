import { Award, LockKeyhole, Sparkles } from "lucide-react";

export interface ProfileBadgeItem {
  id: string | number;
  name: string;
  description: string;
  iconUrl?: string | null;
  color?: string;
}

interface ProfileBadgesProps {
  badges?: ProfileBadgeItem[];
}

const upcomingBadges = [
  {
    id: "first-review",
    name: "İlk Rəy",
    description: "İlk oyun rəyini paylaş.",
  },
  {
    id: "game-collector",
    name: "Kolleksiyaçı",
    description: "25 oyunu statusla qeyd et.",
  },
  {
    id: "community-voice",
    name: "İcma Səsi",
    description: "İcmada davamlı iştirak et.",
  },
] satisfies ProfileBadgeItem[];

export function ProfileBadges({ badges = [] }: ProfileBadgesProps) {
  return (
    <section className="rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.18em] text-amber-300">
            <Award size={16} />
            Nişanlar
          </div>
          <p className="mt-2 text-sm leading-6 text-zinc-500">
            Qazanılan nailiyyətlər burada görünəcək.
          </p>
        </div>
        <span className="rounded-full border border-white/[0.08] bg-white/[0.03] px-3 py-1 text-xs font-semibold text-zinc-500">
          {badges.length} qazanılıb
        </span>
      </div>

      {badges.length > 0 ? (
        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {badges.map((badge) => (
            <article
              key={badge.id}
              className="rounded-2xl border border-amber-300/15 bg-amber-500/[0.05] p-4"
              style={{
                borderColor: badge.color ? `${badge.color}40` : undefined,
                backgroundColor: badge.color ? `${badge.color}0d` : undefined,
              }}
            >
              <Sparkles
                size={20}
                className="text-amber-300"
                style={{ color: badge.color }}
              />
              <h3 className="mt-3 font-semibold text-zinc-100">
                {badge.name}
              </h3>
              <p className="mt-1 text-xs leading-5 text-zinc-500">
                {badge.description}
              </p>
            </article>
          ))}
        </div>
      ) : (
        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {upcomingBadges.map((badge) => (
            <article
              key={badge.id}
              className="relative overflow-hidden rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4"
            >
              <LockKeyhole size={19} className="text-zinc-700" />
              <h3 className="mt-3 font-semibold text-zinc-400">
                {badge.name}
              </h3>
              <p className="mt-1 text-xs leading-5 text-zinc-600">
                {badge.description}
              </p>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
