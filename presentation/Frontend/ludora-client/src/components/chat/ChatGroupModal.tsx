import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Camera,
  LoaderCircle,
  LogOut,
  Search,
  Trash2,
  UserPlus,
  Users,
  X,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import { useConfirmation } from "../../lib/confirmation";
import { resolveAssetUrl } from "../../lib/helpers";
import {
  chatQueryKeys,
  createChatGroup,
  getChatGroup,
  leaveChatGroup,
  removeChatGroupMember,
  updateChatGroup,
} from "../../services/chatService";
import { globalSearch } from "../../services/searchService";
import type { SearchUserResultDTO } from "../../types/search";

interface ChatGroupModalProps {
  open: boolean;
  conversationId?: number | null;
  currentUserId: string;
  onClose: () => void;
  onSaved: (conversationId: number) => void;
  onLeft?: (conversationId: number) => void;
}

function avatarUrl(value: string | null | undefined): string | null {
  return resolveAssetUrl(value) ?? value ?? null;
}

const MAX_GROUP_IMAGE_BYTES = 5 * 1024 * 1024;
const GROUP_IMAGE_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);

export function ChatGroupModal({
  open,
  conversationId,
  currentUserId,
  onClose,
  onSaved,
  onLeft,
}: ChatGroupModalProps) {
  const { language } = useI18n();
  const requestConfirmation = useConfirmation();
  const queryClient = useQueryClient();
  const imageInputRef = useRef<HTMLInputElement>(null);
  const isEdit = typeof conversationId === "number";
  const [nameDraft, setNameDraft] = useState<string | null>(null);
  const [descriptionDraft, setDescriptionDraft] = useState<string | null>(null);
  const [image, setImage] = useState<File | null>(null);
  const [removeImage, setRemoveImage] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedUsers, setSelectedUsers] = useState<SearchUserResultDTO[]>([]);
  const [error, setError] = useState<string | null>(null);

  const groupQuery = useQuery({
    queryKey: ["chat", "group", conversationId],
    queryFn: () => getChatGroup(conversationId!),
    enabled: open && isEdit,
  });

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(searchText.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [searchText]);

  const userSearchQuery = useQuery({
    queryKey: ["chat", "group-user-search", debouncedSearch],
    queryFn: () => globalSearch(debouncedSearch, 30),
    enabled: open && debouncedSearch.length >= 2,
    staleTime: 30_000,
  });

  const name = nameDraft ?? (isEdit ? groupQuery.data?.name ?? "" : "");
  const description =
    descriptionDraft ?? (isEdit ? groupQuery.data?.description ?? "" : "");

  const existingMemberIds = useMemo(
    () => new Set(groupQuery.data?.members.map((member) => member.id) ?? []),
    [groupQuery.data?.members],
  );
  const selectedIds = useMemo(
    () => new Set(selectedUsers.map((user) => user.id)),
    [selectedUsers],
  );
  const searchResults = useMemo(
    () =>
      (userSearchQuery.data?.users ?? []).filter(
        (user) =>
          user.id !== currentUserId &&
          !existingMemberIds.has(user.id) &&
          !selectedIds.has(user.id),
      ),
    [currentUserId, existingMemberIds, selectedIds, userSearchQuery.data?.users],
  );

  const createMutation = useMutation({
    mutationFn: createChatGroup,
    onSuccess: async (group) => {
      await queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversationLists });
      onSaved(group.conversationId);
      onClose();
    },
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(
          mutationError,
          language === "en" ? "The group could not be created." : "Qrup yaradıla bilmədi.",
        ),
      ),
  });

  const updateMutation = useMutation({
    mutationFn: async () => {
      const updated = await updateChatGroup(conversationId!, {
        name,
        description,
        image,
        removeImage,
        memberUserIdsToAdd: selectedUsers.map((user) => user.id),
      });
      return getChatGroup(updated.conversationId);
    },
    onSuccess: async (group) => {
      queryClient.setQueryData(["chat", "group", conversationId], group);
      await queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversationLists });
      setSelectedUsers([]);
      setImage(null);
      setRemoveImage(false);
      onSaved(group.conversationId);
      onClose();
    },
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(
          mutationError,
          language === "en" ? "The group could not be updated." : "Qrup yenilənə bilmədi.",
        ),
      ),
  });

  const removeMemberMutation = useMutation({
    mutationFn: (userId: string) => removeChatGroupMember(conversationId!, userId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["chat", "group", conversationId] }),
        queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversationLists }),
      ]);
    },
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(
          mutationError,
          language === "en" ? "The member could not be removed." : "İstifadəçi qrupdan çıxarıla bilmədi.",
        ),
      ),
  });

  const leaveMutation = useMutation({
    mutationFn: () => leaveChatGroup(conversationId!),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: chatQueryKeys.conversationLists }),
        queryClient.invalidateQueries({ queryKey: chatQueryKeys.requestLists }),
      ]);
      queryClient.removeQueries({ queryKey: ["chat", "group", conversationId] });
      queryClient.removeQueries({ queryKey: chatQueryKeys.messages(conversationId!) });
      queryClient.removeQueries({ queryKey: chatQueryKeys.pinnedMessages(conversationId!) });
      onLeft?.(conversationId!);
      onClose();
    },
    onError: (mutationError) =>
      setError(
        getApiErrorMessage(
          mutationError,
          language === "en" ? "The group could not be left." : "Qrupdan çıxmaq mümkün olmadı.",
        ),
      ),
  });

  const isPending =
    createMutation.isPending ||
    updateMutation.isPending ||
    removeMemberMutation.isPending ||
    leaveMutation.isPending;
  const localImagePreview = useMemo(
    () => (image ? URL.createObjectURL(image) : null),
    [image],
  );
  const imagePreview = localImagePreview ?? (!removeImage ? avatarUrl(groupQuery.data?.imageUrl) : null);

  useEffect(() => {
    if (!localImagePreview) return undefined;
    return () => URL.revokeObjectURL(localImagePreview);
  }, [localImagePreview]);

  if (!open) return null;

  function toggleSelectedUser(user: SearchUserResultDTO) {
    setSelectedUsers((current) =>
      current.some((item) => item.id === user.id)
        ? current.filter((item) => item.id !== user.id)
        : [...current, user],
    );
  }

  async function leaveGroup() {
    if (!conversationId || !groupQuery.data) return;

    const isCreator = groupQuery.data.isCurrentUserCreator;
    const confirmed = await requestConfirmation({
      title: language === "en" ? "Leave group?" : "Qrupdan çıxılsın?",
      description: isCreator
        ? language === "en"
          ? "You created this group. If other members remain, ownership will be transferred automatically to one of them. If nobody remains, the group will be deleted."
          : "Bu qrupu sən yaratmısan. Başqa üzvlər qalarsa sahiblik avtomatik onlardan birinə veriləcək. Heç kim qalmazsa qrup silinəcək."
        : language === "en"
          ? "You will lose access to this group and its messages."
          : "Bu qrupa və mesajlarına girişini itirəcəksən.",
      confirmLabel: language === "en" ? "Leave group" : "Qrupdan çıx",
      cancelLabel: language === "en" ? "Cancel" : "Ləğv et",
      tone: "danger",
      kind: "leave",
    });

    if (confirmed) leaveMutation.mutate();
  }

  function submit() {
    setError(null);
    const normalizedName = name.trim();
    if (normalizedName.length < 2) {
      setError(
        language === "en"
          ? "The group name must contain at least 2 characters."
          : "Qrup adı ən azı 2 simvol olmalıdır.",
      );
      return;
    }
    if (!isEdit && selectedUsers.length === 0) {
      setError(
        language === "en"
          ? "Select at least one person for the group."
          : "Qrup üçün ən azı bir istifadəçi seç.",
      );
      return;
    }

    if (isEdit) {
      updateMutation.mutate();
    } else {
      createMutation.mutate({
        name: normalizedName,
        description,
        image,
        memberUserIds: selectedUsers.map((user) => user.id),
      });
    }
  }

  return createPortal(
    <div
      className="fixed inset-0 z-[190] flex items-center justify-center bg-black/70 p-3 backdrop-blur-sm sm:p-5"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !isPending) onClose();
      }}
    >
      <section className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-[1.75rem] border border-[var(--wp-border)] bg-[var(--wp-surface-strong)] text-[var(--wp-text)] shadow-2xl">
        <header className="flex items-start justify-between gap-4 border-b border-[var(--wp-border)] px-5 py-4 sm:px-6">
          <div>
            <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.16em] text-violet-400">
              <Users size={15} />
              {language === "en" ? "Group chat" : "Qrup söhbəti"}
            </div>
            <h2 className="mt-1.5 text-xl font-bold text-[var(--wp-text-strong)]">
              {isEdit
                ? language === "en"
                  ? "Manage group"
                  : "Qrupu idarə et"
                : language === "en"
                  ? "Create a group"
                  : "Qrup yarat"}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="flex h-9 w-9 items-center justify-center rounded-xl text-[var(--wp-text-muted)] transition hover:bg-violet-500/10 hover:text-violet-400 disabled:opacity-40"
            aria-label={language === "en" ? "Close" : "Bağla"}
          >
            <X size={19} />
          </button>
        </header>

        <div className="min-h-0 flex-1 space-y-5 overflow-y-auto px-5 py-5 sm:px-6">
          {groupQuery.isLoading && isEdit ? (
            <div className="flex min-h-60 items-center justify-center">
              <LoaderCircle className="animate-spin text-violet-400" />
            </div>
          ) : groupQuery.isError && isEdit ? (
            <div className="flex min-h-60 flex-col items-center justify-center gap-3 text-center">
              <p className="max-w-md text-sm leading-6 text-red-400">
                {getApiErrorMessage(
                  groupQuery.error,
                  language === "en"
                    ? "The group information could not be loaded."
                    : "Qrup məlumatları yüklənə bilmədi.",
                )}
              </p>
              <button
                type="button"
                onClick={() => void groupQuery.refetch()}
                className="rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-4 py-2 text-sm font-semibold text-[var(--wp-text-strong)] transition hover:border-violet-400/35"
              >
                {language === "en" ? "Try again" : "Yenidən yoxla"}
              </button>
            </div>
          ) : (
            <>
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
                <button
                  type="button"
                  onClick={() => {
                    if (!isEdit || groupQuery.data?.isCurrentUserCreator) {
                      imageInputRef.current?.click();
                    }
                  }}
                  disabled={isEdit && groupQuery.data?.isCurrentUserCreator === false}
                  className="group relative flex h-24 w-24 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-[var(--wp-border)] bg-violet-500/10 text-violet-400"
                >
                  {imagePreview ? (
                    <img src={imagePreview} alt="" className="h-full w-full object-cover" />
                  ) : (
                    <Users size={34} />
                  )}
                  <span className="absolute inset-0 flex items-center justify-center bg-black/45 text-white opacity-0 transition group-hover:opacity-100">
                    <Camera size={22} />
                  </span>
                </button>
                <input
                  ref={imageInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  className="sr-only"
                  onChange={(event) => {
                    const file = event.target.files?.[0] ?? null;
                    event.target.value = "";
                    if (!file) return;

                    if (!GROUP_IMAGE_TYPES.has(file.type)) {
                      setError(
                        language === "en"
                          ? "Only JPEG, PNG and WEBP group images are supported."
                          : "Qrup şəkli üçün yalnız JPEG, PNG və WEBP formatları dəstəklənir.",
                      );
                      return;
                    }
                    if (file.size > MAX_GROUP_IMAGE_BYTES) {
                      setError(
                        language === "en"
                          ? "The group image must not exceed 5 MB."
                          : "Qrup şəklinin ölçüsü 5 MB-dan çox ola bilməz.",
                      );
                      return;
                    }

                    setError(null);
                    setImage(file);
                    setRemoveImage(false);
                  }}
                />
                <div className="min-w-0 flex-1 space-y-3">
                  <label className="block">
                    <span className="mb-1.5 block text-xs font-semibold text-[var(--wp-text-muted)]">
                      {language === "en" ? "Group name" : "Qrup adı"}
                    </span>
                    <input
                      value={name}
                      onChange={(event) => setNameDraft(event.target.value)}
                      maxLength={100}
                      disabled={isEdit && groupQuery.data?.isCurrentUserCreator === false}
                      className="w-full rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-3.5 py-2.5 text-sm text-[var(--wp-text-strong)] outline-none focus:border-violet-400/50"
                    />
                  </label>
                  {(imagePreview || groupQuery.data?.imageUrl) &&
                    (!isEdit || groupQuery.data?.isCurrentUserCreator) && (
                    <button
                      type="button"
                      onClick={() => {
                        setImage(null);
                        setRemoveImage(true);
                      }}
                      className="text-xs font-semibold text-red-400 hover:text-red-300"
                    >
                      {language === "en" ? "Remove group image" : "Qrup şəklini sil"}
                    </button>
                  )}
                </div>
              </div>

              <label className="block">
                <span className="mb-1.5 block text-xs font-semibold text-[var(--wp-text-muted)]">
                  {language === "en" ? "Description" : "Açıqlama"}
                </span>
                <textarea
                  value={description}
                  onChange={(event) => setDescriptionDraft(event.target.value)}
                  maxLength={500}
                  rows={3}
                  disabled={isEdit && groupQuery.data?.isCurrentUserCreator === false}
                  className="w-full resize-none rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] px-3.5 py-3 text-sm text-[var(--wp-text-strong)] outline-none focus:border-violet-400/50"
                  placeholder={
                    language === "en"
                      ? "What is this group about?"
                      : "Bu qrup nə haqqındadır?"
                  }
                />
              </label>

              {isEdit && groupQuery.data && (
                <div>
                  <h3 className="text-sm font-bold text-[var(--wp-text-strong)]">
                    {language === "en"
                      ? `Members (${groupQuery.data.members.length})`
                      : `Üzvlər (${groupQuery.data.members.length})`}
                  </h3>
                  <div className="mt-2 max-h-52 space-y-2 overflow-y-auto rounded-2xl border border-[var(--wp-border)] p-2">
                    {groupQuery.data.members.map((member) => (
                      <div
                        key={member.id}
                        className="flex items-center gap-3 rounded-xl bg-[var(--wp-surface-soft)] px-3 py-2"
                      >
                        <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/15 text-sm font-bold text-violet-400">
                          {avatarUrl(member.profileImageUrl) ? (
                            <img
                              src={avatarUrl(member.profileImageUrl)!}
                              alt=""
                              className="h-full w-full object-cover"
                            />
                          ) : (
                            member.userName.charAt(0).toUpperCase()
                          )}
                        </span>
                        <div className="min-w-0 flex-1">
                          <strong className="block truncate text-sm text-[var(--wp-text-strong)]">
                            {member.userName}
                          </strong>
                          {member.isCreator && (
                            <span className="text-[10px] font-bold uppercase tracking-wider text-violet-400">
                              {language === "en" ? "Creator" : "Qurucu"}
                            </span>
                          )}
                        </div>
                        {!member.isCreator && groupQuery.data.isCurrentUserCreator && (
                          <button
                            type="button"
                            disabled={removeMemberMutation.isPending}
                            onClick={() => removeMemberMutation.mutate(member.id)}
                            className="flex h-8 w-8 items-center justify-center rounded-lg text-red-400 transition hover:bg-red-500/10 disabled:opacity-40"
                            title={language === "en" ? "Remove member" : "Qrupdan çıxar"}
                          >
                            <Trash2 size={16} />
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {(!isEdit || groupQuery.data?.isCurrentUserCreator) && (
                <div>
                  <div className="flex items-center gap-2 text-sm font-bold text-[var(--wp-text-strong)]">
                    <UserPlus size={17} className="text-violet-400" />
                    {isEdit
                      ? language === "en"
                        ? "Add members"
                        : "Üzv əlavə et"
                      : language === "en"
                        ? "Select members"
                        : "Üzvləri seç"}
                  </div>
                  {selectedUsers.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {selectedUsers.map((user) => (
                        <button
                          key={user.id}
                          type="button"
                          onClick={() => toggleSelectedUser(user)}
                          className="flex items-center gap-2 rounded-full border border-violet-400/25 bg-violet-500/10 px-3 py-1.5 text-xs font-semibold text-violet-400"
                        >
                          {user.userName}
                          <X size={13} />
                        </button>
                      ))}
                    </div>
                  )}
                  <div className="relative mt-3">
                    <Search
                      size={17}
                      className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--wp-text-muted)]"
                    />
                    <input
                      value={searchText}
                      onChange={(event) => setSearchText(event.target.value)}
                      className="w-full rounded-xl border border-[var(--wp-border)] bg-[var(--wp-surface-soft)] py-2.5 pl-10 pr-3 text-sm text-[var(--wp-text-strong)] outline-none focus:border-violet-400/50"
                      placeholder={
                        language === "en" ? "Search users..." : "İstifadəçi axtar..."
                      }
                    />
                  </div>
                  {debouncedSearch.length >= 2 && (
                    <div className="mt-2 max-h-48 space-y-1 overflow-y-auto rounded-2xl border border-[var(--wp-border)] p-2">
                      {userSearchQuery.isLoading ? (
                        <LoaderCircle className="mx-auto my-5 animate-spin text-violet-400" />
                      ) : searchResults.length > 0 ? (
                        searchResults.map((user) => (
                          <button
                            key={user.id}
                            type="button"
                            onClick={() => toggleSelectedUser(user)}
                            className="flex w-full items-center gap-3 rounded-xl px-3 py-2 text-left transition hover:bg-violet-500/10"
                          >
                            <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-violet-500/15 text-sm font-bold text-violet-400">
                              {avatarUrl(user.profileImageUrl) ? (
                                <img
                                  src={avatarUrl(user.profileImageUrl)!}
                                  alt=""
                                  className="h-full w-full object-cover"
                                />
                              ) : (
                                user.userName.charAt(0).toUpperCase()
                              )}
                            </span>
                            <strong className="truncate text-sm text-[var(--wp-text-strong)]">
                              {user.userName}
                            </strong>
                          </button>
                        ))
                      ) : (
                        <p className="px-3 py-5 text-center text-xs text-[var(--wp-text-muted)]">
                          {language === "en" ? "No users found." : "İstifadəçi tapılmadı."}
                        </p>
                      )}
                    </div>
                  )}
                </div>
              )}

              {error && (
                <div className="rounded-xl border border-red-400/20 bg-red-500/10 px-4 py-3 text-sm text-red-400">
                  {error}
                </div>
              )}
            </>
          )}
        </div>

        <footer className="flex flex-wrap items-center gap-2 border-t border-[var(--wp-border)] px-5 py-4 sm:px-6">
          {isEdit && groupQuery.data && (
            <button
              type="button"
              onClick={() => void leaveGroup()}
              disabled={isPending}
              className="mr-auto flex items-center gap-2 rounded-xl border border-red-400/25 bg-red-500/[0.08] px-4 py-2.5 text-sm font-semibold text-red-400 transition hover:bg-red-500/[0.13] disabled:opacity-40"
            >
              {leaveMutation.isPending ? (
                <LoaderCircle size={16} className="animate-spin" />
              ) : (
                <LogOut size={16} />
              )}
              {language === "en" ? "Leave group" : "Qrupdan çıx"}
            </button>
          )}
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="rounded-xl border border-[var(--wp-border)] px-4 py-2.5 text-sm font-semibold text-[var(--wp-text-muted)] transition hover:bg-[var(--wp-surface-soft)] disabled:opacity-40"
          >
            {language === "en" ? "Cancel" : "Ləğv et"}
          </button>
          {(!isEdit || groupQuery.data?.isCurrentUserCreator) && (
            <button
              type="button"
              onClick={submit}
              disabled={
                isPending ||
                (isEdit && (groupQuery.isLoading || groupQuery.isError))
              }
              className="flex items-center gap-2 rounded-xl bg-violet-600 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:opacity-45"
            >
              {(createMutation.isPending || updateMutation.isPending) && (
                <LoaderCircle size={16} className="animate-spin" />
              )}
              {isEdit
                ? language === "en"
                  ? "Save changes"
                  : "Yadda saxla"
                : language === "en"
                  ? "Create group"
                  : "Qrup yarat"}
            </button>
          )}
        </footer>
      </section>
    </div>,
    document.body,
  );
}
