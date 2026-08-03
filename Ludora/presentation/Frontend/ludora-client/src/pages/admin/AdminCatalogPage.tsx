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
  ImagePlus,
  Layers3,
  LoaderCircle,
  Pencil,
  Plus,
  Save,
  Search,
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
  uploadAdminGameCover,
  type AdminCatalogKind,
  type AdminGameInput,
  type CatalogReferenceInput,
  type CompanyReferenceInput,
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

const relationGameTypes: GameType[] = [
  "DlcAddon",
  "Expansion",
  "StandaloneExpansion",
  "Bundle",
  "Remake",
  "Remaster",
];

const gameTypesWithParent = new Set<GameType>(relationGameTypes);

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
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [search, setSearch] = useState("");
  const [loadingGameId, setLoadingGameId] = useState<number | null>(null);
  const [relatedSource, setRelatedSource] = useState<CatalogSource>("Igdb");
  const [relatedId, setRelatedId] = useState("");
  const [relationType, setRelationType] = useState<GameType>("DlcAddon");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const normalizedSearch = search.trim();
  const activeQuery = useQuery({
    queryKey: ["admin", "catalog", "games", "active", normalizedSearch],
    queryFn: ({ signal }) =>
      getGames(
        {
          searchName: normalizedSearch || undefined,
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
      const coverImageUrl = coverFile
        ? await uploadAdminGameCover(coverFile)
        : form.coverImageUrl;
      const request: AdminGameInput = {
        ...form,
        coverImageUrl,
      };

      if (editor === "new") {
        await createAdminGame(request);
        return;
      }

      await updateAdminGame(editor!.source, editor!.id, request);
    },
    onMutate: () => {
      setError(null);
      setNotice(null);
    },
    onSuccess: async () => {
      setEditor(null);
      setForm(emptyGameForm);
      setCoverFile(null);
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
    setCoverFile(null);
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
        parentGameSource: detail.relatedGames.parentGame?.source,
        parentGameId: detail.relatedGames.parentGame?.id,
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
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <label className="relative block w-full sm:max-w-md">
          <Search
            size={17}
            className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-zinc-600"
          />
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Oyunun adına görə axtar..."
            className={`${inputClass} pl-10`}
          />
        </label>
        <button
          type="button"
          onClick={() => {
            setEditor("new");
            setForm({ ...emptyGameForm });
            setCoverFile(null);
          }}
          className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white"
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
          coverFile={coverFile}
          setCoverFile={setCoverFile}
          isSaving={saveMutation.isPending}
          onClose={() => {
            setEditor(null);
            setCoverFile(null);
          }}
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
          {activeItems.length === 0 && (
            <p className="col-span-full rounded-2xl border border-dashed border-white/[0.08] px-5 py-12 text-center text-sm text-zinc-600">
              {normalizedSearch
                ? "Bu ada uyğun oyun tapılmadı."
                : "Aktiv oyun tapılmadı."}
            </p>
          )}
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
  coverFile,
  setCoverFile,
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
  coverFile: File | null;
  setCoverFile: (value: File | null) => void;
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
  const [parentSearch, setParentSearch] = useState("");
  const [genreSearch, setGenreSearch] = useState("");
  const [platformSearch, setPlatformSearch] = useState("");
  const [companySearch, setCompanySearch] = useState("");
  const requiresParent = gameTypesWithParent.has(form.gameType);
  const parentGamesQuery = useQuery({
    queryKey: ["admin", "catalog", "games", "parent-options", parentSearch],
    queryFn: ({ signal }) =>
      getGames(
        {
          searchName: parentSearch,
          sortBy: "name_asc",
          includeRelatedContent: false,
          pageNumber: 1,
          pageSize: 30,
        },
        signal,
      ),
    enabled: requiresParent,
  });
  const parentCandidates = (parentGamesQuery.data?.items ?? []).filter(
    (game) =>
      game.gameType === "MainGame" &&
      !(
        editor !== "new" &&
        game.source === editor.source &&
        game.id === editor.id
      ),
  );
  const selectedParentValue =
    form.parentGameSource && form.parentGameId
      ? `${form.parentGameSource}:${form.parentGameId}`
      : "";
  const selectedParentInResults = parentCandidates.some(
    (game) =>
      game.source === form.parentGameSource && game.id === form.parentGameId,
  );
  const initialParent = editor === "new" ? null : editor.relatedGames.parentGame;
  const genresQuery = useQuery({
    queryKey: ["admin", "catalog", "genres", "game-editor-options"],
    queryFn: ({ signal }) => getGameGenres(signal),
  });
  const platformsQuery = useQuery({
    queryKey: ["admin", "catalog", "platforms", "game-editor-options"],
    queryFn: ({ signal }) => getGamePlatforms(signal),
  });
  const companiesQuery = useQuery({
    queryKey: ["admin", "catalog", "companies", "game-editor-options"],
    queryFn: ({ signal }) => getGameCompanies(undefined, signal),
  });

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
            onChange={(event) => {
              const gameType = event.target.value as GameType;
              setForm({
                ...form,
                gameType,
                parentGameSource: gameTypesWithParent.has(gameType)
                  ? form.parentGameSource
                  : undefined,
                parentGameId: gameTypesWithParent.has(gameType)
                  ? form.parentGameId
                  : undefined,
              });
            }}
            className={inputClass}
          >
            {gameTypes.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </Field>
        {requiresParent && (
          <Field label="Aid olduğu əsas oyun" wide>
            <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
              <input
                type="search"
                value={parentSearch}
                onChange={(event) => setParentSearch(event.target.value)}
                placeholder="Əsas oyunu adına görə axtar"
                className={inputClass}
              />
              <select
                value={selectedParentValue}
                onChange={(event) => {
                  const [source, id] = event.target.value.split(":");
                  setForm({
                    ...form,
                    parentGameSource: source
                      ? (source as CatalogSource)
                      : undefined,
                    parentGameId: id ? Number(id) : undefined,
                  });
                }}
                className={inputClass}
              >
                <option value="">
                  {parentGamesQuery.isPending
                    ? "Əsas oyunlar yüklənir..."
                    : "Əsas oyunu seç"}
                </option>
                {selectedParentValue && !selectedParentInResults && (
                  <option value={selectedParentValue}>
                    {initialParent?.source === form.parentGameSource &&
                    initialParent?.id === form.parentGameId
                      ? (initialParent?.title ?? selectedParentValue)
                      : `${form.parentGameSource} #${form.parentGameId}`}
                  </option>
                )}
                {parentCandidates.map((game) => (
                  <option
                    key={`${game.source}-${game.id}`}
                    value={`${game.source}:${game.id}`}
                  >
                    {game.title} ({game.source})
                  </option>
                ))}
              </select>
            </div>
            <span className="mt-2 block text-xs leading-5 text-zinc-600">
              Yaradılan kontent seçilmiş oyunun Related bölməsində avtomatik
              görünəcək.
            </span>
            {parentGamesQuery.isError && (
              <span className="mt-2 block text-xs text-red-300">
                Əsas oyun siyahısı yüklənmədi. Axtarışı yenidən sınayın.
              </span>
            )}
          </Field>
        )}
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
        <Field label="Oyun şəkli">
          <label className="flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border border-dashed border-violet-300/20 bg-black/20 px-3 py-2.5 text-sm text-zinc-400 transition hover:border-violet-300/40 hover:bg-violet-500/[0.04]">
            <ImagePlus size={18} className="shrink-0 text-violet-300" />
            <span className="min-w-0 flex-1 truncate">
              {coverFile?.name ??
                (form.coverImageUrl
                  ? "Mövcud şəkli dəyişmək üçün fayl seç"
                  : "JPEG, PNG və ya WEBP faylı seç")}
            </span>
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="sr-only"
              onChange={(event) => {
                setCoverFile(event.target.files?.[0] ?? null);
                event.currentTarget.value = "";
              }}
            />
          </label>
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
        <CatalogReferencePicker
          label="Janrlar"
          searchPlaceholder="Janr axtar..."
          items={genresQuery.data ?? []}
          selected={form.genreReferences}
          search={genreSearch}
          setSearch={setGenreSearch}
          isLoading={genresQuery.isPending}
          isError={genresQuery.isError}
          onChange={(genreReferences) =>
            setForm({ ...form, genreReferences })
          }
        />
        <CatalogReferencePicker
          label="Platformalar"
          searchPlaceholder="Platforma axtar..."
          items={platformsQuery.data ?? []}
          selected={form.platformReferences}
          search={platformSearch}
          setSearch={setPlatformSearch}
          isLoading={platformsQuery.isPending}
          isError={platformsQuery.isError}
          onChange={(platformReferences) =>
            setForm({ ...form, platformReferences })
          }
        />
        <CompanyReferencePicker
          items={companiesQuery.data ?? []}
          selected={form.companyReferences}
          search={companySearch}
          setSearch={setCompanySearch}
          isLoading={companiesQuery.isPending}
          isError={companiesQuery.isError}
          onChange={(companyReferences) =>
            setForm({ ...form, companyReferences })
          }
        />
      </div>
      <button
        type="button"
        disabled={
          !form.title.trim() ||
          isSaving ||
          (requiresParent &&
            (!form.parentGameSource || !form.parentGameId))
        }
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
              {relationGameTypes.map((type) => (
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

function CatalogReferencePicker({
  label,
  searchPlaceholder,
  items,
  selected,
  search,
  setSearch,
  isLoading,
  isError,
  onChange,
}: {
  label: string;
  searchPlaceholder: string;
  items: CatalogFilterItemDTO[];
  selected: CatalogReferenceInput[];
  search: string;
  setSearch: (value: string) => void;
  isLoading: boolean;
  isError: boolean;
  onChange: (value: CatalogReferenceInput[]) => void;
}) {
  const selectedKeys = new Set(
    selected.map((reference) => catalogReferenceKey(reference)),
  );
  const normalizedSearch = search.trim().toLocaleLowerCase("az-AZ");
  const filteredItems = items.filter((item) =>
    item.name.toLocaleLowerCase("az-AZ").includes(normalizedSearch),
  );

  return (
    <div>
      <span className="mb-1.5 block text-xs font-medium text-zinc-500">
        {label}
      </span>
      <div className="overflow-hidden rounded-xl border border-white/[0.08] bg-black/20">
        <div className="relative border-b border-white/[0.07]">
          <Search
            size={15}
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
          />
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={searchPlaceholder}
            className="h-10 w-full bg-transparent pl-9 pr-3 text-sm text-white outline-none"
          />
        </div>
        <div className="max-h-48 space-y-1 overflow-y-auto p-2">
          {isLoading ? (
            <PickerStatus message="Siyahı yüklənir..." loading />
          ) : isError ? (
            <PickerStatus message="Siyahı yüklənmədi." error />
          ) : filteredItems.length === 0 ? (
            <PickerStatus message="Uyğun nəticə tapılmadı." />
          ) : (
            filteredItems.map((item) => {
              const key = catalogReferenceKey(item);
              const isSelected = selectedKeys.has(key);

              return (
                <label
                  key={key}
                  className="flex cursor-pointer items-center gap-3 rounded-lg px-2.5 py-2 text-sm transition hover:bg-white/[0.04]"
                >
                  <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={() =>
                      onChange(
                        isSelected
                          ? selected.filter(
                              (reference) =>
                                catalogReferenceKey(reference) !== key,
                            )
                          : [
                              ...selected,
                              { source: item.source, id: item.id },
                            ],
                      )
                    }
                    className="h-4 w-4 accent-violet-500"
                  />
                  <span className="min-w-0 flex-1 truncate text-zinc-300">
                    {item.name}
                  </span>
                  <span className="text-[10px] font-semibold uppercase text-zinc-600">
                    {item.source}
                  </span>
                </label>
              );
            })
          )}
        </div>
      </div>
      <p className="mt-1.5 text-xs text-zinc-600">
        {selected.length} seçim
      </p>
    </div>
  );
}

function CompanyReferencePicker({
  items,
  selected,
  search,
  setSearch,
  isLoading,
  isError,
  onChange,
}: {
  items: CatalogFilterItemDTO[];
  selected: CompanyReferenceInput[];
  search: string;
  setSearch: (value: string) => void;
  isLoading: boolean;
  isError: boolean;
  onChange: (value: CompanyReferenceInput[]) => void;
}) {
  const normalizedSearch = search.trim().toLocaleLowerCase("az-AZ");
  const filteredItems = items.filter((item) =>
    item.name.toLocaleLowerCase("az-AZ").includes(normalizedSearch),
  );
  const roles: CompanyReferenceInput["role"][] = [
    "Developer",
    "Publisher",
  ];

  const toggleRole = (
    item: CatalogFilterItemDTO,
    role: CompanyReferenceInput["role"],
  ) => {
    const exists = selected.some(
      (reference) =>
        reference.source === item.source &&
        reference.id === item.id &&
        reference.role === role,
    );

    onChange(
      exists
        ? selected.filter(
            (reference) =>
              !(
                reference.source === item.source &&
                reference.id === item.id &&
                reference.role === role
              ),
          )
        : [
            ...selected,
            { source: item.source, id: item.id, role },
          ],
    );
  };

  return (
    <div className="md:col-span-2">
      <span className="mb-1.5 block text-xs font-medium text-zinc-500">
        Şirkətlər
      </span>
      <div className="overflow-hidden rounded-xl border border-white/[0.08] bg-black/20">
        <div className="relative border-b border-white/[0.07]">
          <Search
            size={15}
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-600"
          />
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Şirkət axtar..."
            className="h-10 w-full bg-transparent pl-9 pr-3 text-sm text-white outline-none"
          />
        </div>
        <div className="max-h-52 space-y-1 overflow-y-auto p-2">
          {isLoading ? (
            <PickerStatus message="Şirkətlər yüklənir..." loading />
          ) : isError ? (
            <PickerStatus message="Şirkətlər yüklənmədi." error />
          ) : filteredItems.length === 0 ? (
            <PickerStatus message="Uyğun şirkət tapılmadı." />
          ) : (
            filteredItems.map((item) => (
              <div
                key={catalogReferenceKey(item)}
                className="flex flex-col gap-2 rounded-lg px-2.5 py-2 transition hover:bg-white/[0.04] sm:flex-row sm:items-center"
              >
                <span className="min-w-0 flex-1 truncate text-sm text-zinc-300">
                  {item.name}
                  <span className="ml-2 text-[10px] font-semibold uppercase text-zinc-600">
                    {item.source}
                  </span>
                </span>
                <div className="flex gap-2">
                  {roles.map((role) => {
                    const isSelected = selected.some(
                      (reference) =>
                        reference.source === item.source &&
                        reference.id === item.id &&
                        reference.role === role,
                    );

                    return (
                      <button
                        key={role}
                        type="button"
                        aria-pressed={isSelected}
                        onClick={() => toggleRole(item, role)}
                        className={`rounded-md border px-2.5 py-1 text-[11px] font-semibold transition ${
                          isSelected
                            ? "border-violet-400/40 bg-violet-500/20 text-violet-200"
                            : "border-white/[0.08] text-zinc-500 hover:text-zinc-300"
                        }`}
                      >
                        {role}
                      </button>
                    );
                  })}
                </div>
              </div>
            ))
          )}
        </div>
      </div>
      <p className="mt-1.5 text-xs text-zinc-600">
        {selected.length} şirkət rolu seçilib
      </p>
    </div>
  );
}

function PickerStatus({
  message,
  loading,
  error,
}: {
  message: string;
  loading?: boolean;
  error?: boolean;
}) {
  return (
    <div
      className={`flex min-h-16 items-center justify-center gap-2 px-3 text-xs ${
        error ? "text-red-300" : "text-zinc-600"
      }`}
    >
      {loading && <LoaderCircle size={14} className="animate-spin" />}
      {message}
    </div>
  );
}

function catalogReferenceKey(reference: {
  source: CatalogSource;
  id: number;
}) {
  return `${reference.source}:${reference.id}`;
}

function SimpleCatalogPanel({ kind }: { kind: AdminCatalogKind }) {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const canDelete = hasAuthRole(accessToken, "Admin", user?.roles ?? []);
  const [editor, setEditor] = useState<CatalogFilterItemDTO | "new" | null>(null);
  const [name, setName] = useState("");
  const [search, setSearch] = useState("");
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

  const normalizedSearch = search.trim().toLocaleLowerCase("az-AZ");
  const activeItems = (activeQuery.data ?? []).filter((item) =>
    item.name.toLocaleLowerCase("az-AZ").includes(normalizedSearch),
  );

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <label className="relative block w-full sm:max-w-md">
          <Search
            size={17}
            className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-zinc-600"
          />
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Ada görə axtar..."
            className={`${inputClass} pl-10`}
          />
        </label>
        <button
          type="button"
          onClick={() => {
            setEditor("new");
            setName("");
          }}
          className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white"
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
        {activeItems.length === 0 && (
          <p className="col-span-full rounded-2xl border border-dashed border-white/[0.08] px-5 py-12 text-center text-sm text-zinc-600">
            {normalizedSearch
              ? "Bu ada uyğun məlumat tapılmadı."
              : "Aktiv məlumat tapılmadı."}
          </p>
        )}
        {activeItems.map((item) => (
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
