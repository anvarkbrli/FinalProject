import {
  Check,
  Copy,
  LoaderCircle,
  LockKeyhole,
  Users,
  X,
} from "lucide-react";
import { useState } from "react";
import { useI18n } from "../../i18n/useI18n";
import type { ListVisibility } from "../../types/profile";

interface GameListCloneModalProps {
  open: boolean;
  sourceName: string;
  initialName: string;
  isPending: boolean;
  errorMessage?: string | null;
  onClose: () => void;
  onSubmit: (values: {
    name: string;
    visibility: ListVisibility;
  }) => void;
}

const visibilityIcons = {
  Public: Users,
  FollowersOnly: Check,
  Private: LockKeyhole,
} as const;

export function GameListCloneModal({
  open,
  sourceName,
  initialName,
  isPending,
  errorMessage,
  onClose,
  onSubmit,
}: GameListCloneModalProps) {
  const { t } = useI18n();
  const [name, setName] = useState(initialName);
  const [visibility, setVisibility] =
    useState<ListVisibility>("Private");
  const [localError, setLocalError] = useState<string | null>(null);

  if (!open) return null;

  const options: ListVisibility[] = [
    "Public",
    "FollowersOnly",
    "Private",
  ];

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-black/75 px-4 py-8 backdrop-blur-sm">
      <div className="w-full max-w-xl overflow-hidden rounded-3xl border border-white/[0.09] bg-[#101015] shadow-2xl shadow-black/70">
        <div className="flex items-start justify-between border-b border-white/[0.07] px-6 py-5">
          <div>
            <div className="flex items-center gap-2 text-violet-300">
              <Copy size={19} />
              <h2 className="text-lg font-black text-white">
                {t("lists:clone.title")}
              </h2>
            </div>
            <p className="mt-2 text-sm leading-6 text-zinc-500">
              {t("lists:clone.description", { name: sourceName })}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="flex h-10 w-10 items-center justify-center rounded-xl text-zinc-500 transition hover:bg-white/[0.06] hover:text-white disabled:opacity-40"
            aria-label={t("lists:common.close")}
          >
            <X size={18} />
          </button>
        </div>

        <div className="space-y-5 p-6">
          {(localError || errorMessage) && (
            <div className="rounded-2xl border border-red-400/15 bg-red-500/[0.07] px-4 py-3 text-sm text-red-200">
              {localError ?? errorMessage}
            </div>
          )}

          <label className="block">
            <span className="mb-2 block text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              {t("lists:clone.nameLabel")}
            </span>
            <input
              value={name}
              onChange={(event) => {
                setName(event.target.value);
                setLocalError(null);
              }}
              maxLength={100}
              className="h-12 w-full rounded-2xl border border-white/[0.08] bg-white/[0.035] px-4 text-sm text-white outline-none transition focus:border-violet-400/45 focus:ring-4 focus:ring-violet-500/10"
            />
          </label>

          <div>
            <span className="mb-3 block text-xs font-bold uppercase tracking-[0.14em] text-zinc-500">
              {t("lists:clone.visibilityLabel")}
            </span>
            <div className="grid gap-2 sm:grid-cols-3">
              {options.map((option) => {
                const Icon = visibilityIcons[option];
                const selected = visibility === option;
                return (
                  <button
                    key={option}
                    type="button"
                    onClick={() => setVisibility(option)}
                    className={`rounded-2xl border p-4 text-left transition ${
                      selected
                        ? "border-violet-400/35 bg-violet-500/[0.09]"
                        : "border-white/[0.07] bg-white/[0.025] hover:bg-white/[0.045]"
                    }`}
                  >
                    <Icon
                      size={18}
                      className={selected ? "text-violet-300" : "text-zinc-600"}
                    />
                    <span className="mt-3 block text-xs font-bold text-zinc-100">
                      {t(`lists:visibility.${option}`)}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>
        </div>

        <div className="flex justify-end gap-3 border-t border-white/[0.07] px-6 py-5">
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="h-11 rounded-xl border border-white/[0.08] px-4 text-sm font-bold text-zinc-400 transition hover:bg-white/[0.05] hover:text-white disabled:opacity-40"
          >
            {t("lists:common.cancel")}
          </button>
          <button
            type="button"
            disabled={isPending}
            onClick={() => {
              const normalized = name.trim();
              if (!normalized) {
                setLocalError(t("lists:clone.nameRequired"));
                return;
              }
              onSubmit({ name: normalized, visibility });
            }}
            className="inline-flex h-11 items-center gap-2 rounded-xl bg-violet-600 px-5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-50"
          >
            {isPending ? (
              <LoaderCircle size={16} className="animate-spin" />
            ) : (
              <Copy size={16} />
            )}
            {t("lists:clone.submit")}
          </button>
        </div>
      </div>
    </div>
  );
}
