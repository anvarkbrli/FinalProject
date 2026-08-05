# Deployment files

Əsas təlimat repository kökündəki `DEPLOYMENT.md` faylındadır.

- `docker-compose.production.yml` — production services və persistent volume-lar;
- `.env.production.example` — secret/config template;
- `nginx/default.conf.template` — SPA fallback, API və SignalR proxy;
- `scripts/deploy.sh` — lokal source-dan build, backup, migration, startup və smoke test;
- `scripts/deploy-prebuilt.sh` — registry-dən hazır image pull edib təhlükəsiz release;
- `scripts/backup-db.sh` — SQL Server backup;
- `scripts/rollback-db.sh` — backup-dan database restore;
- `scripts/rollback-image.sh` — əvvəlki image tag-a qayıtmaq;
- `scripts/smoke-test.sh` — public route və health yoxlamaları.

- `reverse-proxy/Caddyfile.example` — public HTTPS və avtomatik sertifikat yenilənməsi üçün host proxy nümunəsi.
