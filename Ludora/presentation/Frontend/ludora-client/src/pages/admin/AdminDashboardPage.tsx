import { useQuery } from "@tanstack/react-query";
import {
  BellRing,
  ChevronRight,
  CircleAlert,
  FileText,
  Gamepad2,
  MessageSquareText,
  RefreshCw,
  ShieldAlert,
  Trash2,
  UserPlus,
  UsersRound,
} from "lucide-react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import { hasAuthRole } from "../../lib/authRoles";
import { getAdminDashboard } from "../../services/adminService";
import { useAuthStore } from "../../stores/authStore";

export function AdminDashboardPage() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const panelRole = hasAuthRole(
    accessToken,
    "Admin",
    user?.roles ?? [],
  )
    ? "Administrator"
    : "Moderator";

  const dashboardQuery = useQuery({
    queryKey: ["admin", "dashboard"],
    queryFn: ({ signal }) => getAdminDashboard(signal),
    refetchInterval: 15_000,
  });

  if (dashboardQuery.isPending) {
    return <DashboardSkeleton />;
  }

  if (dashboardQuery.isError || !dashboardQuery.data) {
    return (
      <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-6">
        <div className="flex items-start gap-3">
          <CircleAlert className="mt-0.5 text-red-300" size={20} />
          <div>
            <h2 className="font-semibold text-red-100">
              Statistika yüklənmədi
            </h2>
            <p className="mt-2 text-sm text-red-200/70">
              {getApiErrorMessage(
                dashboardQuery.error,
                "Admin dashboard məlumatlarını almaq mümkün olmadı.",
              )}
            </p>
            <button
              type="button"
              className="mt-4 inline-flex items-center gap-2 rounded-xl bg-red-400/10 px-4 py-2 text-sm font-semibold text-red-100 transition hover:bg-red-400/20"
              onClick={() => dashboardQuery.refetch()}
            >
              <RefreshCw size={16} />
              Yenidən yoxla
            </button>
          </div>
        </div>
      </div>
    );
  }

  const data = dashboardQuery.data;
  const mainMetrics = [
    {
      label: "Ümumi istifadəçi",
      value: data.totalUsers,
      detail: `Son 30 gündə +${data.newUsersLast30Days}`,
      icon: UsersRound,
      accent: "violet",
      to: "/admin/users",
    },
    {
      label: "Aktiv ban",
      value: data.bannedUsers,
      detail: `${ratio(data.bannedUsers, data.totalUsers)}% istifadəçi`,
      icon: ShieldAlert,
      accent: "red",
      to: "/admin/users?ban=banned",
    },
    {
      label: "Ümumi post",
      value: data.totalPosts,
      detail: `${data.deletedPosts} silinmiş post`,
      icon: FileText,
      accent: "blue",
      to: "/admin/posts",
    },
    {
      label: "Oyun kataloqu",
      value: data.totalGames,
      detail: "Sistemdəki oyunlar",
      icon: Gamepad2,
      accent: "emerald",
      to: "/games",
    },
  ] as const;

  const moderationItems = [
    {
      label: "Xəbərdarlıqlar",
      value: data.totalWarnings,
      subValue: data.unreadWarnings,
      subLabel: "oxunmamış",
      icon: BellRing,
      to: "/admin/users?sort=mostwarned",
    },
    {
      label: "Müzakirələr",
      value: data.totalDiscussions,
      subValue: data.deletedDiscussions,
      subLabel: "silinmiş",
      icon: MessageSquareText,
      to: "/forum",
    },
    {
      label: "Cavablar",
      value: data.totalReplies,
      subValue: data.deletedReplies,
      subLabel: "silinmiş",
      icon: MessageSquareText,
      to: "/forum",
    },
    {
      label: "Elanlar",
      value: data.totalAnnouncements,
      subValue: data.publishedAnnouncements,
      subLabel: "yayımlanmış",
      icon: BellRing,
      to: "/",
    },
  ];

  return (
    <div className="space-y-8">
      <section className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="text-sm font-medium text-violet-300">
            Sistemin ümumi vəziyyəti
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
            Xoş gəldin, {panelRole}
          </h2>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-zinc-500">
            Burada istifadəçi fəaliyyəti və moderasiya göstəricilərinin
            ümumi xülasəsini görürsən.
          </p>
        </div>
        <Link
          to="/admin/users"
          className="inline-flex items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 py-3 text-sm font-bold text-white shadow-lg shadow-violet-950/30 transition hover:bg-violet-500"
        >
          <UsersRound size={17} />
          İstifadəçiləri idarə et
        </Link>
      </section>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {mainMetrics.map((metric) => {
          const Icon = metric.icon;
          const accentClasses = {
            violet: "bg-violet-500/12 text-violet-300",
            red: "bg-red-500/10 text-red-300",
            blue: "bg-sky-500/10 text-sky-300",
            emerald: "bg-emerald-500/10 text-emerald-300",
          }[metric.accent];

          return (
            <Link
              key={metric.label}
              to={metric.to}
              className="group rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5 transition duration-300 hover:-translate-y-0.5 hover:border-violet-400/20 hover:bg-white/[0.04] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500/50"
            >
              <div className="flex items-start justify-between">
                <span className={`flex h-11 w-11 items-center justify-center rounded-xl ${accentClasses}`}>
                  <Icon size={20} />
                </span>
                <span className="flex items-center gap-1 text-xs font-semibold text-zinc-600 transition group-hover:text-violet-300">
                  Ətraflı
                  <ChevronRight size={14} />
                </span>
              </div>
              <p className="mt-5 text-3xl font-bold tracking-tight text-white">
                {metric.value.toLocaleString("az-AZ")}
              </p>
              <p className="mt-1 text-sm font-medium text-zinc-300">
                {metric.label}
              </p>
              <p className="mt-3 text-xs text-zinc-600">
                {metric.detail}
              </p>
            </Link>
          );
        })}
      </section>

      <div className="grid gap-6 xl:grid-cols-[1.4fr_0.6fr]">
        <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025]">
          <div className="border-b border-white/[0.06] p-5">
            <h3 className="font-bold text-white">Moderasiya xülasəsi</h3>
            <p className="mt-1 text-sm text-zinc-600">
              Kontent və xəbərdarlıq göstəriciləri
            </p>
          </div>
          <div className="grid gap-px bg-white/[0.05] sm:grid-cols-2">
            {moderationItems.map((item) => {
              const Icon = item.icon;
              return (
                <Link
                  key={item.label}
                  to={item.to}
                  className="group bg-[#0b0b0f] p-5 transition hover:bg-white/[0.025] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-violet-500/50"
                >
                  <div className="flex items-center gap-3">
                    <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/[0.05] text-zinc-400">
                      <Icon size={17} />
                    </span>
                    <p className="text-sm font-medium text-zinc-400">
                      {item.label}
                    </p>
                    <ChevronRight
                      size={14}
                      className="ml-auto text-zinc-700 transition group-hover:text-violet-300"
                    />
                  </div>
                  <div className="mt-5 flex items-end justify-between">
                    <p className="text-2xl font-bold text-white">
                      {item.value.toLocaleString("az-AZ")}
                    </p>
                    <p className="text-xs text-zinc-600">
                      <span className="font-semibold text-amber-300">
                        {item.subValue}
                      </span>{" "}
                      {item.subLabel}
                    </p>
                  </div>
                </Link>
              );
            })}
          </div>
        </section>

        <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
              <UserPlus size={18} />
            </span>
            <div>
              <h3 className="font-bold text-white">İstifadəçi artımı</h3>
              <p className="text-xs text-zinc-600">Son 30 gün</p>
            </div>
          </div>
          <p className="mt-8 text-4xl font-bold tracking-tight text-white">
            +{data.newUsersLast30Days.toLocaleString("az-AZ")}
          </p>
          <div className="mt-5 h-2 overflow-hidden rounded-full bg-white/[0.05]">
            <div
              className="h-full rounded-full bg-gradient-to-r from-violet-600 to-fuchsia-500"
              style={{
                width: `${Math.max(
                  4,
                  Math.min(100, ratio(data.newUsersLast30Days, data.totalUsers)),
                )}%`,
              }}
            />
          </div>
          <p className="mt-3 text-xs leading-5 text-zinc-600">
            Ümumi istifadəçilərin{" "}
            {ratio(data.newUsersLast30Days, data.totalUsers)}%-i son 30 gündə
            qeydiyyatdan keçib.
          </p>

          <div className="mt-8 flex items-center justify-between rounded-xl border border-red-400/10 bg-red-500/[0.04] p-4">
            <span className="flex items-center gap-2 text-sm text-zinc-400">
              <Trash2 size={16} className="text-red-300" />
              Silinmiş kontent
            </span>
            <strong className="text-sm text-red-200">
              {(
                data.deletedPosts +
                data.deletedDiscussions +
                data.deletedReplies +
                data.deletedAnnouncements
              ).toLocaleString("az-AZ")}
            </strong>
          </div>
        </section>
      </div>
    </div>
  );
}

function ratio(value: number, total: number): number {
  if (total <= 0) return 0;
  return Math.round((value / total) * 100);
}

function DashboardSkeleton() {
  return (
    <div className="animate-pulse space-y-8" aria-label="Dashboard yüklənir">
      <div className="space-y-3">
        <div className="h-4 w-36 rounded bg-white/[0.06]" />
        <div className="h-9 w-80 max-w-full rounded bg-white/[0.06]" />
        <div className="h-4 w-[32rem] max-w-full rounded bg-white/[0.04]" />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <div
            key={index}
            className="h-52 rounded-2xl border border-white/[0.05] bg-white/[0.025]"
          />
        ))}
      </div>
    </div>
  );
}
