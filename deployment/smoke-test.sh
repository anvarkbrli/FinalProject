#!/usr/bin/env bash
set -euo pipefail

: "${FRONTEND_ORIGIN:?Set FRONTEND_ORIGIN, for example https://wellplayd.example}"
: "${API_ORIGIN:?Set API_ORIGIN, for example https://api.wellplayd.example}"

CURL=(curl --fail --silent --show-error --location --max-time 20)

printf 'Checking frontend: %s\n' "$FRONTEND_ORIGIN"
"${CURL[@]}" --output /dev/null "$FRONTEND_ORIGIN/"

printf 'Checking API liveness: %s\n' "$API_ORIGIN/health/live"
LIVE_BODY="$("${CURL[@]}" "$API_ORIGIN/health/live")"
printf '%s' "$LIVE_BODY" | grep -qi 'healthy\|ok' || {
  printf 'Unexpected liveness response: %s\n' "$LIVE_BODY" >&2
  exit 1
}

printf 'Checking API readiness: %s\n' "$API_ORIGIN/health/ready"
READY_BODY="$("${CURL[@]}" "$API_ORIGIN/health/ready")"
printf '%s' "$READY_BODY" | grep -qi 'healthy\|ok' || {
  printf 'Unexpected readiness response: %s\n' "$READY_BODY" >&2
  exit 1
}

printf 'Smoke test passed.\n'
