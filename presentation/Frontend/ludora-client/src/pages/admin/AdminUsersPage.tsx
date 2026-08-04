import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  Eye,
  ShieldAlert,
  UsersRound,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import { AdminFilterSelect } from "../../components/admin/AdminFilterSelect";
import { AdminLiveSearch } from "../../components/admin/AdminLiveSearch";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatDate,
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
import { getAdminUsers } from "../../services/adminService";

const pageSize = 20;

export function AdminUsersPage() {
  const { language } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();

  const pageNumber = Math.max(
    1,
    Number(searchParams.get("page") ?? "1") || 1,
  );
  const search = searchParams.get("search") ?? "";
  const role = searchParams.get("role") ?? "";
  const banState = searchParams.get("ban") ?? "";
  const sortBy = searchParams.get("sort") ?? "newest";

  const filter = {
    search: search || undefined,
    role: role || undefined,
    isBanned: banState === "banned"
      ? true
      : banState === "active"
        ? false
        : undefined,
    sortBy,
    pageNumber,
    pageSize,
  };

  const usersQuery = useQuery({
    queryKey: ["admin", "users", filter],
    queryFn: ({ signal }) => getAdminUsers(filter, signal),
    placeholderData: keepPreviousData,
    refetchInterval: 10_000,
  });

  function updateParam(name: string, value: string) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value);
    else next.delete(name);
    next.delete("page");
    setSearchParams(next);
  }

  function setPage(nextPage: number) {
    const next = new URLSearchParams(searchParams);
    if (nextPage <= 1) next.delete("page");
    else next.set("page", String(nextPage));
    setSearchParams(next);
  }

  const data = usersQuery.data;

  return (
    <div className="space-y-6">
      <section className="flex flex-col justify-between gap-4 xl:flex-row xl:items-end">
        <div>
          <p className="text-sm font-medium text-violet-300">
            Hesablar və moderasiya
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
            İstifadəçilər
          </h2>
          <p className="mt-2 text-sm text-zinc-500">
            İstifadəçiləri axtar, statuslarını yoxla və moderasiya et.
          </p>
        </div>
        <div className="flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.025] px-4 py-3">
          <UsersRound size={18} className="text-violet-300" />
          <span className="text-sm text-zinc-500">Nəticə</span>
          <strong className="text-sm text-white">
            {data?.totalCount.toLocaleString("az-AZ") ?? "—"}
          </strong>
        </div>
      </section>

      <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4">
        <div className="grid gap-3 lg:grid-cols-[minmax(18rem,1fr)_auto_auto_auto]">
          <AdminLiveSearch
            placeholder="İstifadəçi adı və ya email..."
            ariaLabel="İstifadəçilər arasında axtarış"
          />

          <AdminFilterSelect
            label={language === "en" ? "Role" : "Rol"}
            value={role}
            onChange={(value) => updateParam("role", value)}
            showFilterIcon
            options={[
              ["", language === "en" ? "All roles" : "Bütün rollar"],
              ["User", language === "en" ? "User" : "İstifadəçi"],
              ["Moderator", "Moderator"],
              ["Admin", "Admin"],
            ]}
          />
          <AdminFilterSelect
            label={language === "en" ? "Account status" : "Hesab statusu"}
            value={banState}
            onChange={(value) => updateParam("ban", value)}
            showFilterIcon
            options={[
              ["", language === "en" ? "All statuses" : "Bütün statuslar"],
              ["active", language === "en" ? "Active" : "Aktiv"],
              ["banned", language === "en" ? "Banned" : "Banlı"],
            ]}
          />
          <AdminFilterSelect
            label={language === "en" ? "Sort users" : "İstifadəçiləri sırala"}
            value={sortBy}
            onChange={(value) => updateParam("sort", value)}
            options={[
              ["newest", language === "en" ? "Newest" : "Ən yeni"],
              ["oldest", language === "en" ? "Oldest" : "Ən köhnə"],
              ["username", language === "en" ? "Name A–Z" : "Ad A–Z"],
              ["username_desc", language === "en" ? "Name Z–A" : "Ad Z–A"],
              ["mostreported", language === "en" ? "Most reported" : "Ən çox şikayət"],
              ["mostwarned", language === "en" ? "Most warned" : "Ən çox xəbərdarlıq"],
            ]}
          />
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
        {usersQuery.isError ? (
          <div className="flex min-h-64 items-center justify-center p-8 text-center">
            <div>
              <CircleAlert className="mx-auto text-red-300" size={26} />
              <h3 className="mt-3 font-semibold text-white">
                İstifadəçilər yüklənmədi
              </h3>
              <p className="mt-2 max-w-md text-sm text-zinc-500">
                {getApiErrorMessage(usersQuery.error)}
              </p>
              <button
                type="button"
                onClick={() => usersQuery.refetch()}
                className="mt-4 rounded-xl bg-white/[0.06] px-4 py-2 text-sm font-semibold text-zinc-200"
              >
                Yenidən yoxla
              </button>
            </div>
          </div>
        ) : usersQuery.isPending ? (
          <UsersTableSkeleton />
        ) : data && data.items.length > 0 ? (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px] border-collapse text-left">
                <thead>
                  <tr className="border-b border-white/[0.06] text-[10px] font-bold uppercase tracking-[0.15em] text-zinc-600">
                    <th className="px-5 py-4">İstifadəçi</th>
                    <th className="px-5 py-4">Rol</th>
                    <th className="px-5 py-4">Status</th>
                    <th className="px-5 py-4">Moderasiya</th>
                    <th className="px-5 py-4">Qeydiyyat</th>
                    <th className="px-5 py-4 text-right">Əməliyyat</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((user) => {
                    const avatarUrl = resolveAssetUrl(user.profileImageUrl);
                    return (
                      <tr
                        key={user.id}
                        className="border-b border-white/[0.045] transition last:border-b-0 hover:bg-white/[0.025]"
                      >
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-3">
                            <span className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/10 text-sm font-bold text-violet-200">
                              {avatarUrl ? (
                                <img
                                  src={avatarUrl}
                                  alt=""
                                  className="h-full w-full object-cover"
                                />
                              ) : (
                                user.userName.charAt(0).toUpperCase()
                              )}
                            </span>
                            <div className="min-w-0">
                              <p className="max-w-56 truncate text-sm font-semibold text-zinc-200">
                                {user.userName}
                              </p>
                              <p className="mt-1 max-w-56 truncate text-xs text-zinc-600">
                                {user.email}
                              </p>
                            </div>
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-bold ${
                            user.role.toLowerCase() === "admin"
                              ? "border-violet-400/15 bg-violet-500/10 text-violet-200"
                              : "border-white/[0.07] bg-white/[0.035] text-zinc-400"
                          }`}>
                            {user.role}
                          </span>
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ${
                              user.isOnline
                                ? "bg-emerald-500/10 text-emerald-300"
                                : "bg-zinc-500/10 text-zinc-500"
                            }`}>
                              <span className={`h-1.5 w-1.5 rounded-full ${
                                user.isOnline ? "bg-emerald-400" : "bg-zinc-600"
                              }`} />
                              {user.isOnline ? "Onlayn" : "Oflayn"}
                            </span>
                            {user.isBanned && (
                              <span
                                title={
                                  user.isPermanentlyBanned
                                    ? "Müddətsiz ban"
                                    : formatDateTime(user.bannedUntilUtc)
                                }
                                className="inline-flex items-center gap-1.5 rounded-full bg-red-500/10 px-2.5 py-1 text-[11px] font-semibold text-red-300"
                              >
                                <span className="h-1.5 w-1.5 rounded-full bg-red-400" />
                                Banlı
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-2 text-xs">
                            <span className="rounded-lg bg-amber-500/[0.08] px-2 py-1 text-amber-300">
                              {user.warningCount} warning
                            </span>
                            <span className="rounded-lg bg-red-500/[0.08] px-2 py-1 text-red-300">
                              {user.reportCount} şikayət
                            </span>
                            {user.pendingReportCount > 0 && (
                              <span className="rounded-lg bg-violet-500/10 px-2 py-1 text-violet-200">
                                {user.pendingReportCount} açıq
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-5 py-4 text-sm text-zinc-500">
                          {formatDate(user.createdAt)}
                        </td>
                        <td className="px-5 py-4 text-right">
                          <Link
                            to={`/admin/users/${user.id}`}
                            className="inline-flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] bg-white/[0.035] px-3 text-xs font-semibold text-zinc-300 transition hover:border-violet-400/25 hover:bg-violet-500/10 hover:text-violet-200"
                          >
                            <Eye size={15} />
                            Detal
                          </Link>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="flex flex-col gap-3 border-t border-white/[0.06] px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-xs text-zinc-600">
                Səhifə {data.pageNumber} / {Math.max(1, data.totalPages)} ·{" "}
                {data.totalCount.toLocaleString("az-AZ")} nəticə
              </p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  disabled={pageNumber <= 1}
                  onClick={() => setPage(pageNumber - 1)}
                  className="flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] px-3 text-xs font-semibold text-zinc-300 disabled:cursor-not-allowed disabled:opacity-35"
                >
                  <ChevronLeft size={15} />
                  Əvvəlki
                </button>
                <button
                  type="button"
                  disabled={pageNumber >= data.totalPages}
                  onClick={() => setPage(pageNumber + 1)}
                  className="flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] px-3 text-xs font-semibold text-zinc-300 disabled:cursor-not-allowed disabled:opacity-35"
                >
                  Növbəti
                  <ChevronRight size={15} />
                </button>
              </div>
            </div>
          </>
        ) : (
          <div className="flex min-h-64 items-center justify-center p-8 text-center">
            <div>
              <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-white/[0.04] text-zinc-600">
                <ShieldAlert size={22} />
              </span>
              <h3 className="mt-4 font-semibold text-white">
                İstifadəçi tapılmadı
              </h3>
              <p className="mt-2 text-sm text-zinc-600">
                Axtarış və filtr seçimlərini dəyiş.
              </p>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}

function UsersTableSkeleton() {
  return (
    <div className="animate-pulse p-5">
      {Array.from({ length: 7 }).map((_, index) => (
        <div
          key={index}
          className="flex items-center gap-4 border-b border-white/[0.04] py-4 last:border-0"
        >
          <div className="h-10 w-10 rounded-xl bg-white/[0.05]" />
          <div className="space-y-2">
            <div className="h-3 w-36 rounded bg-white/[0.06]" />
            <div className="h-2.5 w-48 rounded bg-white/[0.035]" />
          </div>
        </div>
      ))}
    </div>
  );
}
