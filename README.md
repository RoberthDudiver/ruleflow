# RuleFlow

**A dynamic rules & process engine** — design process lifecycles (state machines) visually, decide with
point-and-click rules, and do the work with hot-pluggable DLL plugins. Multi-tenant, audited, with a built-in
HTTP API and AI steps.

> **Evaluation build.** Free for testing and development. **Production or commercial use requires a paid
> license** — see [LICENSE](LICENSE) and contact **rdudiver@gmail.com**. The source code is private; this
> repository only distributes the installable binaries and the installer scripts.

---

## Install

### Docker (Windows · Linux · macOS)

```bash
docker run -d --name ruleflow --restart unless-stopped -p 8080:8080 \
  -v ruleflow-data:/app/App_Data -v ruleflow-plugins:/app/pluginstore \
  ghcr.io/roberthdudiver/ruleflow:latest
```

Or with Compose (see [docker-compose.yml](docker-compose.yml)): `docker compose up -d`.

Then open **http://localhost:8080** and complete the setup wizard.
`--restart unless-stopped` is required so the wizard can restart the app to apply the configuration.

### Linux (no Docker) — one command

```bash
curl -fsSL https://raw.githubusercontent.com/RoberthDudiver/ruleflow/main/install.sh | bash
```

Install as a service instead: `curl -fsSL …/install.sh | sudo RULEFLOW_SERVICE=1 bash`.

### Windows (no Docker) — one command (PowerShell)

```powershell
irm https://raw.githubusercontent.com/RoberthDudiver/ruleflow/main/install.ps1 | iex
```

Install as a Windows Service instead (run PowerShell as Administrator):
`$env:RULEFLOW_SERVICE=1; irm …/install.ps1 | iex`.

### Windows with IIS

Publish sits behind IIS via the **ASP.NET Core Hosting Bundle**:
1. Install the [.NET Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Unzip the `win-x64` release into your site folder (e.g. `C:\inetpub\ruleflow`).
3. Create an IIS site pointing at that folder; the bundled `web.config` runs it via the ASP.NET Core Module.
4. Browse the site to run the wizard.

---

## First run

The wizard asks for the **database** (SQLite / PostgreSQL / SQL Server / MongoDB, with a connection test),
creates the **admin** user, and optionally configures **AI**. It then writes its config and the app restarts,
ready to use. To skip the wizard and configure by environment (e.g. in orchestration), set `Setup__Skip=true`
and the `Persistence__*` variables.

## After install

- Web UI at `/` (login required; roles: Viewer / Operator / Admin).
- API reference at `/scalar`, OpenAPI at `/openapi/v1.json`.
- Consume the API with a per-tenant `X-Api-Key` (create keys in **Settings → API keys**).
- In-app help at `/ayuda`.

## Requirements

- **Docker**: nothing else — the image is self-contained.
- **No-Docker binaries**: self-contained (the .NET runtime is bundled). For IIS, install the .NET Hosting Bundle.
