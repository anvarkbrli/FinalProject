import { useMutation } from "@tanstack/react-query";
import {
  LoaderCircle,
  LogOut,
  Send,
  Trash2,
  UserRoundPlus,
  Users,
  X,
} from "lucide-react";
import { useState } from "react";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  inviteGameListCollaborator,
  removeGameListCollaborator,
} from "../../services/gameListsService";
import type { GameListCollaboratorDTO } from "../../types/gameLists";

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

export function GameListCollaboratorsModal({
  open,
  listId,
  currentUserId,
  isOwner,
  collaborators,
  onClose,
  onUpdated,
}: GameListCollaboratorsModalProps) {
  const { t } = useI18n();
  const [userName, setUserName] = useState("");
  const [notice, setNotice] = useState<string | null>(null);

  const inviteMutation = useMutation({
    mutationFn: () => inviteGameListCollaborator(listId, userName.trim()),
    onSuccess: async () => {
      setUserName("");
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

  if (!open) return null;

  const acceptedCount = collaborators.filter(
    (collaborator) => collaborator.status === "Accepted",
  ).length;

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
              <div className="mt-4 flex gap-2">
                <input
                  value={userName}
                  onChange={(event) => {
                    setUserName(event.target.value);
                    setNotice(null);
                  }}
                  placeholder={t("lists:collaboration.userNamePlaceholder")}
                  className="h-11 min-w-0 flex-1 rounded-xl border border-white/[0.08] bg-black/20 px-4 text-sm text-white outline-none transition placeholder:text-zinc-700 focus:border-violet-400/45"
                />
                <button
                  type="button"
                  disabled={!userName.trim() || inviteMutation.isPending}
                  onClick={() => inviteMutation.mutate()}
                  className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-4 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-45"
                >
                  {inviteMutation.isPending ? (
                    <LoaderCircle size={16} className="animate-spin" />
                  ) : (
                    <Send size={16} />
                  )}
                  {t("lists:collaboration.invite")}
                </button>
              </div>
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
                            const message = isCurrentUser && !isOwner
                              ? t("lists:collaboration.leaveConfirm")
                              : t("lists:collaboration.removeConfirm", {
                                  name: collaborator.userName,
                                });
                            if (window.confirm(message)) {
                              removeMutation.mutate(collaborator.userId);
                            }
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
                            <Trash2 size={16} />
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
