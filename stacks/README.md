# Ready-to-run stacks (RuleFlow + a database)

One Docker Compose file per supported database. Each runs the RuleFlow engine
(`ghcr.io/roberthdudiver/ruleflow:latest`) already wired to a database, **skips the wizard** (configured by
env) and seeds the demo data plus a default **`admin` / `admin`** (change it after first login).

```bash
docker compose -f sqlite.yml     up -d     # simplest — no separate DB container
docker compose -f postgres.yml   up -d
docker compose -f sqlserver.yml  up -d
docker compose -f mongo.yml      up -d
```

Then open **http://localhost:8080**. Stop and wipe with `docker compose -f <file>.yml down -v`.

| File | Database | Notes |
|------|----------|-------|
| `sqlite.yml`    | SQLite     | Zero-config, single container. |
| `postgres.yml`  | PostgreSQL | Includes a `postgres:16` container. |
| `sqlserver.yml` | SQL Server | Includes `mssql/server:2022` (needs ~2 GB RAM). |
| `mongo.yml`     | MongoDB    | Includes a `mongo:7` container. |

All four are smoke-tested in CI (the engine seeds on startup and refuses to start on a bad database, so a
healthy `:8080` means the database works).

> Prefer the guided setup instead? Just run the plain image and use the **installation wizard** — see the
> main [README](../README.md). For a public domain with HTTPS (nginx / IIS), see [docs/DEPLOY.md](../docs/DEPLOY.md).
>
> Change the seeded `admin` / `admin` password after first login. These compose files use demo DB passwords —
> change them for anything beyond local use.
