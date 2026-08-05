import { rm } from "node:fs/promises";
import path from "node:path";
import process from "node:process";

const projectRoot = process.cwd();
const generatedPaths = [
  path.join(projectRoot, "dist"),
  path.join(projectRoot, "node_modules", ".vite"),
  path.join(projectRoot, "tsconfig.app.tsbuildinfo"),
  path.join(projectRoot, "tsconfig.node.tsbuildinfo"),
];

await Promise.all(
  generatedPaths.map((generatedPath) =>
    rm(generatedPath, { recursive: true, force: true }),
  ),
);

console.log("Frontend build output cleaned.");
