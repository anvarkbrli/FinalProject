import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  BellRing,
  LoaderCircle,
  RotateCcw,
  ShieldAlert,
} from "lucide-react";
import { getApiErrorMessage } from "../../lib/api";
import {
  getNotificationSettings,
  resetNotificationPreferences,
  updateNotificationPreference,
} from "../../services/notificationsService";
import type {
  NotificationAudience,
  NotificationType,
} from "../../types/notifications";

interface PreferenceDefinition {
  type: NotificationType;
  title: string;
  description: string;
}

interface PreferenceGroup {
  title: string;
  description: string;
  items: PreferenceDefinition[];
}

const audienceOptions: Array<{
  value: NotificationAudience;
  label: string;
}> = [
  { value: "Everyone", label: "Hər kəs" },
  { value: "ConnectionsOnly", label: "Yalnız əlaqələrim" },
  { value: "Never", label: "Heç vaxt" },
];

const preferenceGroups: PreferenceGroup[] = [
  {
    title: "İzlədiyim istifadəçilər",
    description: "İzlədiyin hesabların yeni fəaliyyətləri.",
    items: [
      {
        type: "FollowingPostCreated",
        title: "Yeni post",
        description: "İzlədiyim istifadəçi post paylaşdıqda.",
      },
      {
        type: "FollowingReviewCreated",
        title: "Yeni rəy",
        description: "İzlədiyim istifadəçi oyun rəyi yazdıqda.",
      },
      {
        type: "FollowingGameStatusChanged",
        title: "Oyun statusu",
        description: "Oynayır, oynayıb və ya yarıda buraxıb statusları dəyişdikdə.",
      },
      {
        type: "FollowingPublicListCreated",
        title: "Yeni açıq siyahı",
        description: "İzlədiyim istifadəçi public siyahı yaratdıqda.",
      },
      {
        type: "FollowingDiscussionCreated",
        title: "Yeni müzakirə",
        description: "İzlədiyim istifadəçi forumda müzakirə açdıqda.",
      },
    ],
  },
  {
    title: "Post və rəylərim",
    description: "Paylaşdığın məzmuna gələn reaksiyalar.",
    items: [
      {
        type: "PostLiked",
        title: "Post bəyənməsi",
        description: "Kimsə postunu bəyəndikdə.",
      },
      {
        type: "PostCommented",
        title: "Post şərhi",
        description: "Kimsə postuna şərh yazdıqda.",
      },
      {
        type: "ReviewLiked",
        title: "Rəy bəyənməsi",
        description: "Kimsə oyun rəyini bəyəndikdə.",
      },
      {
        type: "ReviewCommented",
        title: "Rəyə cavab",
        description: "Kimsə oyun rəyinə cavab yazdıqda.",
      },
    ],
  },
  {
    title: "Siyahı və müzakirələrim",
    description: "Siyahı və forum məzmununa gələn reaksiyalar.",
    items: [
      {
        type: "GameListLiked",
        title: "Siyahı bəyənməsi",
        description: "Kimsə yaratdığın siyahını bəyəndikdə.",
      },
      {
        type: "GameListCommented",
        title: "Siyahı şərhi",
        description: "Kimsə yaratdığın siyahıya şərh yazdıqda.",
      },
      {
        type: "DiscussionLiked",
        title: "Müzakirə bəyənməsi",
        description: "Kimsə açdığın müzakirəni bəyəndikdə.",
      },
      {
        type: "DiscussionReplied",
        title: "Müzakirə cavabı",
        description: "Kimsə açdığın müzakirəyə cavab yazdıqda.",
      },
      {
        type: "DiscussionReplyLiked",
        title: "Cavab bəyənməsi",
        description: "Kimsə forum cavabını bəyəndikdə.",
      },
    ],
  },
  {
    title: "İzləmə əlaqələri",
    description: "İzləmə sorğuları və yeni əlaqələr.",
    items: [
      {
        type: "FollowRequestReceived",
        title: "İzləmə sorğusu",
        description: "Gizli profilinə yeni izləmə sorğusu gəldikdə.",
      },
      {
        type: "UserFollowed",
        title: "Yeni izləyici",
        description: "Kimsə səni izləməyə başladıqda.",
      },
      {
        type: "FollowRequestAccepted",
        title: "Sorğu qəbul edildi",
        description: "Göndərdiyin izləmə sorğusu qəbul edildikdə.",
      },
    ],
  },
];

export function NotificationSettingsPanel() {
  const queryClient = useQueryClient();

  const settingsQuery = useQuery({
    queryKey: ["notification-settings"],
    queryFn: getNotificationSettings,
  });

  const updateMutation = useMutation({
    mutationFn: ({
      type,
      audience,
    }: {
      type: NotificationType;
      audience: NotificationAudience;
    }) => updateNotificationPreference(type, audience),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["notification-settings"],
      });
    },
  });

  const resetMutation = useMutation({
    mutationFn: resetNotificationPreferences,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["notification-settings"],
      });
    },
  });

  const preferenceMap = new Map(
    settingsQuery.data?.items.map((item) => [item.type, item]) ?? [],
  );

  return (
    <section
      id="notifications"
      className="scroll-mt-24 rounded-3xl border border-white/[0.07] bg-[#101015] p-5 sm:p-6"
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex items-start gap-3">
          <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-500/12 text-violet-300">
            <BellRing size={20} />
          </span>
          <div>
            <h2 className="font-semibold text-white">Bildiriş ayarları</h2>
            <p className="mt-1 max-w-2xl text-xs leading-5 text-zinc-600">
              Hər fəaliyyət üçün kimdən bildiriş almaq istədiyini ayrıca seç.
              “Əlaqələrim” qəbul edilmiş izləmə əlaqələrini nəzərdə tutur.
            </p>
          </div>
        </div>

        <button
          type="button"
          disabled={resetMutation.isPending || settingsQuery.isLoading}
          onClick={() => resetMutation.mutate()}
          className="flex h-9 shrink-0 items-center justify-center gap-2 rounded-lg border border-white/[0.08] bg-white/[0.03] px-3 text-xs font-semibold text-zinc-400 transition hover:bg-white/[0.07] hover:text-white disabled:opacity-50"
        >
          {resetMutation.isPending ? (
            <LoaderCircle size={14} className="animate-spin" />
          ) : (
            <RotateCcw size={14} />
          )}
          Standarta qaytar
        </button>
      </div>

      <div className="mt-5 flex items-start gap-3 rounded-2xl border border-amber-300/15 bg-amber-400/[0.06] p-4">
        <ShieldAlert size={18} className="mt-0.5 shrink-0 text-amber-300" />
        <p className="text-xs leading-5 text-amber-100/75">
          Elan və fərdi admin xəbərdarlıqları hesab təhlükəsizliyi və platforma
          məlumatlandırması üçün həmişə aktivdir; bu bildirişlər söndürülmür.
        </p>
      </div>

      {settingsQuery.isLoading ? (
        <div className="flex min-h-40 items-center justify-center gap-2 text-sm text-zinc-500">
          <LoaderCircle size={17} className="animate-spin" />
          Bildiriş ayarları yüklənir...
        </div>
      ) : settingsQuery.isError ? (
        <div className="mt-5 rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-4 text-sm text-red-200">
          {getApiErrorMessage(
            settingsQuery.error,
            "Bildiriş ayarları yüklənmədi.",
          )}
        </div>
      ) : (
        <div className="mt-6 space-y-7">
          {preferenceGroups.map((group) => (
            <div key={group.title}>
              <div className="mb-3">
                <h3 className="text-sm font-semibold text-zinc-200">
                  {group.title}
                </h3>
                <p className="mt-1 text-xs text-zinc-600">
                  {group.description}
                </p>
              </div>

              <div className="divide-y divide-white/[0.05] overflow-hidden rounded-2xl border border-white/[0.06] bg-white/[0.018]">
                {group.items.map((definition) => {
                  const preference = preferenceMap.get(definition.type);
                  const isPending =
                    updateMutation.isPending &&
                    updateMutation.variables?.type === definition.type;

                  return (
                    <div
                      key={definition.type}
                      className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-center sm:justify-between"
                    >
                      <div className="min-w-0 sm:pr-5">
                        <p className="text-sm font-medium text-zinc-200">
                          {definition.title}
                        </p>
                        <p className="mt-1 text-xs leading-5 text-zinc-600">
                          {definition.description}
                        </p>
                      </div>

                      <div className="relative shrink-0">
                        <select
                          aria-label={`${definition.title} bildiriş auditoriyası`}
                          value={preference?.audience ?? "Everyone"}
                          disabled={isPending}
                          onChange={(event) =>
                            updateMutation.mutate({
                              type: definition.type,
                              audience: event.target.value as NotificationAudience,
                            })
                          }
                          className="h-10 min-w-44 appearance-none rounded-xl border border-white/[0.08] bg-[#15151b] px-3 pr-9 text-xs font-semibold text-zinc-300 outline-none transition focus:border-violet-400/50 disabled:opacity-50"
                        >
                          {audienceOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                              {option.label}
                            </option>
                          ))}
                        </select>
                        {isPending && (
                          <LoaderCircle
                            size={14}
                            className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 animate-spin text-violet-300"
                          />
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      )}

      {updateMutation.isError && (
        <div className="mt-5 rounded-xl border border-red-400/15 bg-red-500/[0.06] px-4 py-3 text-xs text-red-200">
          {getApiErrorMessage(
            updateMutation.error,
            "Bildiriş seçimi yenilənmədi.",
          )}
        </div>
      )}
    </section>
  );
}
