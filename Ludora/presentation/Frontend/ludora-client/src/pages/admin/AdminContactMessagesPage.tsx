import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock3,
  LoaderCircle,
  Mail,
  MessageSquareReply,
  RefreshCw,
  Send,
  UserRound,
  X,
  XCircle,
} from "lucide-react";
import { useMemo, useState, type FormEvent } from "react";
import { createPortal } from "react-dom";
import { Link, useSearchParams } from "react-router-dom";
import { AdminFilterSelect } from "../../components/admin/AdminFilterSelect";
import { AdminLiveSearch } from "../../components/admin/AdminLiveSearch";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { formatDateTime } from "../../lib/helpers";
import {
  getAdminContactMessage,
  getAdminContactMessages,
  replyToAdminContactMessage,
  updateAdminContactMessageStatus,
} from "../../services/adminService";
import type {
  AdminContactMessage,
  AdminContactMessageStatus,
  AdminContactMessageSubject,
} from "../../types/admin";

const pageSize = 15;

function getStatusLabel(status: AdminContactMessageStatus, language: "az" | "en") {
  const labels: Record<AdminContactMessageStatus, [string, string]> = {
    New: ["Yeni", "New"],
    InProgress: ["İşlənir", "In progress"],
    ReplyQueued: ["Cavab növbədədir", "Reply queued"],
    Replied: ["Cavablandırılıb", "Replied"],
    ReplyFailed: ["Göndəriş uğursuzdur", "Reply failed"],
    Closed: ["Bağlanıb", "Closed"],
  };
  return labels[status][language === "en" ? 1 : 0];
}

function getSubjectLabel(subject: AdminContactMessageSubject, language: "az" | "en") {
  const labels: Record<AdminContactMessageSubject, [string, string]> = {
    general: ["Ümumi sual", "General question"],
    account: ["Hesab və giriş", "Account and access"],
    bug: ["Xəta bildirişi", "Bug report"],
    feedback: ["Rəy və təklif", "Feedback and suggestion"],
    moderation: ["Təhlükəsizlik və moderasiya", "Safety and moderation"],
  };
  return labels[subject][language === "en" ? 1 : 0];
}

function getStatusClasses(status: AdminContactMessageStatus) {
  switch (status) {
    case "New":
      return "border-sky-400/20 bg-sky-500/[0.08] text-sky-300";
    case "InProgress":
      return "border-amber-400/20 bg-amber-500/[0.08] text-amber-300";
    case "ReplyQueued":
      return "border-violet-400/20 bg-violet-500/[0.08] text-violet-300";
    case "Replied":
      return "border-emerald-400/20 bg-emerald-500/[0.08] text-emerald-300";
    case "ReplyFailed":
      return "border-red-400/20 bg-red-500/[0.08] text-red-300";
    default:
      return "border-white/[0.1] bg-white/[0.04] text-zinc-400";
  }
}

export function AdminContactMessagesPage() {
  const { language } = useI18n();
  const isEnglish = language === "en";
  const [searchParams, setSearchParams] = useSearchParams();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [replyOpen, setReplyOpen] = useState(false);
  const [replySubject, setReplySubject] = useState("");
  const [replyBody, setReplyBody] = useState("");
  const queryClient = useQueryClient();

  const pageNumber = Math.max(1, Number(searchParams.get("page") ?? "1") || 1);
  const status = searchParams.get("status") as AdminContactMessageStatus | null;
  const subject = searchParams.get("subject") as AdminContactMessageSubject | null;
  const sortBy = searchParams.get("sort") ?? "newest";
  const search = searchParams.get("search") ?? "";

  const filter = {
    search: search || undefined,
    status: status || undefined,
    subject: subject || undefined,
    sortBy,
    pageNumber,
    pageSize,
  };

  const messagesQuery = useQuery({
    queryKey: ["admin", "contact-messages", filter],
    queryFn: ({ signal }) => getAdminContactMessages(filter, signal),
    placeholderData: keepPreviousData,
  });

  const currentItems = messagesQuery.data?.items ?? [];
  const effectiveSelectedId =
    selectedId !== null && currentItems.some((item) => item.id === selectedId)
      ? selectedId
      : currentItems[0]?.id ?? null;

  const detailQuery = useQuery({
    queryKey: ["admin", "contact-message", effectiveSelectedId],
    queryFn: ({ signal }) => getAdminContactMessage(effectiveSelectedId!, signal),
    enabled: effectiveSelectedId !== null,
  });

  const selectedMessage = detailQuery.data ??
    currentItems.find((item) => item.id === effectiveSelectedId) ??
    null;

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["admin", "contact-messages"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "contact-message"] }),
    ]);
  };

  const statusMutation = useMutation({
    mutationFn: ({ id, nextStatus }: { id: number; nextStatus: "New" | "InProgress" | "Closed" }) =>
      updateAdminContactMessageStatus(id, nextStatus),
    onSuccess: refresh,
  });

  const replyMutation = useMutation({
    mutationFn: ({ id, subject: emailSubject, body }: { id: number; subject: string; body: string }) =>
      replyToAdminContactMessage(id, { subject: emailSubject, body }),
    onSuccess: async () => {
      setReplyOpen(false);
      setReplyBody("");
      await refresh();
    },
  });

  const pageCount = messagesQuery.data
    ? Math.max(1, Math.ceil(messagesQuery.data.totalCount / messagesQuery.data.pageSize))
    : 1;

  const updateParam = (name: string, value: string) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value);
    else next.delete(name);
    next.delete("page");
    setSearchParams(next);
  };

  const setPage = (page: number) => {
    const next = new URLSearchParams(searchParams);
    if (page <= 1) next.delete("page");
    else next.set("page", String(page));
    setSearchParams(next);
  };

  const openReply = (message: AdminContactMessage) => {
    const topic = getSubjectLabel(message.subject, message.language);
    setReplySubject(`Re: [WellPlayd] ${topic}`);
    setReplyBody("");
    replyMutation.reset();
    setReplyOpen(true);
  };

  const submitReply = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedMessage) return;
    replyMutation.mutate({
      id: selectedMessage.id,
      subject: replySubject.trim(),
      body: replyBody.trim(),
    });
  };

  const statusOptions = useMemo(
    () => [
      ["", isEnglish ? "All statuses" : "Bütün statuslar"],
      ["New", getStatusLabel("New", language)],
      ["InProgress", getStatusLabel("InProgress", language)],
      ["ReplyQueued", getStatusLabel("ReplyQueued", language)],
      ["Replied", getStatusLabel("Replied", language)],
      ["ReplyFailed", getStatusLabel("ReplyFailed", language)],
      ["Closed", getStatusLabel("Closed", language)],
    ] as const,
    [isEnglish, language],
  );

  const subjectOptions = useMemo(
    () => [
      ["", isEnglish ? "All topics" : "Bütün mövzular"],
      ["general", getSubjectLabel("general", language)],
      ["account", getSubjectLabel("account", language)],
      ["bug", getSubjectLabel("bug", language)],
      ["feedback", getSubjectLabel("feedback", language)],
      ["moderation", getSubjectLabel("moderation", language)],
    ] as const,
    [isEnglish, language],
  );

  return (
    <div className="space-y-6">
      <section className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <p className="text-sm font-medium text-violet-300">
            {isEnglish ? "Support inbox" : "Dəstək qutusu"}
          </p>
          <h2 className="mt-2 text-3xl font-bold tracking-tight text-white">
            {isEnglish ? "Contact messages" : "Əlaqə mesajları"}
          </h2>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-zinc-500">
            {isEnglish
              ? "Review messages sent from the contact page, manage their state and reply by email."
              : "Əlaqə səhifəsindən göndərilən mesajlara bax, statuslarını idarə et və email ilə cavablandır."}
          </p>
        </div>

        <button
          type="button"
          onClick={refresh}
          disabled={messagesQuery.isFetching}
          className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.03] px-4 text-sm font-semibold text-zinc-300 transition hover:border-violet-400/25 hover:text-white disabled:opacity-60"
        >
          <RefreshCw size={16} className={messagesQuery.isFetching ? "animate-spin" : ""} />
          {isEnglish ? "Refresh" : "Yenilə"}
        </button>
      </section>

      <section className="grid gap-3 md:grid-cols-2 xl:grid-cols-[minmax(18rem,1fr)_auto_auto_auto]">
        <AdminLiveSearch
          placeholder={isEnglish ? "Search name, email or message" : "Ad, email və ya mesaj axtar"}
        />
        <AdminFilterSelect
          label={isEnglish ? "Status" : "Status"}
          value={status ?? ""}
          options={statusOptions}
          onChange={(value) => updateParam("status", value)}
          showFilterIcon
        />
        <AdminFilterSelect
          label={isEnglish ? "Topic" : "Mövzu"}
          value={subject ?? ""}
          options={subjectOptions}
          onChange={(value) => updateParam("subject", value)}
        />
        <AdminFilterSelect
          label={isEnglish ? "Sort" : "Sıralama"}
          value={sortBy}
          options={[
            ["newest", isEnglish ? "Newest" : "Ən yeni"],
            ["oldest", isEnglish ? "Oldest" : "Ən köhnə"],
            ["status", isEnglish ? "By status" : "Statusa görə"],
          ]}
          onChange={(value) => updateParam("sort", value)}
        />
      </section>

      {messagesQuery.isLoading ? (
        <div className="flex min-h-72 items-center justify-center rounded-3xl border border-white/[0.07] bg-white/[0.02]">
          <LoaderCircle size={26} className="animate-spin text-violet-300" />
        </div>
      ) : messagesQuery.isError ? (
        <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-5 text-sm text-red-300">
          {getApiErrorMessage(
            messagesQuery.error,
            isEnglish ? "Contact messages could not be loaded." : "Əlaqə mesajları yüklənmədi.",
          )}
        </div>
      ) : (messagesQuery.data?.items.length ?? 0) === 0 ? (
        <div className="flex min-h-72 flex-col items-center justify-center rounded-3xl border border-dashed border-white/[0.09] bg-white/[0.02] px-6 text-center">
          <Mail size={28} className="text-zinc-600" />
          <h3 className="mt-4 text-lg font-semibold text-zinc-300">
            {isEnglish ? "No matching messages" : "Uyğun mesaj tapılmadı"}
          </h3>
        </div>
      ) : (
        <div className="grid min-h-[36rem] gap-5 xl:grid-cols-[minmax(18rem,0.82fr)_minmax(0,1.18fr)]">
          <div className="space-y-3">
            {messagesQuery.data?.items.map((message) => (
              <button
                key={message.id}
                type="button"
                onClick={() => setSelectedId(message.id)}
                className={`w-full rounded-2xl border p-4 text-left transition ${
                  effectiveSelectedId === message.id
                    ? "border-violet-400/35 bg-violet-500/[0.08]"
                    : "border-white/[0.07] bg-white/[0.02] hover:border-white/[0.13] hover:bg-white/[0.04]"
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-semibold text-zinc-200">{message.name}</p>
                    <p className="mt-1 truncate text-xs text-zinc-600">{message.email}</p>
                  </div>
                  <span className={`shrink-0 rounded-full border px-2 py-1 text-[10px] font-bold ${getStatusClasses(message.status)}`}>
                    {getStatusLabel(message.status, language)}
                  </span>
                </div>
                <p className="mt-3 text-xs font-semibold text-violet-300">
                  {getSubjectLabel(message.subject, language)}
                </p>
                <p className="mt-2 line-clamp-2 text-sm leading-6 text-zinc-500">
                  {message.message}
                </p>
                <p className="mt-3 text-[11px] text-zinc-700">{formatDateTime(message.createdAtUtc)}</p>
              </button>
            ))}

            <div className="flex items-center justify-between gap-3 pt-2">
              <button
                type="button"
                disabled={pageNumber <= 1}
                onClick={() => setPage(pageNumber - 1)}
                className="flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] px-3 text-xs font-semibold text-zinc-400 disabled:opacity-40"
              >
                <ChevronLeft size={15} />
                {isEnglish ? "Previous" : "Əvvəlki"}
              </button>
              <span className="text-xs text-zinc-600">{pageNumber} / {pageCount}</span>
              <button
                type="button"
                disabled={pageNumber >= pageCount}
                onClick={() => setPage(pageNumber + 1)}
                className="flex h-10 items-center gap-2 rounded-xl border border-white/[0.08] px-3 text-xs font-semibold text-zinc-400 disabled:opacity-40"
              >
                {isEnglish ? "Next" : "Növbəti"}
                <ChevronRight size={15} />
              </button>
            </div>
          </div>

          <section className="rounded-3xl border border-white/[0.07] bg-white/[0.025] p-5 sm:p-6">
            {detailQuery.isFetching && !selectedMessage ? (
              <div className="flex min-h-72 items-center justify-center">
                <LoaderCircle size={24} className="animate-spin text-violet-300" />
              </div>
            ) : selectedMessage ? (
              <div>
                <div className="flex flex-col gap-4 border-b border-white/[0.07] pb-5 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={`rounded-full border px-2.5 py-1 text-[11px] font-bold ${getStatusClasses(selectedMessage.status)}`}>
                        {getStatusLabel(selectedMessage.status, language)}
                      </span>
                      <span className="rounded-full border border-white/[0.08] bg-white/[0.03] px-2.5 py-1 text-[11px] text-zinc-500">
                        #{selectedMessage.id}
                      </span>
                    </div>
                    <h3 className="mt-4 text-xl font-bold text-white">
                      {getSubjectLabel(selectedMessage.subject, language)}
                    </h3>
                    <p className="mt-2 text-xs text-zinc-600">{formatDateTime(selectedMessage.createdAtUtc)}</p>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    {selectedMessage.status !== "InProgress" &&
                     selectedMessage.status !== "ReplyQueued" &&
                     selectedMessage.status !== "Replied" && (
                      <button
                        type="button"
                        onClick={() => statusMutation.mutate({ id: selectedMessage.id, nextStatus: "InProgress" })}
                        disabled={statusMutation.isPending}
                        className="inline-flex h-10 items-center gap-2 rounded-xl border border-amber-400/20 bg-amber-500/[0.07] px-3 text-xs font-semibold text-amber-300"
                      >
                        <Clock3 size={15} />
                        {isEnglish ? "Mark in progress" : "İşlənir et"}
                      </button>
                    )}
                    {selectedMessage.status !== "Closed" && selectedMessage.status !== "ReplyQueued" && (
                      <button
                        type="button"
                        onClick={() => statusMutation.mutate({ id: selectedMessage.id, nextStatus: "Closed" })}
                        disabled={statusMutation.isPending}
                        className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/[0.09] bg-white/[0.03] px-3 text-xs font-semibold text-zinc-400"
                      >
                        <XCircle size={15} />
                        {isEnglish ? "Close" : "Bağla"}
                      </button>
                    )}
                    {selectedMessage.status === "Closed" && (
                      <button
                        type="button"
                        onClick={() => statusMutation.mutate({ id: selectedMessage.id, nextStatus: "New" })}
                        disabled={statusMutation.isPending}
                        className="inline-flex h-10 items-center gap-2 rounded-xl border border-sky-400/20 bg-sky-500/[0.07] px-3 text-xs font-semibold text-sky-300"
                      >
                        <RefreshCw size={15} />
                        {isEnglish ? "Reopen" : "Yenidən aç"}
                      </button>
                    )}
                  </div>
                </div>

                <div className="mt-5 grid gap-3 sm:grid-cols-2">
                  <div className="rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4">
                    <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.14em] text-zinc-600">
                      <UserRound size={14} /> {isEnglish ? "Sender" : "Göndərən"}
                    </div>
                    <p className="mt-3 font-semibold text-zinc-200">{selectedMessage.name}</p>
                    <a href={`mailto:${selectedMessage.email}`} className="mt-1 block break-all text-sm text-violet-300 hover:text-violet-200">
                      {selectedMessage.email}
                    </a>
                    {selectedMessage.userId && selectedMessage.userName && (
                      <Link to={`/admin/users/${selectedMessage.userId}`} className="mt-2 inline-block text-xs text-zinc-500 hover:text-white">
                        @{selectedMessage.userName}
                      </Link>
                    )}
                  </div>
                  <div className="rounded-2xl border border-white/[0.06] bg-white/[0.02] p-4">
                    <p className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-600">
                      {isEnglish ? "Message language" : "Mesaj dili"}
                    </p>
                    <p className="mt-3 text-sm font-semibold text-zinc-200">
                      {selectedMessage.language === "en" ? "English" : "Azərbaycan dili"}
                    </p>
                  </div>
                </div>

                <div className="mt-5 rounded-2xl border border-white/[0.07] bg-[#0a0a0e]/55 p-5">
                  <p className="whitespace-pre-wrap break-words text-sm leading-7 text-zinc-300">
                    {selectedMessage.message}
                  </p>
                </div>

                {selectedMessage.replyBody && (
                  <div className={`mt-5 rounded-2xl border p-5 ${
                    selectedMessage.status === "ReplyFailed"
                      ? "border-red-400/15 bg-red-500/[0.05]"
                      : "border-emerald-400/15 bg-emerald-500/[0.05]"
                  }`}>
                    <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
                      <CheckCircle2 size={15} />
                      {isEnglish ? "Support reply" : "Dəstək cavabı"}
                    </div>
                    <p className="mt-3 font-semibold text-zinc-200">{selectedMessage.replySubject}</p>
                    <p className="mt-3 whitespace-pre-wrap break-words text-sm leading-7 text-zinc-400">
                      {selectedMessage.replyBody}
                    </p>
                    <p className="mt-3 text-xs text-zinc-600">
                      {selectedMessage.repliedAtUtc
                        ? formatDateTime(selectedMessage.repliedAtUtc)
                        : selectedMessage.replyQueuedAtUtc
                          ? formatDateTime(selectedMessage.replyQueuedAtUtc)
                          : ""}
                      {selectedMessage.repliedByAdminUserName
                        ? ` · ${selectedMessage.repliedByAdminUserName}`
                        : ""}
                    </p>
                  </div>
                )}

                {statusMutation.isError && (
                  <div className="mt-4 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-300">
                    {getApiErrorMessage(statusMutation.error, isEnglish ? "Status could not be updated." : "Status yenilənmədi.")}
                  </div>
                )}

                <div className="mt-6 flex justify-end">
                  <button
                    type="button"
                    onClick={() => openReply(selectedMessage)}
                    disabled={selectedMessage.status === "ReplyQueued" || selectedMessage.status === "Replied"}
                    className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <MessageSquareReply size={17} />
                    {selectedMessage.status === "ReplyFailed"
                      ? isEnglish ? "Retry reply" : "Cavabı yenidən göndər"
                      : isEnglish ? "Reply by email" : "Email ilə cavablandır"}
                  </button>
                </div>
              </div>
            ) : null}
          </section>
        </div>
      )}

      {replyOpen && selectedMessage && createPortal(
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
          <button
            type="button"
            aria-label={isEnglish ? "Close reply modal" : "Cavab modalını bağla"}
            className="absolute inset-0 bg-black/75 backdrop-blur-sm"
            onClick={() => !replyMutation.isPending && setReplyOpen(false)}
          />
          <form
            onSubmit={submitReply}
            className="relative z-10 w-full max-w-2xl rounded-3xl border border-white/[0.1] bg-[#15151d] p-5 shadow-2xl sm:p-6"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.16em] text-violet-300">
                  {isEnglish ? "Email reply" : "Email cavabı"}
                </p>
                <h3 className="mt-2 text-xl font-bold text-white">{selectedMessage.email}</h3>
              </div>
              <button
                type="button"
                onClick={() => setReplyOpen(false)}
                className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/[0.05] text-zinc-400"
              >
                <X size={17} />
              </button>
            </div>

            <label className="mt-6 block text-sm font-semibold text-zinc-300">
              {isEnglish ? "Subject" : "Başlıq"}
              <input
                value={replySubject}
                onChange={(event) => setReplySubject(event.target.value)}
                maxLength={300}
                required
                className="mt-2 h-11 w-full rounded-xl border border-white/[0.09] bg-black/20 px-4 text-sm text-white outline-none focus:border-violet-400/50"
              />
            </label>

            <label className="mt-4 block text-sm font-semibold text-zinc-300">
              {isEnglish ? "Reply" : "Cavab"}
              <textarea
                value={replyBody}
                onChange={(event) => setReplyBody(event.target.value)}
                minLength={5}
                maxLength={5000}
                required
                className="mt-2 min-h-56 w-full resize-y rounded-xl border border-white/[0.09] bg-black/20 px-4 py-3 text-sm leading-7 text-white outline-none focus:border-violet-400/50"
                placeholder={isEnglish ? "Write the support reply..." : "Dəstək cavabını yaz..."}
              />
            </label>

            {replyMutation.isError && (
              <div className="mt-4 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm text-red-300">
                {getApiErrorMessage(replyMutation.error, isEnglish ? "The reply could not be queued." : "Cavab göndəriş növbəsinə əlavə edilmədi.")}
              </div>
            )}

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setReplyOpen(false)}
                disabled={replyMutation.isPending}
                className="h-11 rounded-xl border border-white/[0.09] px-5 text-sm font-semibold text-zinc-400"
              >
                {isEnglish ? "Cancel" : "Ləğv et"}
              </button>
              <button
                type="submit"
                disabled={replyMutation.isPending}
                className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white disabled:opacity-60"
              >
                {replyMutation.isPending ? <LoaderCircle size={16} className="animate-spin" /> : <Send size={16} />}
                {replyMutation.isPending
                  ? isEnglish ? "Queuing..." : "Növbəyə əlavə edilir..."
                  : isEnglish ? "Send reply" : "Cavabı göndər"}
              </button>
            </div>
          </form>
        </div>,
        document.body,
      )}
    </div>
  );
}
