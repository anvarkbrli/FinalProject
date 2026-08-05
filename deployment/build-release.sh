#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${1:-$ROOT_DIR/deployment-artifacts}"
FRONTEND_DIR="$ROOT_DIR/presentation/Frontend/ludora-client"
API_PROJECT="$ROOT_DIR/presentation/Ludora.API/Ludora.API.csproj"

: "${VITE_API_BASE_URL:?Set VITE_API_BASE_URL before building the frontend}"
: "${VITE_API_ORIGIN:?Set VITE_API_ORIGIN before building the frontend}"

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR/api" "$OUTPUT_DIR/frontend"

dotnet restore "$API_PROJECT"
dotnet publish "$API_PROJECT" \
  --configuration Release \
  --no-restore \
  --output "$OUTPUT_DIR/api" \
  /p:UseAppHost=false

pushd "$FRONTEND_DIR" >/dev/null
npm ci --include=optional
npm run verify
npm run audit:prod
npm run build
cp -R dist/. "$OUTPUT_DIR/frontend/"
popd >/dev/null

printf 'Deployment artifacts created in: %s\n' "$OUTPUT_DIR"
