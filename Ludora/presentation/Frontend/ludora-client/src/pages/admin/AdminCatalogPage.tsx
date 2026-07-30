import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArchiveRestore,
  Building2,
  CircleAlert,
  Gamepad2,
  Layers3,
  LoaderCircle,
  Pencil,
  Plus,
  Save,
  Tags,
  Trash2,
  X,
} from "lucide-react";
import type { ReactNode } from "react";
import { useState } from "react";
import { getApiErrorMessage } from "../../lib/api";
import { hasAuthRole } from "../../lib/authRoles";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  addAdminGameRelation,
  createAdminGame,
  createCatalogItem,
  deleteAdminGame,
  deleteCatalogItem,
  getDeletedAdminGames,
  getDeletedCatalogItems,
  restoreAdminGame,
  restoreCatalogItem,
  updateAdminGame,
  updateCatalogItem,
  type AdminCatalogKind,
  type AdminGameInput,
} from "../../services/adminCatalogService";
import {
  getGameById,
  getGameCompanies,
  getGameGenres,
  getGamePlatforms,
  getGames,
} from "../../services/gamesService";
import { useAuthStore } from "../../stores/authStore";
import type {
  CatalogFilterItemDTO,
  GameDetailDTO,
} from "../../types/games";
import type {
  CatalogSource,
  GameType,
} from "../../types/home";

type CatalogTab = "games" | AdminCatalogKind;

const inputClass =
  "h-11 w-full rounded-xl border border-white/[0.08] bg-black/20 px-3 text-sm text-white outline-none focus:border-violet-500/50";

const gameTypes: GameType[] = [
  "MainGame",
  "DlcAddon",
  "Expansion",
  "Bundle",
  "StandaloneExpansion",
  "Mod",
  "Episode",
  "Season",
  "Remake",
  "Remaster",
  "ExpandedGame",
  "Port",
  "Fork",
  "Pack",
  "Update",
];

const emptyGameForm: AdminGameInput = {
  gameType: "MainGame",
  title: "",
  description: "",
  releaseDate: "",
  coverImageUrl: "",
  companyReferences: [],
  genreReferences: [],
  platformReferences: [],
};

export function AdminCatalogPage() {
  const [tab, setTab] = useState<CatalogTab>("games");
  const tabs: Array<{
    value: CatalogTab;
    label: string;
    icon: ReactNode;
  }> = [
    { value: "games", label: "Oyunlar", icon: <Gamepad2 size={16} /> },
    { value: "genres", label: "Janrlar", icon: <Tags size={16} /> },
    {
      value: "platforms",
      label: "Platformalar",
      icon: <Layers3 size={16} />,
    },
    { value: "companies", label: "Şirkətlər", icon: <Building2 size={16} /> },
  ];

  return (
    <div className="space-y-6">
      <section>
        <p className="text-sm font-medium text-violet-300">
          Məzmun kataloqu
        </p>
        <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
          Kataloq idarəetməsi
        </h2>
        <p className="mt-3 max-w-2xl text-sm leading-6 text-zinc-500">
          Local və IGDB mənbəli oyun, janr, platforma və şirkət
          məlumatlarını idarə edin.
        </p>
      </section>

      <div className="flex flex-wrap gap-2 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-2">
        {tabs.map((item) => (
          <button
            key={item.value}
            type="button"
            onClick={() => setTab(item.value)}
            className={`inline-flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-semibold transition ${
              tab === item.value
                ? "bg-violet-500/15 text-violet-200"
                : "text-zinc-500 hover:bg-white/[0.04] hover:text-zinc-200"
            }`}
          >
            {item.icon}
            {item.label}
          </button>
        ))}
      </div>

      {tab === "games" ? <GamesCatalogPanel /> : <SimpleCatalogPanel kind={tab} />}
    </div>
  );
}

function GamesCatalogPanel() {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const canDelete = hasAuthRole(accessToken, "Admin", user?.roles ?? []);
  const [editor, setEditor] = useState<GameDetailDTO | "new" | null>(null);
  const [form, setForm] = useState<AdminGameInput>(emptyGameForm);
  const [loadingGameId, setLoadingGameId] = useState<number | null>(null);
  const [relatedSource, setRelatedSource] = useState<CatalogSource>("Igdb");
  const [relatedId, setRelatedId] = useState("");
  const [relationType, setRelationType] = useState<GameType>("DlcAddon");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const activeQuery = useQuery({
    queryKey: ["admin", "catalog", "games", "active"],
    queryFn: ({ signal }) =>
      getGames(
        {
          pageNumber: 1,
          pageSize: 50,
          releasedOnly: false,
          includeRelatedContent: true,
        },
        signal,
      ),
  });
  const deletedQuery = useQuery({
    queryKey: ["admin", "catalog", "games", "deleted"],
    queryFn: ({ signal }) => getDeletedAdminGames(signal),
  });

  const refresh = () =>
    queryClient.invalidateQueries({
      queryKey: ["admin", "catalog", "games"],
    });

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (editor === "new") {
        await createAdminGame(form);
        return;
      }

      await updateAdminGame(editor!.source, editor!.id, form);
    },
    onMutate: () => {
      setError(null);
      setNotice(null);
    },
    onSuccess: async () => {
      setEditor(null);
      setForm(emptyGameForm);
      setNotice("Oyun məlumatı yadda saxlanıldı.");
      await refresh();
    },
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Oyun yadda saxlanmadı.")),
  });

  const deleteMutation = useMutation({
    mutationFn: ({
      source,
      id,
    }: {
      source: CatalogSource;
      id: number;
    }) => deleteAdminGame(source, id),
    onSuccess: refresh,
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Oyun silinmədi.")),
  });

  const restoreMutation = useMutation({
    mutationFn: ({
      source,
      id,
    }: {
      source: CatalogSource;
      id: number;
    }) => restoreAdminGame(source, id),
    onSuccess: refresh,
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Oyun bərpa edilmədi.")),
  });

  const relationMutation = useMutation({
    mutationFn: () => {
      if (!editor || editor === "new") {
        throw new Error("Əvvəlcə oyunu yadda saxlayın.");
      }
      return addAdminGameRelation(editor.source, editor.id, {
        relatedSource,
        relatedId: Number(relatedId),
        relationType,
      });
    },
    onSuccess: () => {
      setRelatedId("");
      setNotice("Əlaqəli oyun əlavə edildi.");
    },
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(mutationError, "Əlaqəli oyun əlavə edilmədi."),
      ),
  });

  async function openEditor(source: CatalogSource, id: number) {
    setLoadingGameId(id);
    setError(null);
    try {
      const detail = await getGameById(source, id);
      setEditor(detail);
      setForm({
        gameType: detail.gameType,
        title: detail.title,
        description: detail.description ?? "",
        releaseDate: detail.releaseDate ?? "",
        coverImageUrl: detail.coverImageUrl ?? "",
        companyReferences: detail.companies.map((company) => ({
          source: company.source,
          id: company.id,
          role: company.role,
        })),
        genreReferences: detail.genres.map((genre) => ({
          source: genre.source,
          id: genre.id,
        })),
        platformReferences: detail.platforms.map((platform) => ({
          source: platform.source,
          id: platform.id,
        })),
      });
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, "Oyun detalı yüklənmədi."));
    } finally {
      setLoadingGameId(null);
    }
  }

  const activeItems = activeQuery.data?.items ?? [];
  const deletedItems = deletedQuery.data ?? [];

  return (
    <div className="space-y-5">
      <div className="flex justify-end">
        <button
          type="button"
          onClick={() => {
            setEditor("new");
            setForm({ ...emptyGameForm });
          }}
          className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white"
        >
          <Plus size={17} />
          Yeni oyun
        </button>
      </div>
      {notice && <SuccessPanel message={notice} />}
      {error && <ErrorPanel message={error} />}
      {editor && (
        <GameEditor
          editor={editor}
          form={form}
          setForm={setForm}
          isSaving={saveMutation.isPending}
          onClose={() => setEditor(null)}
          onSave={() => saveMutation.mutate()}
          relatedSource={relatedSource}
          setRelatedSource={setRelatedSource}
          relatedId={relatedId}
          setRelatedId={setRelatedId}
          relationType={relationType}
          setRelationType={setRelationType}
          relationPending={relationMutation.isPending}
          onAddRelation={() => relationMutation.mutate()}
        />
      )}
      {activeQuery.isPending ? (
        <LoadingPanel />
      ) : activeQuery.isError ? (
        <ErrorPanel
          message={getApiErrorMessage(
            activeQuery.error,
            "Oyun kataloqu yüklənmədi.",
          )}
        />
      ) : (
        <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {activeItems.map((game) => (
            <article
              key={`${game.source}-${game.id}`}
              className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4"
            >
              <div className="flex gap-4">
                {resolveAssetUrl(game.coverImageUrl) ? (
                  <img
                    src={resolveAssetUrl(game.coverImageUrl)!}
                    alt=""
                    className="h-24 w-16 rounded-lg object-cover"
                  />
                ) : (
                  <span className="flex h-24 w-16 items-center justify-center rounded-lg bg-white/[0.04] text-zinc-700">
                    <Gamepad2 size={20} />
                  </span>
                )}
                <div className="min-w-0 flex-1">
                  <p className="text-[10px] font-bold uppercase tracking-wider text-violet-300">
                    {game.source} · {game.gameType}
                  </p>
                  <h3 className="mt-2 line-clamp-2 font-bold text-white">
                    {game.title}
                  </h3>
                  <p className="mt-2 text-xs text-zinc-600">
                    {game.releaseDate ?? "Tarix yoxdur"}
                  </p>
                </div>
              </div>
              <div className="mt-4 flex gap-2">
                <SmallButton
                  label="Redaktə et"
                  icon={
                    loadingGameId === game.id ? (
                      <LoaderCircle size={14} className="animate-spin" />
                    ) : (
                      <Pencil size={14} />
                    )
                  }
                  disabled={loadingGameId !== null}
                  onClick={() => openEditor(game.source, game.id)}
                />
                {canDelete && (
                  <SmallButton
                    label="Sil"
                    icon={<Trash2 size={14} />}
                    danger
                    disabled={deleteMutation.isPending}
                    onClick={() =>
                      deleteMutation.mutate({
                        source: game.source,
                        id: game.id,
                      })
                    }
                  />
                )}
              </div>
            </article>
          ))}
        </section>
      )}

      {deletedItems.length > 0 && (
        <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5">
          <h3 className="font-bold text-white">Silinmiş oyunlar</h3>
          <div className="mt-4 space-y-3">
            {deletedItems.map((game) => (
              <div
                key={`${game.source}-${game.id}`}
                className="flex flex-col justify-between gap-3 rounded-xl border border-white/[0.06] bg-black/15 p-4 sm:flex-row sm:items-center"
              >
                <div>
                  <p className="font-semibold text-zinc-300">{game.title}</p>
                  <p className="mt-1 text-xs text-zinc-600">
                    {game.source} #{game.id}
                  </p>
                </div>
                <SmallButton
                  label="Bərpa et"
                  icon={<ArchiveRestore size={14} />}
                  disabled={restoreMutation.isPending}
                  onClick={() =>
                    restoreMutation.mutate({
                      source: game.source,
                      id: game.id,
                    })
                  }
                />
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

function GameEditor({
  editor,
  form,
  setForm,
  isSaving,
  onClose,
  onSave,
  relatedSource,
  setRelatedSource,
  relatedId,
  setRelatedId,
  relationType,
  setRelationType,
  relationPending,
  onAddRelation,
}: {
  editor: GameDetailDTO | "new";
  form: AdminGameInput;
  setForm: (value: AdminGameInput) => void;
  isSaving: boolean;
  onClose: () => void;
  onSave: () => void;
  relatedSource: CatalogSource;
  setRelatedSource: (value: CatalogSource) => void;
  relatedId: string;
  setRelatedId: (value: string) => void;
  relationType: GameType;
  setRelationType: (value: GameType) => void;
  relationPending: boolean;
  onAddRelation: () => void;
}) {
  return (
    <section className="rounded-2xl border border-violet-400/15 bg-violet-500/[0.04] p-5">
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-white">
          {editor === "new" ? "Yeni oyun" : "Oyunu redaktə et"}
        </h3>
        <button type="button" onClick={onClose} className="p-2 text-zinc-500">
          <X size={17} />
        </button>
      </div>
      <div className="mt-5 grid gap-4 md:grid-cols-2">
        <Field label="Başlıq">
          <input
            value={form.title}
            onChange={(event) => setForm({ ...form, title: event.target.value })}
            className={inputClass}
          />
        </Field>
        <Field label="Kontent növü">
          <select
            value={form.gameType}
            onChange={(event) =>
              setForm({ ...form, gameType: event.target.value as GameType })
            }
            className={inputClass}
          >
            {gameTypes.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Buraxılış tarixi">
          <input
            type="date"
            value={form.releaseDate ?? ""}
            onChange={(event) =>
              setForm({ ...form, releaseDate: event.target.value })
            }
            className={inputClass}
          />
        </Field>
        <Field label="Cover URL">
          <input
            value={form.coverImageUrl ?? ""}
            onChange={(event) =>
              setForm({ ...form, coverImageUrl: event.target.value })
            }
            className={inputClass}
          />
        </Field>
        <Field label="Açıqlama" wide>
          <textarea
            value={form.description ?? ""}
            rows={4}
            onChange={(event) =>
              setForm({ ...form, description: event.target.value })
            }
            className="w-full resize-none rounded-xl border border-white/[0.08] bg-black/20 p-3 text-sm text-white outline-none focus:border-violet-500/50"
          />
        </Field>
      </div>
      <button
        type="button"
        disabled={!form.title.trim() || isSaving}
        onClick={onSave}
        className="mt-5 inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white disabled:opacity-40"
      >
        {isSaving ? (
          <LoaderCircle size={16} className="animate-spin" />
        ) : (
          <Save size={16} />
        )}
        Yadda saxla
      </button>

      {editor !== "new" && (
        <div className="mt-6 border-t border-white/[0.07] pt-5">
          <h4 className="text-sm font-bold text-white">Əlaqəli oyun əlavə et</h4>
          <div className="mt-3 grid gap-3 sm:grid-cols-[8rem_1fr_13rem_auto]">
            <select
              value={relatedSource}
              onChange={(event) =>
                setRelatedSource(event.target.value as CatalogSource)
              }
              className={inputClass}
            >
              <option value="Local">Local</option>
              <option value="Igdb">IGDB</option>
            </select>
            <input
              type="number"
              min={1}
              value={relatedId}
              onChange={(event) => setRelatedId(event.target.value)}
              placeholder="Oyun ID"
              className={inputClass}
            />
            <select
              value={relationType}
              onChange={(event) =>
                setRelationType(event.target.value as GameType)
              }
              className={inputClass}
            >
              {gameTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
            <button
              type="button"
              disabled={!relatedId || relationPending}
              onClick={onAddRelation}
              className="h-11 rounded-xl border border-violet-400/20 px-4 text-sm font-bold text-violet-200 disabled:opacity-40"
            >
              Əlavə et
            </button>
          </div>
        </div>
      )}
    </section>
  );
}

function SimpleCatalogPanel({ kind }: { kind: AdminCatalogKind }) {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const canDelete = hasAuthRole(accessToken, "Admin", user?.roles ?? []);
  const [editor, setEditor] = useState<CatalogFilterItemDTO | "new" | null>(null);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const activeQuery = useQuery({
    queryKey: ["admin", "catalog", kind, "active"],
    queryFn: ({ signal }) => loadCatalog(kind, signal),
  });
  const deletedQuery = useQuery({
    queryKey: ["admin", "catalog", kind, "deleted"],
    queryFn: ({ signal }) => getDeletedCatalogItems(kind, signal),
  });
  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "catalog", kind] });

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (editor === "new") {
        await createCatalogItem(kind, name.trim());
        return;
      }

      await updateCatalogItem(
        kind,
        editor!.source,
        editor!.id,
        name.trim(),
      );
    },
    onSuccess: async () => {
      setEditor(null);
      setName("");
      await refresh();
    },
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Məlumat yadda saxlanmadı.")),
  });
  const deleteMutation = useMutation({
    mutationFn: (item: CatalogFilterItemDTO) =>
      deleteCatalogItem(kind, item.source, item.id),
    onSuccess: refresh,
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Məlumat silinmədi.")),
  });
  const restoreMutation = useMutation({
    mutationFn: (item: CatalogFilterItemDTO) =>
      restoreCatalogItem(kind, item.source, item.id),
    onSuccess: refresh,
    onError: (mutationError) =>
      setError(getApiErrorMessage(mutationError, "Məlumat bərpa edilmədi.")),
  });

  if (activeQuery.isPending || deletedQuery.isPending) return <LoadingPanel />;

  return (
    <div className="space-y-5">
      <div className="flex justify-end">
        <button
          type="button"
          onClick={() => {
            setEditor("new");
            setName("");
          }}
          className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white"
        >
          <Plus size={17} />
          Yeni əlavə et
        </button>
      </div>
      {error && <ErrorPanel message={error} />}
      {editor && (
        <div className="flex flex-col gap-3 rounded-2xl border border-violet-400/15 bg-violet-500/[0.04] p-5 sm:flex-row">
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Ad"
            className={`${inputClass} flex-1`}
          />
          <button
            type="button"
            disabled={!name.trim() || saveMutation.isPending}
            onClick={() => saveMutation.mutate()}
            className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white disabled:opacity-40"
          >
            <Save size={16} />
            Yadda saxla
          </button>
          <button
            type="button"
            onClick={() => setEditor(null)}
            className="h-11 rounded-xl border border-white/[0.08] px-4 text-zinc-400"
          >
            Ləğv et
          </button>
        </div>
      )}
      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {(activeQuery.data ?? []).map((item) => (
          <article
            key={`${item.source}-${item.id}`}
            className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5"
          >
            <p className="text-[10px] font-bold uppercase tracking-wider text-violet-300">
              {item.source} #{item.id}
            </p>
            <h3 className="mt-2 font-bold text-white">{item.name}</h3>
            <div className="mt-4 flex gap-2">
              <SmallButton
                label="Redaktə et"
                icon={<Pencil size={14} />}
                onClick={() => {
                  setEditor(item);
                  setName(item.name);
                }}
              />
              {canDelete && (
                <SmallButton
                  label="Sil"
                  icon={<Trash2 size={14} />}
                  danger
                  onClick={() => deleteMutation.mutate(item)}
                />
              )}
            </div>
          </article>
        ))}
      </section>
      {(deletedQuery.data?.length ?? 0) > 0 && (
        <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5">
          <h3 className="font-bold text-white">Silinmiş qeydlər</h3>
          <div className="mt-4 space-y-3">
            {deletedQuery.data!.map((item) => (
              <div
                key={`${item.source}-${item.id}`}
                className="flex items-center justify-between gap-3 rounded-xl border border-white/[0.06] bg-black/15 p-4"
              >
                <span className="font-semibold text-zinc-300">{item.name}</span>
                <SmallButton
                  label="Bərpa et"
                  icon={<ArchiveRestore size={14} />}
                  onClick={() => restoreMutation.mutate(item)}
                />
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

async function loadCatalog(
  kind: AdminCatalogKind,
  signal?: AbortSignal,
) {
  if (kind === "genres") return getGameGenres(signal);
  if (kind === "platforms") return getGamePlatforms(signal);
  return getGameCompanies(undefined, signal);
}

function Field({
  label,
  wide,
  children,
}: {
  label: string;
  wide?: boolean;
  children: ReactNode;
}) {
  return (
    <label className={wide ? "md:col-span-2" : undefined}>
      <span className="mb-1.5 block text-xs font-medium text-zinc-500">
        {label}
      </span>
      {children}
    </label>
  );
}

function SmallButton({
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
      onClick={onClick}
      disabled={disabled}
      className={`inline-flex h-9 items-center gap-2 rounded-lg border px-3 text-xs font-semibold transition disabled:opacity-40 ${
        danger
          ? "border-red-400/15 bg-red-500/[0.06] text-red-200"
          : "border-white/[0.08] bg-white/[0.025] text-zinc-300 hover:bg-white/[0.06]"
      }`}
    >
      {icon}
      {label}
    </button>
  );
}

function LoadingPanel() {
  return (
    <div className="flex min-h-48 items-center justify-center rounded-2xl border border-white/[0.07] bg-white/[0.025]">
      <LoaderCircle size={24} className="animate-spin text-violet-300" />
    </div>
  );
}

function ErrorPanel({ message }: { message: string }) {
  return (
    <div className="flex items-start gap-3 rounded-xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
      <CircleAlert size={18} className="mt-0.5 shrink-0" />
      {message}
    </div>
  );
}

function SuccessPanel({ message }: { message: string }) {
  return (
    <div className="rounded-xl border border-emerald-400/15 bg-emerald-500/[0.06] p-4 text-sm text-emerald-200">
      {message}
    </div>
  );
}
