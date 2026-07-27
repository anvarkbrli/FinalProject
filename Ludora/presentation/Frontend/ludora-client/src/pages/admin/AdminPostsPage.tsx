import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  FileText,
  Filter,
  Image as ImageIcon,
  LoaderCircle,
  RotateCcw,
  Search,
  Trash2,
  X,
} from "lucide-react";
import type { FormEvent, ReactNode } from "react";
import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import { hasAuthRole } from "../../lib/authRoles";
import {
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
import {
  deleteAdminPost,
  getAdminPosts,
  restoreAdminPost,
} from "../../services/adminService";
import { useAuthStore } from "../../stores/authStore";
import type { AdminPost } from "../../types/admin";

const pageSize = 20;

export function AdminPostsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [searchText, setSearchText] = useState(
    searchParams.get("search") ?? "",
  );
  const [postToDelete, setPostToDelete] =
    useState<AdminPost | null>(null);
  const [deleteReason, setDeleteReason] = useState("");
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const canDelete = hasAuthRole(
    accessToken,
    "Admin",
    user?.roles ?? [],
  );

  const pageNumber = Math.max(
    1,
    Number(searchParams.get("page") ?? "1") || 1,
  );
  const search = searchParams.get("search") ?? "";
  const state = searchParams.get("state") ?? "";
  const sortBy = searchParams.get("sort") ?? "newest";
  const filter = {
    search: search || undefined,
    isDeleted:
      state === "deleted"
        ? true
        : state === "active"
          ? false
          : undefined,
    sortBy,
    pageNumber,
    pageSize,
  };

  const postsQuery = useQuery({
    queryKey: ["admin", "posts", filter],
    queryFn: ({ signal }) => getAdminPosts(filter, signal),
    placeholderData: keepPreviousData,
  });

  const deleteMutation = useMutation({
    mutationFn: () =>
      deleteAdminPost(
        postToDelete!.id,
        deleteReason.trim(),
      ),
    onSuccess: async () => {
      setPostToDelete(null);
      setDeleteReason("");
      await refreshPostData();
    },
  });

  const restoreMutation = useMutation({
    mutationFn: restoreAdminPost,
    onSuccess: refreshPostData,
  });

  function refreshPostData() {
    return Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["admin", "posts"],
      }),
      queryClient.invalidateQueries({
        queryKey: ["admin", "dashboard"],
      }),
    ]);
  }

  function updateParam(name: string, value: string) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value);
    else next.delete(name);
    next.delete("page");
    setSearchParams(next);
  }

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    updateParam("search", searchText.trim());
  }

  function setPage(nextPage: number) {
    const next = new URLSearchParams(searchParams);
    if (nextPage <= 1) next.delete("page");
    else next.set("page", String(nextPage));
    setSearchParams(next);
  }

  const data = postsQuery.data;

  return (
    <div className="space-y-6">
      <section className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="text-sm font-medium text-violet-300">
            Kontent moderasiyası
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
            Postlar
          </h2>
          <p className="mt-2 text-sm text-zinc-500">
            Postları axtar, silinmiş məzmunu yoxla və bərpa et.
          </p>
        </div>
        <div className="flex items-center gap-3 rounded-xl border border-white/[0.07] bg-white/[0.025] px-4 py-3">
          <FileText size={18} className="text-violet-300" />
          <span className="text-sm text-zinc-500">Nəticə</span>
          <strong className="text-sm text-white">
            {data?.totalCount.toLocaleString("az-AZ") ?? "—"}
          </strong>
        </div>
      </section>

      <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4">
        <div className="grid gap-3 lg:grid-cols-[minmax(18rem,1fr)_auto_auto]">
          <form onSubmit={handleSearch} className="relative">
            <Search
              size={17}
              className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-zinc-600"
            />
            <input
              type="search"
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Post mətni və ya istifadəçi..."
              className="h-11 w-full rounded-xl border border-white/[0.08] bg-[#0a0a0e] pl-10 pr-24 text-sm text-white outline-none transition placeholder:text-zinc-700 focus:border-violet-500/50"
            />
            <button
              type="submit"
              className="absolute right-1.5 top-1.5 h-8 rounded-lg bg-violet-600 px-3 text-xs font-bold text-white transition hover:bg-violet-500"
            >
              Axtar
            </button>
          </form>

          <FilterSelect
            value={state}
            onChange={(value) => updateParam("state", value)}
            options={[
              ["", "Bütün postlar"],
              ["active", "Aktiv postlar"],
              ["deleted", "Silinmiş postlar"],
            ]}
          />
          <FilterSelect
            value={sortBy}
            onChange={(value) => updateParam("sort", value)}
            options={[
              ["newest", "Ən yeni"],
              ["oldest", "Ən köhnə"],
              ["updated", "Son yenilənən"],
            ]}
          />
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
        {postsQuery.isError ? (
          <ErrorState
            message={getApiErrorMessage(postsQuery.error)}
            onRetry={() => postsQuery.refetch()}
          />
        ) : postsQuery.isPending ? (
          <PostsSkeleton />
        ) : data && data.items.length > 0 ? (
          <>
            <div className="divide-y divide-white/[0.05]">
              {data.items.map((post) => {
                const imageUrl = resolveAssetUrl(post.imageUrl);

                return (
                  <article
                    key={post.id}
                    className="grid gap-4 p-5 transition hover:bg-white/[0.02] md:grid-cols-[5rem_minmax(0,1fr)_auto] md:items-center"
                  >
                    <div className="flex h-20 w-20 items-center justify-center overflow-hidden rounded-xl border border-white/[0.07] bg-black/20 text-zinc-700">
                      {imageUrl ? (
                        <img
                          src={imageUrl}
                          alt=""
                          className="h-full w-full object-cover"
                        />
                      ) : (
                        <ImageIcon size={22} />
                      )}
                    </div>

                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-bold text-white">
                          Post #{post.id}
                        </span>
                        <span
                          className={`rounded-full px-2.5 py-1 text-[10px] font-bold uppercase ${
                            post.isDeleted
                              ? "bg-red-500/10 text-red-300"
                              : "bg-emerald-500/10 text-emerald-300"
                          }`}
                        >
                          {post.isDeleted ? "Silinib" : "Aktiv"}
                        </span>
                      </div>
                      <p className="mt-2 line-clamp-2 text-sm leading-6 text-zinc-400">
                        {post.content || "Yalnız şəkil paylaşımı"}
                      </p>
                      <p className="mt-2 text-xs text-zinc-600">
                        <Link
                          to={`/users/${post.userName}`}
                          className="font-semibold text-zinc-400 hover:text-violet-200"
                        >
                          @{post.userName}
                        </Link>
                        {" · "}
                        {formatDateTime(post.createdAt)}
                      </p>
                      {post.isDeleted && post.deleteReason && (
                        <p className="mt-2 text-xs text-red-200/60">
                          Silinmə səbəbi: {post.deleteReason}
                        </p>
                      )}
                    </div>

                    <div className="flex flex-wrap gap-2 md:justify-end">
                      <Link
                        to={`/users/${post.userName}?tab=posts`}
                        className="inline-flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] px-3 text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.05]"
                      >
                        <ChevronRight size={14} />
                        Profilə bax
                      </Link>
                      {post.isDeleted ? (
                        <button
                          type="button"
                          onClick={() =>
                            restoreMutation.mutate(post.id)
                          }
                          disabled={restoreMutation.isPending}
                          className="inline-flex h-9 items-center gap-2 rounded-lg bg-emerald-500/10 px-3 text-xs font-semibold text-emerald-200 transition hover:bg-emerald-500/20 disabled:opacity-50"
                        >
                          <RotateCcw size={14} />
                          Bərpa et
                        </button>
                      ) : canDelete ? (
                        <button
                          type="button"
                          onClick={() => {
                            setDeleteReason("");
                            setPostToDelete(post);
                          }}
                          className="inline-flex h-9 items-center gap-2 rounded-lg bg-red-500/10 px-3 text-xs font-semibold text-red-200 transition hover:bg-red-500/20"
                        >
                          <Trash2 size={14} />
                          Sil
                        </button>
                      ) : null}
                    </div>
                  </article>
                );
              })}
            </div>

            <div className="flex flex-col gap-3 border-t border-white/[0.06] px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-xs text-zinc-600">
                Səhifə {data.pageNumber} / {Math.max(1, data.totalPages)}
                {" · "}
                {data.totalCount.toLocaleString("az-AZ")} nəticə
              </p>
              <div className="flex gap-2">
                <PageButton
                  disabled={pageNumber <= 1}
                  onClick={() => setPage(pageNumber - 1)}
                >
                  <ChevronLeft size={15} />
                  Əvvəlki
                </PageButton>
                <PageButton
                  disabled={pageNumber >= data.totalPages}
                  onClick={() => setPage(pageNumber + 1)}
                >
                  Növbəti
                  <ChevronRight size={15} />
                </PageButton>
              </div>
            </div>
          </>
        ) : (
          <div className="flex min-h-64 items-center justify-center p-8 text-center">
            <div>
              <FileText className="mx-auto text-zinc-700" size={28} />
              <h3 className="mt-4 font-semibold text-white">
                Post tapılmadı
              </h3>
              <p className="mt-2 text-sm text-zinc-600">
                Axtarış və filtr seçimlərini dəyiş.
              </p>
            </div>
          </div>
        )}
      </section>

      {postToDelete && (
        <div
          className="fixed inset-0 z-[1000] grid place-items-center bg-black/75 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-post-title"
        >
          <div className="w-full max-w-md rounded-2xl border border-white/[0.08] bg-[#101014] p-5 shadow-2xl">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h3
                  id="delete-post-title"
                  className="font-bold text-white"
                >
                  Post #{postToDelete.id} silinsin?
                </h3>
                <p className="mt-2 text-sm leading-6 text-zinc-500">
                  İstifadəçiyə görünəcək silinmə səbəbini qeyd et.
                </p>
              </div>
              <button
                type="button"
                onClick={() => {
                  setPostToDelete(null);
                  setDeleteReason("");
                }}
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-zinc-500 hover:bg-white/[0.06] hover:text-white"
                aria-label="Pəncərəni bağla"
              >
                <X size={18} />
              </button>
            </div>
            <textarea
              value={deleteReason}
              onChange={(event) => setDeleteReason(event.target.value)}
              maxLength={500}
              rows={4}
              placeholder="Silmə səbəbi..."
              className="mt-5 w-full resize-none rounded-xl border border-white/[0.08] bg-black/20 p-3 text-sm leading-6 text-white outline-none placeholder:text-zinc-700 focus:border-red-400/40"
            />
            {deleteMutation.isError && (
              <p className="mt-3 text-xs text-red-300">
                {getApiErrorMessage(deleteMutation.error)}
              </p>
            )}
            <div className="mt-5 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => {
                  setPostToDelete(null);
                  setDeleteReason("");
                }}
                disabled={deleteMutation.isPending}
                className="h-10 rounded-xl border border-white/[0.08] px-4 text-sm font-semibold text-zinc-400"
              >
                Ləğv et
              </button>
              <button
                type="button"
                onClick={() => deleteMutation.mutate()}
                disabled={
                  deleteReason.trim().length < 3 ||
                  deleteMutation.isPending
                }
                className="inline-flex h-10 items-center gap-2 rounded-xl bg-red-500 px-4 text-sm font-bold text-white transition hover:bg-red-400 disabled:cursor-not-allowed disabled:opacity-40"
              >
                {deleteMutation.isPending ? (
                  <LoaderCircle size={16} className="animate-spin" />
                ) : (
                  <Trash2 size={16} />
                )}
                Postu sil
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function FilterSelect({
  value,
  options,
  onChange,
}: {
  value: string;
  options: Array<[string, string]>;
  onChange: (value: string) => void;
}) {
  return (
    <label className="relative">
      <span className="sr-only">Filtr</span>
      <Filter
        size={15}
        className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
      />
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="h-11 w-full appearance-none rounded-xl border border-white/[0.08] bg-[#0a0a0e] pl-9 pr-8 text-sm text-zinc-300 outline-none focus:border-violet-500/50 lg:w-auto"
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

function PageButton({
  disabled,
  onClick,
  children,
}: {
  disabled: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] px-3 text-xs font-semibold text-zinc-300 disabled:cursor-not-allowed disabled:opacity-35"
    >
      {children}
    </button>
  );
}

function ErrorState({
  message,
  onRetry,
}: {
  message: string;
  onRetry: () => void;
}) {
  return (
    <div className="flex min-h-64 items-center justify-center p-8 text-center">
      <div>
        <CircleAlert className="mx-auto text-red-300" size={26} />
        <h3 className="mt-3 font-semibold text-white">
          Postlar yüklənmədi
        </h3>
        <p className="mt-2 max-w-md text-sm text-zinc-500">
          {message}
        </p>
        <button
          type="button"
          onClick={onRetry}
          className="mt-4 rounded-xl bg-white/[0.06] px-4 py-2 text-sm font-semibold text-zinc-200"
        >
          Yenidən yoxla
        </button>
      </div>
    </div>
  );
}

function PostsSkeleton() {
  return (
    <div className="animate-pulse divide-y divide-white/[0.04]">
      {Array.from({ length: 6 }).map((_, index) => (
        <div key={index} className="flex gap-4 p-5">
          <div className="h-20 w-20 rounded-xl bg-white/[0.05]" />
          <div className="flex-1 space-y-3 py-1">
            <div className="h-3 w-32 rounded bg-white/[0.06]" />
            <div className="h-3 w-3/4 rounded bg-white/[0.04]" />
            <div className="h-2.5 w-48 rounded bg-white/[0.035]" />
          </div>
        </div>
      ))}
    </div>
  );
}
