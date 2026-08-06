import fs from "node:fs";
import path from "node:path";

function readEnvFile(filePath) {
  if (!fs.existsSync(filePath)) {
    return {};
  }

  const result = {};
  const content = fs.readFileSync(filePath, "utf8");

  for (const rawLine of content.split(/\r?\n/)) {
    const line = rawLine.trim();

    if (!line || line.startsWith("#")) {
      continue;
    }

    const separatorIndex = line.indexOf("=");
    if (separatorIndex < 1) {
      continue;
    }

    const key = line.slice(0, separatorIndex).trim();
    let value = line.slice(separatorIndex + 1).trim();

    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }

    result[key] = value;
  }

  return result;
}

const fileEnv = readEnvFile(path.join(process.cwd(), ".env.production"));
const apiBaseUrl = String(
  process.env.VITE_API_BASE_URL ?? fileEnv.VITE_API_BASE_URL ?? "",
).trim();
const apiOrigin = String(
  process.env.VITE_API_ORIGIN ?? fileEnv.VITE_API_ORIGIN ?? "",
).trim();
const errors = [];

if (!apiBaseUrl) {
  errors.push("VITE_API_BASE_URL is required for a production build.");
}

if (apiBaseUrl && !apiBaseUrl.startsWith("/")) {
  let parsedBaseUrl;

  try {
    parsedBaseUrl = new URL(apiBaseUrl);
  } catch {
    errors.push("VITE_API_BASE_URL must be an absolute URL or a path beginning with '/'.");
  }

  if (parsedBaseUrl) {
    if (parsedBaseUrl.protocol !== "https:" && parsedBaseUrl.hostname !== "localhost") {
      errors.push("VITE_API_BASE_URL must use HTTPS outside localhost.");
    }

    if (!apiOrigin) {
      errors.push("VITE_API_ORIGIN is required when VITE_API_BASE_URL is absolute.");
    } else {
      try {
        const parsedOrigin = new URL(apiOrigin);
        const normalizedOrigin = apiOrigin.replace(/\/$/, "");

        if (parsedOrigin.origin !== normalizedOrigin) {
          errors.push("VITE_API_ORIGIN must contain only the origin, without a path.");
        }

        if (parsedOrigin.origin !== parsedBaseUrl.origin) {
          errors.push("VITE_API_ORIGIN must match the origin of VITE_API_BASE_URL.");
        }
      } catch {
        errors.push("VITE_API_ORIGIN must be a valid absolute URL.");
      }
    }
  }
}

if (errors.length > 0) {
  console.error("Production environment validation failed:\n");
  for (const error of errors) {
    console.error(`- ${error}`);
  }
  process.exit(1);
}

console.log(`Production API configuration validated: ${apiBaseUrl}`);
