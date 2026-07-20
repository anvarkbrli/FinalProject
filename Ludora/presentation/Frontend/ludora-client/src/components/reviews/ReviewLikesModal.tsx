import { useQuery } from "@tanstack/react-query";
import { Heart, LoaderCircle, X } from "lucide-react";
import { Link } from "react-router-dom";
import { resolveAssetUrl } from "../../lib/helpers";
import { getReviewLikes } from "../../services/reviewsService";

export function ReviewLikesModal({
  reviewId,
  likeCount,
  onClose,
}: {
  reviewId: number | null;
  likeCount: number;
  onClose: () => void;
}) {
  const likesQuery = useQuery({
    queryKey: ["review-likes", reviewId, likeCount],
    queryFn: () => getReviewLikes(reviewId!, 1, 100),
    enabled: reviewId !== null,
  });

  if (reviewId === null) return null;

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/75 p-4 backdrop-blur-sm"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <section className="w-full max-w-md overflow-hidden rounded-3xl border border-white/[0.09] bg-[#111116] shadow-2xl shadow-black/60">
        <header className="flex items-center justify-between border-b border-white/[0.07] px-5 py-4">
          <div className="flex items-center gap-2.5">
            <Heart size={18} fill="currentColor" className="text-rose-300" />
            <h2 className="font-black text-white">{likesQuery.data?.totalCount ?? likeCount} bəyəni</h2>
          </div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white" aria-label="Pəncərəni bağla">
            <X size={18} />
          </button>
        </header>

        <div className="max-h-[60vh] overflow-y-auto p-2">
          {likesQuery.isLoading ? (
            <div className="flex h-40 items-center justify-center"><LoaderCircle className="animate-spin text-violet-300" /></div>
          ) : likesQuery.isError ? (
            <p className="p-8 text-center text-sm text-red-300">Bəyənənlər yüklənmədi.</p>
          ) : likesQuery.data?.items.length ? (
            likesQuery.data.items.map((user) => {
              const imageUrl = resolveAssetUrl(user.profileImageUrl);
              return (
                <Link key={user.userId} to={`/users/${user.userName}`} onClick={onClose} className="flex items-center gap-3 rounded-2xl px-3 py-3 transition hover:bg-white/[0.06]">
                  <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/10 font-black text-violet-200">
                    {imageUrl ? <img src={imageUrl} alt="" className="h-full w-full object-cover" /> : user.userName.charAt(0).toUpperCase()}
                  </span>
                  <span className="truncate text-sm font-bold text-zinc-100">{user.userName}</span>
                </Link>
              );
            })
          ) : (
            <p className="p-8 text-center text-sm text-zinc-500">Hələ heç kim bəyənməyib.</p>
          )}
        </div>
      </section>
    </div>
  );
}
