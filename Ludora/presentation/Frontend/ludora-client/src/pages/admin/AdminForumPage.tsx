import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArchiveRestore,
  CircleAlert,
  FolderCog,
  LoaderCircle,
  Lock,
  LockOpen,
  MessageSquareText,
  Pencil,
  Pin,
  PinOff,
  Plus,
  Save,
  Trash2,
  X,
} from "lucide-react";
import type { ReactNode } from "react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import { hasAuthRole } from "../../lib/authRoles";
import { formatDateTime } from "../../lib/helpers";
import {
  createForumCategory,
  deleteDiscussion as deleteAdminDiscussion,
  deleteForumCategory,
  getDeletedDiscussionReplies,
  getDeletedDiscussions,
  getForumCategories,
  restoreDiscussionReply,
  restoreForumCategory,
  setDiscussionState,
  updateForumCategory,
} from "../../services/adminService";
import { getForumDiscussions } from "../../services/forumService";
import { useAuthStore } from "../../stores/authStore";
import type { ForumCategory } from "../../types/admin";

type Tab = "discussions" | "categories" | "trash";

export function AdminForumPage() {
  const [tab, setTab] = useState<Tab>("discussions");

  return (
    <div className="space-y-6">
      <section>
        <p className="text-sm font-medium text-violet-300">İcma nəzarəti</p>
        <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
          Forum idarəetməsi
        </h2>
        <p className="mt-3 max-w-2xl text-sm leading-6 text-zinc-500">
          Kateqoriyaları, müzakirələrin görünüşünü və silinmiş forum
          kontentini bir yerdən idarə edin.
        </p>
      </section>

      <div className="flex flex-wrap gap-2 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-2">
        {([
          ["discussions", "Müzakirələr"],
          ["categories", "Kateqoriyalar"],
          ["trash", "Silinmiş kontent"],
        ] as const).map(([value, label]) => (
          <button
            key={value}
            type="button"
            onClick={() => setTab(value)}
            className={`rounded-xl px-4 py-2.5 text-sm font-semibold transition ${
              tab === value
                ? "bg-violet-500/15 text-violet-200"
                : "text-zinc-500 hover:bg-white/[0.04] hover:text-zinc-200"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "discussions" && <DiscussionsPanel />}
      {tab === "categories" && <CategoriesPanel />}
      {tab === "trash" && <ForumTrashPanel />}
    </div>
  );
}

function DiscussionsPanel() {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const canDelete = hasAuthRole(accessToken, "Admin", user?.roles ?? []);
  const [error, setError] = useState<string | null>(null);

  const discussionsQuery = useQuery({
    queryKey: ["admin", "forum", "active-discussions"],
    queryFn: ({ signal }) =>
      getForumDiscussions("newest", undefined, signal, undefined, undefined, 1, 50),
  });

  const stateMutation = useMutation({
    mutationFn: ({
      id,
      action,
    }: {
      id: number;
      action: "pin" | "unpin" | "lock" | "unlock";
    }) => setDiscussionState(id, action),
    onMutate: () => setError(null),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: ["admin", "forum", "active-discussions"],
      }),
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Forum əməliyyatı alınmadı.")),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteAdminDiscussion,
    onMutate: () => setError(null),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["admin", "forum", "active-discussions"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "forum", "deleted-discussions"],
        }),
      ]);
    },
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Müzakirə silinmədi.")),
  });

  if (discussionsQuery.isPending) return <PanelLoader />;
  if (discussionsQuery.isError) {
    return <ErrorPanel error={discussionsQuery.error} />;
  }

  const items = discussionsQuery.data?.items ?? [];

  return (
    <section className="space-y-4">
      {error && <InlineError message={error} />}
      {items.map((discussion) => (
        <article
          key={discussion.id}
          className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5"
        >
          <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-center">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2 text-[10px] font-bold uppercase tracking-wider">
                <span className="rounded-full bg-violet-500/10 px-2 py-1 text-violet-300">
                  {discussion.categoryName}
                </span>
                {discussion.isPinned && (
                  <span className="rounded-full bg-amber-500/10 px-2 py-1 text-amber-300">
                    Pin edilib
                  </span>
                )}
                {discussion.isLocked && (
                  <span className="rounded-full bg-red-500/10 px-2 py-1 text-red-300">
                    Bağlıdır
                  </span>
                )}
              </div>
              <Link
                to={`/forum/discussions/${discussion.slug}`}
                className="mt-3 block truncate text-base font-bold text-white transition hover:text-violet-200"
              >
                {discussion.title}
              </Link>
              <p className="mt-2 text-xs text-zinc-600">
                {discussion.authorUserName} · {discussion.replyCount} cavab ·{" "}
                {discussion.viewCount} baxış
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <ActionButton
                label={discussion.isPinned ? "Pini sil" : "Pin et"}
                icon={discussion.isPinned ? <PinOff size={15} /> : <Pin size={15} />}
                disabled={stateMutation.isPending}
                onClick={() =>
                  stateMutation.mutate({
                    id: discussion.id,
                    action: discussion.isPinned ? "unpin" : "pin",
                  })
                }
              />
              <ActionButton
                label={discussion.isLocked ? "Kilidi aç" : "Kilidlə"}
                icon={
                  discussion.isLocked ? <LockOpen size={15} /> : <Lock size={15} />
                }
                disabled={stateMutation.isPending}
                onClick={() =>
                  stateMutation.mutate({
                    id: discussion.id,
                    action: discussion.isLocked ? "unlock" : "lock",
                  })
                }
              />
              {canDelete && (
                <ActionButton
                  label="Sil"
                  icon={<Trash2 size={15} />}
                  danger
                  disabled={deleteMutation.isPending}
                  onClick={() => deleteMutation.mutate(discussion.id)}
                />
              )}
            </div>
          </div>
        </article>
      ))}
      {items.length === 0 && <EmptyPanel text="Aktiv müzakirə yoxdur." />}
    </section>
  );
}

function CategoriesPanel() {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const canDelete = hasAuthRole(accessToken, "Admin", user?.roles ?? []);
  const [editor, setEditor] = useState<ForumCategory | "new" | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const activeQuery = useQuery({
    queryKey: ["admin", "forum", "categories", "active"],
    queryFn: ({ signal }) => getForumCategories(false, signal),
  });
  const deletedQuery = useQuery({
    queryKey: ["admin", "forum", "categories", "deleted"],
    queryFn: ({ signal }) => getForumCategories(true, signal),
  });

  const refresh = () =>
    queryClient.invalidateQueries({
      queryKey: ["admin", "forum", "categories"],
    });

  const saveMutation = useMutation({
    mutationFn: () =>
      editor === "new"
        ? createForumCategory({
            name: name.trim(),
            description: description.trim() || undefined,
          })
        : updateForumCategory(editor!.id, {
            name: name.trim(),
            description: description.trim() || undefined,
          }),
    onMutate: () => setError(null),
    onSuccess: async () => {
      setEditor(null);
      setName("");
      setDescription("");
      await refresh();
    },
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Kateqoriya yadda saxlanmadı.")),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteForumCategory,
    onSuccess: refresh,
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Kateqoriya silinmədi.")),
  });
  const restoreMutation = useMutation({
    mutationFn: restoreForumCategory,
    onSuccess: refresh,
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Kateqoriya bərpa edilmədi.")),
  });

  function openEditor(category: ForumCategory | "new") {
    setEditor(category);
    setName(category === "new" ? "" : category.name);
    setDescription(category === "new" ? "" : category.description ?? "");
    setError(null);
  }

  if (activeQuery.isPending || deletedQuery.isPending) return <PanelLoader />;
  if (activeQuery.isError) return <ErrorPanel error={activeQuery.error} />;

  return (
    <div className="space-y-5">
      <div className="flex justify-end">
        <button
          type="button"
          onClick={() => openEditor("new")}
          className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500"
        >
          <Plus size={17} />
          Yeni kateqoriya
        </button>
      </div>
      {error && <InlineError message={error} />}
      {editor && (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (name.trim()) saveMutation.mutate();
          }}
          className="rounded-2xl border border-violet-400/15 bg-violet-500/[0.04] p-5"
        >
          <div className="flex items-center justify-between gap-3">
            <h3 className="font-bold text-white">
              {editor === "new" ? "Yeni kateqoriya" : "Kateqoriyanı redaktə et"}
            </h3>
            <button
              type="button"
              onClick={() => setEditor(null)}
              className="rounded-lg p-2 text-zinc-500 hover:bg-white/[0.06] hover:text-white"
            >
              <X size={17} />
            </button>
          </div>
          <div className="mt-4 grid gap-3 md:grid-cols-2">
            <input
              value={name}
              maxLength={100}
              onChange={(event) => setName(event.target.value)}
              placeholder="Kateqoriya adı"
              className="h-11 rounded-xl border border-white/[0.08] bg-black/20 px-3 text-sm text-white outline-none focus:border-violet-500/50"
            />
            <input
              value={description}
              maxLength={500}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Qısa açıqlama"
              className="h-11 rounded-xl border border-white/[0.08] bg-black/20 px-3 text-sm text-white outline-none focus:border-violet-500/50"
            />
          </div>
          <button
            type="submit"
            disabled={!name.trim() || saveMutation.isPending}
            className="mt-4 inline-flex h-10 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white disabled:opacity-40"
          >
            {saveMutation.isPending ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <Save size={16} />
            )}
            Yadda saxla
          </button>
        </form>
      )}

      <section className="grid gap-4 lg:grid-cols-2">
        {(activeQuery.data ?? []).map((category) => (
          <article
            key={category.id}
            className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5"
          >
            <div className="flex items-start gap-3">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/10 text-violet-300">
                <FolderCog size={18} />
              </span>
              <div className="min-w-0 flex-1">
                <h3 className="font-bold text-white">{category.name}</h3>
                <p className="mt-1 text-xs text-zinc-600">
                  /{category.slug} · {category.discussionCount} müzakirə
                </p>
                {category.description && (
                  <p className="mt-3 text-sm leading-6 text-zinc-500">
                    {category.description}
                  </p>
                )}
              </div>
            </div>
            <div className="mt-4 flex gap-2">
              <ActionButton
                label="Redaktə et"
                icon={<Pencil size={15} />}
                onClick={() => openEditor(category)}
              />
              {canDelete && (
                <ActionButton
                  label="Sil"
                  icon={<Trash2 size={15} />}
                  danger
                  disabled={deleteMutation.isPending}
                  onClick={() => deleteMutation.mutate(category.id)}
                />
              )}
            </div>
          </article>
        ))}
      </section>

      {(deletedQuery.data?.length ?? 0) > 0 && (
        <section>
          <h3 className="mb-3 text-sm font-bold text-zinc-400">
            Silinmiş kateqoriyalar
          </h3>
          <div className="space-y-3">
            {deletedQuery.data!.map((category) => (
              <div
                key={category.id}
                className="flex items-center justify-between gap-4 rounded-xl border border-white/[0.06] bg-white/[0.02] p-4"
              >
                <div>
                  <p className="font-semibold text-zinc-300">{category.name}</p>
                  <p className="mt-1 text-xs text-zinc-600">/{category.slug}</p>
                </div>
                <ActionButton
                  label="Bərpa et"
                  icon={<ArchiveRestore size={15} />}
                  disabled={restoreMutation.isPending}
                  onClick={() => restoreMutation.mutate(category.id)}
                />
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

function ForumTrashPanel() {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const discussionsQuery = useQuery({
    queryKey: ["admin", "forum", "deleted-discussions"],
    queryFn: ({ signal }) =>
      getDeletedDiscussions({ pageNumber: 1, pageSize: 50 }, signal),
  });
  const repliesQuery = useQuery({
    queryKey: ["admin", "forum", "deleted-replies"],
    queryFn: ({ signal }) =>
      getDeletedDiscussionReplies({ pageNumber: 1, pageSize: 50 }, signal),
  });

  const restoreDiscussionMutation = useMutation({
    mutationFn: (id: number) => setDiscussionState(id, "restore"),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["admin", "forum"],
      });
    },
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Müzakirə bərpa edilmədi.")),
  });
  const restoreReplyMutation = useMutation({
    mutationFn: restoreDiscussionReply,
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: ["admin", "forum", "deleted-replies"],
      }),
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Cavab bərpa edilmədi.")),
  });

  if (discussionsQuery.isPending || repliesQuery.isPending) {
    return <PanelLoader />;
  }
  if (discussionsQuery.isError) return <ErrorPanel error={discussionsQuery.error} />;

  const discussions = discussionsQuery.data?.items ?? [];
  const replies = repliesQuery.data?.items ?? [];

  return (
    <div className="space-y-6">
      {error && <InlineError message={error} />}
      <TrashSection title="Silinmiş müzakirələr" count={discussions.length}>
        {discussions.map((discussion) => (
          <div
            key={discussion.id}
            className="flex flex-col justify-between gap-4 rounded-xl border border-white/[0.06] bg-white/[0.02] p-4 sm:flex-row sm:items-center"
          >
            <div>
              <p className="font-semibold text-zinc-300">{discussion.title}</p>
              <p className="mt-1 text-xs text-zinc-600">
                {discussion.authorUserName} · {formatDateTime(discussion.createdAt)}
              </p>
            </div>
            <ActionButton
              label="Bərpa et"
              icon={<ArchiveRestore size={15} />}
              disabled={restoreDiscussionMutation.isPending}
              onClick={() => restoreDiscussionMutation.mutate(discussion.id)}
            />
          </div>
        ))}
        {discussions.length === 0 && (
          <EmptyPanel text="Silinmiş müzakirə yoxdur." />
        )}
      </TrashSection>

      <TrashSection title="Silinmiş cavablar" count={replies.length}>
        {replies.map((reply) => (
          <div
            key={reply.id}
            className="flex flex-col justify-between gap-4 rounded-xl border border-white/[0.06] bg-white/[0.02] p-4 sm:flex-row sm:items-center"
          >
            <div className="min-w-0">
              <p className="line-clamp-2 text-sm text-zinc-300">{reply.content}</p>
              <p className="mt-1 text-xs text-zinc-600">
                {reply.authorUserName} · {formatDateTime(reply.createdAt)}
              </p>
            </div>
            <ActionButton
              label="Bərpa et"
              icon={<ArchiveRestore size={15} />}
              disabled={restoreReplyMutation.isPending}
              onClick={() => restoreReplyMutation.mutate(reply.id)}
            />
          </div>
        ))}
        {replies.length === 0 && <EmptyPanel text="Silinmiş cavab yoxdur." />}
      </TrashSection>
    </div>
  );
}

function TrashSection({
  title,
  count,
  children,
}: {
  title: string;
  count: number;
  children: ReactNode;
}) {
  return (
    <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5">
      <div className="mb-4 flex items-center gap-3">
        <MessageSquareText size={18} className="text-violet-300" />
        <h3 className="font-bold text-white">{title}</h3>
        <span className="rounded-full bg-white/[0.05] px-2 py-1 text-xs text-zinc-500">
          {count}
        </span>
      </div>
      <div className="space-y-3">{children}</div>
    </section>
  );
}

function ActionButton({
  label,
  icon,
  onClick,
  disabled,
  danger,
}: {
  label: string;
  icon: ReactNode;
  onClick: () => void;
  disabled?: boolean;
  danger?: boolean;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`inline-flex h-9 items-center gap-2 rounded-lg border px-3 text-xs font-semibold transition disabled:opacity-40 ${
        danger
          ? "border-red-400/15 bg-red-500/[0.06] text-red-200 hover:bg-red-500/10"
          : "border-white/[0.08] bg-white/[0.025] text-zinc-300 hover:bg-white/[0.06]"
      }`}
    >
      {icon}
      {label}
    </button>
  );
}

function PanelLoader() {
  return (
    <div className="flex min-h-48 items-center justify-center rounded-2xl border border-white/[0.07] bg-white/[0.025]">
      <LoaderCircle size={24} className="animate-spin text-violet-300" />
    </div>
  );
}

function ErrorPanel({ error }: { error: unknown }) {
  return (
    <InlineError
      message={getApiErrorMessage(error, "Forum məlumatları yüklənmədi.")}
    />
  );
}

function InlineError({ message }: { message: string }) {
  return (
    <div className="flex items-start gap-3 rounded-xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
      <CircleAlert size={18} className="mt-0.5 shrink-0" />
      {message}
    </div>
  );
}

function EmptyPanel({ text }: { text: string }) {
  return (
    <div className="rounded-xl border border-dashed border-white/[0.08] px-5 py-10 text-center text-sm text-zinc-600">
      {text}
    </div>
  );
}
