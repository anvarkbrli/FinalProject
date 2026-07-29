import {
  useEffect,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from "react";
import { useAuthStore } from "../stores/authStore";
import {
  getCurrentLanguage,
  setLanguage,
  subscribeToLanguage,
  t,
  translatePage,
} from "./localization";
import { I18nContext, type I18nContextValue } from "./useI18n";

export function I18nProvider({ children }: { children: ReactNode }) {
  const language = useSyncExternalStore(
    subscribeToLanguage,
    getCurrentLanguage,
    getCurrentLanguage,
  );
  const preferredLanguage = useAuthStore((state) => state.user?.preferredLanguage);

  useEffect(() => {
    if (preferredLanguage === "az" || preferredLanguage === "en") {
      setLanguage(preferredLanguage, { persist: true });
    }
  }, [preferredLanguage]);

  useEffect(() => {
    document.documentElement.lang = language;
    document.title = t("app.name");
    translatePage();
  }, [language]);

  useEffect(() => {
    let frameId: number | null = null;
    const observer = new MutationObserver(() => {
      if (frameId !== null) return;
      frameId = window.requestAnimationFrame(() => {
        frameId = null;
        translatePage();
      });
    });

    observer.observe(document.body, {
      childList: true,
      characterData: true,
      subtree: true,
    });

    return () => {
      observer.disconnect();
      if (frameId !== null) window.cancelAnimationFrame(frameId);
    };
  }, []);

  const value = useMemo<I18nContextValue>(
    () => ({
      language,
      t,
      setLanguage: (nextLanguage) => setLanguage(nextLanguage),
    }),
    [language],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}
