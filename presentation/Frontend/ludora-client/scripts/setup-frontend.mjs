import { existsSync, rmSync } from "node:fs";
import { spawnSync } from "node:child_process";
import process from "node:process";

const nodeMajor = Number.parseInt(process.versions.node.split(".")[0] ?? "0", 10);
if (!Number.isFinite(nodeMajor) || nodeMajor < 20) {
  console.error("WellPlayd frontend requires Node.js 20 or newer.");
  process.exit(1);
}

if (existsSync("node_modules")) {
  console.log("Removing the existing node_modules to avoid OS-specific native binding errors...");
  rmSync("node_modules", { recursive: true, force: true });
}

const npmCommand = process.platform === "win32" ? "npm.cmd" : "npm";
const result = spawnSync(
  npmCommand,
  ["ci", "--include=optional"],
  { stdio: "inherit", shell: false },
);

if (result.error) {
  console.error(result.error.message);
  process.exit(1);
}

process.exit(result.status ?? 1);
