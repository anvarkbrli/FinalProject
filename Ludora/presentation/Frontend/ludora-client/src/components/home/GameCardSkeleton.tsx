export function GameCardSkeleton() {
  return (
    <div className="animate-pulse overflow-hidden rounded-2xl border border-white/5 bg-[#111116]">
      <div className="aspect-[3/4] bg-white/5" />

      <div className="space-y-3 p-4">
        <div className="h-4 w-4/5 rounded bg-white/5" />

        <div className="h-4 w-2/3 rounded bg-white/5" />

        <div className="h-3 w-1/2 rounded bg-white/5" />

        <div className="flex gap-2 pt-1">
          <div className="h-5 w-14 rounded bg-white/5" />
          <div className="h-5 w-16 rounded bg-white/5" />
        </div>
      </div>
    </div>
  );
}