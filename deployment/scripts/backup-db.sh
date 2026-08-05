#!/usr/bin/env bash
set -Eeuo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_environment_file
mkdir -p "${DEPLOYMENT_DIR}/backups"
backup_file="WellPlayd_$(date -u +%Y%m%dT%H%M%SZ).bak"

echo "Waiting for SQL Server..."
for attempt in $(seq 1 36); do
  if compose exec -T sqlserver bash -lc '
    SQLCMD="$(command -v sqlcmd || true)"
    [[ -n "$SQLCMD" ]] || [[ ! -x /opt/mssql-tools18/bin/sqlcmd ]] || SQLCMD=/opt/mssql-tools18/bin/sqlcmd
    [[ -n "$SQLCMD" ]] || [[ ! -x /opt/mssql-tools/bin/sqlcmd ]] || SQLCMD=/opt/mssql-tools/bin/sqlcmd
    [[ -n "$SQLCMD" ]] && "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" >/dev/null
  ' >/dev/null 2>&1; then
    break
  fi

  if (( attempt == 36 )); then
    echo "SQL Server did not become ready in time." >&2
    exit 1
  fi
  sleep 5
done

compose exec -T -e BACKUP_FILE="${backup_file}" sqlserver bash -lc '
  set -Eeuo pipefail
  SQLCMD="$(command -v sqlcmd || true)"
  [[ -n "$SQLCMD" ]] || [[ ! -x /opt/mssql-tools18/bin/sqlcmd ]] || SQLCMD=/opt/mssql-tools18/bin/sqlcmd
  [[ -n "$SQLCMD" ]] || [[ ! -x /opt/mssql-tools/bin/sqlcmd ]] || SQLCMD=/opt/mssql-tools/bin/sqlcmd
  test -n "$SQLCMD"

  exists="$($SQLCMD -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'"'"'$WELLPLAYD_DB_NAME'"'"') IS NULL THEN 0 ELSE 1 END" | tr -d "[:space:]")"
  if [[ "$exists" != "1" ]]; then
    echo "Database $WELLPLAYD_DB_NAME does not exist yet; backup skipped."
    exit 0
  fi

  "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q "BACKUP DATABASE [$WELLPLAYD_DB_NAME] TO DISK = N'"'"'/var/opt/mssql/backup/$BACKUP_FILE'"'"' WITH COPY_ONLY, CHECKSUM, INIT, STATS = 10"
'

if [[ -f "${DEPLOYMENT_DIR}/backups/${backup_file}" ]]; then
  echo "Database backup created: deployment/backups/${backup_file}"
fi
