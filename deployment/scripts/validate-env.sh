#!/usr/bin/env bash
set -Eeuo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_command docker
require_environment_file

required_keys=(
  PUBLIC_FRONTEND_ORIGIN
  PUBLIC_API_BASE_URL
  API_ALLOWED_HOSTS
  DB_NAME
  MSSQL_SA_PASSWORD
  JWT_SIGNING_KEY
  SMTP_HOST
  SMTP_FROM_EMAIL
)

failed=0
for key in "${required_keys[@]}"; do
  value="$(get_env_value "${key}")"
  if [[ -z "${value}" ]]; then
    echo "Missing required value: ${key}" >&2
    failed=1
  elif [[ "${value}" == *CHANGE_ME* || "${value}" == *example.com* ]]; then
    echo "Placeholder value is still present: ${key}" >&2
    failed=1
  fi
done

jwt_key="$(get_env_value JWT_SIGNING_KEY)"
if (( ${#jwt_key} < 64 )); then
  echo "JWT_SIGNING_KEY must contain at least 64 characters." >&2
  failed=1
fi

sql_password="$(get_env_value MSSQL_SA_PASSWORD)"
if (( ${#sql_password} < 16 )) ||
   [[ ! "${sql_password}" =~ [A-Z] ]] ||
   [[ ! "${sql_password}" =~ [a-z] ]] ||
   [[ ! "${sql_password}" =~ [0-9] ]] ||
   [[ "${sql_password}" =~ ^[[:alnum:]]+$ ]]; then
  echo "MSSQL_SA_PASSWORD must be at least 16 characters and include upper, lower, digit and symbol." >&2
  failed=1
fi

for key in PUBLIC_FRONTEND_ORIGIN PUBLIC_API_BASE_URL; do
  value="$(get_env_value "${key}")"
  if [[ ! "${value}" =~ ^https://[^/]+(/)?$ ]]; then
    echo "${key} must be a production HTTPS origin/base URL." >&2
    failed=1
  fi
done

db_name="$(get_env_value DB_NAME)"
if [[ ! "${db_name}" =~ ^[A-Za-z0-9_]+$ ]]; then
  echo "DB_NAME may only contain letters, numbers and underscore." >&2
  failed=1
fi

api_host="$(get_env_value API_ALLOWED_HOSTS)"
if [[ ! "${api_host}" =~ ^([A-Za-z0-9-]+\.)*[A-Za-z0-9-]+$ ]]; then
  echo "API_ALLOWED_HOSTS must contain one hostname without scheme, port or path." >&2
  failed=1
fi

if (( failed != 0 )); then
  exit 1
fi

compose config --quiet
echo "Production environment validation passed."
