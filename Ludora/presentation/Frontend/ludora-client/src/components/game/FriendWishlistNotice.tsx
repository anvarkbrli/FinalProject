import { useQuery } from "@tanstack/react-query";
import { Bookmark } from "lucide-react";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { resolveAssetUrl } from "../../lib/helpers";
import { getWishlistedFriends } from "../../services/userGamesService";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";
import type { CatalogSource } from "../../types/home";

interface FriendWishlistNoticeProps {
  source: CatalogSource;
  gameId: number;
}

export function FriendWishlistNotice({
  source,
  gameId,
}: FriendWishlistNoticeProps) {
  const { language } = useI18n();
  const accessToken = useAuthStore((state) => state.accessToken);
  const expiresAtUtc = useAuthStore((state) => state.expiresAtUtc);
  const isAuthenticated = isAuthSessionValid(accessToken, expiresAtUtc);

  const query = useQuery({
    queryKey: ["wishlisted-friends", source, gameId],
    queryFn: () => getWishlistedFriends(source, gameId),
    enabled: isAuthenticated,
    staleTime: 2 * 60 * 1000,
  });

  const friends = query.data ?? [];
  if (!isAuthenticated || query.isLoading || query.isError || friends.length === 0) {
    return null;
  }

  const firstFriend = friends[0];
  const profileImageUrl = resolveAssetUrl(firstFriend.profileImageUrl);
  const remainingCount = friends.length - 1;

  return (
    <section className="rounded-2xl border border-fuchsia-400/15 bg-[linear-gradient(135deg,rgba(124,58,237,0.1),rgba(217,70,239,0.055))] p-4">
      <div className="flex items-center gap-3">
        <Link
          to={`/users/${firstFriend.userName}`}
          className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.08] bg-violet-500/10 text-sm font-black text-violet-200"
        >
          {profileImageUrl ? (
            <img
              src={profileImageUrl}
              alt={`${firstFriend.userName} profil şəkli`}
              className="h-full w-full object-cover"
            />
          ) : (
            firstFriend.userName.charAt(0).toUpperCase()
          )}
        </Link>

        <div className="min-w-0 flex-1">
          <p className="text-sm leading-6 text-zinc-300">
            {language === "en" ? (
              <>
                Your friend{" "}
                <Link
                  to={`/users/${firstFriend.userName}`}
                  className="font-bold text-fuchsia-200 transition hover:text-fuchsia-100"
                >
                  {firstFriend.userName}
                </Link>{" "}
                wants this game.
              </>
            ) : (
              <>
                Dostun{" "}
                <Link
                  to={`/users/${firstFriend.userName}`}
                  className="font-bold text-fuchsia-200 transition hover:text-fuchsia-100"
                >
                  {firstFriend.userName}
                </Link>{" "}
                bu oyunu istəyir.
              </>
            )}
          </p>
          {remainingCount > 0 && (
            <p className="mt-1 text-xs font-semibold text-zinc-600">
              {language === "en"
                ? `${remainingCount} more ${remainingCount === 1 ? "friend has" : "friends have"} it on their wishlist.`
                : `Daha ${remainingCount} dostunun wishlist-indədir.`}
            </p>
          )}
        </div>

        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-fuchsia-500/10 text-fuchsia-200">
          <Bookmark size={16} className="fill-current" />
        </span>
      </div>
    </section>
  );
}
