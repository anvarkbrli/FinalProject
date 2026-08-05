#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOYMENT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ROOT_DIR="$(cd "${DEPLOYMENT_DIR}/.." && pwd)"
ENV_FILE="${ENV_FILE:-${DEPLOYMENT_DIR}/.env.production}"
COMPOSE_FILE="${DEPLOYMENT_DIR}/docker-compose.production.yml"

compose() {
  docker compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" "$@"
}

get_env_value() {
  local key="$1"
  local value
  value="$(grep -E "^[[:space:]]*${key}=" "${ENV_FILE}" | tail -n 1 | cut -d= -f2- || true)"
  value="${value%$'\r'}"
  value="${value#\"}"
  value="${value%\"}"
  value="${value#\'}"
  value="${value%\'}"
  printf '%s' "${value}"
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 1
  fi
}

require_environment_file() {
  if [[ ! -f "${ENV_FILE}" ]]; then
    echo "Production environment file not found: ${ENV_FILE}" >&2
    echo "Copy deployment/.env.production.example to deployment/.env.production first." >&2
    exit 1
  fi
}
