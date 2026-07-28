import {
  BellRing,
  ChevronLeft,
  FileText,
  Gamepad2,
  LayoutDashboard,
  Menu,
  MessageSquareText,
  ScrollText,
  ShieldCheck,
  UsersRound,
  X,
} from "lucide-react";
import { useState } from "react";
import {
  Link,
  NavLink,
  Outlet,
  useLocation,
} from "react-router-dom";
import { useNotificationRealtime } from "../../hooks/useNotificationRealtime";
import {
  hasAuthRole,
} from "../../lib/authRoles";
import { useAuthStore } from "../../stores/authStore";

const activeItems = [
  {
    label: "İdarə paneli",
    to: "/admin",
    icon: LayoutDashboard,
    end: true,
  },
  {
    label: "İstifadəçilər",
    to: "/admin/users",
    icon: UsersRound,
    end: false,
  },
  {
    label: "Şikayətlər",
    to: "/admin/reports",
    icon: ShieldCheck,
    end: false,
  },
  {
    label: "Postlar",
    to: "/admin/posts",
    icon: FileText,
    end: false,
  },
  {
    label: "Elanlar",
    to: "/admin/announcements",
    icon: BellRing,
    end: false,
  },
];

const upcomingItems = [
  { label: "Forum", icon: MessageSquareText },
  { label: "Audit jurnalı", icon: ScrollText },
];

export function AdminLayout() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const location = useLocation();
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const isAdmin = hasAuthRole(
    accessToken,
    "Admin",
    user?.roles ?? [],
  );
  useNotificationRealtime(Boolean(accessToken));

  const pageTitle = location.pathname === "/admin"
    ? "İdarə paneli"
    : location.pathname.startsWith("/admin/announcements")
      ? "Elanlar"
    : location.pathname.startsWith("/admin/posts")
      ? "Postlar"
    : location.pathname.startsWith("/admin/reports/users/")
      ? "Şikayət detalları"
      : location.pathname.startsWith("/admin/reports")
        ? "Şikayətlər"
    : location.pathname.startsWith("/admin/users/")
      ? "İstifadəçi detalı"
      : "İstifadəçilər";

  const sidebar = (
    <div className="flex h-full flex-col">
      <div className="flex h-20 items-center border-b border-white/[0.06] px-6">
        <Link
          to="/admin"
          className="flex items-center gap-3"
          onClick={() => setMobileMenuOpen(false)}
        >
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-600 text-white shadow-lg shadow-violet-950/40">
            <Gamepad2 size={20} />
          </span>
          <span>
            <span className="block text-base font-bold text-white">
              Ludora
            </span>
            <span className="block text-[10px] font-semibold uppercase tracking-[0.2em] text-violet-300">
              Admin
            </span>
          </span>
        </Link>
      </div>

      <nav className="flex-1 overflow-y-auto px-4 py-5">
        <p className="px-3 text-[10px] font-bold uppercase tracking-[0.2em] text-zinc-600">
          İdarəetmə
        </p>
        <div className="mt-3 space-y-1">
          {activeItems.map((item) => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                onClick={() => setMobileMenuOpen(false)}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-xl px-3 py-3 text-sm font-medium transition ${
                    isActive
                      ? "bg-violet-500/15 text-violet-200"
                      : "text-zinc-400 hover:bg-white/[0.05] hover:text-white"
                  }`
                }
              >
                <Icon size={18} />
                {item.label}
              </NavLink>
            );
          })}
        </div>

        <p className="mt-8 px-3 text-[10px] font-bold uppercase tracking-[0.2em] text-zinc-600">
          Növbəti mərhələ
        </p>
        <div className="mt-3 space-y-1">
          {upcomingItems.map((item) => {
            const Icon = item.icon;
            return (
              <div
                key={item.label}
                className="flex items-center justify-between rounded-xl px-3 py-3 text-sm text-zinc-600"
              >
                <span className="flex items-center gap-3">
                  <Icon size={18} />
                  {item.label}
                </span>
                <span className="rounded-full border border-white/[0.06] px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide">
                  Tezliklə
                </span>
              </div>
            );
          })}
        </div>
      </nav>

      <div className="border-t border-white/[0.06] p-4">
        <Link
          to="/"
          className="flex items-center gap-3 rounded-xl px-3 py-3 text-sm text-zinc-400 transition hover:bg-white/[0.05] hover:text-white"
        >
          <ChevronLeft size={18} />
          Sayta qayıt
        </Link>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-[#07070a] text-zinc-100">
      <aside className="fixed inset-y-0 left-0 z-40 hidden w-72 border-r border-white/[0.06] bg-[#0b0b0f] lg:block">
        {sidebar}
      </aside>

      {mobileMenuOpen && (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-black/70 backdrop-blur-sm"
            aria-label="Menyunu bağla"
            onClick={() => setMobileMenuOpen(false)}
          />
          <aside className="relative h-full w-[min(86vw,19rem)] border-r border-white/[0.08] bg-[#0b0b0f]">
            <button
              type="button"
              className="absolute right-4 top-6 z-10 flex h-9 w-9 items-center justify-center rounded-lg bg-white/[0.06] text-zinc-300"
              onClick={() => setMobileMenuOpen(false)}
              aria-label="Menyunu bağla"
            >
              <X size={18} />
            </button>
            {sidebar}
          </aside>
        </div>
      )}

      <div className="lg:pl-72">
        <header className="sticky top-0 z-30 flex h-20 items-center justify-between border-b border-white/[0.06] bg-[#08080c]/90 px-5 backdrop-blur-xl sm:px-8">
          <div className="flex items-center gap-3">
            <button
              type="button"
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-white/[0.08] bg-white/[0.04] text-zinc-300 lg:hidden"
              onClick={() => setMobileMenuOpen(true)}
              aria-label="Admin menyusunu aç"
            >
              <Menu size={19} />
            </button>
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-zinc-600">
                Admin panel
              </p>
              <h1 className="mt-1 text-lg font-bold text-white">
                {pageTitle}
              </h1>
            </div>
          </div>

          <div className="flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.035] px-3 py-2">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-violet-500/15 text-xs font-bold text-violet-200">
              {user?.userName.charAt(0).toUpperCase() ?? "A"}
            </span>
            <div className="hidden sm:block">
              <p className="text-sm font-semibold text-zinc-200">
                {user?.userName}
              </p>
              <p className="text-[10px] uppercase tracking-wider text-violet-400">
                {isAdmin ? "Administrator" : "Moderator"}
              </p>
            </div>
          </div>
        </header>

        <main className="p-5 sm:p-8">
          <div className="mx-auto max-w-[1500px]">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
