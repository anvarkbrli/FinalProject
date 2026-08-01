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
    const pendingRoots = new Set<HTMLElement>();

    const scheduleTranslation = (element: HTMLElement | null) => {
      if (!element || element.closest("[data-i18n-ignore], [data-user-content]")) {
        return;
      }

      pendingRoots.add(element);
      if (frameId !== null) return;

      frameId = window.requestAnimationFrame(() => {
        frameId = null;
        const roots = [...pendingRoots];
        pendingRoots.clear();

        roots.forEach((root) => {
          const hasQueuedAncestor = roots.some(
            (candidate) => candidate !== root && candidate.contains(root),
          );
          if (!hasQueuedAncestor) translatePage(root);
        });
      });
    };

    const observer = new MutationObserver((records) => {
      records.forEach((record) => {
        if (record.type === "characterData") {
          scheduleTranslation(record.target.parentElement);
          return;
        }

        record.addedNodes.forEach((node) => {
          if (node instanceof HTMLElement) {
            scheduleTranslation(node);
          } else if (node.parentElement) {
            scheduleTranslation(node.parentElement);
          }
        });
      });
    });

    observer.observe(document.body, {
      childList: true,
      characterData: true,
      subtree: true,
    });

    return () => {
      observer.disconnect();
      pendingRoots.clear();
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
