import { existsSync, rmSync } from "node:fs";
import { spawnSync } from "node:child_process";
import process from "node:process";

const requiredNodeVersion = [22, 23, 2];
const currentNodeVersion = process.versions.node
  .split(".")
  .slice(0, 3)
  .map((part) => Number.parseInt(part, 10));

const isSupportedNodeVersion =
  currentNodeVersion.length === 3 &&
  currentNodeVersion.every(Number.isFinite) &&
  currentNodeVersion[0] === requiredNodeVersion[0] &&
  (currentNodeVersion[1] > requiredNodeVersion[1] ||
    (currentNodeVersion[1] === requiredNodeVersion[1] &&
      currentNodeVersion[2] >= requiredNodeVersion[2]));

if (!isSupportedNodeVersion) {
  console.error(
    `WellPlayd frontend requires Node.js 22.23.2 or newer within the Node 22 LTS line. Current: ${process.versions.node}`,
  );
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
