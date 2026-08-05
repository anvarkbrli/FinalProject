#!/usr/bin/env bash
set -Eeuo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_environment_file
previous_tag="${1:-}"
if [[ -z "${previous_tag}" || ! "${previous_tag}" =~ ^[A-Za-z0-9._-]+$ ]]; then
  echo "Usage: $0 <previous-image-tag>" >&2
  exit 1
fi

export IMAGE_TAG="${previous_tag}"
compose up -d --no-build api frontend
"${SCRIPT_DIR}/smoke-test.sh"
