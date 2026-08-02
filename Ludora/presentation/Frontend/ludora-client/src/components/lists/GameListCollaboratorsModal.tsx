import { useMutation, useQuery } from "@tanstack/react-query";
import {
  LoaderCircle,
  LogOut,
  Search,
  Send,
  UserRoundPlus,
  Users,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { useConfirmation } from "../../lib/confirmation";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  inviteGameListCollaborator,
  removeGameListCollaborator,
} from "../../services/gameListsService";
import { globalSearch } from "../../services/searchService";
import type { GameListCollaboratorDTO } from "../../types/gameLists";
import type { SearchUserResultDTO } from "../../types/search";

interface GameListCollaboratorsModalProps {
  open: boolean;
  listId: number;
  currentUserId: string | null;
  isOwner: boolean;
  collaborators: GameListCollaboratorDTO[];
  onClose: () => void;
  onUpdated: () => Promise<void> | void;
}

function CollaboratorAvatar({ collaborator }: { collaborator: GameListCollaboratorDTO }) {
  const imageUrl = resolveAssetUrl(collaborator.profileImageUrl);
  return (
    <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-white/[0.08] bg-violet-500/10 text-sm font-black text-violet-200">
      {imageUrl ? (
        <img src={imageUrl} alt="" className="h-full w-full object-cover" />
      ) : (
        collaborator.userName.slice(0, 1).toUpperCase()
      )}
    </span>
  );
}

function SearchUserAvatar({ user }: { user: SearchUserResultDTO }) {
  const imageUrl = resolveAssetUrl(user.profileImageUrl);
  return (
    <span className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/[0.08] bg-violet-500/10 text-xs font-black text-violet-200">
      {imageUrl ? (
        <img src={imageUrl} alt="" className="h-full w-full object-cover" />
      ) : (
        user.userName.slice(0, 1).toUpperCase()
      )}
    </span>
  );
}

export function GameListCollaboratorsModal({
  open,
  listId,
  currentUserId,
  isOwner,
  collaborators,
  onClose,
  onUpdated,
}: GameListCollaboratorsModalProps) {
  const { language, t } = useI18n();
  const requestConfirmation = useConfirmation();
  const [userName, setUserName] = useState("");
  const [debouncedUserName, setDebouncedUserName] = useState("");
  const [selectedUser, setSelectedUser] = useState<SearchUserResultDTO | null>(null);
  const [isSuggestionsOpen, setIsSuggestionsOpen] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    const timeoutId = window.setTimeout(
      () => setDebouncedUserName(userName.trim()),
      250,
    );
    return () => window.clearTimeout(timeoutId);
  }, [userName]);

  const searchQuery = useQuery({
    queryKey: ["game-list-collaborator-search", debouncedUserName],
    queryFn: () => globalSearch(debouncedUserName, 8),
    enabled: open && isOwner && debouncedUserName.length >= 2,
    staleTime: 30_000,
  });

  const collaboratorIds = useMemo(
    () => new Set(collaborators.map((collaborator) => collaborator.userId)),
    [collaborators],
  );

  const userSuggestions = useMemo(
    () =>
      (searchQuery.data?.users ?? []).filter(
        (user) =>
          user.id !== currentUserId &&
          !collaboratorIds.has(user.id),
      ),
    [collaboratorIds, currentUserId, searchQuery.data?.users],
  );

  const inviteMutation = useMutation({
    mutationFn: (targetUserName: string) =>
      inviteGameListCollaborator(listId, targetUserName),
    onSuccess: async () => {
      setUserName("");
      setDebouncedUserName("");
      setSelectedUser(null);
      setIsSuggestionsOpen(false);
      setNotice(t("lists:collaboration.inviteSent"));
      await onUpdated();
    },
    onError: (error) => {
      setNotice(
        getApiErrorMessage(error, t("lists:collaboration.inviteError")),
      );
    },
  });

  const removeMutation = useMutation({
    mutationFn: (collaboratorUserId: string) =>
      removeGameListCollaborator(listId, collaboratorUserId),
    onSuccess: async () => {
      setNotice(t("lists:collaboration.removed"));
      await onUpdated();
    },
    onError: (error) => {
      setNotice(
        getApiErrorMessage(error, t("lists:collaboration.removeError")),
      );
    },
  });

  async function submitInvite() {
    const query = userName.trim();
    if (!query || inviteMutation.isPending) return;

    setNotice(null);

    let matchedUser =
      selectedUser?.userName.toLocaleLowerCase() === query.toLocaleLowerCase()
        ? selectedUser
        : userSuggestions.find(
            (user) =>
              user.userName.toLocaleLowerCase() === query.toLocaleLowerCase(),
          ) ?? null;

    if (!matchedUser) {
      try {
        const result = await globalSearch(query, 10);
        matchedUser = result.users.find(
          (user) =>
            user.userName.toLocaleLowerCase() === query.toLocaleLowerCase() &&
            user.id !== currentUserId &&
            !collaboratorIds.has(user.id),
        ) ?? null;
      } catch (error) {
        setNotice(
          getApiErrorMessage(error, t("lists:collaboration.searchError")),
        );
        return;
      }
    }

    if (!matchedUser) {
      setSelectedUser(null);
      setIsSuggestionsOpen(true);
      setNotice(t("lists:collaboration.userNotFound"));
      return;
    }

    inviteMutation.mutate(matchedUser.userName);
  }

  async function confirmRemove(collaborator: GameListCollaboratorDTO) {
    const isCurrentUser = collaborator.userId === currentUserId;
    const isLeaving = isCurrentUser && !isOwner;
    const confirmed = await requestConfirmation({
      title: isLeaving
        ? language === "en"
          ? "Leave this list?"
          : "Siyahıdan ayrılmaq istəyirsən?"
        : language === "en"
          ? `Remove ${collaborator.userName}?`
          : `${collaborator.userName} iştirakçılardan çıxarılsın?`,
      description: isLeaving
        ? t("lists:collaboration.leaveConfirm")
        : t("lists:collaboration.removeConfirm", {
            name: collaborator.userName,
          }),
      detail: collaborator.userName,
      confirmLabel: isLeaving
        ? t("lists:collaboration.leave")
        : t("lists:collaboration.remove"),
      cancelLabel: t("lists:common.cancel"),
      kind: isLeaving ? "leave" : "remove-user",
      tone: "danger",
    });

    if (confirmed) {
      removeMutation.mutate(collaborator.userId);
    }
  }

  if (!open) return null;

  const acceptedCount = collaborators.filter(
    (collaborator) => collaborator.status === "Accepted",
  ).length;
  const showNoResults =
    debouncedUserName.length >= 2 &&
    !searchQuery.isFetching &&
    userSuggestions.length === 0;

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-black/75 px-4 py-8 backdrop-blur-sm">
      <div className="max-h-full w-full max-w-2xl overflow-y-auto rounded-3xl border border-white/[0.09] bg-[#101015] shadow-2xl shadow-black/70">
        <div className="flex items-start justify-between border-b border-white/[0.07] px-6 py-5">
          <div>
            <div className="flex items-center gap-2 text-violet-300">
              <Users size={20} />
              <h2 className="text-lg font-black text-white">
                {t("lists:collaboration.title")}
              </h2>
            </div>
            <p className="mt-2 text-sm leading-6 text-zinc-500">
              {t("lists:collaboration.description", { count: acceptedCount })}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="flex h-10 w-10 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white"
            aria-label={t("lists:common.close")}
          >
            <X size={18} />
          </button>
        </div>

        <div className="space-y-6 p-6">
          {notice && (
            <div className="rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4 py-3 text-sm text-zinc-300">
              {notice}
            </div>
          )}

          {isOwner && (
            <section className="rounded-2xl border border-violet-400/15 bg-violet-500/[0.055] p-4">
              <div className="flex items-center gap-2 text-sm font-bold text-violet-200">
                <UserRoundPlus size={17} />
                {t("lists:collaboration.inviteTitle")}
              </div>
              <p className="mt-2 text-xs leading-5 text-zinc-500">
                {t("lists:collaboration.inviteDescription")}
              </p>

              <form
                className="mt-4 flex gap-2"
                onSubmit={(event) => {
                  event.preventDefault();
                  void submitInvite();
                }}
              >
                <div className="relative min-w-0 flex-1">
                  <Search
                    size={16}
                    className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-zinc-600"
                  />
                  <input
                    value={userName}
                    onChange={(event) => {
                      setUserName(event.target.value);
                      setSelectedUser(null);
                      setNotice(null);
                      setIsSuggestionsOpen(true);
                    }}
                    onFocus={() => setIsSuggestionsOpen(true)}
                    placeholder={t("lists:collaboration.userNamePlaceholder")}
                    autoComplete="off"
                    className="h-11 w-full rounded-xl border border-white/[0.08] bg-black/20 pl-10 pr-10 text-sm text-white outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45"
                  />
                  {(searchQuery.isFetching || inviteMutation.isPending) && (
                    <LoaderCircle
                      size={16}
                      className="absolute right-3.5 top-1/2 -translate-y-1/2 animate-spin text-violet-300"
                    />
                  )}

                  {isSuggestionsOpen && debouncedUserName.length >= 2 && (
                    <div className="absolute left-0 right-0 top-[calc(100%+8px)] z-40 overflow-hidden rounded-2xl border border-white/[0.09] bg-[#15151b] p-2 shadow-2xl shadow-black/60">
                      {searchQuery.isError ? (
                        <p className="px-3 py-4 text-center text-xs text-red-300">
                          {t("lists:collaboration.searchError")}
                        </p>
                      ) : showNoResults ? (
                        <p className="px-3 py-4 text-center text-xs text-zinc-500">
                          {t("lists:collaboration.userNotFound")}
                        </p>
                      ) : (
                        userSuggestions.map((user) => (
                          <button
                            key={user.id}
                            type="button"
                            onClick={() => {
                              setUserName(user.userName);
                              setSelectedUser(user);
                              setIsSuggestionsOpen(false);
                              setNotice(null);
                            }}
                            className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left transition hover:bg-white/[0.06]"
                          >
                            <SearchUserAvatar user={user} />
                            <span className="min-w-0">
                              <span className="block truncate text-sm font-bold text-zinc-100">
                                {user.userName}
                              </span>
                              {user.bio && (
                                <span className="mt-0.5 block truncate text-xs text-zinc-600">
                                  {user.bio}
                                </span>
                              )}
                            </span>
                          </button>
                        ))
                      )}
                    </div>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={!userName.trim() || inviteMutation.isPending}
                  className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-45"
                >
                  {inviteMutation.isPending ? (
                    <LoaderCircle size={16} className="animate-spin" />
                  ) : (
                    <Send size={16} />
                  )}
                  {t("lists:collaboration.invite")}
                </button>
              </form>
            </section>
          )}

          <section>
            <h3 className="text-xs font-black uppercase tracking-[0.14em] text-zinc-500">
              {t("lists:collaboration.people")}
            </h3>

            {collaborators.length === 0 ? (
              <div className="mt-4 rounded-2xl border border-dashed border-white/[0.08] px-5 py-10 text-center text-sm text-zinc-600">
                {t("lists:collaboration.empty")}
              </div>
            ) : (
              <div className="mt-4 space-y-2">
                {collaborators.map((collaborator) => {
                  const isCurrentUser = collaborator.userId === currentUserId;
                  const canRemove = isOwner || isCurrentUser;
                  return (
                    <div
                      key={collaborator.userId}
                      className="flex items-center gap-3 rounded-2xl border border-white/[0.07] bg-white/[0.025] p-3"
                    >
                      <CollaboratorAvatar collaborator={collaborator} />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-bold text-zinc-100">
                          {collaborator.userName}
                        </p>
                        <p className="mt-1 text-xs text-zinc-600">
                          {collaborator.status === "Accepted"
                            ? t("lists:collaboration.accepted")
                            : t("lists:collaboration.pending")}
                        </p>
                      </div>
                      {canRemove && (
                        <button
                          type="button"
                          disabled={removeMutation.isPending}
                          onClick={() => {
                            void confirmRemove(collaborator);
                          }}
                          className="flex h-10 w-10 items-center justify-center rounded-xl border border-red-400/10 text-red-300/70 transition hover:bg-red-500/[0.08] hover:text-red-300 disabled:opacity-40"
                          aria-label={
                            isCurrentUser && !isOwner
                              ? t("lists:collaboration.leave")
                              : t("lists:collaboration.remove")
                          }
                        >
                          {isCurrentUser && !isOwner ? (
                            <LogOut size={16} />
                          ) : (
                            <X size={17} />
                          )}
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}
