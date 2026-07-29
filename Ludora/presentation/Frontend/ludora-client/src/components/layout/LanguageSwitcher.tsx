import { Languages } from "lucide-react";
import { useState } from "react";
import { useI18n } from "../../i18n/useI18n";
import type { SupportedLanguage } from "../../i18n/localization";
import { updatePreferredLanguage } from "../../services/settingsService";
import { useAuthStore } from "../../stores/authStore";

export function LanguageSwitcher({ compact = false }: { compact?: boolean }) {
  const { language, setLanguage, t } = useI18n();
  const user = useAuthStore((state) => state.user);
  const setUser = useAuthStore((state) => state.setUser);
  const [isSaving, setIsSaving] = useState(false);

  async function changeLanguage(nextLanguage: SupportedLanguage) {
    if (nextLanguage === language || isSaving) return;
    setLanguage(nextLanguage);
    if (!user) return;

    setIsSaving(true);
    try {
      setUser(await updatePreferredLanguage(nextLanguage));
    } catch {
      // Lokal seçim işlək qalır; növbəti uğurlu login server seçimini tətbiq edir.
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div
      className={`flex items-center rounded-xl border border-white/[0.08] bg-white/[0.035] p-1 ${
        compact ? "w-full justify-center" : ""
      }`}
      role="group"
      aria-label={t("language.label")}
    >
      {!compact && <Languages size={14} className="mx-1.5 text-zinc-500" />}
      {(["az", "en"] as const).map((item) => (
        <button
          key={item}
          type="button"
          onClick={() => void changeLanguage(item)}
          disabled={isSaving}
          aria-pressed={language === item}
          title={t(`language.${item}`)}
          className={`rounded-lg px-2.5 py-1.5 text-[11px] font-black transition ${
            language === item
              ? "bg-violet-500 text-white"
              : "text-zinc-500 hover:text-zinc-200"
          } disabled:opacity-50`}
        >
          {item.toUpperCase()}
        </button>
      ))}
    </div>
  );
}
