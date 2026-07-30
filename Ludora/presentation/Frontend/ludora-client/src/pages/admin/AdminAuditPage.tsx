import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  ScrollText,
  ShieldCheck,
  SlidersHorizontal,
  UserRound,
  X,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import { AdminDateRangeFilter } from "../../components/admin/AdminDateRangeFilter";
import { AdminLiveSearch } from "../../components/admin/AdminLiveSearch";
import { getApiErrorMessage } from "../../lib/api";
import { formatDateTime } from "../../lib/helpers";
import {
  getModerationActionTitle,
  getModerationResourceType,
} from "../../lib/moderationAudit";
import { useI18n } from "../../i18n/useI18n";
import { getModerationAudit } from "../../services/adminService";

const pageSize = 20;

const actionOptions = [
  ["", "Bütün əməliyyatlar"],
  ["1", "Şikayət statusu"],
  ["2", "Xəbərdarlıq"],
  ["3", "Müddətli ban"],
  ["4", "Müddətsiz ban"],
  ["5", "Ban açılması"],
  ["6", "Rol dəyişikliyi"],
  ["7", "Admin əməliyyatları"],
] as const;

export function AdminAuditPage() {
  const { language } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const pageNumber = Math.max(
    1,
    Number(searchParams.get("page") ?? "1") || 1,
  );
  const search = searchParams.get("search") ?? "";
  const action = searchParams.get("action") ?? "";
  const from = searchParams.get("from") ?? "";
  const to = searchParams.get("to") ?? "";

  const auditQuery = useQuery({
    queryKey: ["admin", "audit", search, action, from, to, pageNumber],
    queryFn: ({ signal }) =>
      getModerationAudit(
        {
          search: search || undefined,
          actionType: action ? Number(action) : undefined,
          fromUtc: from
            ? new Date(`${from}T00:00:00.000Z`).toISOString()
            : undefined,
          toUtc: to
            ? new Date(`${to}T23:59:59.999Z`).toISOString()
            : undefined,
          pageNumber,
          pageSize,
        },
        signal,
      ),
    placeholderData: keepPreviousData,
  });

  function updateParam(name: string, value: string) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value);
    else next.delete(name);
    next.delete("page");
    setSearchParams(next, { replace: true });
  }

  const activeFilterCount = [search, action, from, to].filter(Boolean).length;

  function clearFilters() {
    setSearchParams(new URLSearchParams(), { replace: true });
  }

  function clearDateRange() {
    const next = new URLSearchParams(searchParams);
    next.delete("from");
    next.delete("to");
    next.delete("page");
    setSearchParams(next, { replace: true });
  }

  const data = auditQuery.data;

  return (
    <div className="space-y-6">
      <section>
        <p className="text-sm font-medium text-violet-300">
          Moderasiya nəzarəti
        </p>
        <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
          Audit jurnalı
        </h2>
        <p className="mt-3 max-w-2xl text-sm leading-6 text-zinc-500">
          Admin və moderatorların istifadəçi, kataloq, forum, post, elan
          və nişanlar üzrə etdiyi bütün əməliyyatları izləyin.
        </p>
      </section>

      <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5">
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 border-b border-white/[0.06] pb-4">
          <div className="flex items-center gap-3">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
              <SlidersHorizontal size={17} />
            </span>
            <div>
              <h3 className="text-sm font-semibold text-zinc-200">Filtrlər</h3>
              <p className="mt-0.5 text-xs text-zinc-600">
                Audit qeydlərini əməliyyat və tarixə görə süzgəcdən keçir
              </p>
            </div>
          </div>
          {activeFilterCount > 0 && (
            <button
              type="button"
              onClick={clearFilters}
              className="inline-flex h-9 items-center gap-2 rounded-xl border border-white/[0.08] px-3 text-xs font-semibold text-zinc-400 transition hover:bg-white/[0.05] hover:text-zinc-200"
            >
              <X size={14} />
              Hamısını təmizlə
              <span className="rounded-md bg-violet-500/15 px-1.5 py-0.5 text-[10px] text-violet-200">
                {activeFilterCount}
              </span>
            </button>
          )}
        </div>

        <div className="grid items-end gap-4 md:grid-cols-2 xl:grid-cols-[minmax(18rem,1.2fr)_minmax(14rem,0.8fr)_minmax(28rem,1.5fr)]">
          <div className="min-w-0 [&_input]:h-14">
            <span className="mb-2 block text-[11px] font-semibold uppercase tracking-[0.12em] text-zinc-500">
              Axtarış
            </span>
            <AdminLiveSearch
              placeholder="Admin, istifadəçi və ya səbəb..."
              ariaLabel="Audit jurnalında axtarış"
            />
          </div>
          <label className="block min-w-0">
            <span className="mb-2 block text-[11px] font-semibold uppercase tracking-[0.12em] text-zinc-500">
              Əməliyyat növü
            </span>
            <select
              value={action}
              onChange={(event) => updateParam("action", event.target.value)}
              className="h-14 w-full rounded-xl border border-white/[0.08] bg-[#0a0a0e] px-3 text-sm text-zinc-300 outline-none transition focus:border-violet-500/50 focus:ring-2 focus:ring-violet-500/10"
            >
              {actionOptions.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <AdminDateRangeFilter
            from={from}
            to={to}
            onFromChange={(value) => updateParam("from", value)}
            onToChange={(value) => updateParam("to", value)}
            onClear={clearDateRange}
            className="md:col-span-2 xl:col-span-1"
          />
        </div>
      </section>

      {auditQuery.isError ? (
        <div className="flex items-start gap-3 rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-5 text-red-200">
          <CircleAlert size={20} className="mt-0.5 shrink-0" />
          <div>
            <p className="font-semibold">Audit jurnalı yüklənmədi</p>
            <p className="mt-1 text-sm text-red-200/70">
              {getApiErrorMessage(
                auditQuery.error,
                "Audit məlumatlarını almaq mümkün olmadı.",
              )}
            </p>
          </div>
        </div>
      ) : auditQuery.isPending ? (
        <AuditSkeleton />
      ) : data && data.items.length > 0 ? (
        <>
          <section className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
            <div className="border-b border-white/[0.06] px-5 py-4 text-sm text-zinc-500">
              {data.totalCount.toLocaleString(
                language === "en" ? "en-US" : "az-AZ",
              )}{" "}
              audit qeydi
            </div>
            <div className="divide-y divide-white/[0.06]">
              {data.items.map((entry) => (
                <article
                  key={entry.id}
                  className="grid gap-4 p-5 md:grid-cols-[minmax(12rem,0.8fr)_minmax(14rem,1.3fr)_auto] md:items-center"
                >
                  <div>
                    <p className="flex items-center gap-2 text-xs uppercase tracking-wider text-zinc-600">
                      <ShieldCheck size={14} />
                      Əməliyyatı edən
                    </p>
                    <Link
                      to={`/admin/users/${entry.adminUserId}`}
                      className="mt-2 inline-flex font-semibold text-violet-200 transition hover:text-violet-100"
                    >
                      {entry.adminUserName}
                    </Link>
                  </div>
                  <div>
                    <p className="flex items-center gap-2 text-sm font-semibold text-zinc-200">
                      <ScrollText size={16} className="text-amber-300" />
                      {getModerationActionTitle(entry, language)}
                    </p>
                    {entry.resourceType && (
                      <p className="mt-2 text-sm text-zinc-500">
                        {getModerationResourceType(
                          entry.resourceType,
                          language,
                        )}
                        {entry.resourceName
                          ? `: ${entry.resourceName}`
                          : entry.resourceId
                            ? ` #${entry.resourceId}`
                            : ""}
                      </p>
                    )}
                    {entry.targetUserId && (
                      <p className="mt-2 flex items-center gap-2 text-sm text-zinc-500">
                        <UserRound size={14} />
                        <Link
                          to={`/admin/users/${entry.targetUserId}`}
                          className="transition hover:text-violet-200"
                        >
                          {entry.targetUserName}
                        </Link>
                      </p>
                    )}
                    {entry.reason && (
                      <p className="mt-2 line-clamp-2 text-xs leading-5 text-zinc-600">
                        {entry.reason}
                      </p>
                    )}
                  </div>
                  <time
                    dateTime={entry.createdAtUtc}
                    className="text-xs text-zinc-600 md:text-right"
                  >
                    {formatDateTime(entry.createdAtUtc)}
                  </time>
                </article>
              ))}
            </div>
          </section>

          <Pagination
            pageNumber={data.pageNumber}
            totalPages={data.totalPages}
            onChange={(page) => {
              const next = new URLSearchParams(searchParams);
              if (page <= 1) next.delete("page");
              else next.set("page", String(page));
              setSearchParams(next);
            }}
          />
        </>
      ) : (
        <div className="rounded-2xl border border-white/[0.07] bg-white/[0.025] px-6 py-16 text-center">
          <ScrollText size={34} className="mx-auto text-zinc-700" />
          <p className="mt-4 font-semibold text-zinc-300">
            Audit qeydi tapılmadı
          </p>
          <p className="mt-2 text-sm text-zinc-600">
            Seçilmiş filtrlərə uyğun moderasiya əməliyyatı yoxdur.
          </p>
        </div>
      )}
    </div>
  );
}

function Pagination({
  pageNumber,
  totalPages,
  onChange,
}: {
  pageNumber: number;
  totalPages: number;
  onChange: (page: number) => void;
}) {
  if (totalPages <= 1) return null;

  const pages = getVisiblePages(pageNumber, totalPages);

  return (
    <nav
      aria-label="Audit jurnalı səhifələri"
      className="flex flex-wrap items-center justify-between gap-3"
    >
      <button
        type="button"
        disabled={pageNumber <= 1}
        onClick={() => onChange(pageNumber - 1)}
        className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.04] disabled:cursor-not-allowed disabled:opacity-30"
      >
        <ChevronLeft size={16} />
        Əvvəlki
      </button>
      <div className="flex items-center gap-1">
        {pages.map((page, index) =>
          page === "…" ? (
            <span
              key={`ellipsis-${index}`}
              className="flex h-10 w-8 items-center justify-center text-sm text-zinc-600"
            >
              …
            </span>
          ) : (
            <button
              key={page}
              type="button"
              aria-current={page === pageNumber ? "page" : undefined}
              onClick={() => onChange(page)}
              className={`h-10 min-w-10 rounded-xl border px-3 text-sm font-semibold transition ${
                page === pageNumber
                  ? "border-violet-400/40 bg-violet-500/20 text-violet-100"
                  : "border-white/[0.08] text-zinc-400 hover:bg-white/[0.04] hover:text-zinc-200"
              }`}
            >
              {page}
            </button>
          ),
        )}
      </div>
      <button
        type="button"
        disabled={pageNumber >= totalPages}
        onClick={() => onChange(pageNumber + 1)}
        className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.04] disabled:cursor-not-allowed disabled:opacity-30"
      >
        Növbəti
        <ChevronRight size={16} />
      </button>
    </nav>
  );
}

function getVisiblePages(
  pageNumber: number,
  totalPages: number,
): Array<number | "…"> {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  if (pageNumber <= 4) {
    return [1, 2, 3, 4, 5, "…", totalPages];
  }

  if (pageNumber >= totalPages - 3) {
    return [
      1,
      "…",
      totalPages - 4,
      totalPages - 3,
      totalPages - 2,
      totalPages - 1,
      totalPages,
    ];
  }

  return [
    1,
    "…",
    pageNumber - 1,
    pageNumber,
    pageNumber + 1,
    "…",
    totalPages,
  ];
}

function AuditSkeleton() {
  return (
    <div className="animate-pulse overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
      {[1, 2, 3, 4, 5].map((item) => (
        <div
          key={item}
          className="grid gap-4 border-b border-white/[0.05] p-5 md:grid-cols-3"
        >
          <div className="h-10 rounded-xl bg-white/[0.05]" />
          <div className="h-10 rounded-xl bg-white/[0.05]" />
          <div className="h-10 rounded-xl bg-white/[0.05]" />
        </div>
      ))}
    </div>
  );
}
