import { createContext, useContext } from "react";
import type {
  SupportedLanguage,
  TranslationParameters,
} from "./localization";

export interface I18nContextValue {
  language: SupportedLanguage;
  t: (key: string, parameters?: TranslationParameters, fallback?: string) => string;
  setLanguage: (language: SupportedLanguage) => void;
}

export const I18nContext = createContext<I18nContextValue | null>(null);

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) throw new Error("useI18n must be used inside I18nProvider.");
  return context;
}
