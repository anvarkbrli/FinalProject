#!/usr/bin/env bash
set -Eeuo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_environment_file
backup_name="${1:-}"
if [[ -z "${backup_name}" ]]; then
  echo "Usage: $0 <backup-file.bak>" >&2
  exit 1
fi

backup_name="$(basename "${backup_name}")"
if [[ ! "${backup_name}" =~ ^[A-Za-z0-9._-]+\.bak$ ]]; then
  echo "Invalid backup file name." >&2
  exit 1
fi
if [[ ! -f "${DEPLOYMENT_DIR}/backups/${backup_name}" ]]; then
  echo "Backup not found: deployment/backups/${backup_name}" >&2
  exit 1
fi

read -r -p "This will replace the production database from ${backup_name}. Type RESTORE to continue: " confirmation
if [[ "${confirmation}" != "RESTORE" ]]; then
  echo "Rollback cancelled."
  exit 1
fi

compose stop frontend api
compose exec -T -e BACKUP_FILE="${backup_name}" sqlserver bash -lc '
  set -Eeuo pipefail
  SQLCMD="$(command -v sqlcmd || true)"
  [[ -n "$SQLCMD" ]] || [[ ! -x /opt/mssql-tools18/bin/sqlcmd ]] || SQLCMD=/opt/mssql-tools18/bin/sqlcmd
  [[ -n "$SQLCMD" ]] || [[ ! -x /opt/mssql-tools/bin/sqlcmd ]] || SQLCMD=/opt/mssql-tools/bin/sqlcmd
  test -n "$SQLCMD"

  "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q "
    IF DB_ID(N'"'"'$WELLPLAYD_DB_NAME'"'"') IS NOT NULL
      ALTER DATABASE [$WELLPLAYD_DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    RESTORE DATABASE [$WELLPLAYD_DB_NAME]
      FROM DISK = N'"'"'/var/opt/mssql/backup/$BACKUP_FILE'"'"'
      WITH REPLACE, RECOVERY, CHECKSUM, STATS = 10;
    ALTER DATABASE [$WELLPLAYD_DB_NAME] SET MULTI_USER;"
'

compose up -d api frontend
"${SCRIPT_DIR}/smoke-test.sh"
