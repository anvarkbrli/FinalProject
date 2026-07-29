import { useMutation, useQuery } from "@tanstack/react-query";
import {
  CheckCircle2,
  Clock3,
  Gamepad2,
  HelpCircle,
  Layers3,
  LoaderCircle,
  Monitor,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { getCurrentLanguage } from "../../i18n/localization";
import { useI18n } from "../../i18n/useI18n";
import { getApiErrorMessage } from "../../lib/api";
import {
  formatPlaytime as formatLocalizedPlaytime,
  resolveAssetUrl,
} from "../../lib/helpers";
import {
  applySteamPlaytimeReconciliation,
  getSteamPlaytimeReconciliation,
} from "../../services/steamService";
import type {
  SteamApplyPlaytimeResultDTO,
  SteamPlaytimeResolution,
} from "../../types/steam";

interface SteamPlaytimeReconciliationModalProps {
  isOpen: boolean;
  onClose: () => void;
  onApplied: (result: SteamApplyPlaytimeResultDTO) => void | Promise<void>;
}

type UserResolution =
  | "SteamIncludesManual"
  | "SeparateFromSteam"
  | "ConservativeMaximum";

const resolutionOptions: Array<{
  value: UserResolution;
  label: string;
  description: string;
}> = [
  {
    value: "SteamIncludesManual",
    label: "Steam saatının içindədir",
    description: "Manual saat ayrıca toplanmır; böyük olan dəyər saxlanılır.",
  },
  {
    value: "SeparateFromSteam",
    label: "Steam-dən ayrı oynanıb",
    description: "Manual və Steam saatları bir-birinin üstünə gəlir.",
  },
  {
    value: "ConservativeMaximum",
    label: "Bilmirəm — təhlükəsiz hesabla",
    description: "İki dəfə saymamaq üçün böyük olan dəyər istifadə edilir.",
  },
];

function formatPlaytime(minutes: number): string {
  if (getCurrentLanguage() === "en") {
    return formatLocalizedPlaytime(minutes);
  }

  if (minutes <= 0) return "0 saat";
  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;
  if (hours === 0) return `${remainder} dəq`;
  if (remainder === 0) return `${hours} saat`;
  return `${hours} saat ${remainder} dəq`;
}

function calculatePreview(
  manualMinutes: number,
  steamMinutes: number,
  resolution: SteamPlaytimeResolution,
): number {
  if (
    resolution === "SeparateFromSteam" ||
    resolution === "SeparatePlatformAutomatic"
  ) {
    return manualMinutes + steamMinutes;
  }

  if (
    resolution === "SteamIncludesManual" ||
    resolution === "ConservativeMaximum"
  ) {
    return Math.max(manualMinutes, steamMinutes);
  }

  return steamMinutes;
}

export function SteamPlaytimeReconciliationModal({
  isOpen,
  onClose,
  onApplied,
}: SteamPlaytimeReconciliationModalProps) {
  useI18n();
  const [decisions, setDecisions] = useState<Record<number, UserResolution>>({});

  const reconciliationQuery = useQuery({
    queryKey: ["steam-playtime-reconciliation"],
    queryFn: getSteamPlaytimeReconciliation,
    enabled: isOpen,
    staleTime: 0,
  });

  const applyMutation = useMutation({
    mutationFn: applySteamPlaytimeReconciliation,
    onSuccess: async (result) => {
      setDecisions({});
      await onApplied(result);
      onClose();
    },
  });

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !applyMutation.isPending) onClose();
    };

    document.addEventListener("keydown", handleKeyDown);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "";
    };
  }, [applyMutation.isPending, isOpen, onClose]);

  const pendingItems = useMemo(
    () => reconciliationQuery.data?.items.filter((item) => item.needsUserDecision) ?? [],
    [reconciliationQuery.data],
  );

  if (!isOpen) return null;

  function applyBulkResolution(resolution: UserResolution) {
    setDecisions(
      Object.fromEntries(pendingItems.map((item) => [item.steamAppId, resolution])),
    );
  }

  function getEffectiveResolution(item: (typeof pendingItems)[number]): UserResolution {
    const selectedResolution = decisions[item.steamAppId];
    if (selectedResolution) return selectedResolution;

    return item.suggestedResolution === "SteamIncludesManual" ||
      item.suggestedResolution === "SeparateFromSteam"
      ? item.suggestedResolution
      : "ConservativeMaximum";
  }

  function submit() {
    applyMutation.mutate({
      defaultPendingResolution: "ConservativeMaximum",
      decisions: pendingItems.map((item) => ({
        steamAppId: item.steamAppId,
        resolution: getEffectiveResolution(item),
      })),
    });
  }

  return (
    <div
      className="fixed inset-0 z-[110] flex items-center justify-center bg-black/80 px-3 py-6 backdrop-blur-sm sm:px-6"
      role="dialog"
      aria-modal="true"
      aria-labelledby="steam-playtime-title"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !applyMutation.isPending) onClose();
      }}
    >
      <div className="flex max-h-[92vh] w-full max-w-4xl flex-col overflow-hidden rounded-3xl border border-white/[0.09] bg-[#101015] shadow-2xl shadow-black/70">
        <header className="flex items-start justify-between gap-4 border-b border-white/[0.07] px-5 py-5 sm:px-7">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl border border-violet-400/20 bg-violet-500/10 text-violet-300">
              <Layers3 size={21} />
            </div>
            <div>
              <h2 id="steam-playtime-title" className="text-lg font-bold text-white">
                Steam oyun saatlarını uyğunlaşdır
              </h2>
              <p className="mt-1 max-w-2xl text-sm leading-6 text-zinc-500">
                Heç bir köhnə log silinmir. Qeyri-müəyyən PC oyunlarında saatların
                üst-üstə düşüb-düşmədiyini bir dəfə seç, sonrakı sync-lər avtomatik işləsin.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={applyMutation.isPending}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-50"
            aria-label="Pəncərəni bağla"
          >
            <X size={18} />
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-7 sm:py-6">
          {reconciliationQuery.isLoading ? (
            <div className="flex items-center justify-center gap-3 py-20 text-sm text-zinc-500">
              <LoaderCircle size={20} className="animate-spin" />
              Oyun saatları hazırlanır...
            </div>
          ) : reconciliationQuery.isError ? (
            <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.06] p-5 text-sm leading-6 text-red-200/80">
              {getApiErrorMessage(
                reconciliationQuery.error,
                "Steam saatlarını uyğunlaşdırmaq mümkün olmadı.",
              )}
            </div>
          ) : reconciliationQuery.data ? (
            <div className="space-y-6">
              <section className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-2xl border border-white/[0.07] bg-white/[0.025] p-4">
                  <span className="text-xs text-zinc-500">Oynanmış Steam oyunları</span>
                  <strong className="mt-2 block text-2xl text-white">
                    {reconciliationQuery.data.totalPlayedGames}
                  </strong>
                </div>
                <div className="rounded-2xl border border-amber-400/15 bg-amber-500/[0.05] p-4">
                  <span className="text-xs text-amber-200/60">Sənin qərarın lazımdır</span>
                  <strong className="mt-2 block text-2xl text-amber-200">
                    {reconciliationQuery.data.pendingCount}
                  </strong>
                </div>
                <div className="rounded-2xl border border-emerald-400/15 bg-emerald-500/[0.05] p-4">
                  <span className="text-xs text-emerald-200/60">Avtomatik hazırdır</span>
                  <strong className="mt-2 block text-2xl text-emerald-200">
                    {reconciliationQuery.data.readyToApplyCount}
                  </strong>
                </div>
              </section>

              {pendingItems.length > 0 ? (
                <>
                  <section className="rounded-2xl border border-white/[0.07] bg-white/[0.02] p-4">
                    <p className="text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
                      Toplu seçim
                    </p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {resolutionOptions.map((option) => (
                        <button
                          key={option.value}
                          type="button"
                          onClick={() => applyBulkResolution(option.value)}
                          className="rounded-xl border border-white/[0.08] bg-[#17171d] px-3 py-2 text-xs font-semibold text-zinc-300 transition hover:border-violet-400/30 hover:text-white"
                        >
                          {option.label}
                        </button>
                      ))}
                    </div>
                  </section>

                  <section className="space-y-3">
                    {pendingItems.map((item) => {
                      const selected = getEffectiveResolution(item);
                      const coverUrl = resolveAssetUrl(item.matchedCoverImageUrl ?? null) ??
                        `https://cdn.akamai.steamstatic.com/steam/apps/${item.steamAppId}/header.jpg`;
                      const preview = calculatePreview(
                        item.manualPlaytimeMinutes,
                        item.steamPlaytimeMinutes,
                        selected,
                      );

                      return (
                        <article
                          key={item.steamAppId}
                          className="grid gap-4 rounded-2xl border border-white/[0.07] bg-[#141419] p-4 md:grid-cols-[112px_minmax(0,1fr)_260px] md:items-center"
                        >
                          <div className="aspect-video overflow-hidden rounded-xl bg-zinc-900">
                            <img
                              src={coverUrl}
                              alt={`${item.steamName} şəkli`}
                              className="h-full w-full object-cover"
                            />
                          </div>

                          <div className="min-w-0">
                            <h3 className="truncate text-sm font-bold text-white">
                              {item.steamName}
                            </h3>
                            {item.steamName !== item.matchedGameName && (
                              <p className="mt-1 truncate text-xs text-zinc-600">
                                Statistika: {item.matchedGameName}
                              </p>
                            )}
                            <div className="mt-3 flex flex-wrap gap-2 text-xs">
                              <span className="inline-flex items-center gap-1.5 rounded-lg bg-violet-500/10 px-2.5 py-1.5 text-violet-200">
                                <Clock3 size={13} /> Steam: {formatPlaytime(item.steamPlaytimeMinutes)}
                              </span>
                              <span className="inline-flex items-center gap-1.5 rounded-lg bg-white/[0.05] px-2.5 py-1.5 text-zinc-300">
                                <Gamepad2 size={13} /> Manual: {formatPlaytime(item.manualPlaytimeMinutes)}
                              </span>
                              <span className="inline-flex items-center gap-1.5 rounded-lg bg-white/[0.05] px-2.5 py-1.5 text-zinc-400">
                                <Monitor size={13} /> {item.manualPlatformName ?? "PC / bilinmir"}
                              </span>
                            </div>
                          </div>

                          <div>
                            <label
                              htmlFor={`steam-resolution-${item.steamAppId}`}
                              className="mb-2 block text-xs font-semibold text-zinc-400"
                            >
                              Bu manual saat necə hesablanmalıdır?
                            </label>
                            <select
                              id={`steam-resolution-${item.steamAppId}`}
                              value={selected}
                              onChange={(event) => {
                                setDecisions((current) => ({
                                  ...current,
                                  [item.steamAppId]: event.target.value as UserResolution,
                                }));
                              }}
                              className="w-full rounded-xl border border-white/[0.09] bg-[#0d0d11] px-3 py-2.5 text-sm text-white outline-none transition focus:border-violet-400/50"
                            >
                              {resolutionOptions.map((option) => (
                                <option key={option.value} value={option.value}>
                                  {option.label}
                                </option>
                              ))}
                            </select>
                            <p className="mt-2 text-xs leading-5 text-zinc-600">
                              Nəticə: <strong className="text-zinc-300">{formatPlaytime(preview)}</strong>
                            </p>
                          </div>
                        </article>
                      );
                    })}
                  </section>
                </>
              ) : (
                <div className="rounded-2xl border border-emerald-400/15 bg-emerald-500/[0.05] p-5">
                  <div className="flex items-start gap-3">
                    <CheckCircle2 size={21} className="mt-0.5 shrink-0 text-emerald-300" />
                    <div>
                      <p className="font-semibold text-emerald-100">Əlavə seçim tələb olunmur</p>
                      <p className="mt-1 text-sm leading-6 text-emerald-100/60">
                        Steam-only və başqa platformada oynanmış loglar avtomatik, təhlükəsiz qaydada hesablanıb.
                      </p>
                    </div>
                  </div>
                </div>
              )}

              <div className="flex items-start gap-3 rounded-2xl border border-sky-400/15 bg-sky-500/[0.04] p-4 text-sm leading-6 text-sky-100/65">
                <HelpCircle size={18} className="mt-0.5 shrink-0 text-sky-300" />
                Təsdiqdən sonra yekun saat avtomatik loga yazılır. Platforma seçilməyibsə PC təyin edilir;
                manual tarixçə ayrıca qorunur və Steam ayrıldıqda bərpa olunur.
              </div>
            </div>
          ) : null}
        </div>

        <footer className="flex flex-col-reverse gap-3 border-t border-white/[0.07] px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-7">
          <p className="text-xs text-zinc-600">
            Sonrakı Steam sinxronizasiyalarında yalnız yeni saat artımı avtomatik işlənəcək.
          </p>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={applyMutation.isPending}
              className="rounded-xl border border-white/[0.08] px-4 py-2.5 text-sm font-semibold text-zinc-400 transition hover:bg-white/[0.04] hover:text-white disabled:opacity-50"
            >
              Sonra
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={
                applyMutation.isPending ||
                reconciliationQuery.isLoading ||
                reconciliationQuery.isError ||
                !reconciliationQuery.data
              }
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-violet-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {applyMutation.isPending ? (
                <LoaderCircle size={17} className="animate-spin" />
              ) : (
                <CheckCircle2 size={17} />
              )}
              Nəticələri logla
            </button>
          </div>
        </footer>

        {applyMutation.isError && (
          <div className="border-t border-red-400/10 bg-red-500/[0.05] px-7 py-3 text-sm text-red-200/75">
            {getApiErrorMessage(
              applyMutation.error,
              "Hesablanmış saatları loglamaq mümkün olmadı.",
            )}
          </div>
        )}
      </div>
    </div>
  );
}
