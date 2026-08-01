import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import process from "node:process";

const root = path.resolve(process.cwd(), "src", "locales");
const sourceRoot = path.resolve(process.cwd(), "src");
const languages = ["az", "en"];

function flatten(value, prefix = "", result = new Map()) {
  for (const [key, child] of Object.entries(value)) {
    const next = prefix ? `${prefix}.${key}` : key;
    if (typeof child === "string") result.set(next, child);
    else if (child && typeof child === "object") flatten(child, next, result);
  }
  return result;
}

const namespaceFiles = await readdir(path.join(root, "az"));
const errors = [];
const allKeys = new Set();

for (const file of namespaceFiles.filter((item) => item.endsWith(".json"))) {
  const dictionaries = {};
  for (const language of languages) {
    const filePath = path.join(root, language, file);
    dictionaries[language] = flatten(
      JSON.parse(await readFile(filePath, "utf8")),
    );
  }

  for (const [sourceLanguage, targetLanguage] of [
    ["az", "en"],
    ["en", "az"],
  ]) {
    for (const key of dictionaries[sourceLanguage].keys()) {
      if (!dictionaries[targetLanguage].has(key)) {
        errors.push(`${file}: ${targetLanguage} dilində çatışmayan açar: ${key}`);
      }
    }
  }

  for (const language of languages) {
    for (const [key, value] of dictionaries[language]) {
      if (!value.trim()) errors.push(`${file}: ${language}.${key} boşdur`);
    }
  }

  const namespace = path.basename(file, ".json");
  for (const key of dictionaries.az.keys()) {
    allKeys.add(`${namespace}:${key}`);
  }
}

async function collectSourceFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await collectSourceFiles(fullPath));
    else if (/\.(?:ts|tsx)$/.test(entry.name)) files.push(fullPath);
  }
  return files;
}

const usedKeys = new Set();
const literalKeyPattern =
  /(?:\bt\s*\(\s*|data-i18n(?:-placeholder|-title|-aria-label|-alt)?=["'])["']([^"'`$]+)["']/g;

for (const file of await collectSourceFiles(sourceRoot)) {
  const source = await readFile(file, "utf8");
  for (const match of source.matchAll(literalKeyPattern)) {
    const rawKey = match[1];
    const normalizedKey = rawKey.includes(":") ? rawKey : `common:${rawKey}`;
    usedKeys.add(normalizedKey);
    if (!allKeys.has(normalizedKey)) {
      errors.push(
        `${path.relative(process.cwd(), file)}: JSON-da olmayan açar: ${rawKey}`,
      );
    }
  }
}

// These families are selected dynamically at runtime. Treat every concrete
// member as used so the audit reports only genuinely orphaned keys.
for (const key of allKeys) {
  if (
    key.startsWith("notifications:types.") ||
    key.startsWith("common:language.")
  ) {
    usedKeys.add(key);
  }
}

const unusedKeys = [...allKeys].filter((key) => !usedKeys.has(key));
const meaningfulUnusedKeys = unusedKeys.filter(
  (key) => !key.startsWith("auto-ui:"),
);

if (errors.length) {
  console.error(errors.join("\n"));
  process.exitCode = 1;
} else {
  console.log("AZ/EN localization açarları uyğundur və boş dəyər yoxdur.");
}

if (meaningfulUnusedKeys.length) {
  console.warn(
    `${meaningfulUnusedKeys.length} açar statik analizdə istifadə olunmamış görünür ` +
    "(dinamik açarlar false-positive ola bilər).",
  );
}
