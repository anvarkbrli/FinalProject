import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  BellRing,
  Check,
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  Eye,
  FilePenLine,
  LoaderCircle,
  Plus,
  Target,
  Users,
  RotateCcw,
  Send,
  Trash2,
  X,
} from "lucide-react";
import {
  useEffect,
  useId,
  useState,
  type FormEvent,
} from "react";
import { createPortal } from "react-dom";
import { useSearchParams } from "react-router-dom";
import { AdminFilterSelect } from "../../components/admin/AdminFilterSelect";
import { AdminLiveSearch } from "../../components/admin/AdminLiveSearch";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { hasAuthRole } from "../../lib/authRoles";
import { useConfirmation } from "../../lib/confirmation";
import { formatDate } from "../../lib/helpers";
import {
  createAdminAnnouncement,
  deleteAdminAnnouncement,
  getAdminAnnouncements,
  restoreAdminAnnouncement,
  updateAdminAnnouncement,
} from "../../services/adminService";
import { useAuthStore } from "../../stores/authStore";
import type {
  Announcement,
  AnnouncementAudience,
} from "../../types/admin";

const pageSize = 12;

const announcementAudienceOptions: AnnouncementAudience[] = [
  "Everyone",
  "AzerbaijaniUsers",
  "EnglishUsers",
  "SteamLinkedUsers",
  "NewUsers",
  "RegularUsers",
  "Moderators",
  "Administrators",
];

function getAudienceLabel(
  audience: AnnouncementAudience,
  language: "az" | "en",
) {
  const labels: Record<AnnouncementAudience, [string, string]> = {
    Everyone: ["Bütün istifadəçilər", "All users"],
    AzerbaijaniUsers: ["Azərbaycan dilini seçənlər", "Azerbaijani-language users"],
    EnglishUsers: ["İngilis dilini seçənlər", "English-language users"],
    SteamLinkedUsers: ["Steam hesabı bağlı olanlar", "Steam-linked users"],
    NewUsers: ["Son 30 gündə qeydiyyatdan keçənlər", "Users registered in the last 30 days"],
    RegularUsers: ["Adi istifadəçilər", "Regular users"],
    Moderators: ["Moderatorlar", "Moderators"],
    Administrators: ["Adminlər", "Administrators"],
  };

  return labels[audience][language === "en" ? 1 : 0];
}

interface EditorState {
  announcement: Announcement | null;
}

interface AnnouncementFormValues {
  title: string;
  content: string;
  audience: AnnouncementAudience;
  isPublished: boolean;
}

interface AnnouncementFieldErrors {
  title?: string;
  content?: string;
}

export function AdminAnnouncementsPage() {
  const { language } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [editor, setEditor] = useState<EditorState | null>(null);
  const [actionError, setActionError] = useState<{
    announcementId: number;
    message: string;
  } | null>(null);
  const queryClient = useQueryClient();
  const requestConfirmation = useConfirmation();
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
  const publication = searchParams.get("publication") ?? "";
  const state = searchParams.get("state") ?? "active";
  const sortBy = searchParams.get("sort") ?? "newest";
  const filter = {
    search: search || undefined,
    isPublished:
      publication === "published"
        ? true
        : publication === "draft"
          ? false
          : undefined,
    isDeleted:
      state === "deleted"
        ? true
        : state === "all"
          ? undefined
          : false,
    sortBy,
    pageNumber,
    pageSize,
  };

  const announcementsQuery = useQuery({
    queryKey: ["admin", "announcements", filter],
    queryFn: ({ signal }) => getAdminAnnouncements(filter, signal),
    placeholderData: keepPreviousData,
  });

  const saveMutation = useMutation({
    mutationFn: (values: AnnouncementFormValues) =>
      editor?.announcement
        ? updateAdminAnnouncement(editor.announcement.id, values)
        : createAdminAnnouncement(values),
    onSuccess: async () => {
      setEditor(null);
      await refreshAnnouncements();
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteAdminAnnouncement,
    onMutate: () => setActionError(null),
    onSuccess: refreshAnnouncements,
    onError: (error, announcementId) => {
      setActionError({
        announcementId,
        message: getApiErrorMessage(error, "Elan silinmədi."),
      });
    },
  });

  const restoreMutation = useMutation({
    mutationFn: restoreAdminAnnouncement,
    onMutate: () => setActionError(null),
    onSuccess: refreshAnnouncements,
    onError: (error, announcementId) => {
      setActionError({
        announcementId,
        message: getApiErrorMessage(error, "Elan bərpa edilmədi."),
      });
    },
  });

  function refreshAnnouncements() {
    return Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["admin", "announcements"],
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

  function setPage(nextPage: number) {
    const next = new URLSearchParams(searchParams);
    if (nextPage <= 1) next.delete("page");
    else next.set("page", String(nextPage));
    setSearchParams(next);
  }

  async function requestDelete(announcement: Announcement) {
    const confirmed = await requestConfirmation({
      title: "Elan silinsin?",
      description: `“${announcement.title}” elanı istifadəçilər üçün əlçatmaz olacaq.`,
      detail:
        "Elan yumşaq silinəcək və administrator daha sonra onu bərpa edə biləcək.",
      confirmLabel: "Elanı sil",
      kind: "delete",
      tone: "danger",
    });

    if (confirmed) {
      deleteMutation.mutate(announcement.id);
    }
  }

  const data = announcementsQuery.data;
  const pageCount = data
    ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
    : 1;

  return (
    <div className="space-y-6">
      <section className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="text-sm font-medium text-violet-300">
            Platforma məlumatlandırması
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
            Elanlar
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-500">
            İstifadəçilərə göndərilən elanları hazırla, yayımla və mövcud
            elanları idarə et.
          </p>
        </div>

        <button
          type="button"
          onClick={() => {
            saveMutation.reset();
            setEditor({ announcement: null });
          }}
          className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500"
        >
          <Plus size={17} />
          Yeni elan
        </button>
      </section>

      <section className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4">
        <div className="grid gap-3 xl:grid-cols-[minmax(18rem,1fr)_auto_auto_auto]">
          <AdminLiveSearch
            placeholder="Elan başlığı və ya mətni..."
            ariaLabel="Elanlar arasında axtarış"
          />

          <AdminFilterSelect
            label={language === "en" ? "Publication status" : "Yayım statusu"}
            value={publication}
            onChange={(value) => updateParam("publication", value)}
            options={[
              ["", language === "en" ? "All broadcasts" : "Bütün yayımlar"],
              ["published", language === "en" ? "Published" : "Yayımlanmış"],
              ["draft", language === "en" ? "Draft" : "Qaralama"],
            ]}
          />
          <AdminFilterSelect
            label={language === "en" ? "Announcement state" : "Elan vəziyyəti"}
            value={state}
            onChange={(value) => updateParam("state", value)}
            options={[
              ["active", language === "en" ? "Active announcements" : "Aktiv elanlar"],
              ["deleted", language === "en" ? "Deleted announcements" : "Silinmiş elanlar"],
              ["all", language === "en" ? "All announcements" : "Hamısı"],
            ]}
          />
          <AdminFilterSelect
            label={language === "en" ? "Sort announcements" : "Elanları sırala"}
            value={sortBy}
            onChange={(value) => updateParam("sort", value)}
            options={[
              ["newest", language === "en" ? "Newest" : "Ən yeni"],
              ["oldest", language === "en" ? "Oldest" : "Ən köhnə"],
              ["updated", language === "en" ? "Recently updated" : "Son yenilənən"],
              ["published", language === "en" ? "Recently published" : "Son yayımlanan"],
              ["title", language === "en" ? "Title A–Z" : "Başlıq A–Z"],
            ]}
          />
        </div>
      </section>

      {announcementsQuery.isLoading ? (
        <LoadingState />
      ) : announcementsQuery.isError ? (
        <ErrorState
          message={getApiErrorMessage(
            announcementsQuery.error,
            "Elanlar yüklənmədi.",
          )}
          onRetry={() => void announcementsQuery.refetch()}
        />
      ) : !data || data.items.length === 0 ? (
        <section className="rounded-2xl border border-dashed border-white/[0.09] bg-white/[0.015] px-5 py-16 text-center">
          <BellRing size={30} className="mx-auto text-zinc-700" />
          <h3 className="mt-4 font-semibold text-zinc-300">
            Elan tapılmadı
          </h3>
          <p className="mt-2 text-sm text-zinc-600">
            Filtrləri dəyiş və ya yeni elan yarat.
          </p>
        </section>
      ) : (
        <>
          <section className="grid gap-4 xl:grid-cols-2">
            {data.items.map((announcement) => {
              const isDeleting =
                deleteMutation.isPending &&
                deleteMutation.variables === announcement.id;
              const isRestoring =
                restoreMutation.isPending &&
                restoreMutation.variables === announcement.id;

              return (
                <article
                  key={announcement.id}
                  className={`flex min-w-0 flex-col rounded-2xl border p-5 ${
                    announcement.isDeleted
                      ? "border-red-400/10 bg-red-500/[0.025]"
                      : "border-white/[0.07] bg-white/[0.025]"
                  }`}
                >
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span
                          className={`rounded-full border px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider ${
                            announcement.isDeleted
                              ? "border-red-400/15 bg-red-500/[0.07] text-red-300"
                              : announcement.isPublished
                                ? "border-emerald-400/15 bg-emerald-500/[0.07] text-emerald-300"
                                : "border-amber-300/15 bg-amber-400/[0.07] text-amber-200"
                          }`}
                        >
                          {announcement.isDeleted
                            ? "Silinib"
                            : announcement.isPublished
                              ? "Yayımlanıb"
                              : "Qaralama"}
                        </span>
                        <span className="rounded-full border border-sky-400/15 bg-sky-500/[0.06] px-2.5 py-1 text-[10px] font-bold text-sky-200">
                          {getAudienceLabel(announcement.audience, language)}
                        </span>
                        <span className="text-xs text-zinc-700">
                          #{announcement.id}
                        </span>
                      </div>
                      <h3 className="mt-3 break-words text-lg font-bold text-white">
                        {announcement.title}
                      </h3>
                    </div>
                  </div>

                  <p className="mt-4 line-clamp-4 whitespace-pre-wrap break-words text-sm leading-6 text-zinc-400">
                    {announcement.content}
                  </p>

                  <div className="mt-5 border-t border-white/[0.06] pt-4">
                    <div className="flex flex-wrap gap-x-5 gap-y-2 text-xs text-zinc-600">
                      <span>
                        Müəllif:{" "}
                        <strong className="font-semibold text-zinc-400">
                          {announcement.createdByUserName}
                        </strong>
                      </span>
                      <span>
                        Yaradılıb: {formatDate(announcement.createdAt)}
                      </span>
                      {announcement.publishedAtUtc && (
                        <span>
                          Yayımlanıb:{" "}
                          {formatDate(announcement.publishedAtUtc)}
                        </span>
                      )}
                    </div>

                    <div className="mt-4 grid gap-2 sm:grid-cols-3">
                      <div className="rounded-xl border border-white/[0.06] bg-black/15 px-3 py-2.5">
                        <div className="flex items-center gap-2 text-[11px] text-zinc-600">
                          <Eye size={13} />
                          {language === "en" ? "Unique views" : "Unikal baxış"}
                        </div>
                        <p className="mt-1 text-sm font-bold text-zinc-200">
                          {announcement.uniqueViewCount}
                        </p>
                      </div>
                      <div className="rounded-xl border border-white/[0.06] bg-black/15 px-3 py-2.5">
                        <div className="flex items-center gap-2 text-[11px] text-zinc-600">
                          <Users size={13} />
                          {language === "en" ? "Target audience" : "Hədəf auditoriya"}
                        </div>
                        <p className="mt-1 text-sm font-bold text-zinc-200">
                          {announcement.targetUserCount}
                        </p>
                      </div>
                      <div className="rounded-xl border border-white/[0.06] bg-black/15 px-3 py-2.5">
                        <div className="flex items-center gap-2 text-[11px] text-zinc-600">
                          <Target size={13} />
                          {language === "en" ? "View rate" : "Baxış faizi"}
                        </div>
                        <p className="mt-1 text-sm font-bold text-zinc-200">
                          {announcement.viewRatePercent.toFixed(1)}%
                        </p>
                      </div>
                    </div>

                    <div className="mt-4 flex flex-wrap items-center gap-2">
                      {announcement.isDeleted ? (
                        <button
                          type="button"
                          disabled={isRestoring}
                          onClick={() =>
                            restoreMutation.mutate(announcement.id)
                          }
                          className="inline-flex h-9 items-center gap-2 rounded-lg border border-emerald-400/15 bg-emerald-500/[0.06] px-3 text-xs font-bold text-emerald-300 transition hover:bg-emerald-500/10 disabled:opacity-50"
                        >
                          {isRestoring ? (
                            <LoaderCircle size={15} className="animate-spin" />
                          ) : (
                            <RotateCcw size={15} />
                          )}
                          Bərpa et
                        </button>
                      ) : (
                        <>
                          <button
                            type="button"
                            onClick={() => {
                              saveMutation.reset();
                              setEditor({ announcement });
                            }}
                            className="inline-flex h-9 items-center gap-2 rounded-lg border border-violet-400/15 bg-violet-500/[0.06] px-3 text-xs font-bold text-violet-300 transition hover:bg-violet-500/10"
                          >
                            <FilePenLine size={15} />
                            Redaktə et
                          </button>
                          {canDelete && (
                            <button
                              type="button"
                              disabled={isDeleting}
                              onClick={() => void requestDelete(announcement)}
                              className="inline-flex h-9 items-center gap-2 rounded-lg border border-red-400/15 bg-red-500/[0.05] px-3 text-xs font-bold text-red-300 transition hover:bg-red-500/10 disabled:opacity-50"
                            >
                              {isDeleting ? (
                                <LoaderCircle
                                  size={15}
                                  className="animate-spin"
                                />
                              ) : (
                                <Trash2 size={15} />
                              )}
                              Sil
                            </button>
                          )}
                        </>
                      )}
                    </div>

                    {actionError?.announcementId === announcement.id && (
                      <div
                        role="alert"
                        className="mt-3 flex items-start gap-2 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-3 py-2.5 text-xs leading-5 text-red-200"
                      >
                        <CircleAlert size={15} className="mt-0.5 shrink-0" />
                        {actionError.message}
                      </div>
                    )}
                  </div>
                </article>
              );
            })}
          </section>

          <Pagination
            pageNumber={data.pageNumber}
            pageCount={pageCount}
            totalCount={data.totalCount}
            onPageChange={setPage}
          />
        </>
      )}

      {editor &&
        createPortal(
          <AnnouncementEditorModal
            announcement={editor.announcement}
            language={language}
            isPending={saveMutation.isPending}
            apiError={saveMutation.error}
            onClearError={() => saveMutation.reset()}
            onClose={() => {
              if (saveMutation.isPending) return;
              saveMutation.reset();
              setEditor(null);
            }}
            onSubmit={(values) => saveMutation.mutate(values)}
          />,
          document.body,
        )}
    </div>
  );
}

function AnnouncementEditorModal({
  announcement,
  language,
  isPending,
  apiError,
  onClearError,
  onClose,
  onSubmit,
}: {
  announcement: Announcement | null;
  language: "az" | "en";
  isPending: boolean;
  apiError: unknown;
  onClearError: () => void;
  onClose: () => void;
  onSubmit: (values: AnnouncementFormValues) => void;
}) {
  const titleId = useId();
  const [values, setValues] = useState<AnnouncementFormValues>(() => ({
    title: announcement?.title ?? "",
    content: announcement?.content ?? "",
    audience: announcement?.audience ?? "Everyone",
    isPublished: announcement?.isPublished ?? true,
  }));
  const [fieldErrors, setFieldErrors] =
    useState<AnnouncementFieldErrors>({});

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, []);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !isPending) onClose();
    };

    window.addEventListener("keydown", handleKeyDown);

    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isPending, onClose]);

  const apiMessage = apiError
    ? getApiErrorMessage(
        apiError,
        announcement ? "Elan yenilənmədi." : "Elan yaradılmadı.",
      )
    : null;
  const normalizedApiMessage = apiMessage?.toLocaleLowerCase("az-AZ") ?? "";
  const titleApiError =
    normalizedApiMessage.includes("başlıq") ||
    normalizedApiMessage.includes("title")
      ? apiMessage
      : null;
  const contentApiError =
    normalizedApiMessage.includes("mətni") ||
    normalizedApiMessage.includes("content")
      ? apiMessage
      : null;
  const generalApiError =
    apiMessage && !titleApiError && !contentApiError ? apiMessage : null;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isPending) return;

    const nextErrors: AnnouncementFieldErrors = {};
    const title = values.title.trim();
    const content = values.content.trim();

    if (!title) {
      nextErrors.title = "Elanın başlığını daxil edin.";
    } else if (title.length > 200) {
      nextErrors.title = "Elanın başlığı maksimum 200 simvol ola bilər.";
    }

    if (!content) {
      nextErrors.content = "Elanın mətnini daxil edin.";
    } else if (content.length > 4000) {
      nextErrors.content = "Elanın mətni maksimum 4000 simvol ola bilər.";
    }

    setFieldErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    onSubmit({
      title,
      content,
      audience: values.audience,
      isPublished: values.isPublished,
    });
  }

  return (
    <div
      className="fixed inset-0 z-[150] flex items-center justify-center bg-black/75 px-4 py-6 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !isPending) onClose();
      }}
    >
      <form
        onSubmit={handleSubmit}
        className="flex max-h-[min(90vh,48rem)] w-full max-w-2xl flex-col overflow-hidden rounded-3xl border border-white/[0.09] bg-[#111116] shadow-2xl shadow-black/60"
      >
        <div className="flex items-start justify-between gap-4 border-b border-white/[0.07] px-5 py-5 sm:px-6">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.18em] text-violet-300">
              {announcement ? `Elan #${announcement.id}` : "Yeni elan"}
            </p>
            <h2 id={titleId} className="mt-2 text-xl font-bold text-white">
              {announcement ? "Elanı redaktə et" : "Elan yarat"}
            </h2>
          </div>
          <button
            type="button"
            aria-label="Pəncərəni bağla"
            disabled={isPending}
            onClick={onClose}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-white/[0.08] text-zinc-500 transition hover:bg-white/[0.05] hover:text-white disabled:opacity-50"
          >
            <X size={18} />
          </button>
        </div>

        <div className="overflow-y-auto px-5 py-5 sm:px-6">
          <div className="space-y-5">
            <label className="block">
              <span className="text-xs font-bold text-zinc-300">Başlıq</span>
              <input
                autoFocus
                value={values.title}
                maxLength={200}
                disabled={isPending}
                aria-invalid={Boolean(fieldErrors.title || titleApiError)}
                onChange={(event) => {
                  onClearError();
                  setValues((current) => ({
                    ...current,
                    title: event.target.value,
                  }));
                  setFieldErrors((current) => ({
                    ...current,
                    title: undefined,
                  }));
                }}
                placeholder="İstifadəçilərin görəcəyi elan başlığı"
                className={`mt-2 h-12 w-full rounded-xl border bg-white/[0.035] px-4 text-sm text-white outline-none transition placeholder:text-zinc-700 disabled:opacity-60 ${
                  fieldErrors.title || titleApiError
                    ? "border-red-400/45 focus:border-red-400"
                    : "border-white/[0.08] focus:border-violet-400/50"
                }`}
              />
              <FieldError message={fieldErrors.title ?? titleApiError} />
              <span className="mt-1 block text-right text-[11px] text-zinc-700">
                {values.title.length}/200
              </span>
            </label>

            <label className="block">
              <span className="text-xs font-bold text-zinc-300">Elan mətni</span>
              <textarea
                value={values.content}
                maxLength={4000}
                rows={10}
                disabled={isPending}
                aria-invalid={Boolean(fieldErrors.content || contentApiError)}
                onChange={(event) => {
                  onClearError();
                  setValues((current) => ({
                    ...current,
                    content: event.target.value,
                  }));
                  setFieldErrors((current) => ({
                    ...current,
                    content: undefined,
                  }));
                }}
                placeholder="Elanın tam mətnini daxil edin..."
                className={`mt-2 w-full resize-none rounded-xl border bg-white/[0.035] px-4 py-3 text-sm leading-6 text-white outline-none transition placeholder:text-zinc-700 disabled:opacity-60 ${
                  fieldErrors.content || contentApiError
                    ? "border-red-400/45 focus:border-red-400"
                    : "border-white/[0.08] focus:border-violet-400/50"
                }`}
              />
              <FieldError message={fieldErrors.content ?? contentApiError} />
              <span className="mt-1 block text-right text-[11px] text-zinc-700">
                {values.content.length}/4000
              </span>
            </label>

            <label className="block">
              <span className="text-xs font-bold text-zinc-300">
                {language === "en" ? "Audience" : "Auditoriya"}
              </span>
              <select
                value={values.audience}
                disabled={isPending || Boolean(announcement?.isPublished)}
                onChange={(event) => {
                  onClearError();
                  setValues((current) => ({
                    ...current,
                    audience: event.target.value as AnnouncementAudience,
                  }));
                }}
                className="mt-2 h-12 w-full rounded-xl border border-white/[0.08] bg-[#0a0a0e] px-4 text-sm text-zinc-200 outline-none transition focus:border-violet-400/50 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {announcementAudienceOptions.map((audience) => (
                  <option key={audience} value={audience}>
                    {getAudienceLabel(audience, language)}
                  </option>
                ))}
              </select>
              <p className="mt-2 text-xs leading-5 text-zinc-600">
                {announcement?.isPublished
                  ? language === "en"
                    ? "Unpublish the announcement before changing its audience."
                    : "Auditoriyanı dəyişmək üçün elanı əvvəlcə qaralama vəziyyətinə keçirin."
                  : language === "en"
                    ? "Only users matching this audience will see the announcement and receive its notification."
                    : "Elanı və bildirişi yalnız bu auditoriyaya uyğun istifadəçilər görəcək."}
              </p>
            </label>

            <div className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4">
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="text-sm font-semibold text-zinc-200">
                    Dərhal yayımla
                  </p>
                  <p className="mt-1 text-xs leading-5 text-zinc-600">
                    Aktiv olduqda elan yadda saxlanılan kimi istifadəçilərə
                    bildiriş göndərilir.
                  </p>
                </div>
                <button
                  type="button"
                  role="switch"
                  aria-checked={values.isPublished}
                  disabled={isPending}
                  onClick={() => {
                    onClearError();
                    setValues((current) => ({
                      ...current,
                      isPublished: !current.isPublished,
                    }));
                  }}
                  className={`relative h-7 w-13 shrink-0 rounded-full transition disabled:opacity-50 ${
                    values.isPublished ? "bg-violet-600" : "bg-zinc-700"
                  }`}
                >
                  <span
                    className={`absolute top-1 h-5 w-5 rounded-full bg-white transition ${
                      values.isPublished ? "left-7" : "left-1"
                    }`}
                  />
                </button>
              </div>
            </div>

            {generalApiError && (
              <div
                role="alert"
                className="flex items-start gap-2 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-200"
              >
                <CircleAlert size={17} className="mt-0.5 shrink-0" />
                {generalApiError}
              </div>
            )}
          </div>
        </div>

        <div className="flex flex-col-reverse gap-2 border-t border-white/[0.07] px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
          <button
            type="button"
            disabled={isPending}
            onClick={onClose}
            className="h-11 rounded-xl border border-white/[0.08] px-4 text-sm font-semibold text-zinc-300 transition hover:bg-white/[0.05] disabled:opacity-50"
          >
            Ləğv et
          </button>
          <button
            type="submit"
            disabled={isPending}
            className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-50"
          >
            {isPending ? (
              <LoaderCircle size={17} className="animate-spin" />
            ) : values.isPublished ? (
              <Send size={17} />
            ) : (
              <Check size={17} />
            )}
            {isPending
              ? "Yadda saxlanılır..."
              : values.isPublished
                ? "Yadda saxla və yayımla"
                : "Qaralama kimi saxla"}
          </button>
        </div>
      </form>
    </div>
  );
}

function FieldError({ message }: { message?: string | null }) {
  if (!message) return null;

  return (
    <p role="alert" className="mt-2 text-xs leading-5 text-red-300">
      {message}
    </p>
  );
}

function LoadingState() {
  return (
    <section className="flex min-h-72 items-center justify-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.02] text-sm text-zinc-500">
      <LoaderCircle size={19} className="animate-spin" />
      Elanlar yüklənir...
    </section>
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
    <section className="rounded-2xl border border-red-400/15 bg-red-500/[0.05] p-6 text-center">
      <CircleAlert size={25} className="mx-auto text-red-300" />
      <p className="mt-3 text-sm text-red-200">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-4 inline-flex h-9 items-center gap-2 rounded-lg border border-red-300/15 px-3 text-xs font-bold text-red-200"
      >
        <RotateCcw size={14} />
        Yenidən yoxla
      </button>
    </section>
  );
}

function Pagination({
  pageNumber,
  pageCount,
  totalCount,
  onPageChange,
}: {
  pageNumber: number;
  pageCount: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}) {
  return (
    <div className="flex flex-col gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.02] px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-xs text-zinc-600">
        Cəmi <strong className="text-zinc-400">{totalCount}</strong> elan ·
        Səhifə {pageNumber}/{pageCount}
      </p>
      <div className="flex items-center gap-2">
        <button
          type="button"
          disabled={pageNumber <= 1}
          onClick={() => onPageChange(pageNumber - 1)}
          className="inline-flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] px-3 text-xs font-semibold text-zinc-400 transition hover:text-white disabled:opacity-35"
        >
          <ChevronLeft size={15} />
          Əvvəlki
        </button>
        <button
          type="button"
          disabled={pageNumber >= pageCount}
          onClick={() => onPageChange(pageNumber + 1)}
          className="inline-flex h-9 items-center gap-2 rounded-lg border border-white/[0.08] px-3 text-xs font-semibold text-zinc-400 transition hover:text-white disabled:opacity-35"
        >
          Növbəti
          <ChevronRight size={15} />
        </button>
      </div>
    </div>
  );
}
