import azAuth from "../locales/az/auth.json";
import azCommon from "../locales/az/common.json";
import azForum from "../locales/az/forum.json";
import azGames from "../locales/az/games.json";
import azHome from "../locales/az/home.json";
import azMessages from "../locales/az/messages.json";
import azLists from "../locales/az/lists.json";
import azNotifications from "../locales/az/notifications.json";
import azProfile from "../locales/az/profile.json";
import azValidation from "../locales/az/validation.json";
import azAutoUi from "../locales/az/auto-ui.json";
import enAuth from "../locales/en/auth.json";
import enCommon from "../locales/en/common.json";
import enForum from "../locales/en/forum.json";
import enGames from "../locales/en/games.json";
import enHome from "../locales/en/home.json";
import enMessages from "../locales/en/messages.json";
import enLists from "../locales/en/lists.json";
import enNotifications from "../locales/en/notifications.json";
import enProfile from "../locales/en/profile.json";
import enValidation from "../locales/en/validation.json";
import enAutoUi from "../locales/en/auto-ui.json";

export type SupportedLanguage = "az" | "en";
export type TranslationParameters = Record<
  string,
  string | number | boolean | null | undefined
>;
type TranslationNode = string | { [key: string]: TranslationNode };
type TranslationNamespace = Record<string, TranslationNode>;
type TranslationBundle = Record<string, TranslationNamespace>;

const storageKey = "preferredLanguage";
const supportedLanguages: readonly SupportedLanguage[] = ["az", "en"];
const bundles: Record<SupportedLanguage, TranslationBundle> = {
  az: {
    auto: azAutoUi,
    common: azCommon,
    auth: azAuth,
    games: azGames,
    home: azHome,
    profile: azProfile,
    forum: azForum,
    messages: azMessages,
    lists: azLists,
    notifications: azNotifications,
    validation: azValidation,
  },
  en: {
    auto: enAutoUi,
    common: enCommon,
    auth: enAuth,
    games: enGames,
    home: enHome,
    profile: enProfile,
    forum: enForum,
    messages: enMessages,
    lists: enLists,
    notifications: enNotifications,
    validation: enValidation,
  },
};

const automaticTranslationKeys = new Map<string, string>();
const automaticTemplateTranslations: Array<{
  key: string;
  pattern: RegExp;
  parameterNames: string[];
}> = [];

function normalizeAutomaticText(value: string) {
  return value.replace(/\s+/g, " ").trim();
}

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function addAutomaticTemplate(value: string, key: string) {
  const parameterNames: string[] = [];
  let cursor = 0;
  let pattern = "^";
  for (const match of value.matchAll(/\{(\w+)\}/g)) {
    pattern += escapeRegExp(value.slice(cursor, match.index));
    pattern += "(.+?)";
    parameterNames.push(match[1]);
    cursor = (match.index ?? 0) + match[0].length;
  }
  pattern += `${escapeRegExp(value.slice(cursor))}$`;
  automaticTemplateTranslations.push({
    key,
    pattern: new RegExp(pattern, "u"),
    parameterNames,
  });
}

function collectAutomaticTranslationKeys(
  node: TranslationNode,
  path: string,
) {
  if (typeof node === "string") {
    const value = normalizeAutomaticText(node);
    if (value.length > 1 && value.includes("{")) {
      addAutomaticTemplate(value, path);
    } else if (value.length > 1) {
      automaticTranslationKeys.set(value, path);
    }
    return;
  }

  Object.entries(node).forEach(([key, value]) =>
    collectAutomaticTranslationKeys(
      value,
      path.endsWith(":") ? `${path}${key}` : path ? `${path}.${key}` : key,
    ),
  );
}

supportedLanguages.forEach((language) => {
  collectAutomaticTranslationKeys(bundles[language].auto, "auto:");
});

automaticTemplateTranslations.sort(
  (left, right) => right.pattern.source.length - left.pattern.source.length,
);

function translateAutomaticValue(value: string): string | undefined {
  const normalized = normalizeAutomaticText(value);
  const exactKey = automaticTranslationKeys.get(normalized);
  if (exactKey) return t(exactKey);

  for (const template of automaticTemplateTranslations) {
    const match = template.pattern.exec(normalized);
    if (!match) continue;
    const parameters: TranslationParameters = {};
    template.parameterNames.forEach((name, index) => {
      parameters[name] = match[index + 1];
    });
    return t(template.key, parameters);
  }

  return undefined;
}

function isSupportedLanguage(value: string | null | undefined): value is SupportedLanguage {
  return supportedLanguages.includes(value as SupportedLanguage);
}

function detectInitialLanguage(): SupportedLanguage {
  const stored = localStorage.getItem(storageKey);
  if (isSupportedLanguage(stored)) return stored;
  const browserLanguage = navigator.language.toLowerCase().split("-")[0];
  return isSupportedLanguage(browserLanguage) ? browserLanguage : "az";
}

let currentLanguage: SupportedLanguage = detectInitialLanguage();
const listeners = new Set<() => void>();

function resolveNode(language: SupportedLanguage, key: string): string | undefined {
  const separatorIndex = key.indexOf(":");
  const namespace = separatorIndex >= 0 ? key.slice(0, separatorIndex) : "common";
  const path = separatorIndex >= 0 ? key.slice(separatorIndex + 1) : key;
  let current: TranslationNode | undefined = bundles[language][namespace];

  for (const part of path.split(".").filter(Boolean)) {
    if (!current || typeof current === "string") return undefined;
    current = current[part];
  }
  return typeof current === "string" ? current : undefined;
}

function interpolate(value: string, parameters?: TranslationParameters) {
  if (!parameters) return value;
  return value.replace(/\{(\w+)\}/g, (placeholder, name: string) => {
    const parameter = parameters[name];
    return parameter === null || parameter === undefined
      ? placeholder
      : String(parameter);
  });
}

export function t(
  key: string,
  parameters?: TranslationParameters,
  fallback?: string,
): string {
  const localized = resolveNode(currentLanguage, key);
  const azerbaijaniFallback = resolveNode("az", key);
  const value = localized ?? azerbaijaniFallback ?? fallback ?? key;
  if (import.meta.env.DEV && localized === undefined && azerbaijaniFallback === undefined) {
    console.warn(`[i18n] Missing translation: ${key}`);
  }
  return interpolate(value, parameters);
}

export function setLanguage(
  language: SupportedLanguage,
  options: { persist?: boolean } = {},
) {
  if (!isSupportedLanguage(language)) return;
  if (options.persist !== false) localStorage.setItem(storageKey, language);
  document.documentElement.lang = language;
  if (currentLanguage === language) return;
  currentLanguage = language;
  listeners.forEach((listener) => listener());
}

export function getCurrentLanguage(): SupportedLanguage {
  return currentLanguage;
}

export function getCurrentLocale(): "az-AZ" | "en-US" {
  return currentLanguage === "en" ? "en-US" : "az-AZ";
}

export function getAcceptLanguage(): "az-AZ" | "en-US" {
  return getCurrentLocale();
}

export function loadTranslations(language: SupportedLanguage): TranslationBundle {
  return bundles[language];
}

export function subscribeToLanguage(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function translatePage(root: ParentNode = document) {
  const ignoredContentSelector = "[data-i18n-ignore], [data-user-content]";
  const elementsMatching = (selector: string) => {
    const descendants = Array.from(root.querySelectorAll<HTMLElement>(selector));
    if (root instanceof HTMLElement && root.matches(selector)) {
      descendants.unshift(root);
    }
    return descendants;
  };

  elementsMatching("[data-i18n]").forEach((element) => {
    const key = element.dataset.i18n;
    if (key) element.textContent = t(key, undefined, element.textContent ?? key);
  });
  const attributes = [
    ["data-i18n-placeholder", "placeholder"],
    ["data-i18n-title", "title"],
    ["data-i18n-aria-label", "aria-label"],
    ["data-i18n-alt", "alt"],
  ] as const;
  for (const [dataAttribute, targetAttribute] of attributes) {
    elementsMatching(`[${dataAttribute}]`).forEach((element) => {
      const key = element.getAttribute(dataAttribute);
      if (key) {
        element.setAttribute(
          targetAttribute,
          t(key, undefined, element.getAttribute(targetAttribute) ?? key),
        );
      }
    });
  }

  const walker = document.createTreeWalker(
    root,
    NodeFilter.SHOW_TEXT,
    {
      acceptNode(node) {
        const parent = node.parentElement;
        if (!parent || parent.closest(ignoredContentSelector)) {
          return NodeFilter.FILTER_REJECT;
        }
        if (
          ["SCRIPT", "STYLE", "TEXTAREA", "INPUT", "CODE", "PRE"].includes(
            parent.tagName,
          )
        ) {
          return NodeFilter.FILTER_REJECT;
        }
        return NodeFilter.FILTER_ACCEPT;
      },
    },
  );

  let textNode = walker.nextNode();
  while (textNode) {
    const value = textNode.nodeValue ?? "";
    const match = value.match(/^(\s*)(.*?)(\s*)$/s);
    const normalized = match?.[2] ?? "";
    const localizedValue = translateAutomaticValue(normalized);
    if (localizedValue) {
      const translated = `${match?.[1] ?? ""}${localizedValue}${match?.[3] ?? ""}`;
      if (translated !== value) textNode.nodeValue = translated;
    }
    textNode = walker.nextNode();
  }

  const automaticAttributes = ["placeholder", "title", "aria-label"] as const;
  const elements =
    root instanceof Element
      ? [root, ...Array.from(root.querySelectorAll<HTMLElement>("*"))]
      : Array.from(root.querySelectorAll<HTMLElement>("*"));
  elements.forEach((element) => {
    if (element.closest(ignoredContentSelector)) return;
    automaticAttributes.forEach((attribute) => {
      const value = element.getAttribute(attribute)?.trim();
      const translated = value ? translateAutomaticValue(value) : undefined;
      if (translated) element.setAttribute(attribute, translated);
    });
  });
}

document.documentElement.lang = currentLanguage;
