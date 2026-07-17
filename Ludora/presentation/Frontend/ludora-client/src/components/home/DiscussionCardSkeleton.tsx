export function DiscussionCardSkeleton() {
  return (
    <div className="animate-pulse rounded-2xl border border-white/5 bg-white/[0.025] p-4">
      <div className="h-5 w-24 rounded-full bg-white/5" />

      <div className="mt-4 h-4 w-full rounded bg-white/5" />

      <div className="mt-2 h-4 w-3/4 rounded bg-white/5" />

      <div className="mt-5 flex items-center gap-3">
        <div className="h-8 w-8 rounded-full bg-white/5" />

        <div className="space-y-2">
          <div className="h-3 w-24 rounded bg-white/5" />
          <div className="h-3 w-16 rounded bg-white/5" />
        </div>
      </div>

      <div className="mt-4 flex gap-4 border-t border-white/5 pt-3">
        <div className="h-3 w-8 rounded bg-white/5" />
        <div className="h-3 w-8 rounded bg-white/5" />
        <div className="h-3 w-8 rounded bg-white/5" />
      </div>
    </div>
  );
}