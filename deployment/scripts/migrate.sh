#!/usr/bin/env bash
set -Eeuo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_environment_file
"${SCRIPT_DIR}/validate-env.sh"
compose --profile tools run --rm migrate
