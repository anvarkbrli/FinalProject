import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  Archive,
  ArchiveRestore,
  Award,
  CircleAlert,
  ImagePlus,
  LoaderCircle,
  Pencil,
  Play,
  Plus,
  Save,
  Search,
  UserRound,
  X,
} from "lucide-react";
import type {
  Dispatch,
  ReactNode,
  SetStateAction,
} from "react";
import {
  useDeferredValue,
  useEffect,
  useMemo,
  useState,
} from "react";
import { getApiErrorMessage } from "../../lib/api";
import { hasAuthRole } from "../../lib/authRoles";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  archiveAdminBadge,
  createAdminBadge,
  evaluateAdminBadgesForUser,
  getAdminBadges,
  getAdminUsers,
  restoreAdminBadge,
  updateAdminBadge,
  uploadAdminBadgeIcon,
} from "../../services/adminService";
import { useAuthStore } from "../../stores/authStore";
import type {
  AdminBadge,
  AdminBadgeUpsert,
  AdminUserListItem,
  BadgeMetric,
} from "../../types/admin";

const automaticMetricOptions: Array<[BadgeMetric, string]> = [
  ["AccountAgeDays", "Hesabın yaşı (gün)"],
  ["GamesLogged", "Kitabxanaya əlavə edilən oyun"],
  ["GamesPlayed", "Oynanılmış oyun"],
  ["ReviewsWritten", "Yazılmış review"],
  ["RatingsGiven", "Qiymətləndirilmiş oyun"],
  ["PostsCreated", "Yaradılmış post"],
  ["Followers", "İzləyici"],
  ["Following", "İzlənilən istifadəçi"],
  ["ListsCreated", "Yaradılmış oyun siyahısı"],
  ["Favorites", "Sevimli oyun"],
  ["WishlistItems", "İstək siyahısındakı oyun"],
  ["TotalPlaytimeHours", "Ümumi oyun saatı"],
  ["ForumDiscussions", "Forum müzakirəsi"],
  ["ForumReplies", "Forum cavabı"],
  ["CommentsWritten", "Yazılmış şərh"],
  ["SteamGamesImported", "Steam-dən gətirilmiş oyun"],
  ["SteamAccountLinked", "Steam hesabı bağlantısı"],
  ["MessagesSent", "Göndərilmiş mesaj"],
];

const emptyForm: AdminBadgeUpsert = {
  code: "",
  name: "",
  description: "",
  icon: "Award",
  color: "#8b5cf6",
  metric: "GamesLogged",
  threshold: 1,
  isActive: true,
  displayOrder: 0,
};

const inputClass =
  "h-11 w-full rounded-xl border border-white/[0.08] bg-black/20 px-3 text-sm text-white outline-none transition focus:border-violet-500/50";

function toBadgeCode(value: string) {
  return value
    .toLocaleLowerCase("az")
    .replaceAll("ə", "e")
    .replaceAll("ı", "i")
    .replaceAll("ö", "o")
    .replaceAll("ü", "u")
    .replaceAll("ş", "s")
    .replaceAll("ç", "c")
    .replaceAll("ğ", "g")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 80);
}

export function AdminBadgesPage() {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);
  const user = useAuthStore((state) => state.user);
  const canArchive = hasAuthRole(accessToken, "Admin", user?.roles ?? []);
  const [editor, setEditor] = useState<AdminBadge | "new" | null>(null);
  const [form, setForm] = useState<AdminBadgeUpsert>(emptyForm);
  const [iconFile, setIconFile] = useState<File | null>(null);
  const [evaluationSearch, setEvaluationSearch] = useState("");
  const [selectedUser, setSelectedUser] =
    useState<AdminUserListItem | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const deferredSearch = useDeferredValue(evaluationSearch.trim());

  const badgesQuery = useQuery({
    queryKey: ["admin", "badges"],
    queryFn: ({ signal }) => getAdminBadges(signal),
  });

  const usersQuery = useQuery({
    queryKey: ["admin", "badge-user-search", deferredSearch],
    queryFn: ({ signal }) =>
      getAdminUsers(
        {
          search: deferredSearch,
          pageNumber: 1,
          pageSize: 6,
        },
        signal,
      ),
    enabled: !selectedUser && deferredSearch.length >= 2,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "badges"] });

  const saveMutation = useMutation({
    mutationFn: async () => {
      const isSameDraft = (badge: AdminBadge) =>
        badge.code.toLocaleLowerCase("az-AZ") ===
          form.code.trim().toLocaleLowerCase("az-AZ") &&
        badge.name.trim().toLocaleLowerCase("az-AZ") ===
          form.name.trim().toLocaleLowerCase("az-AZ") &&
        !badge.iconUrl;

      let savedBadge: AdminBadge;

      if (editor !== "new") {
        savedBadge = await updateAdminBadge(editor!.id, form);
      } else {
        const existingBadge = (badgesQuery.data ?? []).find(
          (badge) =>
            badge.code.toLocaleLowerCase("az-AZ") ===
            form.code.trim().toLocaleLowerCase("az-AZ"),
        );

        if (existingBadge) {
          if (!isSameDraft(existingBadge)) {
            throw new Error(
              "Bu adda və ya daxili kodla nişan artıq mövcuddur.",
            );
          }

          savedBadge = await updateAdminBadge(existingBadge.id, form);
        } else {
          try {
            savedBadge = await createAdminBadge(form);
          } catch (createError) {
            let currentBadges: AdminBadge[];

            try {
              currentBadges = await getAdminBadges();
            } catch {
              throw createError;
            }

            const interruptedDraft = currentBadges.find(isSameDraft);
            if (!interruptedDraft) {
              throw createError;
            }

            savedBadge = await updateAdminBadge(
              interruptedDraft.id,
              form,
            );
          }
        }
      }

      if (iconFile) {
        try {
          savedBadge = await uploadAdminBadgeIcon(
            savedBadge.id,
            iconFile,
          );
        } catch (uploadError) {
          if (editor === "new") {
            setEditor(savedBadge);
            await refresh();
          }

          throw uploadError;
        }
      }

      return savedBadge;
    },
    onMutate: () => {
      setError(null);
      setNotice(null);
    },
    onSuccess: async () => {
      setEditor(null);
      setForm(emptyForm);
      setIconFile(null);
      setNotice("Nişan yadda saxlanıldı.");
      await refresh();
    },
    onError: (mutationError) => {
      const fallbackMessage =
        mutationError instanceof Error &&
        !("isAxiosError" in mutationError)
          ? mutationError.message
          : "Nişan yadda saxlanmadı.";

      setError(
        getApiErrorMessage(mutationError, fallbackMessage),
      );
    },
  });

  const archiveMutation = useMutation({
    mutationFn: archiveAdminBadge,
    onMutate: () => {
      setError(null);
      setNotice(null);
    },
    onSuccess: async () => {
      setNotice(
        "Nişan arxivləndi. İstifadəçilərin nişan tarixçəsi qorundu.",
      );
      await refresh();
    },
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(mutationError, "Nişan arxivlənmədi."),
      ),
  });

  const restoreMutation = useMutation({
    mutationFn: restoreAdminBadge,
    onMutate: () => {
      setError(null);
      setNotice(null);
    },
    onSuccess: async () => {
      setNotice("Nişan yenidən aktiv edildi.");
      await refresh();
    },
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(mutationError, "Nişan aktiv edilmədi."),
      ),
  });

  const evaluateMutation = useMutation({
    mutationFn: () => evaluateAdminBadgesForUser(selectedUser!.id),
    onMutate: () => {
      setError(null);
      setNotice(null);
    },
    onSuccess: (awardedCount) =>
      setNotice(
        awardedCount > 0
          ? `${selectedUser?.userName} üçün ${awardedCount} yeni avtomatik nişan verildi.`
          : `${selectedUser?.userName} hazırda heç bir yeni nişan şərtini ödəmir.`,
      ),
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(
          mutationError,
          "Nişan hesablanması alınmadı.",
        ),
      ),
  });

  function openEditor(badge: AdminBadge | "new") {
    setEditor(badge);
    setIconFile(null);
    setForm(
      badge === "new"
        ? { ...emptyForm }
        : {
            code: badge.code,
            name: badge.name,
            description: badge.description,
            icon: badge.icon,
            color: badge.color,
            metric: badge.metric,
            threshold: badge.threshold,
            isActive: badge.isActive,
            displayOrder: badge.displayOrder,
          },
    );
    setError(null);
    setNotice(null);
  }

  return (
    <div className="space-y-6">
      <section className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="text-sm font-medium text-violet-300">
            Nailiyyət sistemi
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
            Nişan idarəetməsi
          </h2>
          <p className="mt-3 max-w-3xl text-sm leading-6 text-zinc-500">
            Avtomatik nişan istifadəçinin statistikasına görə sistem
            tərəfindən verilir. Xüsusi nişan isə yalnız istifadəçi
            profilindən admin tərəfindən verilir.
          </p>
        </div>
        <button
          type="button"
          onClick={() => openEditor("new")}
          className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500"
        >
          <Plus size={17} />
          Yeni nişan
        </button>
      </section>

      {notice && (
        <div className="rounded-xl border border-emerald-400/15 bg-emerald-500/[0.06] p-4 text-sm text-emerald-200">
          {notice}
        </div>
      )}
      {error && <InlineError message={error} />}

      <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-5">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-500/10 text-amber-300">
            <Play size={17} />
          </span>
          <div>
            <h3 className="font-bold text-white">
              Avtomatik nişanları yoxla
            </h3>
            <p className="text-xs text-zinc-600">
              İstifadəçi adı ilə tap və mövcud statistikasını yenidən
              yoxlat.
            </p>
          </div>
        </div>

        <div className="relative mt-4">
          {selectedUser ? (
            <div className="flex min-h-12 items-center justify-between gap-3 rounded-xl border border-violet-400/20 bg-violet-500/[0.06] px-4">
              <div className="flex min-w-0 items-center gap-3">
                <UserRound size={17} className="text-violet-300" />
                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold text-white">
                    {selectedUser.userName}
                  </p>
                  <p className="truncate text-xs text-zinc-600">
                    {selectedUser.email}
                  </p>
                </div>
              </div>
              <button
                type="button"
                aria-label="İstifadəçi seçimini sil"
                onClick={() => {
                  setSelectedUser(null);
                  setEvaluationSearch("");
                }}
                className="rounded-lg p-2 text-zinc-500 hover:bg-white/[0.06] hover:text-white"
              >
                <X size={16} />
              </button>
            </div>
          ) : (
            <>
              <Search
                size={17}
                className="pointer-events-none absolute left-3.5 top-[1.35rem] text-zinc-600"
              />
              <input
                value={evaluationSearch}
                onChange={(event) =>
                  setEvaluationSearch(event.target.value)
                }
                placeholder="İstifadəçi adı və ya email yaz..."
                className={`${inputClass} pl-10`}
              />
              {deferredSearch.length >= 2 && (
                <div className="absolute z-20 mt-2 w-full overflow-hidden rounded-xl border border-white/[0.08] bg-[#111117] shadow-2xl">
                  {usersQuery.isPending ? (
                    <div className="flex h-20 items-center justify-center text-zinc-600">
                      <LoaderCircle size={18} className="animate-spin" />
                    </div>
                  ) : (usersQuery.data?.items.length ?? 0) > 0 ? (
                    usersQuery.data!.items.map((searchUser) => (
                      <button
                        key={searchUser.id}
                        type="button"
                        onClick={() => {
                          setSelectedUser(searchUser);
                          setEvaluationSearch(searchUser.userName);
                        }}
                        className="flex w-full items-center gap-3 border-b border-white/[0.05] px-4 py-3 text-left last:border-0 hover:bg-white/[0.05]"
                      >
                        <UserRound
                          size={17}
                          className="text-violet-300"
                        />
                        <span className="min-w-0">
                          <span className="block truncate text-sm font-semibold text-white">
                            {searchUser.userName}
                          </span>
                          <span className="block truncate text-xs text-zinc-600">
                            {searchUser.email}
                          </span>
                        </span>
                      </button>
                    ))
                  ) : (
                    <p className="px-4 py-5 text-center text-sm text-zinc-600">
                      İstifadəçi tapılmadı.
                    </p>
                  )}
                </div>
              )}
            </>
          )}
        </div>

        <button
          type="button"
          disabled={!selectedUser || evaluateMutation.isPending}
          onClick={() => evaluateMutation.mutate()}
          className="mt-3 inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-white/[0.08] px-4 text-sm font-semibold text-zinc-200 transition hover:bg-white/[0.05] disabled:opacity-40"
        >
          {evaluateMutation.isPending ? (
            <LoaderCircle size={16} className="animate-spin" />
          ) : (
            <Play size={16} />
          )}
          Nişanları yoxla
        </button>
      </section>

      {editor && (
        <BadgeEditor
          editor={editor}
          form={form}
          setForm={setForm}
          iconFile={iconFile}
          setIconFile={setIconFile}
          existingIconUrl={
            editor === "new" ? null : editor.iconUrl
          }
          isPending={saveMutation.isPending}
          onClose={() => setEditor(null)}
          onSave={() => saveMutation.mutate()}
        />
      )}

      {badgesQuery.isPending ? (
        <div className="flex min-h-48 items-center justify-center">
          <LoaderCircle
            size={24}
            className="animate-spin text-violet-300"
          />
        </div>
      ) : badgesQuery.isError ? (
        <InlineError
          message={getApiErrorMessage(
            badgesQuery.error,
            "Nişan siyahısı yüklənmədi.",
          )}
        />
      ) : (
        <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {(badgesQuery.data ?? []).map((badge) => (
            <article
              key={badge.id}
              className={`rounded-2xl border p-5 ${
                badge.isActive
                  ? "border-white/[0.07] bg-white/[0.025]"
                  : "border-white/[0.05] bg-black/15 opacity-65"
              }`}
            >
              <div className="flex items-start justify-between gap-3">
                <BadgeIcon badge={badge} size="large" />
                <span
                  className={`rounded-full px-2 py-1 text-[9px] font-bold uppercase ${
                    badge.isActive
                      ? "bg-emerald-500/10 text-emerald-300"
                      : "bg-zinc-500/10 text-zinc-500"
                  }`}
                >
                  {badge.isActive ? "Aktiv" : "Arxiv"}
                </span>
              </div>
              <h3 className="mt-4 font-bold text-white">{badge.name}</h3>
              <p className="mt-3 line-clamp-2 text-sm leading-6 text-zinc-500">
                {badge.description}
              </p>
              <div className="mt-4 rounded-xl bg-black/20 p-3 text-xs text-zinc-500">
                <p>{metricLabel(badge.metric)}</p>
                {badge.metric !== "Manual" && (
                  <p className="mt-1">
                    Lazım olan say:{" "}
                    <strong className="text-zinc-300">
                      {badge.threshold}
                    </strong>
                  </p>
                )}
              </div>
              <div className="mt-4 flex flex-wrap gap-2">
                <SmallButton
                  label="Redaktə et"
                  icon={<Pencil size={14} />}
                  onClick={() => openEditor(badge)}
                />
                {badge.isActive ? (
                  canArchive && (
                    <SmallButton
                      label="Arxivlə"
                      icon={<Archive size={14} />}
                      danger
                      disabled={archiveMutation.isPending}
                      onClick={() =>
                        archiveMutation.mutate(badge.id)
                      }
                    />
                  )
                ) : (
                  <SmallButton
                    label="Aktiv et"
                    icon={<ArchiveRestore size={14} />}
                    disabled={restoreMutation.isPending}
                    onClick={() =>
                      restoreMutation.mutate(badge.id)
                    }
                  />
                )}
              </div>
            </article>
          ))}
        </section>
      )}
    </div>
  );
}

function BadgeEditor({
  editor,
  form,
  setForm,
  iconFile,
  setIconFile,
  existingIconUrl,
  isPending,
  onClose,
  onSave,
}: {
  editor: AdminBadge | "new";
  form: AdminBadgeUpsert;
  setForm: Dispatch<SetStateAction<AdminBadgeUpsert>>;
  iconFile: File | null;
  setIconFile: Dispatch<SetStateAction<File | null>>;
  existingIconUrl: string | null;
  isPending: boolean;
  onClose: () => void;
  onSave: () => void;
}) {
  const isManual = form.metric === "Manual";
  const previewUrl = useMemo(
    () => (iconFile ? URL.createObjectURL(iconFile) : null),
    [iconFile],
  );

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  const valid =
    form.code.trim() &&
    form.name.trim() &&
    form.description.trim() &&
    (isManual || form.threshold > 0);

  return (
    <section className="rounded-2xl border border-violet-400/15 bg-violet-500/[0.04] p-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="font-bold text-white">
            {editor === "new"
              ? "Yeni nişan yarat"
              : "Nişanı redaktə et"}
          </h3>
          <p className="mt-1 text-xs text-zinc-600">
            Profilə görünəcək məlumatı və nişanın necə veriləcəyini seç.
          </p>
        </div>
        <button
          type="button"
          aria-label="Redaktoru bağla"
          onClick={onClose}
          className="rounded-lg p-2 text-zinc-500 hover:bg-white/[0.06] hover:text-white"
        >
          <X size={17} />
        </button>
      </div>

      <div className="mt-6 grid gap-6 xl:grid-cols-[1fr_0.85fr]">
        <div className="space-y-4">
          <Field
            label="Nişan adı"
            hint="İstifadəçi profilində görünən qısa ad."
          >
            <input
              value={form.name}
              maxLength={120}
              placeholder="Məsələn: Turnir qalibi"
              onChange={(event) => {
                const name = event.target.value;
                setForm((current) => ({
                  ...current,
                  name,
                  code:
                    editor === "new"
                      ? toBadgeCode(name)
                      : current.code,
                }));
              }}
              className={inputClass}
            />
          </Field>

          <Field
            label="Açıqlama"
            hint="İstifadəçi bu nişanı niyə aldığını burada görəcək."
          >
            <textarea
              value={form.description}
              maxLength={500}
              rows={3}
              placeholder="Nişanın mənasını qısa və aydın yaz..."
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  description: event.target.value,
                }))
              }
              className="w-full resize-none rounded-xl border border-white/[0.08] bg-black/20 p-3 text-sm text-white outline-none transition focus:border-violet-500/50"
            />
          </Field>

          <Field
            label="Verilmə qaydası"
            hint="Avtomatik nişan statistika ilə, xüsusi nişan isə yalnız admin tərəfindən verilir."
          >
            <select
              value={isManual ? "Manual" : "Automatic"}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  metric:
                    event.target.value === "Manual"
                      ? "Manual"
                      : "GamesLogged",
                  threshold: 1,
                }))
              }
              className={inputClass}
            >
              <option value="Automatic">Sistem avtomatik verir</option>
              <option value="Manual">Yalnız admin verir</option>
            </select>
          </Field>

          {!isManual && (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Şərt"
                hint="Sistemin hansı statistikanı sayacağını seç."
              >
                <select
                  value={form.metric}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      metric: event.target.value as BadgeMetric,
                    }))
                  }
                  className={inputClass}
                >
                  {automaticMetricOptions.map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </Field>
              <Field
                label="Lazım olan say"
                hint="Məsələn, 10 oyun üçün 10 yaz."
              >
                <input
                  type="number"
                  min={1}
                  value={form.threshold}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      threshold: Number(event.target.value),
                    }))
                  }
                  className={inputClass}
                />
              </Field>
            </div>
          )}
        </div>

        <div className="space-y-4">
          <Field
            label="Nişan şəkli"
            hint="PNG, JPG və ya WEBP. Maksimum 5 MB."
          >
            <label className="flex min-h-40 cursor-pointer flex-col items-center justify-center overflow-hidden rounded-2xl border border-dashed border-white/[0.1] bg-black/20 text-center transition hover:border-violet-400/30 hover:bg-violet-500/[0.03]">
              {previewUrl || existingIconUrl ? (
                <img
                  src={
                    previewUrl ??
                    resolveAssetUrl(existingIconUrl) ??
                    undefined
                  }
                  alt="Nişan ikon önizləməsi"
                  className="h-24 w-24 rounded-2xl object-cover"
                />
              ) : (
                <ImagePlus size={28} className="text-violet-300" />
              )}
              <span className="mt-3 text-sm font-semibold text-zinc-300">
                {iconFile ? iconFile.name : "Şəkil seç"}
              </span>
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="sr-only"
                onChange={(event) =>
                  setIconFile(event.target.files?.[0] ?? null)
                }
              />
            </label>
          </Field>

          <Field
            label="Rəng"
            hint="Nişan kartının vurğu rəngi."
          >
            <div className="flex gap-2">
              <input
                type="color"
                value={form.color}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    color: event.target.value,
                  }))
                }
                className="h-11 w-14 rounded-xl border border-white/[0.08] bg-black/20 p-1"
              />
              <input
                value={form.color}
                maxLength={20}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    color: event.target.value,
                  }))
                }
                className={`${inputClass} flex-1`}
              />
            </div>
          </Field>

          <details className="rounded-xl border border-white/[0.07] bg-black/15 p-4">
            <summary className="cursor-pointer text-sm font-semibold text-zinc-300">
              Əlavə ayarlar
            </summary>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <Field
                label="Daxili kod"
                hint="Sistem özü yaradır. Adətən dəyişmək lazım deyil."
              >
                <input
                  value={form.code}
                  readOnly={editor === "new"}
                  maxLength={80}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      code: event.target.value,
                    }))
                  }
                  className={`${inputClass} read-only:text-zinc-600`}
                />
              </Field>
              <Field
                label="Göstərilmə sırası"
                hint="Kiçik rəqəm profildə daha əvvəl görünür."
              >
                <input
                  type="number"
                  min={0}
                  value={form.displayOrder}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      displayOrder: Number(event.target.value),
                    }))
                  }
                  className={inputClass}
                />
              </Field>
            </div>
          </details>
        </div>
      </div>

      <div className="mt-5 flex flex-wrap items-center justify-between gap-4 border-t border-white/[0.06] pt-5">
        <label className="flex items-center gap-3 text-sm text-zinc-300">
          <input
            type="checkbox"
            checked={form.isActive}
            onChange={(event) =>
              setForm((current) => ({
                ...current,
                isActive: event.target.checked,
              }))
            }
            className="h-4 w-4 accent-violet-500"
          />
          Nişan aktivdir
        </label>
        <button
          type="button"
          disabled={!valid || isPending}
          onClick={onSave}
          className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-40"
        >
          {isPending ? (
            <LoaderCircle size={16} className="animate-spin" />
          ) : (
            <Save size={16} />
          )}
          Yadda saxla
        </button>
      </div>
    </section>
  );
}

function BadgeIcon({
  badge,
  size,
}: {
  badge: AdminBadge;
  size: "large" | "small";
}) {
  const iconUrl = resolveAssetUrl(badge.iconUrl);
  const className =
    size === "large"
      ? "h-11 w-11 rounded-xl"
      : "h-9 w-9 rounded-lg";

  return (
    <span
      className={`flex shrink-0 items-center justify-center overflow-hidden ${className}`}
      style={{
        color: badge.color,
        backgroundColor: `${badge.color}18`,
      }}
    >
      {iconUrl ? (
        <img
          src={iconUrl}
          alt=""
          className="h-full w-full object-cover"
        />
      ) : (
        <Award size={size === "large" ? 21 : 17} />
      )}
    </span>
  );
}

function metricLabel(metric: BadgeMetric) {
  if (metric === "Manual") return "Yalnız admin tərəfindən verilir";
  return (
    automaticMetricOptions.find(([value]) => value === metric)?.[1] ??
    metric
  );
}

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-xs font-semibold text-zinc-400">
        {label}
      </span>
      {hint && (
        <span className="mb-2 block text-[11px] leading-4 text-zinc-700">
          {hint}
        </span>
      )}
      {children}
    </label>
  );
}

function SmallButton({
  label,
  icon,
  danger = false,
  disabled = false,
  onClick,
}: {
  label: string;
  icon: ReactNode;
  danger?: boolean;
  disabled?: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`inline-flex h-9 items-center gap-2 rounded-lg border px-3 text-xs font-semibold transition disabled:opacity-40 ${
        danger
          ? "border-red-400/10 text-red-300 hover:bg-red-500/10"
          : "border-white/[0.08] text-zinc-400 hover:bg-white/[0.05] hover:text-white"
      }`}
    >
      {icon}
      {label}
    </button>
  );
}

function InlineError({ message }: { message: string }) {
  return (
    <div className="flex items-start gap-3 rounded-xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
      <CircleAlert size={18} className="mt-0.5 shrink-0" />
      <span>{message}</span>
    </div>
  );
}
