import {
  Activity,
  Bookmark,
  ChevronDown,
  Gamepad2,
  Heart,
  ListChecks,
  LogIn,
  LogOut,
  Menu,
  MessageCircle,
  Plus,
  Settings,
  ShieldCheck,
  Star,
  UserPlus,
  UserRound,
  X,
} from "lucide-react";
import {
  useEffect,
  useRef,
  useState,
} from "react";
import {
  Link,
  useLocation,
} from "react-router-dom";
import { LogGameModal } from "../reviews/LogGameModal";
import { GlobalSearchBox } from "../search/GlobalSearchBox";
import { NotificationBell } from "../notifications/NotificationBell";
import { resolveAssetUrl } from "../../lib/helpers";
import { canAccessAdminPanel } from "../../lib/authRoles";
import { useNotificationRealtime } from "../../hooks/useNotificationRealtime";
import {
  isAuthSessionValid,
  useAuthStore,
} from "../../stores/authStore";

const navigationItems = [
  {
    label: "Ana səhifə",
    to: "/",
  },
  {
    label: "Oyunlar",
    to: "/games",
  },
  {
    label: "Forum",
    to: "/forum",
  },
];

export function Navbar() {
  const location = useLocation();

  const userMenuRef =
    useRef<HTMLDivElement>(null);

  const [isMobileMenuOpen, setIsMobileMenuOpen] =
    useState(false);

  const [isUserMenuOpen, setIsUserMenuOpen] =
    useState(false);

  const [isLogModalOpen, setIsLogModalOpen] =
    useState(false);

  const [
    failedProfileImageUrl,
    setFailedProfileImageUrl,
  ] = useState<string | null>(null);

  const user = useAuthStore(
    (state) => state.user,
  );

  const accessToken = useAuthStore(
    (state) => state.accessToken,
  );

  const expiresAtUtc = useAuthStore(
    (state) => state.expiresAtUtc,
  );

  const clearSession = useAuthStore(
    (state) => state.clearSession,
  );

  const hasValidSession =
    isAuthSessionValid(
      accessToken,
      expiresAtUtc,
    ) && user !== null;

  const canAccessAdmin =
    hasValidSession &&
    canAccessAdminPanel(
      accessToken,
      user?.roles ?? [],
    );

  useNotificationRealtime(hasValidSession);

  const profileImageUrl = resolveAssetUrl(
    user?.profileImageUrl,
  );

  const userInitial =
    user?.userName
      .trim()
      .charAt(0)
      .toUpperCase() || "?";

  useEffect(() => {
    function handleOutsideClick(
      event: MouseEvent,
    ) {
      if (
        userMenuRef.current &&
        !userMenuRef.current.contains(
          event.target as Node,
        )
      ) {
        setIsUserMenuOpen(false);
      }
    }

    function handleEscapeKey(
      event: KeyboardEvent,
    ) {
      if (event.key === "Escape") {
        setIsUserMenuOpen(false);
        setIsMobileMenuOpen(false);
      }
    }

    document.addEventListener(
      "mousedown",
      handleOutsideClick,
    );

    document.addEventListener(
      "keydown",
      handleEscapeKey,
    );

    return () => {
      document.removeEventListener(
        "mousedown",
        handleOutsideClick,
      );

      document.removeEventListener(
        "keydown",
        handleEscapeKey,
      );
    };
  }, []);

  function closeMenus() {
    setIsMobileMenuOpen(false);
    setIsUserMenuOpen(false);
  }

  function handleLogout() {
    clearSession();
    closeMenus();
  }

  function isNavigationItemActive(
    to: string,
  ): boolean {
    if (to === "/games") {
      return location.pathname === "/games";
    }

    if (to === "/forum") {
      return location.pathname.startsWith("/forum");
    }

    if (to === "/") {
      return (
        location.pathname === "/" &&
        location.hash !== "#forum"
      );
    }

    return false;
  }

  function renderProfileAvatar(
    sizeClassName: string,
  ) {
    return (
      <span
        className={`flex shrink-0 items-center justify-center overflow-hidden bg-violet-500/15 font-bold text-violet-300 ${sizeClassName}`}
      >
        {profileImageUrl &&
        profileImageUrl !== failedProfileImageUrl ? (
          <img
            src={profileImageUrl}
            alt={`${user?.userName ?? "İstifadəçi"} profil şəkli`}
            className="h-full w-full object-cover"
            onError={() => {
              setFailedProfileImageUrl(profileImageUrl);
            }}
          />
        ) : (
          userInitial
        )}
      </span>
    );
  }

  return (
    <>
      <header className="site-navbar fixed inset-x-0 top-0 z-50 w-full border-b border-white/5 bg-[#08080b]/85 backdrop-blur-xl">
      <div className="mx-auto flex h-[72px] max-w-7xl items-center justify-between px-5 lg:px-8">
        <Link
          to="/"
          onClick={closeMenus}
          className="flex shrink-0 items-center gap-3"
          aria-label="Ludora ana səhifə"
        >
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-600 text-white shadow-lg shadow-violet-950/50">
            <Gamepad2
              size={21}
              strokeWidth={2.2}
            />
          </span>

          <span>
            <span className="block text-lg font-bold tracking-tight text-white">
              Ludora
            </span>

            <span className="block text-[10px] font-semibold uppercase tracking-[0.24em] text-zinc-500">
              Play. Track. Discuss.
            </span>
          </span>
        </Link>

        <nav
          className="hidden items-center gap-1 md:flex"
          aria-label="Əsas naviqasiya"
        >
          {navigationItems.map((item) => {
            const isActive =
              isNavigationItemActive(item.to);

            return (
              <Link
                key={item.to}
                to={item.to}
                onClick={closeMenus}
                className={`rounded-lg px-4 py-2 text-sm font-medium transition ${
                  isActive
                    ? "bg-violet-500/10 text-violet-200"
                    : "text-zinc-400 hover:bg-white/5 hover:text-white"
                }`}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>

        <div className="hidden items-center gap-2 md:flex">
          <GlobalSearchBox />

          {hasValidSession && <NotificationBell />}

          {hasValidSession && (
            <button
              type="button"
              onClick={() => {
                closeMenus();
                setIsLogModalOpen(true);
              }}
              className="order-3 flex items-center gap-2 rounded-xl bg-violet-600 px-4 py-2.5 text-sm font-bold text-white shadow-lg shadow-violet-950/30 transition hover:bg-violet-500"
            >
              <Plus size={17} />
              Rəy əlavə et
            </button>
          )}

          {hasValidSession && user ? (
            <div
              ref={userMenuRef}
              className="order-2 relative"
            >
              <button
                type="button"
                onClick={() => {
                  setIsUserMenuOpen(
                    (currentValue) =>
                      !currentValue,
                  );
                }}
                aria-expanded={isUserMenuOpen}
                aria-haspopup="menu"
                className="flex items-center gap-2.5 rounded-xl border border-white/[0.08] bg-white/[0.04] py-1.5 pl-1.5 pr-3 transition hover:border-white/[0.14] hover:bg-white/[0.08]"
              >
                {renderProfileAvatar(
                  "h-9 w-9 rounded-lg text-sm",
                )}

                <span className="max-w-28 truncate text-sm font-semibold text-zinc-200">
                  {user.userName}
                </span>

                <ChevronDown
                  size={15}
                  className={`text-zinc-500 transition-transform ${
                    isUserMenuOpen
                      ? "rotate-180"
                      : ""
                  }`}
                />
              </button>

              {isUserMenuOpen && (
                <div
                  role="menu"
                  className="absolute right-0 top-[calc(100%+10px)] max-h-[calc(100vh-6rem)] w-64 overflow-y-auto rounded-2xl border border-white/[0.09] bg-[#111116] p-2 shadow-2xl shadow-black/50"
                >
                  <div className="flex items-center gap-3 border-b border-white/[0.06] px-3 py-3">
                    {renderProfileAvatar(
                      "h-11 w-11 rounded-xl text-sm",
                    )}

                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-white">
                        {user.userName}
                      </p>

                      <p className="mt-1 truncate text-xs text-zinc-500">
                        {user.email}
                      </p>
                    </div>
                  </div>

                  <div className="py-2">
                    {canAccessAdmin && (
                      <Link
                        to="/admin"
                        onClick={closeMenus}
                        role="menuitem"
                        className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-violet-200 transition hover:bg-violet-500/[0.08]"
                      >
                        <ShieldCheck size={17} />
                        Admin panel
                      </Link>
                    )}

                    <Link
                      to={`/users/${user.userName}`}
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <UserRound size={17} />
                      Profilim
                    </Link>

                    <Link
                      to={`/users/${user.userName}?tab=lists`}
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <ListChecks size={17} />
                      Siyahılar
                    </Link>

                    <Link
                      to={`/users/${user.userName}?tab=reviews`}
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <Star size={17} />
                      Rəylər
                    </Link>

                    <Link
                      to={`/users/${user.userName}?tab=wishlist`}
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <Bookmark size={17} />
                      Wishlist
                    </Link>

                    <Link
                      to={`/users/${user.userName}?tab=favorites`}
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <Heart size={17} />
                      Sevimlilər
                    </Link>

                    <Link
                      to={`/users/${user.userName}?tab=feed`}
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <Activity size={17} />
                      Axın
                    </Link>

                    <Link
                      to="/messages"
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <MessageCircle size={17} />
                      Mesajlar
                    </Link>
                  </div>

                  <div className="border-t border-white/[0.06] py-2">
                    <Link
                      to="/settings?section=profile"
                      onClick={closeMenus}
                      role="menuitem"
                      className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-zinc-300 transition hover:bg-white/[0.06] hover:text-white"
                    >
                      <Settings size={17} />
                      Parametrlər
                    </Link>
                  </div>

                  <div className="border-t border-white/[0.06] pt-2">
                    <button
                      type="button"
                      onClick={handleLogout}
                      role="menuitem"
                      className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-red-300 transition hover:bg-red-500/[0.08]"
                    >
                      <LogOut size={17} />
                      Hesabdan çıx
                    </button>
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <Link
                to="/login"
                onClick={closeMenus}
                className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/5 px-4 py-2.5 text-sm font-semibold text-zinc-300 transition hover:bg-white/10 hover:text-white"
              >
                <LogIn size={17} />
                Daxil ol
              </Link>

              <Link
                to="/register"
                onClick={closeMenus}
                className="flex items-center gap-2 rounded-xl bg-violet-600 px-4 py-2.5 text-sm font-semibold text-white shadow-lg shadow-violet-950/30 transition hover:bg-violet-500"
              >
                <UserPlus size={17} />
                Qeydiyyat
              </Link>
            </div>
          )}
        </div>

        <div className="flex items-center gap-2 md:hidden">
          {hasValidSession && <NotificationBell />}

          <button
            type="button"
            onClick={() => {
              setIsMobileMenuOpen(
                (currentValue) =>
                  !currentValue,
              );

              setIsUserMenuOpen(false);
            }}
            className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/10 bg-white/5 text-zinc-300 transition hover:bg-white/10"
            aria-label={
              isMobileMenuOpen
                ? "Mobil menyunu bağla"
                : "Mobil menyunu aç"
            }
            aria-expanded={isMobileMenuOpen}
          >
            {isMobileMenuOpen ? (
              <X size={20} />
            ) : (
              <Menu size={20} />
            )}
          </button>
        </div>
      </div>

      {isMobileMenuOpen && (
        <nav
          className="border-t border-white/5 bg-[#0d0d11] px-5 py-4 md:hidden"
          aria-label="Mobil naviqasiya"
        >
          <div className="mx-auto flex max-w-7xl flex-col gap-1">
            {hasValidSession && user && (
              <div className="mb-3 flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.035] p-3">
                {renderProfileAvatar(
                  "h-11 w-11 rounded-xl text-sm",
                )}

                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold text-white">
                    {user.userName}
                  </p>

                  <p className="truncate text-xs text-zinc-500">
                    {user.email}
                  </p>
                </div>
              </div>
            )}

            {navigationItems.map((item) => {
              const isActive =
                isNavigationItemActive(item.to);

              return (
                <Link
                  key={item.to}
                  to={item.to}
                  onClick={closeMenus}
                  className={`rounded-xl px-4 py-3 text-sm font-medium transition ${
                    isActive
                      ? "bg-violet-500/10 text-violet-200"
                      : "text-zinc-300 hover:bg-white/5 hover:text-white"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}

            <div className="mt-2"><GlobalSearchBox mobile /></div>

            {hasValidSession ? (
              <>
                <button
                  type="button"
                  onClick={() => {
                    closeMenus();
                    setIsLogModalOpen(true);
                  }}
                  className="mt-2 flex items-center justify-center gap-3 rounded-xl bg-violet-600 px-4 py-3 text-sm font-bold text-white transition hover:bg-violet-500"
                >
                  <Plus size={17} />
                  Rəy əlavə et
                </button>

                {user && (
                  <>
                    <Link
                      to={`/users/${user.userName}`}
                      onClick={closeMenus}
                      className="mt-2 flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.035] px-4 py-3 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.07]"
                    >
                      <UserRound size={17} />
                      Profilim
                    </Link>
                    <Link
                      to={`/users/${user.userName}?tab=lists`}
                      onClick={closeMenus}
                      className="mt-2 flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.06]"
                    >
                      <ListChecks size={17} />
                      Siyahılar
                    </Link>
                    <Link
                      to={`/users/${user.userName}?tab=reviews`}
                      onClick={closeMenus}
                      className="flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.06]"
                    >
                      <Star size={17} />
                      Rəylər
                    </Link>
                    <Link
                      to={`/users/${user.userName}?tab=wishlist`}
                      onClick={closeMenus}
                      className="flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.06]"
                    >
                      <Bookmark size={17} />
                      Wishlist
                    </Link>
                    <Link
                      to={`/users/${user.userName}?tab=favorites`}
                      onClick={closeMenus}
                      className="flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.06]"
                    >
                      <Heart size={17} />
                      Sevimlilər
                    </Link>
                    <Link
                      to={`/users/${user.userName}?tab=feed`}
                      onClick={closeMenus}
                      className="flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.06]"
                    >
                      <Activity size={17} />
                      Axın
                    </Link>
                  </>
                )}

                <Link
                  to="/messages"
                  onClick={closeMenus}
                  className="mt-2 flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.035] px-4 py-3 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.07]"
                >
                  <MessageCircle size={17} />
                  Mesajlar
                </Link>

                {canAccessAdmin && (
                  <Link
                    to="/admin"
                    onClick={closeMenus}
                    className="mt-2 flex items-center gap-3 rounded-xl border border-violet-400/15 bg-violet-500/[0.07] px-4 py-3 text-sm font-semibold text-violet-200 transition hover:bg-violet-500/[0.12]"
                  >
                    <ShieldCheck size={17} />
                    Admin panel
                  </Link>
                )}

                <Link
                  to="/settings?section=profile"
                  onClick={closeMenus}
                  className="mt-2 flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.035] px-4 py-3 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.07]"
                >
                  <Settings size={17} />
                  Parametrlər
                </Link>

                <button
                  type="button"
                  onClick={handleLogout}
                  className="mt-2 flex items-center gap-3 rounded-xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm font-semibold text-red-300 transition hover:bg-red-500/15"
                >
                  <LogOut size={17} />
                  Hesabdan çıx
                </button>
              </>
            ) : (
              <div className="mt-2 flex flex-col gap-2">
                <Link
                  to="/login"
                  onClick={closeMenus}
                  className="flex items-center justify-center gap-3 rounded-xl border border-white/[0.08] bg-white/[0.04] px-4 py-3 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.08]"
                >
                  <LogIn size={17} />
                  Daxil ol
                </Link>

                <Link
                  to="/register"
                  onClick={closeMenus}
                  className="flex items-center justify-center gap-3 rounded-xl bg-violet-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-violet-500"
                >
                  <UserPlus size={17} />
                  Qeydiyyat
                </Link>
              </div>
            )}
          </div>
        </nav>
      )}
      </header>

      <div aria-hidden="true" className="h-[72px] shrink-0" />

      <LogGameModal
        open={isLogModalOpen}
        onClose={() => {
          setIsLogModalOpen(false);
        }}
      />
    </>
  );
}
