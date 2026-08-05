import {
  Check,
  CheckSquare2,
  ChevronLeft,
  LoaderCircle,
  Search,
  Square,
  UserCheck,
  UserX,
  UsersRound,
  X,
} from "lucide-react";
import {
  useDeferredValue,
  useEffect,
  useMemo,
  useState,
} from "react";
import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatRelativeDate,
  resolveAssetUrl,
} from "../../lib/helpers";
import {
  acceptFollowRequest,
  bulkAcceptFollowRequests,
  bulkRejectFollowRequests,
  getFollowRequests,
  rejectFollowRequest,
} from "../../services/settingsService";

interface FollowRequestsModalProps {
  profileUserName: string;
  onBack: () => void;
  onClose: () => void;
}

type RequestAction = "accept" | "reject";

interface RequestMutationInput {
  requesterId: string;
  action: RequestAction;
}

interface NoticeState {
  tone: "success" | "error";
  message: string;
}

function RequestAvatar({
  userName,
  profileImageUrl,
}: {
  userName: string;
  profileImageUrl: string | null;
}) {
  const imageUrl = resolveAssetUrl(profileImageUrl);
  const [failedImageUrl, setFailedImageUrl] = useState<string | null>(null);

  return (
    <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.08] bg-violet-500/10 text-sm font-bold text-violet-200">
      {imageUrl && failedImageUrl !== imageUrl ? (
        <img
          src={imageUrl}
          alt=""
          className="h-full w-full object-cover"
          onError={() => setFailedImageUrl(imageUrl)}
        />
      ) : (
        userName.trim().charAt(0).toUpperCase() || "?"
      )}
    </span>
  );
}

export function FollowRequestsModal({
  profileUserName,
  onBack,
  onClose,
}: FollowRequestsModalProps) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [searchInput, setSearchInput] = useState("");
  const deferredSearchInput = useDeferredValue(searchInput);
  const search = deferredSearchInput.trim();
  const [isManaging, setIsManaging] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(
    () => new Set(),
  );
  const [notice, setNotice] = useState<NoticeState | null>(null);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    document.addEventListener("keydown", handleEscape);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleEscape);
    };
  }, [onClose]);

  const requestsQuery = useQuery({
    queryKey: ["follow-requests", "list", search],
    queryFn: ({ signal }) =>
      getFollowRequests({
        search,
        pageNumber: 1,
        pageSize: 100,
        signal,
      }),
  });

  const requests = useMemo(
    () => requestsQuery.data?.items ?? [],
    [requestsQuery.data?.items],
  );
  const visibleIds = useMemo(
    () => requests.map((request) => request.userId),
    [requests],
  );
  const allVisibleSelected =
    visibleIds.length > 0 &&
    visibleIds.every((id) => selectedIds.has(id));

  const invalidateFollowData = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["follow-requests"] }),
      queryClient.invalidateQueries({
        queryKey: ["user-profile", profileUserName],
      }),
      queryClient.invalidateQueries({ queryKey: ["user-connections"] }),
      queryClient.invalidateQueries({ queryKey: ["current-user"] }),
      queryClient.invalidateQueries({ queryKey: ["notifications"] }),
    ]);
  };

  const requestMutation = useMutation({
    mutationFn: async ({ requesterId, action }: RequestMutationInput) => {
      if (action === "accept") {
        await acceptFollowRequest(requesterId);
      } else {
        await rejectFollowRequest(requesterId);
      }

      return action;
    },
    onSuccess: async (action, variables) => {
      setSelectedIds((current) => {
        const next = new Set(current);
        next.delete(variables.requesterId);
        return next;
      });
      setNotice({
        tone: "success",
        message: t(
          action === "accept"
            ? "profile:followRequests.accepted"
            : "profile:followRequests.rejected",
        ),
      });
      await invalidateFollowData();
    },
    onError: (error) => {
      setNotice({
        tone: "error",
        message: getApiErrorMessage(
          error,
          t("profile:followRequests.actionError"),
        ),
      });
    },
  });

  const bulkMutation = useMutation({
    mutationFn: async ({
      requesterIds,
      action,
    }: {
      requesterIds: string[];
      action: RequestAction;
    }) => {
      const result =
        action === "accept"
          ? await bulkAcceptFollowRequests(requesterIds)
          : await bulkRejectFollowRequests(requesterIds);

      return { ...result, action };
    },
    onSuccess: async (result) => {
      setSelectedIds(new Set());
      setNotice({
        tone: "success",
        message: t(
          result.action === "accept"
            ? "profile:followRequests.bulkAccepted"
            : "profile:followRequests.bulkRejected",
          { count: result.processedCount },
        ),
      });
      await invalidateFollowData();
    },
    onError: (error) => {
      setNotice({
        tone: "error",
        message: getApiErrorMessage(
          error,
          t("profile:followRequests.actionError"),
        ),
      });
    },
  });

  const isActionPending =
    requestMutation.isPending || bulkMutation.isPending;

  function toggleSelection(userId: string) {
    setSelectedIds((current) => {
      const next = new Set(current);

      if (next.has(userId)) {
        next.delete(userId);
      } else {
        next.add(userId);
      }

      return next;
    });
  }

  function toggleSelectAll() {
    setSelectedIds((current) => {
      if (allVisibleSelected) {
        return new Set();
      }

      const next = new Set(current);
      visibleIds.forEach((id) => next.add(id));
      return next;
    });
  }

  return (
    <div
      className="fixed inset-0 z-[90] flex items-center justify-center bg-black/75 px-4 py-8 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-label={t("profile:followRequests.title")}
      onMouseDown={(event) => {
        if (event.currentTarget === event.target) {
          onClose();
        }
      }}
    >
      <section className="flex max-h-[82vh] w-full max-w-2xl flex-col overflow-hidden rounded-3xl border border-white/[0.09] bg-[#111116] shadow-2xl shadow-black/60">
        <header className="border-b border-white/[0.07] px-5 py-4">
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={onBack}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white"
              aria-label={t("profile:followRequests.backToFollowers")}
            >
              <ChevronLeft size={18} />
            </button>

            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-violet-500/12 text-violet-300">
              <UsersRound size={19} />
            </span>

            <div className="min-w-0 flex-1">
              <h2 className="truncate font-semibold text-white">
                {t("profile:followRequests.title")}
              </h2>
              <p className="mt-0.5 text-xs text-zinc-600">
                {requestsQuery.data?.totalCount ?? 0}
              </p>
            </div>

            <button
              type="button"
              onClick={() => {
                setIsManaging((current) => !current);
                setSelectedIds(new Set());
              }}
              className="rounded-xl border border-violet-400/15 bg-violet-500/[0.08] px-3 py-2 text-xs font-semibold text-violet-200 transition hover:bg-violet-500/15"
            >
              {t(
                isManaging
                  ? "profile:followRequests.done"
                  : "profile:followRequests.manage",
              )}
            </button>

            <button
              type="button"
              onClick={onClose}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white"
              aria-label={t("profile:followRequests.close")}
            >
              <X size={18} />
            </button>
          </div>

          <label className="mt-4 flex items-center gap-3 rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4 py-3 focus-within:border-violet-400/25 focus-within:bg-violet-500/[0.04]">
            <Search size={17} className="shrink-0 text-zinc-600" />
            <input
              value={searchInput}
              onChange={(event) => {
                setSearchInput(event.target.value);
                setSelectedIds(new Set());
              }}
              placeholder={t("profile:followRequests.searchPlaceholder")}
              className="min-w-0 flex-1 bg-transparent text-sm text-zinc-200 outline-none placeholder:text-zinc-700"
              maxLength={80}
            />
            {searchInput && (
              <button
                type="button"
                onClick={() => setSearchInput("")}
                className="text-zinc-600 transition hover:text-zinc-300"
                aria-label={t("profile:followRequests.clearSearch")}
              >
                <X size={15} />
              </button>
            )}
          </label>

          {isManaging && requests.length > 0 && (
            <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/[0.06] bg-white/[0.025] px-3 py-2.5">
              <button
                type="button"
                onClick={toggleSelectAll}
                className="flex items-center gap-2 text-xs font-semibold text-zinc-400 transition hover:text-white"
              >
                {allVisibleSelected ? (
                  <CheckSquare2 size={17} className="text-violet-300" />
                ) : (
                  <Square size={17} />
                )}
                {t("profile:followRequests.selectAll")}
              </button>

              <span className="text-xs font-medium text-zinc-600">
                {t("profile:followRequests.selected", {
                  count: selectedIds.size,
                })}
              </span>
            </div>
          )}
        </header>

        {notice && (
          <div
            className={`mx-5 mt-4 rounded-2xl border px-4 py-3 text-sm ${
              notice.tone === "success"
                ? "border-emerald-400/15 bg-emerald-500/[0.07] text-emerald-200"
                : "border-red-400/15 bg-red-500/[0.07] text-red-200"
            }`}
          >
            {notice.message}
          </div>
        )}

        <div className="min-h-0 flex-1 overflow-y-auto p-3 sm:p-4">
          {requestsQuery.isLoading ? (
            <div className="flex items-center justify-center gap-3 py-16 text-sm text-zinc-500">
              <LoaderCircle size={19} className="animate-spin" />
              {t("profile:followRequests.loading")}
            </div>
          ) : requestsQuery.isError ? (
            <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.05] p-6 text-center text-sm text-red-100/70">
              {getApiErrorMessage(
                requestsQuery.error,
                t("profile:followRequests.loadError"),
              )}
            </div>
          ) : requests.length === 0 ? (
            <div className="py-16 text-center">
              <UsersRound size={32} className="mx-auto text-zinc-800" />
              <p className="mt-4 text-sm text-zinc-600">
                {t(
                  search
                    ? "profile:followRequests.emptySearch"
                    : "profile:followRequests.empty",
                )}
              </p>
            </div>
          ) : (
            <div className="space-y-2">
              {requests.map((request) => {
                const isSelected = selectedIds.has(request.userId);
                const isThisRequestPending =
                  requestMutation.isPending &&
                  requestMutation.variables?.requesterId === request.userId;

                return (
                  <article
                    key={request.userId}
                    className={`flex items-center gap-3 rounded-2xl border px-3 py-3 transition ${
                      isSelected
                        ? "border-violet-400/20 bg-violet-500/[0.07]"
                        : "border-transparent hover:border-white/[0.06] hover:bg-white/[0.035]"
                    }`}
                  >
                    {isManaging && (
                      <button
                        type="button"
                        onClick={() => toggleSelection(request.userId)}
                        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-600 transition hover:bg-white/[0.06] hover:text-white"
                        aria-label={request.userName}
                      >
                        {isSelected ? (
                          <CheckSquare2 size={19} className="text-violet-300" />
                        ) : (
                          <Square size={19} />
                        )}
                      </button>
                    )}

                    <Link
                      to={`/users/${encodeURIComponent(request.userName)}`}
                      onClick={onClose}
                      className="flex min-w-0 flex-1 items-center gap-3"
                    >
                      <RequestAvatar
                        userName={request.userName}
                        profileImageUrl={request.profileImageUrl}
                      />

                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold text-zinc-200">
                          {request.userName}
                        </p>
                        <p className="mt-1 truncate text-xs text-zinc-600">
                          {t("profile:followRequests.requested", {
                            time: formatRelativeDate(request.requestedAt),
                          })}
                        </p>
                      </div>
                    </Link>

                    {!isManaging && (
                      <div className="flex shrink-0 items-center gap-2">
                        <button
                          type="button"
                          onClick={() =>
                            requestMutation.mutate({
                              requesterId: request.userId,
                              action: "reject",
                            })
                          }
                          disabled={isActionPending}
                          className="flex h-9 items-center gap-1.5 rounded-xl border border-white/[0.08] bg-white/[0.035] px-3 text-xs font-semibold text-zinc-400 transition hover:bg-white/[0.08] hover:text-white disabled:opacity-50"
                        >
                          <UserX size={14} />
                          <span className="hidden sm:inline">
                            {t("profile:followRequests.reject")}
                          </span>
                        </button>
                        <button
                          type="button"
                          onClick={() =>
                            requestMutation.mutate({
                              requesterId: request.userId,
                              action: "accept",
                            })
                          }
                          disabled={isActionPending}
                          className="flex h-9 items-center gap-1.5 rounded-xl bg-violet-500 px-3 text-xs font-semibold text-white transition hover:bg-violet-400 disabled:opacity-50"
                        >
                          {isThisRequestPending ? (
                            <LoaderCircle size={14} className="animate-spin" />
                          ) : (
                            <UserCheck size={14} />
                          )}
                          <span className="hidden sm:inline">
                            {t("profile:followRequests.accept")}
                          </span>
                        </button>
                      </div>
                    )}
                  </article>
                );
              })}
            </div>
          )}
        </div>

        {isManaging && requests.length > 0 && (
          <footer className="grid grid-cols-2 gap-3 border-t border-white/[0.07] p-4">
            <button
              type="button"
              onClick={() =>
                bulkMutation.mutate({
                  requesterIds: Array.from(selectedIds),
                  action: "reject",
                })
              }
              disabled={selectedIds.size === 0 || isActionPending}
              className="flex items-center justify-center gap-2 rounded-2xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-sm font-semibold text-red-300 transition hover:bg-red-500/15 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <UserX size={17} />
              {t("profile:followRequests.rejectSelected")}
            </button>
            <button
              type="button"
              onClick={() =>
                bulkMutation.mutate({
                  requesterIds: Array.from(selectedIds),
                  action: "accept",
                })
              }
              disabled={selectedIds.size === 0 || isActionPending}
              className="flex items-center justify-center gap-2 rounded-2xl bg-violet-500 px-4 py-3 text-sm font-semibold text-white transition hover:bg-violet-400 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {bulkMutation.isPending ? (
                <LoaderCircle size={17} className="animate-spin" />
              ) : (
                <Check size={17} />
              )}
              {t("profile:followRequests.acceptSelected")}
            </button>
          </footer>
        )}
      </section>
    </div>
  );
}
