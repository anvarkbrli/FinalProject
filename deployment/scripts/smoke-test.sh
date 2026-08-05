#!/usr/bin/env bash
set -Eeuo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_command curl
require_environment_file

base_url="$(get_env_value SMOKE_BASE_URL)"
if [[ -z "${base_url}" ]]; then
  port="$(get_env_value PUBLIC_HTTP_PORT)"
  base_url="http://127.0.0.1:${port:-8080}"
fi
base_url="${base_url%/}"
allowed_hosts="$(get_env_value API_ALLOWED_HOSTS)"
request_host="${allowed_hosts%%;*}"
curl_headers=(--header "Host: ${request_host}" --header "X-Forwarded-Proto: https")

wait_for_status() {
  local url="$1"
  local expected="$2"
  local label="$3"

  for attempt in $(seq 1 30); do
    status="$(curl --silent --show-error --output /tmp/wellplayd-smoke-body --write-out '%{http_code}' "${curl_headers[@]}" "${url}" || true)"
    if [[ "${status}" == "${expected}" ]]; then
      echo "PASS: ${label} (${status})"
      return 0
    fi
    sleep 4
  done

  echo "FAIL: ${label}; expected ${expected}, received ${status:-none}." >&2
  cat /tmp/wellplayd-smoke-body >&2 2>/dev/null || true
  return 1
}

wait_for_status "${base_url}/" "200" "frontend root"
wait_for_status "${base_url}/health/live" "200" "API liveness"
wait_for_status "${base_url}/health/ready" "200" "API readiness"
wait_for_status "${base_url}/health/details" "401" "health details are protected"

signalr_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "${curl_headers[@]}" \
  --request POST \
  "${base_url}/hubs/chat/negotiate?negotiateVersion=1" || true)"
if [[ "${signalr_status}" != "401" ]]; then
  echo "FAIL: SignalR proxy route expected 401 without token, received ${signalr_status}." >&2
  exit 1
fi
echo "PASS: SignalR proxy route (${signalr_status})"

echo "Production smoke tests passed."
