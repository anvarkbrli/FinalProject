import { createHash } from "node:crypto";
import { mkdir, readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import ts from "typescript";

const projectRoot = process.cwd();
const sourceRoot = path.join(projectRoot, "src");
const outputRoots = {
  az: path.join(sourceRoot, "locales", "az", "auto-ui.json"),
  en: path.join(sourceRoot, "locales", "en", "auto-ui.json"),
};
const listOnly = process.argv.includes("--list");
const checkOnly = process.argv.includes("--check");
const englishOverridesPath = path.join(projectRoot, "scripts", "auto-ui.en-overrides.json");
const azerbaijaniLetters = /[ƏəĞğİıÖöÜüŞşÇç]/;
const knownSingleWords = new Set([
  "Axtar",
  "Bağla",
  "Bəyən",
  "Bəyənilib",
  "Bərkidilib",
  "Bitirib",
  "Cavab",
  "Daxil",
  "Digər",
  "Filterlər",
  "Forum",
  "Geri",
  "Göndər",
  "Hamısı",
  "Hesab",
  "İcmalar",
  "İrəli",
  "Kateqoriyalar",
  "Kəşf",
  "Ləğv",
  "Məxfilik",
  "Müzakirələr",
  "Nəticələr",
  "Növbəti",
  "Oyunlar",
  "Oynayır",
  "Paylaş",
  "Postlar",
  "Profil",
  "Reytinq",
  "Rəylər",
  "Sevimlilər",
  "Sil",
  "Siyahılar",
  "Sıralama",
  "Sorğu",
  "Təmizlə",
  "Yüklənir",
  "Əlavə",
  "Əvvəlki",
]);
const azerbaijaniWordHint =
  /\b(?:admin|ana|artıq|avatar|bio|buglar|dost|email|filter|forum|hesab|icma|istifadəçi|kateqoriya|kolleksiya|login|media|mesaj|oyun|parol|platforma|post|profil|reytinq|rəy|server|siyahı|sort|steam|standart|status|şifrə|video)\b/iu;

function normalize(value) {
  return value.replace(/\s+/g, " ").trim();
}

function looksLikeUiText(rawValue) {
  const value = normalize(rawValue);
  if (value.length < 2 || value.length > 600) return false;
  if (!/\p{L}/u.test(value)) return false;
  if (/^(?:https?:|\/|\.\/|\.\.\/|[a-z]+\/)/i.test(value)) return false;
  if (/^[a-z0-9_.:/[\]-]+$/i.test(value) && !knownSingleWords.has(value)) {
    return false;
  }
  if (
    /(?:^|\s)(?:bg-|text-|border-|rounded-|hover:|focus:|sm:|md:|lg:|xl:|flex|grid|block|inline|absolute|relative|fixed|sticky|hidden|transition)(?:\s|$)/.test(
      value,
    )
  ) {
    return false;
  }
  return (
    azerbaijaniLetters.test(value) ||
    knownSingleWords.has(value) ||
    azerbaijaniWordHint.test(value)
  );
}

async function collectSourceFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    if (entry.name === "locales" || entry.name === "i18n") continue;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await collectSourceFiles(fullPath));
    else if (/\.(?:ts|tsx)$/.test(entry.name)) files.push(fullPath);
  }
  return files;
}

function collectFromNode(node, values) {
  if (
    ts.isStringLiteral(node) ||
    ts.isNoSubstitutionTemplateLiteral(node) ||
    ts.isJsxText(node)
  ) {
    const value = normalize(node.text);
    if (looksLikeUiText(value)) values.add(value);
  } else if (ts.isTemplateExpression(node)) {
    const fragments = [
      node.head.text,
      ...node.templateSpans.map((span) => span.literal.text),
    ];
    fragments
      .map(normalize)
      .filter(looksLikeUiText)
      .forEach((fragment) => values.add(fragment));
  }
  ts.forEachChild(node, (child) => collectFromNode(child, values));
}

async function extractUiText() {
  const values = new Set();
  for (const filePath of await collectSourceFiles(sourceRoot)) {
    const source = await readFile(filePath, "utf8");
    const sourceFile = ts.createSourceFile(
      filePath,
      source,
      ts.ScriptTarget.Latest,
      true,
      filePath.endsWith(".tsx") ? ts.ScriptKind.TSX : ts.ScriptKind.TS,
    );
    collectFromNode(sourceFile, values);
  }
  return [...values].sort((left, right) => left.localeCompare(right, "az"));
}

function buildEntries(values) {
  const result = {};
  for (const value of values) {
    let key = `ui_${createHash("sha1").update(value).digest("hex").slice(0, 12)}`;
    let suffix = 2;
    while (result[key] && result[key] !== value) key = `${key}_${suffix++}`;
    result[key] = value;
  }
  return result;
}

async function translateChunk(values) {
  const separator = "\n___LUDORA_TRANSLATION_ITEM___\n";
  const body = new URLSearchParams({
    client: "gtx",
    sl: "az",
    tl: "en",
    dt: "t",
    q: values.join(separator),
  });
  const response = await fetch(
    "https://translate.googleapis.com/translate_a/single",
    {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body,
    },
  );
  if (!response.ok) {
    throw new Error(`Translation request failed: HTTP ${response.status}`);
  }
  const payload = await response.json();
  const translated = payload[0].map((part) => part[0]).join("");
  const parts = translated.split(/___LUDORA_TRANSLATION_ITEM___/);
  if (parts.length !== values.length) {
    throw new Error(
      `Translation batch split failed: expected ${values.length}, received ${parts.length}`,
    );
  }
  return parts.map(normalize);
}

async function translate(values) {
  const translated = [];
  const chunkSize = 35;
  for (let index = 0; index < values.length; index += chunkSize) {
    const chunk = values.slice(index, index + chunkSize);
    translated.push(...await translateChunk(chunk));
    process.stdout.write(
      `\rTranslated ${Math.min(index + chunkSize, values.length)}/${values.length}`,
    );
  }
  process.stdout.write("\n");
  return translated;
}

const values = await extractUiText();
console.log(`Found ${values.length} unique UI text fragments.`);
if (listOnly) {
  console.log(values.join("\n"));
  process.exit(0);
}
if (checkOnly) {
  const existing = JSON.parse(await readFile(outputRoots.az, "utf8"));
  const catalogValues = new Set(Object.values(existing).map(normalize));
  const missing = values.filter((value) => !catalogValues.has(value));
  if (missing.length) {
    console.error(
      `${missing.length} UI text fragments are missing from auto-ui.json:\n` +
        missing.join("\n"),
    );
    process.exit(1);
  }
  console.log("All extracted UI text fragments exist in the AZ/EN catalog.");
  process.exit(0);
}

const translatedValues = await translate(values);
const azEntries = buildEntries(values);
const englishOverrides = JSON.parse(
  await readFile(englishOverridesPath, "utf8"),
);
const enEntries = {};
Object.keys(azEntries).forEach((key, index) => {
  const sourceText = azEntries[key];
  enEntries[key] =
    englishOverrides[sourceText] || translatedValues[index] || sourceText;
});

await Promise.all(
  Object.values(outputRoots).map((filePath) =>
    mkdir(path.dirname(filePath), { recursive: true }),
  ),
);
await Promise.all([
  writeFile(outputRoots.az, `${JSON.stringify(azEntries, null, 2)}\n`, "utf8"),
  writeFile(outputRoots.en, `${JSON.stringify(enEntries, null, 2)}\n`, "utf8"),
]);
console.log("Generated AZ/EN automatic UI catalogs.");
