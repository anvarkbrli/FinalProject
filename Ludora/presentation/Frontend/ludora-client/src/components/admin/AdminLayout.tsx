import {
  Award,
  BellRing,
  ChevronLeft,
  FileText,
  Flag,
  Gamepad2,
  LayoutDashboard,
  Menu,
  Mail,
  MessageSquareText,
  ScrollText,
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
import { useI18n } from "../../i18n/useI18n";
import {
  hasAuthRole,
} from "../../lib/authRoles";
import { resolveAssetUrl } from "../../lib/helpers";
import { useAuthStore } from "../../stores/authStore";
import { BrandLogo } from "../common/BrandLogo";
import "./admin.css";

const activeItems = [
  {
    labelAz: "İdarə paneli",
    labelEn: "Dashboard",
    to: "/admin",
    icon: LayoutDashboard,
    end: true,
  },
  {
    labelAz: "İstifadəçilər",
    labelEn: "Users",
    to: "/admin/users",
    icon: UsersRound,
    end: false,
  },
  {
    labelAz: "Şikayətlər",
    labelEn: "Reports",
    to: "/admin/reports",
    icon: Flag,
    end: false,
  },
  {
    labelAz: "Postlar",
    labelEn: "Posts",
    to: "/admin/posts",
    icon: FileText,
    end: false,
  },
  {
    labelAz: "Elanlar",
    labelEn: "Announcements",
    to: "/admin/announcements",
    icon: BellRing,
    end: false,
  },
  {
    labelAz: "Əlaqə mesajları",
    labelEn: "Contact messages",
    to: "/admin/contact-messages",
    icon: Mail,
    end: false,
  },
  {
    labelAz: "Audit jurnalı",
    labelEn: "Audit log",
    to: "/admin/audit",
    icon: ScrollText,
    end: false,
  },
  {
    labelAz: "Forum",
    labelEn: "Forum",
    to: "/admin/forum",
    icon: MessageSquareText,
    end: false,
  },
  {
    labelAz: "Nişanlar",
    labelEn: "Badges",
    to: "/admin/badges",
    icon: Award,
    end: false,
  },
  {
    labelAz: "Kataloq",
    labelEn: "Catalog",
    to: "/admin/catalog",
    icon: Gamepad2,
    end: false,
  },
];

export function AdminLayout() {
  const { language } = useI18n();
  const isEnglish = language === "en";
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [failedProfileImageUrl, setFailedProfileImageUrl] =
    useState<string | null>(null);
  const location = useLocation();
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const isAdmin = hasAuthRole(
    accessToken,
    "Admin",
    user?.roles ?? [],
  );
  useNotificationRealtime(Boolean(accessToken));

  const profileImageUrl = resolveAssetUrl(user?.profileImageUrl);
  const userInitial =
    user?.userName.trim().charAt(0).toUpperCase() || "A";

  const pageTitle = location.pathname === "/admin"
    ? (isEnglish ? "Dashboard" : "İdarə paneli")
    : location.pathname.startsWith("/admin/catalog")
      ? (isEnglish ? "Catalog management" : "Kataloq idarəetməsi")
    : location.pathname.startsWith("/admin/badges")
      ? (isEnglish ? "Badge management" : "Nişan idarəetməsi")
    : location.pathname.startsWith("/admin/forum")
      ? (isEnglish ? "Forum management" : "Forum idarəetməsi")
    : location.pathname.startsWith("/admin/audit")
      ? (isEnglish ? "Audit log" : "Audit jurnalı")
    : location.pathname.startsWith("/admin/contact-messages")
      ? (isEnglish ? "Contact messages" : "Əlaqə mesajları")
    : location.pathname.startsWith("/admin/announcements")
      ? (isEnglish ? "Announcements" : "Elanlar")
    : location.pathname.startsWith("/admin/posts")
      ? (isEnglish ? "Posts" : "Postlar")
    : location.pathname.startsWith("/admin/reports/users/")
      ? (isEnglish ? "Report details" : "Şikayət detalları")
      : location.pathname.startsWith("/admin/reports")
        ? (isEnglish ? "Reports" : "Şikayətlər")
    : location.pathname.startsWith("/admin/users/")
      ? (isEnglish ? "User details" : "İstifadəçi detalı")
      : (isEnglish ? "Users" : "İstifadəçilər");

  const sidebar = (
    <div className="flex h-full flex-col">
      <div className="flex h-20 items-center border-b border-white/[0.06] px-6">
        <Link
          to="/admin"
          className="flex items-center gap-3"
          onClick={() => setMobileMenuOpen(false)}
        >
          <BrandLogo className="h-10 w-12" />
          <span>
            <span className="block text-base font-bold text-white">
              WellPlayd
            </span>
            <span className="block text-[10px] font-semibold uppercase tracking-[0.2em] text-violet-300">
              Admin
            </span>
          </span>
        </Link>
      </div>

      <nav className="flex-1 overflow-y-auto px-4 py-5">
        <p className="px-3 text-[10px] font-bold uppercase tracking-[0.2em] text-zinc-600">
          {isEnglish ? "Management" : "İdarəetmə"}
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
                {isEnglish ? item.labelEn : item.labelAz}
              </NavLink>
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
          {isEnglish ? "Back to site" : "Sayta qayıt"}
        </Link>
      </div>
    </div>
  );

  return (
    <div className="admin-theme min-h-screen text-zinc-100">
      <aside className="admin-sidebar fixed inset-y-0 left-0 z-40 hidden w-72 border-r lg:block">
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
          <aside className="admin-sidebar relative h-full w-[min(86vw,19rem)] border-r">
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
        <header className="admin-header sticky top-0 z-30 flex h-20 items-center justify-between border-b px-5 backdrop-blur-xl sm:px-8">
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
            <span className="flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-violet-300/15 bg-violet-500/15 text-xs font-bold text-violet-200">
              {profileImageUrl &&
              profileImageUrl !== failedProfileImageUrl ? (
                <img
                  src={profileImageUrl}
                  alt={
                    isEnglish
                      ? `${user?.userName ?? "Admin"} profile picture`
                      : `${user?.userName ?? "Admin"} profil şəkli`
                  }
                  className="h-full w-full object-cover"
                  onError={() => {
                    setFailedProfileImageUrl(profileImageUrl);
                  }}
                />
              ) : (
                userInitial
              )}
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

        <main className="admin-main p-5 sm:p-8">
          <div className="mx-auto max-w-[1500px]">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
