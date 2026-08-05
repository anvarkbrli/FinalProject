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
const strictCheck = process.argv.includes("--strict");
const syncMissingOnly = process.argv.includes("--sync-missing");
const englishOverridesPath = path.join(projectRoot, "scripts", "auto-ui.en-overrides.json");
const azerbaijaniLetters = /[ƏəĞğİıÖöÜüŞşÇç]/;
const knownSingleWords = new Set([
  "Axtar",
  "Bağla",
  "Bəyən",
  "Bəyənilib",
  "Bərkidilib",
  "Bərkid",
  "Bitirib",
  "Cavab",
  "Daxil",
  "Digər",
  "Elanlar",
  "Filterlər",
  "Forum",
  "Geri",
  "Göndər",
  "Hamısı",
  "Hesab",
  "İcmalar",
  "Kataloq",
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
  "Aktiv icmalar",
  "Aktiv kateqoriyalar",
  "Aktiv postlar",
  "Bu janra aid oyunlar",
  "Daxil ol",
  "Daxil olunur...",
  "Hesaba daxil ol",
  "Indie oyunlar",
  "Sahib olunan oyunlar",
  "Sevimli oyunlar",
  "Son oynanan oyunlar",
  "Trend oyunlar",
  "Son tarix",
  "Moderator et",
  "Davam et",
  "1 saat",
  "Kvadrat · maksimum 5 MB",
  "Yadda saxla",
  "Sevdiyin janrlar",
  "Sonra tamamla",
  "Sevdiyin oyunu axtar...",
  "Ana lent",
  "FPS hesabla",
  "FPS-i hesabla",
  "yeni videokart",
  "yeni prosessor",
  "Orta FPS",
  "Mobil naviqasiya",
  "Yeni post",
  "25 oyunu statusla qeyd et.",
  "Reytinq qrafiki",
  "Steam kataloqu",
  "Oyunu log et",
  "Spoiler yoxdur",
  "Spoiler var",
  "Log et",
  "Saytda axtar",
  "0 saat",
  "Tarix yoxdur",
  "Ad: A–Z",
  "Ad: Z–A",
  "Ad A–Z",
  "Ad Z–A",
  "sosial link",
  "Sosial linki sil",
  "24 saat",
  "WellPlayd kataloqu",
  "IGDB kataloqu",
  "Janr axtar...",
  "Platforma axtar...",
  "Minimum 8 simvol",
  "Bu mesaj silindi",
  "Mesaj yaz...",
  "Oyunu axtar",
  "Sahib olunanlar",
  "Mesaj yaz",
  "Post silindi.",
  "Postu sil",
  "Ulduz filtrini sil",
  "Cari email:",
  "Yeni email",
  "Elan silinsin?",
  "Yeni elan",
  "Aktiv elanlar",
  "Elan yarat",
  "Qaralama kimi saxla",
  "audit qeydi",
  "Aktiv et",
  "Sistem avtomatik verir",
  "Aktiv ban",
  "cavab ·",
  "silinsin?",
  "Moderasiya et",
  "Admin qeydi",
  "Son baxan:",
  "Ban statusu",
  "Email yoxdur",
  "— Avtomatik",
  "Audit qeydi yoxdur.",
  "Oynayacaq",
  "Dostun",
  "Daha",
  "Ana lent:",
  "1 üzv · yeni yaradılır",
  "Çıxış ili: köhnədən yeniyə",
  "Bu istifadəçinin yeni paylaşımları və fəaliyyətləri artıq lentində görünməyəcək.",
  "Sistemə daxil olmaq mümkün olmadı.",
  "Oyununa uyğun məkan",
  "Moderator admin CRUD əməliyyatlarına daxil ola bilər, lakin silmə əməliyyatlarını yerinə yetirə bilməz.",
  "Bu istifadəçi artıq səni izləməyəcək və izləyicilər siyahında görünməyəcək.",
]);
const azerbaijaniWordHint =
  /\b(?:ana səhifə|artıq|buglar|daxil|digər|filterlər|göndər|hamısı|hesab|icma|icmalar|istifadəçi|istifadəçilər|kateqoriya|kateqoriyalar|kolleksiya|ləğv|mesajlar|müzakirə|müzakirələr|nəticələr|növbəti|oyun|oyunlar|oynayır|paylaş|postlar|profil|reytinq|rəylər|sevimlilər|siyahı|siyahılar|sıralama|sorğu|statuslar|təmizlə|yüklənir|əlavə|əvvəlki)\b/iu;


function extractPlaceholders(value) {
  return [...value.matchAll(/\{(\w+)\}/g)]
    .map((match) => match[1])
    .sort();
}

function isUnsafeAutomaticTemplate(value) {
  const placeholders = extractPlaceholders(value);
  if (!placeholders.length) return false;

  const literalText = normalize(value.replace(/\{\w+\}/g, " "));
  const literalWords = literalText.match(/\p{L}+/gu) ?? [];

  // Generic one-word patterns such as "{value1} game" can match unrelated
  // labels across the app. Runtime localization deliberately ignores them.
  return literalWords.length < 2;
}

function validateAutomaticCatalogs(azCatalog, enCatalog) {
  const errors = [];
  const warnings = [];
  const keys = new Set([...Object.keys(azCatalog), ...Object.keys(enCatalog)]);

  for (const key of keys) {
    const azValue = azCatalog[key];
    const enValue = enCatalog[key];

    if (typeof azValue !== "string") {
      errors.push(`AZ auto-ui catalog is missing string key: ${key}`);
      continue;
    }
    if (typeof enValue !== "string") {
      errors.push(`EN auto-ui catalog is missing string key: ${key}`);
      continue;
    }
    if (!azValue.trim() || !enValue.trim()) {
      errors.push(`Auto-ui catalog contains an empty value: ${key}`);
      continue;
    }

    const azPlaceholders = extractPlaceholders(azValue);
    const enPlaceholders = extractPlaceholders(enValue);
    if (azPlaceholders.join("|") !== enPlaceholders.join("|")) {
      errors.push(
        `Placeholder mismatch for ${key}: AZ [${azPlaceholders.join(", ")}] / EN [${enPlaceholders.join(", ")}]`,
      );
    }

    if (isUnsafeAutomaticTemplate(azValue) || isUnsafeAutomaticTemplate(enValue)) {
      warnings.push(key);
    }
  }

  return { errors, warnings };
}

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
    /(?:^|\s)(?:bg-|text-|border-|rounded-|hover:|focus:|sm:|md:|lg:|xl:|flex|grid|block|inline|absolute|relative|fixed|sticky|hidden|transition)[^\s]*/.test(
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
    let template = node.head.text;
    node.templateSpans.forEach((span, index) => {
      template += `{value${index + 1}}${span.literal.text}`;
    });
    const value = normalize(template);
    if (looksLikeUiText(value)) values.add(value);
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

function buildExistingTranslationMap(existingAz, existingEn) {
  const result = new Map();
  Object.entries(existingAz).forEach(([key, sourceText]) => {
    const translatedText = existingEn[key];
    if (typeof sourceText === "string" && typeof translatedText === "string") {
      result.set(normalize(sourceText), translatedText);
    }
  });
  return result;
}

async function readJsonOrEmpty(filePath) {
  try {
    return JSON.parse(await readFile(filePath, "utf8"));
  } catch (error) {
    if (error?.code === "ENOENT") return {};
    throw error;
  }
}

async function resolveEnglishTranslations(
  sourceValues,
  englishOverrides,
  existingTranslations = new Map(),
) {
  const resolved = new Array(sourceValues.length);
  const valuesToTranslate = [];
  const indexesToTranslate = [];

  sourceValues.forEach((sourceText, index) => {
    const overridden = englishOverrides[sourceText];
    const existing = existingTranslations.get(normalize(sourceText));
    if (typeof overridden === "string" && overridden.trim()) {
      resolved[index] = overridden;
    } else if (typeof existing === "string" && existing.trim()) {
      resolved[index] = existing;
    } else {
      valuesToTranslate.push(sourceText);
      indexesToTranslate.push(index);
    }
  });

  if (valuesToTranslate.length) {
    const translated = await translate(valuesToTranslate);
    indexesToTranslate.forEach((sourceIndex, translatedIndex) => {
      resolved[sourceIndex] = translated[translatedIndex] || sourceValues[sourceIndex];
    });
  }

  return resolved;
}

async function translateChunk(values) {
  const separator = "\n___WELLPLAYD_TRANSLATION_ITEM___\n";
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
  const parts = translated.split(/___WELLPLAYD_TRANSLATION_ITEM___/);
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
  const [existingAz, existingEn] = await Promise.all([
    readJsonOrEmpty(outputRoots.az),
    readJsonOrEmpty(outputRoots.en),
  ]);
  const { errors, warnings } = validateAutomaticCatalogs(existingAz, existingEn);

  if (errors.length) {
    console.error(errors.join("\n"));
    process.exit(1);
  }

  const catalogValues = new Set(Object.values(existingAz).map(normalize));
  const missing = values.filter((value) => !catalogValues.has(value));

  if (warnings.length) {
    console.warn(
      `${warnings.length} generic auto-translation templates are present but are ignored at runtime for safety.`,
    );
  }

  if (missing.length) {
    const message =
      `${missing.length} source UI fragments are not in auto-ui.json. ` +
      "This is informational because explicit t(...) keys are the preferred path.";

    if (strictCheck) {
      console.error(`${message}\n${missing.join("\n")}`);
      process.exit(1);
    }

    console.warn(message);
  }

  console.log(
    "Automatic AZ/EN catalogs are structurally valid and placeholder-safe.",
  );
  process.exit(0);
}

if (syncMissingOnly) {
  const [existingAz, existingEn, englishOverrides] = await Promise.all([
    readFile(outputRoots.az, "utf8").then(JSON.parse),
    readFile(outputRoots.en, "utf8").then(JSON.parse),
    readFile(englishOverridesPath, "utf8").then(JSON.parse),
  ]);
  const existingValues = new Set(Object.values(existingAz).map(normalize));
  const missingValues = values.filter((value) => !existingValues.has(value));

  if (!missingValues.length) {
    console.log("Automatic UI catalogs are already synchronized.");
    process.exit(0);
  }

  console.log(
    `Appending ${missingValues.length} missing UI fragments.`,
  );
  const existingTranslations = buildExistingTranslationMap(existingAz, existingEn);
  const translatedValues = await resolveEnglishTranslations(
    missingValues,
    englishOverrides,
    existingTranslations,
  );
  const missingEntries = buildEntries(missingValues);

  Object.keys(missingEntries).forEach((key, index) => {
    const sourceText = missingEntries[key];
    existingAz[key] = sourceText;
    existingEn[key] = translatedValues[index] || sourceText;
  });

  await Promise.all([
    writeFile(
      outputRoots.az,
      `${JSON.stringify(existingAz, null, 2)}\n`,
      "utf8",
    ),
    writeFile(
      outputRoots.en,
      `${JSON.stringify(existingEn, null, 2)}\n`,
      "utf8",
    ),
  ]);
  console.log("Missing AZ/EN automatic UI entries were appended.");
  process.exit(0);
}

const [existingAz, existingEn, englishOverrides] = await Promise.all([
  readJsonOrEmpty(outputRoots.az),
  readJsonOrEmpty(outputRoots.en),
  readFile(englishOverridesPath, "utf8").then(JSON.parse),
]);
const existingTranslations = buildExistingTranslationMap(existingAz, existingEn);
const translatedValues = await resolveEnglishTranslations(
  values,
  englishOverrides,
  existingTranslations,
);
const azEntries = buildEntries(values);
const enEntries = {};
Object.keys(azEntries).forEach((key, index) => {
  const sourceText = azEntries[key];
  enEntries[key] = translatedValues[index] || sourceText;
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
