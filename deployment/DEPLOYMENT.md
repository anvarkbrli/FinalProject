# WellPlayd production deployment

These files prepare the current application for a **single API instance** deployment without changing any frontend UI.

## 1. Configuration

Copy `deployment/.env.production.example` to `deployment/.env.production` and replace every `CHANGE_ME` or example domain. Do not commit the real file.

The frontend variables are build-time values. Changing `VITE_API_BASE_URL` or `VITE_API_ORIGIN` requires rebuilding the frontend image/artifact.

## 2. Database migration

Back up the production database, then apply migrations before starting the new API version:

```bash
dotnet ef database update \
  --project src/Infrastructure/Ludora.Persistence \
  --startup-project presentation/Ludora.API \
  --configuration Release
```

For stricter production control, generate and review an idempotent SQL script instead:

```bash
dotnet ef migrations script --idempotent \
  --project src/Infrastructure/Ludora.Persistence \
  --startup-project presentation/Ludora.API \
  --output deployment-artifacts/migrate.sql
```

## 3. Docker Compose example

From the `deployment` directory:

```bash
cp .env.production.example .env.production
# edit .env.production
docker compose --env-file .env.production \
  -f docker-compose.production.example.yml \
  build
docker compose --env-file .env.production \
  -f docker-compose.production.example.yml \
  up -d
```

The compose file intentionally starts only one API service. The API also holds a SQL Server application lock; a second API process exits during startup instead of running with inconsistent SignalR presence, local rate limits or local file storage.

Because of that lock, use a stop-then-start deployment, not an overlapping rolling deployment, until distributed SignalR/presence/storage support is added.

## 4. Reverse proxy and TLS

Terminate HTTPS at your external reverse proxy and forward traffic to:

- frontend: `127.0.0.1:8081`
- API: `127.0.0.1:8080`

Set `ForwardedHeaders__KnownNetworks__0` or `ForwardedHeaders__KnownProxies__0` to the actual trusted proxy network/address. Never trust all forwarded headers.

The external proxy must also:

- support WebSocket `Upgrade`/`Connection` forwarding for `/hubs/*`;
- allow at least `220m` request bodies for discussion/chat video uploads;
- use a read timeout of at least 75 seconds for long uploads and SignalR;
- add HSTS only at the HTTPS termination layer.

A complete starting point is available in `deployment/nginx-reverse-proxy.example.conf`. Replace every domain and certificate path before use.

The bundled frontend Nginx configuration supports React SPA refresh/deep links. Netlify (`public/_redirects`), Vercel (`vercel.json`) and IIS (`public/web.config`) fallbacks are also included.

## 5. Health checks

Anonymous endpoints return only a safe status:

- `/health/live`
- `/health/ready`

Detailed readiness information is available only to an authenticated user with the `Admin` role:

- `/health/ready/details`

## 6. ClamAV

Production startup validation requires malware scanning to be enabled and fail closed. The API readiness endpoint remains unhealthy while ClamAV is unavailable. The first ClamAV start can take time while virus definitions are downloaded.

Persist `/var/lib/clamav` so definitions are not downloaded from scratch on every restart.

## 7. Clean release artifacts without Docker

Linux/macOS:

```bash
export VITE_API_BASE_URL=https://api.your-domain.tld/api
export VITE_API_ORIGIN=https://api.your-domain.tld
./deployment/build-release.sh
```

Windows PowerShell:

```powershell
$env:VITE_API_BASE_URL = "https://api.your-domain.tld/api"
$env:VITE_API_ORIGIN = "https://api.your-domain.tld"
./deployment/build-release.ps1
```

The release scripts run the frontend production dependency audit before producing artifacts. NuGet high/critical advisories fail CI through `Directory.Build.props`.

Only `deployment-artifacts/api` and `deployment-artifacts/frontend` should be uploaded. Do not deploy `.git`, `.vs`, `node_modules`, source uploads, private storage or local Data Protection keys.

## 8. Production acceptance checklist

Before routing real traffic:

1. Require a green GitHub Actions `CI` and `CodeQL` result for the exact commit.
2. Restore a recent database backup in staging and test the idempotent migration script.
3. Verify `/health/live` and `/health/ready` after restarting SQL Server, Redis and ClamAV individually.
4. Test registration email confirmation, password reset, refresh/logout cookies and Google/Steam callbacks when enabled.
5. Upload the largest supported image/video through the real TLS proxy.
6. Verify persistent volumes by recreating the API container and confirming uploads and Data Protection-protected sessions remain valid.
7. Configure external database and volume backups, retention, restore drills, centralized logs and uptime alerts.

## 6. Post-deployment smoke test

After DNS, TLS and containers are ready, run the anonymous smoke test from a trusted operator machine:

```bash
export FRONTEND_ORIGIN=https://wellplayd.example
export API_ORIGIN=https://api.wellplayd.example
./deployment/smoke-test.sh
```

Then sign in with an administrator account and validate `/health/ready/details`, login, one media upload, SignalR messaging and Steam synchronization in staging before promoting the release.

## 7. Runtime support lifecycle

The repository is pinned to the latest .NET 8 servicing SDK/package patch used by this audit. .NET 8 reaches end of support on 10 November 2026, so schedule and test a migration to the current LTS .NET release before that date.
