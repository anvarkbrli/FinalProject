#!/usr/bin/env bash
set -Eeuo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_command docker
require_command curl
require_environment_file
"${SCRIPT_DIR}/validate-env.sh"

mkdir -p "${DEPLOYMENT_DIR}/backups"

echo "Pulling prebuilt production images..."
compose --profile tools pull api frontend migrate

echo "Starting stateful dependencies..."
compose up -d sqlserver redis clamav

"${SCRIPT_DIR}/backup-db.sh"

echo "Applying pending database migrations..."
compose --profile tools run --rm --no-build migrate

echo "Starting application services..."
compose up -d --no-build --remove-orphans api frontend

"${SCRIPT_DIR}/smoke-test.sh"
compose ps
