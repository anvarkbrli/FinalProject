import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowLeft,
  Ban,
  CalendarDays,
  ChevronDown,
  ChevronRight,
  ChevronUp,
  CircleAlert,
  FileImage,
  History,
  LoaderCircle,
  MessageSquareWarning,
  Save,
  ShieldAlert,
  UserRound,
  X,
} from "lucide-react";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { createPortal } from "react-dom";
import { Link, useParams } from "react-router-dom";
import { getApiErrorMessage } from "../../lib/api";
import { useConfirmation } from "../../lib/confirmation";
import {
  formatDateTime,
  resolveAssetUrl,
} from "../../lib/helpers";
import { getModerationActionTitle } from "../../lib/moderationAudit";
import {
  getModerationAuditByReport,
  getReportedUser,
  getReportEvidence,
  updateReportStatus,
} from "../../services/adminService";
import type {
  AdminUserReportItem,
  ModeratorActionLog,
  UserReportCategory,
  UserReportContentType,
  UserReportStatus,
} from "../../types/admin";

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

const contentTypeLabels: Record<UserReportContentType, string> = {
  UserProfile: "İstifadəçi profili",
  Post: "Post",
  PostComment: "Post şərhi",
  Review: "Rəy",
  ReviewComment: "Rəy şərhi",
  Discussion: "Müzakirə",
  DiscussionReply: "Müzakirə cavabı",
  DirectMessage: "Şəxsi mesaj",
  Community: "İcma",
};

const statusLabels: Record<UserReportStatus, string> = {
  Pending: "Gözləyir",
  UnderReview: "Araşdırılır",
  Resolved: "Həll edilib",
  Rejected: "Rədd edilib",
};

const statusClasses: Record<UserReportStatus, string> = {
  Pending: "bg-amber-500/10 text-amber-200",
  UnderReview: "bg-blue-500/10 text-blue-200",
  Resolved: "bg-emerald-500/10 text-emerald-200",
  Rejected: "bg-zinc-500/10 text-zinc-400",
};

export function AdminReportedUserPage() {
  const { userId } = useParams<{ userId: string }>();
  const [selectedCategory, setSelectedCategory] =
    useState<UserReportCategory | null>(null);

  const detailQuery = useQuery({
    queryKey: ["admin", "reported-user", userId],
    queryFn: ({ signal }) => getReportedUser(userId!, signal),
    enabled: Boolean(userId),
  });

  if (detailQuery.isPending) {
    return <DetailSkeleton />;
  }

  if (detailQuery.isError || !detailQuery.data) {
    return (
      <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-6">
        <CircleAlert className="text-red-300" size={22} />
        <h2 className="mt-3 font-semibold text-red-100">
          Şikayət detalları yüklənmədi
        </h2>
        <p className="mt-2 text-sm text-red-200/70">
          {getApiErrorMessage(detailQuery.error)}
        </p>
        <div className="mt-4 flex gap-3">
          <Link
            to="/admin/reports"
            className="rounded-xl bg-white/[0.06] px-4 py-2 text-sm font-semibold text-zinc-200"
          >
            Geri qayıt
          </Link>
          <button
            type="button"
            onClick={() => detailQuery.refetch()}
            className="rounded-xl bg-red-400/10 px-4 py-2 text-sm font-semibold text-red-100"
          >
            Yenidən yoxla
          </button>
        </div>
      </div>
    );
  }

  const user = detailQuery.data;
  const avatarUrl = resolveAssetUrl(user.profileImageUrl);
  const filteredReports = selectedCategory
    ? user.reports.filter(
        (report) => report.category === selectedCategory,
      )
    : user.reports;

  return (
    <div className="space-y-6">
      <Link
        to="/admin/reports"
        className="inline-flex items-center gap-2 text-sm font-semibold text-zinc-500 transition hover:text-violet-200"
      >
        <ArrowLeft size={16} />
        Şikayətlərə qayıt
      </Link>

      <section className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.025]">
        <div className="h-24 bg-gradient-to-r from-red-950/40 via-violet-950/35 to-[#0b0b0f]" />
        <div className="-mt-9 flex flex-col justify-between gap-5 px-5 pb-6 sm:flex-row sm:items-end sm:px-7">
          <div className="flex items-end gap-4">
            <span className="flex h-20 w-20 shrink-0 items-center justify-center overflow-hidden rounded-2xl border-4 border-[#0b0b0f] bg-violet-500/15 text-2xl font-bold text-violet-200">
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
            <div className="pb-1">
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-2xl font-bold text-white">
                  {user.userName}
                </h2>
                {user.isBanned && (
                  <span className="rounded-full bg-red-500/10 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-red-300">
                    Banlanıb
                  </span>
                )}
              </div>
              <p className="mt-2 text-sm text-zinc-500">
                {user.email ?? "Email göstərilməyib"}
              </p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link
              to={`/users/${user.userName}`}
              className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] px-3 text-xs font-semibold text-zinc-300 transition hover:bg-white/[0.05]"
            >
              <UserRound size={15} />
              Profili aç
            </Link>
            <Link
              to={`/admin/users/${user.userId}`}
              className="inline-flex h-10 items-center gap-2 rounded-xl bg-violet-500/12 px-3 text-xs font-semibold text-violet-200 transition hover:bg-violet-500/20"
            >
              <ShieldAlert size={15} />
              Moderasiya et
            </Link>
          </div>
        </div>
      </section>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard
          label="Ümumi şikayət"
          value={user.totalReportCount}
          tone="red"
        />
        <SummaryCard
          label="Açıq şikayət"
          value={user.pendingReportCount}
          tone="amber"
        />
        <SummaryCard
          label="Xəbərdarlıq"
          value={user.warningCount}
          tone="violet"
        />
        <SummaryCard
          label="Kateqoriya"
          value={user.categories.length}
          tone="zinc"
        />
      </section>

      {user.isBanned && (
        <section className="rounded-2xl border border-red-400/10 bg-red-500/[0.045] p-5">
          <div className="flex items-start gap-3">
            <Ban size={19} className="mt-0.5 shrink-0 text-red-300" />
            <div>
              <p className="text-sm font-semibold text-red-200">
                {user.isPermanentlyBanned
                  ? "İstifadəçi müddətsiz banlanıb"
                  : `Ban bitir: ${formatDateTime(user.bannedUntilUtc)}`}
              </p>
              {user.banReason && (
                <p className="mt-2 text-sm leading-6 text-zinc-500">
                  {user.banReason}
                </p>
              )}
            </div>
          </div>
        </section>
      )}

      <section>
        <div className="mb-4 flex flex-col justify-between gap-3 sm:flex-row sm:items-end">
          <div>
            <h3 className="text-xl font-bold text-white">
              Şikayət müraciətləri
            </h3>
            <p className="mt-1 text-sm text-zinc-600">
              Ən yeni müraciət yuxarıda göstərilir.
            </p>
          </div>
          <div className="flex flex-wrap gap-1.5">
            <button
              type="button"
              onClick={() => setSelectedCategory(null)}
              aria-pressed={selectedCategory === null}
              className={`rounded-lg border px-2.5 py-1.5 text-xs font-semibold transition ${
                selectedCategory === null
                  ? "border-violet-400/30 bg-violet-500/15 text-violet-200"
                  : "border-white/[0.07] bg-white/[0.03] text-zinc-500 hover:border-white/[0.12] hover:text-zinc-300"
              }`}
            >
              Hamısı · {user.totalReportCount}
            </button>
            {user.categories.map((category) => (
              <button
                type="button"
                key={category.category}
                onClick={() =>
                  setSelectedCategory((current) =>
                    current === category.category
                      ? null
                      : category.category,
                  )
                }
                aria-pressed={
                  selectedCategory === category.category
                }
                className={`rounded-lg border px-2.5 py-1.5 text-xs font-semibold transition ${
                  selectedCategory === category.category
                    ? "border-violet-400/30 bg-violet-500/15 text-violet-200"
                    : "border-white/[0.07] bg-white/[0.03] text-zinc-500 hover:border-white/[0.12] hover:text-zinc-300"
                }`}
              >
                {categoryLabels[category.category]} · {category.count}
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-4">
          {filteredReports.length > 0 ? (
            filteredReports.map((report) => (
              <ReportCard
                key={`${report.id}-${report.status}-${report.reviewedAtUtc ?? ""}`}
                report={report}
                userId={user.userId}
              />
            ))
          ) : (
            <div className="rounded-2xl border border-dashed border-white/[0.08] px-5 py-10 text-center text-sm text-zinc-600">
              Bu kateqoriyada şikayət yoxdur.
            </div>
          )}
        </div>
      </section>
    </div>
  );
}

function ReportCard({
  report,
  userId,
}: {
  report: AdminUserReportItem;
  userId: string;
}) {
  const queryClient = useQueryClient();
  const requestConfirmation = useConfirmation();
  const [status, setStatus] = useState<UserReportStatus>(report.status);
  const [adminNote, setAdminNote] = useState(report.adminNote ?? "");
  const [notice, setNotice] = useState<string | null>(null);
  const [auditOpen, setAuditOpen] = useState(false);
  const [isExpanded, setIsExpanded] = useState(true);
  const contentRegionId = `report-${report.id}-content`;

  const auditQuery = useQuery({
    queryKey: ["admin", "report", report.id, "audit"],
    queryFn: ({ signal }) =>
      getModerationAuditByReport(report.id, signal),
    enabled: auditOpen,
  });

  const statusMutation = useMutation({
    mutationFn: () =>
      updateReportStatus(report.id, {
        status,
        adminNote: adminNote.trim() || undefined,
      }),
    onSuccess: async () => {
      setNotice("Şikayət statusu yeniləndi.");
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["admin", "reported-user", userId],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "reports"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["admin", "report", report.id, "audit"],
        }),
      ]);
    },
  });

  const hasChanges =
    status !== report.status ||
    adminNote.trim() !== (report.adminNote ?? "");

  return (
    <article className="rounded-2xl border border-white/[0.07] bg-white/[0.025]">
      <div className="flex flex-col justify-between gap-3 border-b border-white/[0.06] p-5 sm:flex-row sm:items-start">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-bold text-white">
              Şikayət #{report.id}
            </span>
            <span className="rounded-full bg-red-500/[0.08] px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-red-200">
              {categoryLabels[report.category]}
            </span>
            <span className={`rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide ${statusClasses[report.status]}`}>
              {statusLabels[report.status]}
            </span>
          </div>
          <p className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-zinc-600">
            <span>
              Şikayət edən:{" "}
              <Link
                to={`/users/${report.reporterUserName}`}
                className="font-semibold text-zinc-400 hover:text-violet-200"
              >
                {report.reporterUserName}
              </Link>
            </span>
            <span>·</span>
            <span>{formatDateTime(report.createdAtUtc)}</span>
            <span>·</span>
            <span>
              {contentTypeLabels[report.reportedContentType]}
              {report.reportedContentId
                ? ` #${report.reportedContentId}`
                : ""}
            </span>
          </p>
        </div>
        <button
          type="button"
          onClick={() => setIsExpanded((current) => !current)}
          aria-expanded={isExpanded}
          aria-controls={contentRegionId}
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-white/[0.07] bg-white/[0.025] text-zinc-500 transition duration-300 hover:border-violet-400/25 hover:bg-violet-500/[0.08] hover:text-violet-200"
          title={isExpanded ? "Şikayəti yığ" : "Şikayəti aç"}
        >
          {isExpanded ? (
            <ChevronUp size={19} />
          ) : (
            <ChevronDown size={19} />
          )}
        </button>
      </div>

      <div
        id={contentRegionId}
        aria-hidden={!isExpanded}
        className={`grid transition-[grid-template-rows,opacity] duration-500 ease-in-out ${
          isExpanded
            ? "grid-rows-[1fr] opacity-100"
            : "pointer-events-none grid-rows-[0fr] opacity-0"
        }`}
      >
        <div className="overflow-hidden">
          <div className="grid gap-5 p-5 xl:grid-cols-[minmax(0,1fr)_22rem]">
            <div className="space-y-4">
          <section>
            <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-zinc-600">
              Şikayət mətni
            </p>
            <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-zinc-300">
              {report.description || "Əlavə açıqlama yazılmayıb."}
            </p>
          </section>

          {report.reportedContentSnapshot && (
            <section className="rounded-xl border border-white/[0.06] bg-black/15 p-4">
              <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-zinc-600">
                Məzmunun saxlanmış görüntüsü
              </p>
              <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-6 text-zinc-400">
                {report.reportedContentSnapshot}
              </p>
            </section>
          )}

          {report.reportedContentUrl && (
            <a
              href={report.reportedContentUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-2 text-sm font-semibold text-violet-300 transition hover:text-violet-200"
            >
              <ChevronRight size={15} />
              Şikayət olunan məzmunu aç
            </a>
          )}

          {report.evidenceImageCount > 0 && (
            <section>
              <p className="mb-3 flex items-center gap-2 text-xs font-semibold text-zinc-400">
                <FileImage size={15} />
                Sübut faylları ({report.evidenceImageCount})
              </p>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {Array.from(
                  { length: report.evidenceImageCount },
                  (_, index) => (
                    <EvidenceMedia
                      key={index}
                      reportId={report.id}
                      index={index}
                    />
                  ),
                )}
              </div>
            </section>
          )}
            </div>

            <aside className="rounded-xl border border-white/[0.07] bg-[#09090d] p-4">
          <div className="flex items-center gap-2">
            <MessageSquareWarning size={17} className="text-violet-300" />
            <h4 className="text-sm font-bold text-white">
              Moderasiya qərarı
            </h4>
          </div>

          <label className="mt-4 block">
            <span className="text-xs font-semibold text-zinc-500">
              Status
            </span>
            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value as UserReportStatus);
                setNotice(null);
              }}
              disabled={statusMutation.isPending}
              className="mt-2 h-11 w-full rounded-xl border border-white/[0.08] bg-[#0d0d12] px-3 text-sm text-zinc-200 outline-none focus:border-violet-500/50"
            >
              {Object.entries(statusLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>

          <label className="mt-4 block">
            <span className="text-xs font-semibold text-zinc-500">
              Admin qeydi
            </span>
            <textarea
              value={adminNote}
              onChange={(event) => {
                setAdminNote(event.target.value);
                setNotice(null);
              }}
              disabled={statusMutation.isPending}
              maxLength={1000}
              rows={4}
              placeholder="Qərarın səbəbini qeyd et..."
              className="mt-2 w-full resize-none rounded-xl border border-white/[0.08] bg-[#0d0d12] p-3 text-sm leading-6 text-zinc-200 outline-none placeholder:text-zinc-700 focus:border-violet-500/50"
            />
          </label>

          {statusMutation.isError && (
            <p className="mt-3 text-xs leading-5 text-red-300">
              {getApiErrorMessage(statusMutation.error)}
            </p>
          )}
          {notice && (
            <p className="mt-3 text-xs leading-5 text-emerald-300">
              {notice}
            </p>
          )}

          <button
            type="button"
            disabled={!hasChanges || statusMutation.isPending}
            onClick={async () => {
              const confirmed =
                await requestConfirmation({
                  title: `Şikayətin statusu “${statusLabels[status]}” olaraq yadda saxlanılsın?`,
                  description:
                    "Bu seçim moderasiya qərarı kimi qeydə alınacaq və şikayətin sonrakı emal vəziyyətini dəyişəcək.",
                  detail: adminNote.trim()
                    ? `Admin qeydi: ${adminNote.trim()}`
                    : "Qərar üçün admin qeydi daxil edilməyib.",
                  confirmLabel: "Qərarı yadda saxla",
                  kind: "moderation",
                  tone:
                    status === "Rejected"
                      ? "warning"
                      : "neutral",
                });

              if (confirmed) {
                statusMutation.mutate();
              }
            }}
            className="mt-4 inline-flex h-10 w-full items-center justify-center gap-2 rounded-xl bg-violet-600 text-sm font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {statusMutation.isPending ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <Save size={16} />
            )}
            Qərarı yadda saxla
          </button>

          {report.reviewedByUserName && (
            <p className="mt-4 border-t border-white/[0.06] pt-4 text-xs leading-5 text-zinc-600">
              Son baxan:{" "}
              <span className="font-semibold text-zinc-400">
                {report.reviewedByUserName}
              </span>
              {report.reviewedAtUtc && (
                <> · {formatDateTime(report.reviewedAtUtc)}</>
              )}
            </p>
          )}

          <button
            type="button"
            onClick={() => setAuditOpen((current) => !current)}
            className="mt-3 inline-flex items-center gap-2 text-xs font-semibold text-zinc-500 transition hover:text-violet-200"
          >
            <History size={14} />
            {auditOpen ? "Audit tarixçəsini bağla" : "Audit tarixçəsini aç"}
          </button>
            </aside>
          </div>

          {auditOpen && (
            <AuditTimeline
              queryState={{
                isPending: auditQuery.isPending,
                isError: auditQuery.isError,
                error: auditQuery.error,
                data: auditQuery.data,
              }}
            />
          )}
        </div>
      </div>
    </article>
  );
}

function EvidenceMedia({
  reportId,
  index,
}: {
  reportId: number;
  index: number;
}) {
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const revokeTimer = useRef<number | null>(null);
  const imageQuery = useQuery({
    queryKey: ["admin", "report", reportId, "evidence", index],
    queryFn: ({ signal }) => getReportEvidence(reportId, index, signal),
    staleTime: Number.POSITIVE_INFINITY,
    retry: false,
  });

  const imageUrl = useMemo(
    () => imageQuery.data
      ? URL.createObjectURL(imageQuery.data)
      : null,
    [imageQuery.data],
  );

  useEffect(() => {
    if (revokeTimer.current !== null) {
      window.clearTimeout(revokeTimer.current);
      revokeTimer.current = null;
    }

    return () => {
      if (imageUrl) {
        revokeTimer.current = window.setTimeout(() => {
          URL.revokeObjectURL(imageUrl);
          revokeTimer.current = null;
        }, 1_000);
      }
    };
  }, [imageUrl]);

  useEffect(() => {
    if (!isPreviewOpen) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setIsPreviewOpen(false);
    };

    document.addEventListener("keydown", handleKeyDown);
    document.body.style.overflow = "hidden";

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "";
    };
  }, [isPreviewOpen]);

  if (imageQuery.isPending) {
    return (
      <div className="aspect-video animate-pulse rounded-xl bg-white/[0.05]" />
    );
  }

  if (imageQuery.isError || !imageUrl) {
    return (
      <div className="flex aspect-video items-center justify-center rounded-xl border border-red-400/10 bg-red-500/[0.04] px-3 text-center text-xs text-red-300">
        Sübut faylı yüklənmədi
      </div>
    );
  }

  const isVideo = imageQuery.data.type.startsWith("video/");

  return (
    <>
      <button
        type="button"
        onClick={() => setIsPreviewOpen(true)}
        className="group relative block aspect-video w-full overflow-hidden rounded-xl border border-white/[0.08] bg-black/30"
        aria-label={`Şikayət #${reportId}, sübut ${index + 1} faylını böyüt`}
      >
        {isVideo ? (
          <video
            src={imageUrl}
            muted
            preload="metadata"
            className="h-full w-full object-cover transition duration-300 group-hover:scale-[1.03]"
          />
        ) : (
          <img
            src={imageUrl}
            alt={`Şikayət #${reportId}, sübut ${index + 1}`}
            className="h-full w-full object-cover transition duration-300 group-hover:scale-[1.03]"
          />
        )}
        <span className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/80 to-transparent px-3 pb-2 pt-7 text-left text-[10px] font-semibold text-white/75">
          {isVideo ? "Videonu böyüt" : "Şəkli böyüt"}
        </span>
      </button>

      {isPreviewOpen && createPortal(
        <div
          className="fixed inset-0 z-[2000] flex items-center justify-center bg-black/75 p-4 backdrop-blur-xl sm:p-8"
          role="dialog"
          aria-modal="true"
          aria-label={`Şikayət #${reportId} sübut önizləməsi`}
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              setIsPreviewOpen(false);
            }
          }}
        >
          <button
            type="button"
            onClick={() => setIsPreviewOpen(false)}
            className="absolute right-4 top-4 flex h-11 w-11 items-center justify-center rounded-full border border-white/10 bg-black/45 text-white backdrop-blur transition hover:bg-white/15 sm:right-7 sm:top-7"
            aria-label="Sübut önizləməsini bağla"
          >
            <X size={21} />
          </button>

          <div className="flex max-h-[90dvh] max-w-[94vw] items-center justify-center overflow-hidden rounded-2xl border border-white/10 bg-black/40 shadow-2xl shadow-black">
            {isVideo ? (
              <video
                src={imageUrl}
                controls
                autoPlay
                preload="metadata"
                className="max-h-[88dvh] max-w-[92vw]"
              />
            ) : (
              <img
                src={imageUrl}
                alt={`Şikayət #${reportId}, sübut ${index + 1}`}
                className="max-h-[88dvh] max-w-[92vw] object-contain"
              />
            )}
          </div>
        </div>,
        document.body,
      )}
    </>
  );
}

function AuditTimeline({
  queryState,
}: {
  queryState: {
    isPending: boolean;
    isError: boolean;
    error: unknown;
    data: ModeratorActionLog[] | undefined;
  };
}) {
  return (
    <div className="border-t border-white/[0.06] px-5 py-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-zinc-600">
        Audit tarixçəsi
      </p>
      {queryState.isPending ? (
        <div className="mt-3 h-12 animate-pulse rounded-xl bg-white/[0.035]" />
      ) : queryState.isError ? (
        <p className="mt-3 text-xs text-red-300">
          {getApiErrorMessage(queryState.error)}
        </p>
      ) : queryState.data && queryState.data.length > 0 ? (
        <div className="mt-3 space-y-3">
          {queryState.data.map((entry) => (
            <div
              key={entry.id}
              className="flex flex-col justify-between gap-2 rounded-xl border border-white/[0.06] bg-black/10 p-3 sm:flex-row sm:items-center"
            >
              <div>
                <p className="text-xs font-semibold text-zinc-300">
                  {getModerationActionTitle(entry)}
                </p>
                <p className="mt-1 text-xs text-zinc-600">
                  Əməliyyatı edən: {entry.adminUserName}
                </p>
                {entry.reason && (
                  <p className="mt-1 text-xs leading-5 text-zinc-500">
                    Qeyd: {entry.reason}
                  </p>
                )}
              </div>
              <span className="flex shrink-0 items-center gap-1.5 text-xs text-zinc-600">
                <CalendarDays size={13} />
                {formatDateTime(entry.createdAtUtc)}
              </span>
            </div>
          ))}
        </div>
      ) : (
        <p className="mt-3 text-xs text-zinc-600">
          Bu şikayət üzrə audit qeydi yoxdur.
        </p>
      )}
    </div>
  );
}

function SummaryCard({
  label,
  value,
  tone,
}: {
  label: string;
  value: number;
  tone: "red" | "amber" | "violet" | "zinc";
}) {
  const toneClasses = {
    red: "text-red-200 bg-red-500/[0.06]",
    amber: "text-amber-200 bg-amber-500/[0.06]",
    violet: "text-violet-200 bg-violet-500/[0.06]",
    zinc: "text-zinc-200 bg-white/[0.025]",
  };

  return (
    <div className={`rounded-2xl border border-white/[0.07] p-5 ${toneClasses[tone]}`}>
      <p className="text-xs font-semibold text-zinc-500">{label}</p>
      <p className="mt-2 text-2xl font-bold">{value.toLocaleString("az-AZ")}</p>
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="animate-pulse space-y-5">
      <div className="h-5 w-36 rounded bg-white/[0.05]" />
      <div className="h-48 rounded-2xl bg-white/[0.04]" />
      <div className="grid gap-3 sm:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <div key={index} className="h-24 rounded-2xl bg-white/[0.04]" />
        ))}
      </div>
      <div className="h-96 rounded-2xl bg-white/[0.04]" />
    </div>
  );
}
