import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  Eye,
  Filter,
  ShieldAlert,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import { AdminLiveSearch } from "../../components/admin/AdminLiveSearch";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
import { getReportedUsers } from "../../services/adminService";
import type {
  UserReportCategory,
  UserReportStatus,
} from "../../types/admin";

const pageSize = 20;

const categoryLabels: Record<UserReportCategory, string> = {
  Spam: "Spam",
  HarassmentOrInsult: "Təhqir və təzyiq",
  HateSpeech: "Nifrət nitqi",
  InappropriateContent: "Uyğunsuz məzmun",
  Impersonation: "Başqasını təqlid etmə",
  ScamOrFraud: "Dələduzluq",
  ThreatsOrViolence: "Təhdid və zorakılıq",
  PrivacyViolation: "Məxfilik pozuntusu",
  Other: "Digər",
};

const statusLabels: Record<UserReportStatus, string> = {
  Pending: "Gözləyir",
  UnderReview: "Araşdırılır",
  Resolved: "Həll edilib",
  Rejected: "Rədd edilib",
};

export function AdminReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const pageNumber = Math.max(
    1,
    Number(searchParams.get("page") ?? "1") || 1,
  );
  const search = searchParams.get("search") ?? "";
  const categoryValue = searchParams.get("category") ?? "";
  const statusValue = searchParams.get("status") ?? "";
  const banState = searchParams.get("ban") ?? "";
  const sortBy = searchParams.get("sort") ?? "mostReported";

  const filter = {
    search: search || undefined,
    category: categoryValue
      ? categoryValue as UserReportCategory
      : undefined,
    status: statusValue
      ? statusValue as UserReportStatus
      : undefined,
    isBanned: banState === "banned"
      ? true
      : banState === "notBanned"
        ? false
        : undefined,
    sortBy,
    pageNumber,
    pageSize,
  };

  const reportsQuery = useQuery({
    queryKey: ["admin", "reports", filter],
    queryFn: ({ signal }) => getReportedUsers(filter, signal),
    placeholderData: keepPreviousData,
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

  const data = reportsQuery.data;

  return (
    <div className="space-y-6">
      <section className="flex flex-col justify-between gap-4 xl:flex-row xl:items-end">
        <div>
          <p className="text-sm font-medium text-violet-300">
            Moderasiya növbəsi
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
            Şikayətlər
          </h2>
          <p className="mt-2 text-sm text-zinc-500">
            Şikayət olunan istifadəçiləri araşdır və müraciətləri nəticələndir.
          </p>
        </div>
        <div className="flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.025] px-4 py-3">
          <ShieldAlert size={18} className="text-violet-300" />
          <span className="text-sm text-zinc-500">İstifadəçi</span>
          <strong className="text-sm text-white">
            {data?.totalCount.toLocaleString("az-AZ") ?? "—"}
          </strong>
        </div>
      </section>

      <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4">
        <div className="grid gap-3 xl:grid-cols-[minmax(17rem,1fr)_auto_auto_auto_auto]">
          <AdminLiveSearch
            placeholder="İstifadəçi adı və ya email..."
            ariaLabel="Şikayətlər arasında axtarış"
          />

          <FilterSelect
            label="Kateqoriya"
            value={categoryValue}
            onChange={(value) => updateParam("category", value)}
            options={[
              ["", "Bütün kateqoriyalar"],
              ...Object.entries(categoryLabels),
            ]}
          />
          <FilterSelect
            label="Şikayət statusu"
            value={statusValue}
            onChange={(value) => updateParam("status", value)}
            options={[
              ["", "Bütün statuslar"],
              ...Object.entries(statusLabels),
            ]}
          />
          <FilterSelect
            label="Ban statusu"
            value={banState}
            onChange={(value) => updateParam("ban", value)}
            options={[
              ["", "Ban statusu"],
              ["notBanned", "Banlanmayıb"],
              ["banned", "Banlanıb"],
            ]}
          />
          <FilterSelect
            label="Sıralama"
            value={sortBy}
            onChange={(value) => updateParam("sort", value)}
            options={[
              ["mostReported", "Ən çox şikayət"],
              ["mostPending", "Ən çox gözləyən"],
              ["newest", "Ən yeni"],
              ["oldest", "Ən köhnə"],
              ["username", "Ad A–Z"],
            ]}
          />
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
        {reportsQuery.isError ? (
          <div className="flex min-h-64 items-center justify-center p-8 text-center">
            <div>
              <CircleAlert className="mx-auto text-red-300" size={27} />
              <h3 className="mt-3 font-semibold text-white">
                Şikayətlər yüklənmədi
              </h3>
              <p className="mt-2 max-w-md text-sm text-zinc-500">
                {getApiErrorMessage(reportsQuery.error)}
              </p>
              <button
                type="button"
                onClick={() => reportsQuery.refetch()}
                className="mt-4 rounded-xl bg-white/[0.06] px-4 py-2 text-sm font-semibold text-zinc-200"
              >
                Yenidən yoxla
              </button>
            </div>
          </div>
        ) : reportsQuery.isPending ? (
          <ReportsTableSkeleton />
        ) : data && data.items.length > 0 ? (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[1050px] border-collapse text-left">
                <thead>
                  <tr className="border-b border-white/[0.06] text-[10px] font-bold uppercase tracking-[0.15em] text-zinc-600">
                    <th className="px-5 py-4">İstifadəçi</th>
                    <th className="px-5 py-4">Şikayətlər</th>
                    <th className="px-5 py-4">Kateqoriyalar</th>
                    <th className="px-5 py-4">Moderasiya</th>
                    <th className="px-5 py-4">Son şikayət</th>
                    <th className="px-5 py-4 text-right">Əməliyyat</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((user) => {
                    const avatarUrl = resolveAssetUrl(user.profileImageUrl);
                    return (
                      <tr
                        key={user.userId}
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
                              <p className="max-w-52 truncate text-sm font-semibold text-zinc-200">
                                {user.userName}
                              </p>
                              <p className="mt-1 max-w-52 truncate text-xs text-zinc-600">
                                {user.email ?? "Email yoxdur"}
                              </p>
                            </div>
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-2">
                            <span className="rounded-lg bg-red-500/10 px-2.5 py-1 text-xs font-bold text-red-200">
                              {user.totalReportCount} ümumi
                            </span>
                            {user.pendingReportCount > 0 && (
                              <span className="rounded-lg bg-amber-500/10 px-2.5 py-1 text-xs font-bold text-amber-200">
                                {user.pendingReportCount} açıq
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex max-w-64 flex-wrap gap-1.5">
                            {user.categories.slice(0, 3).map((category) => (
                              <span
                                key={category.category}
                                className="rounded-md border border-white/[0.06] bg-white/[0.03] px-2 py-1 text-[10px] text-zinc-400"
                              >
                                {categoryLabels[category.category]} · {category.count}
                              </span>
                            ))}
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex flex-wrap items-center gap-2 text-xs">
                            <span className="rounded-lg bg-amber-500/[0.08] px-2 py-1 text-amber-300">
                              {user.warningCount} xəbərdarlıq
                            </span>
                            {user.isBanned && (
                              <span className="rounded-lg bg-red-500/10 px-2 py-1 font-semibold text-red-300">
                                Banlanıb
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-5 py-4 text-sm text-zinc-500">
                          {formatDateTime(user.lastReportedAtUtc)}
                        </td>
                        <td className="px-5 py-4 text-right">
                          <Link
                            to={`/admin/reports/users/${user.userId}`}
                            className="inline-flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] bg-white/[0.035] px-3 text-xs font-semibold text-zinc-300 transition hover:border-violet-400/25 hover:bg-violet-500/10 hover:text-violet-200"
                          >
                            <Eye size={15} />
                            Araşdır
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
                {data.totalCount.toLocaleString("az-AZ")} istifadəçi
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
              <ShieldAlert className="mx-auto text-zinc-700" size={28} />
              <h3 className="mt-4 font-semibold text-white">
                Şikayət tapılmadı
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

interface FilterSelectProps {
  label: string;
  value: string;
  options: Array<[string, string]>;
  onChange: (value: string) => void;
}

function FilterSelect({
  label,
  value,
  options,
  onChange,
}: FilterSelectProps) {
  return (
    <label className="relative">
      <span className="sr-only">{label}</span>
      <Filter
        size={15}
        className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
      />
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="h-11 w-full appearance-none rounded-xl border border-white/[0.08] bg-[#0a0a0e] pl-9 pr-8 text-sm text-zinc-300 outline-none transition focus:border-violet-500/50 xl:w-auto"
      >
        {options.map(([optionValue, optionLabel]) => (
          <option key={optionValue} value={optionValue}>
            {optionLabel}
          </option>
        ))}
      </select>
    </label>
  );
}

function ReportsTableSkeleton() {
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
            <div className="h-2.5 w-52 rounded bg-white/[0.035]" />
          </div>
        </div>
      ))}
    </div>
  );
}
