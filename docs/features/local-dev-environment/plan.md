# Plan: Local Developer Environment

**Feature**: FEAT-0058
**Spec**: [spec.md](spec.md)
**Status**: Implemented
**Date**: 2026-05-08

---

## Summary

Deliver a self-contained, reproducible local development environment so contributors can work
productively without depending on any shared or demo BookStack instance. All deliverables are
pure tooling and infrastructure files — no production C# source code is changed. The outcome
is an F5-ready VS Code workflow backed by a locally running BookStack + MariaDB Docker stack
and an optional Ollama service.

---

## Architecture Decisions

| ADR | Title | Status |
|-----|-------|--------|
| [ADR-0009](../../architecture/decisions/ADR-0009-dual-transport-entry-point.md) | Dual-Transport Entry-Point Strategy | Accepted |
| [ADR-0013](../../architecture/decisions/ADR-0013-both-mode-hosting-model.md) | Both-Mode Hosting — stdio as IHostedService inside WebApplication | Accepted |

**No new ADRs are required.** This feature introduces no new architecture patterns. The transport
mode (`http`), the port env var (`BOOKSTACK_MCP_HTTP_PORT`), and the stdout-cleanliness constraint
are all governed by ADR-0009. The `both` transport mode documented in ADR-0013 is not activated
by this feature; the dev environment uses `http` mode exclusively so that the .NET debugger can
attach to a standard process without stdio complications.

---

## Technical Design

### docker-compose.dev.yml

Four services; three always-on, one gated behind the `ollama` profile.

```yaml
name: bookstack-mcp-dev

services:

  bookstack-db:
    image: mariadb:10.11
    volumes:
      - dev_mariadb_data:/var/lib/mysql
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: ${DB_DATABASE}
      MYSQL_USER: ${DB_USERNAME}
      MYSQL_PASSWORD: ${DB_PASSWORD}
    healthcheck:
      test: ["CMD", "healthcheck.sh", "--connect", "--innodb_initialized"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    restart: unless-stopped

  bookstack:
    image: lscr.io/linuxserver/bookstack
    depends_on:
      bookstack-db:
        condition: service_healthy
    environment:
      PUID: 1000
      PGID: 1000
      APP_URL: ${APP_URL}
      DB_HOST: bookstack-db
      DB_PORT: 3306
      DB_DATABASE: ${DB_DATABASE}
      DB_USERNAME: ${DB_USERNAME}
      DB_PASSWORD: ${DB_PASSWORD}
    volumes:
      - dev_bookstack_data:/config
    ports:
      - "6875:80"
    healthcheck:
      test: ["CMD", "curl", "-sf", "http://localhost:80/login"]
      interval: 15s
      timeout: 5s
      retries: 12
      start_period: 90s
    restart: unless-stopped

  bookstack-mcp:
    build:
      context: .
      dockerfile: Dockerfile
    depends_on:
      bookstack:
        condition: service_healthy
    env_file:
      - .env.dev
    environment:
      BOOKSTACK_MCP_TRANSPORT: http
    ports:
      - "${BOOKSTACK_MCP_HTTP_PORT:-3001}:${BOOKSTACK_MCP_HTTP_PORT:-3001}"
    extra_hosts:
      - "host-gateway:host-gateway"
    restart: unless-stopped

  ollama:
    image: ollama/ollama
    profiles:
      - ollama
    volumes:
      - dev_ollama_data:/root/.ollama
    ports:
      - "11434:11434"
    restart: unless-stopped

volumes:
  dev_mariadb_data:
  dev_bookstack_data:
  dev_ollama_data:
```

**Design notes**:

- `bookstack-db` uses `mariadb:10.11` (LTS; matches the BookStack linuxserver image requirement for
  MariaDB 10.x). The MariaDB built-in `healthcheck.sh` script is available from MariaDB 10.4+.
- `bookstack` uses a generous `start_period: 90s` because the linuxserver image runs an
  internal migration on first start. The health check polls `/login`, which is the unauthenticated
  redirect guaranteed to return 200 when the PHP application is ready.
- `bookstack-mcp` sets `BOOKSTACK_MCP_TRANSPORT: http` directly in the compose environment (not
  from `.env.dev`) to enforce http mode regardless of what the developer puts in their env file.
  Per ADR-0009, this makes Kestrel start on `BOOKSTACK_MCP_HTTP_PORT` (sourced from `.env.dev`
  via `env_file`). This keeps stdout clean for any tooling that reads the container output.
- `extra_hosts: host-gateway:host-gateway` enables WSL2 → Windows Ollama connectivity. Docker
  Engine on Linux maps `host-gateway` to the host's bridge IP automatically; this entry is a
  no-op on native Linux but is required for the WSL2 scenario.
- Port `3306` (MariaDB) is intentionally **not** published to the host. Add a commented-out port
  mapping in the file if direct DB access is needed (see File Map notes).
- Named volumes all carry the `dev_` prefix to avoid collisions with production volumes on the
  same host (NFR-1).
- Dev MCP port defaults to `3001` (sourced from `.env.dev` via `BOOKSTACK_MCP_HTTP_PORT`) to avoid
  colliding with production Compose files that use `3000` (NFR-2).

### .env.dev.example

```dotenv
# ─── BookStack API ────────────────────────────────────────────────────────────
# URL BookStack is reachable at from the MCP server container.
# When running via docker-compose.dev.yml, keep the default below.
# When running the MCP server natively (F5 / launch.json), change to:
#   BOOKSTACK_BASE_URL=http://localhost:6875
BOOKSTACK_BASE_URL=http://bookstack:6875

# API token — generated in the BookStack UI after first-run setup:
#   Profile (top-right) → API Tokens → Add Token → copy ID and Secret
BOOKSTACK_TOKEN_ID=replace-me
BOOKSTACK_TOKEN_SECRET=replace-me

# ─── MCP Server ───────────────────────────────────────────────────────────────
# Bearer token that protects the MCP HTTP endpoint.
# Replace with a random string; this is not exposed publicly but should
# still be treated as a secret and never committed.
BOOKSTACK_MCP_HTTP_AUTH_TOKEN=dev-token-replace-me

# Port the MCP server listens on (and is mapped to the host).
# Defaults to 3001 to avoid collision with production Compose (3000).
BOOKSTACK_MCP_HTTP_PORT=3001

# ─── MariaDB ──────────────────────────────────────────────────────────────────
DB_ROOT_PASSWORD=dev-root-password
DB_DATABASE=bookstack
DB_USERNAME=bookstack
DB_PASSWORD=bookstack-dev-password

# ─── BookStack App ────────────────────────────────────────────────────────────
# Public URL BookStack uses when generating links (must be host-accessible).
APP_URL=http://localhost:6875

# ─── Ollama ───────────────────────────────────────────────────────────────────
# When using the ollama Compose profile (embedded container):
#   OLLAMA_BASE_URL=http://ollama:11434
# When using Ollama on the Windows host from WSL2:
#   OLLAMA_BASE_URL=http://host-gateway:11434
OLLAMA_BASE_URL=http://ollama:11434

# ─── Feature Flags ────────────────────────────────────────────────────────────
# Set to true to enable semantic / vector search (requires Ollama).
VECTOR_SEARCH_ENABLED=false
```

**Security notes**:

- `.env.dev` must be added to `.gitignore`. The implementation task must verify this entry exists.
- No real credentials appear above — all values are clearly labelled placeholders.
- `BOOKSTACK_MCP_HTTP_AUTH_TOKEN=dev-token-replace-me` is intentionally non-guessable-looking to
  signal replacement; the guide must reinforce that the token should be replaced.

### .devcontainer/devcontainer.json

```json
{
  "name": "BookStack MCP Server",
  "dockerComposeFile": ["../docker-compose.dev.yml"],
  "service": "bookstack-mcp",
  "workspaceFolder": "/workspace",
  "mounts": [
    "source=${localWorkspaceFolder},target=/workspace,type=bind,consistency=cached"
  ],
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csdevkit",
        "ms-dotnettools.csharp",
        "ms-azuretools.vscode-docker"
      ]
    }
  },
  "remoteUser": "root"
}
```

**Design notes**:

- `dockerComposeFile` is relative to `.devcontainer/` — `../docker-compose.dev.yml` is correct.
- `service: bookstack-mcp` attaches VS Code to the MCP server container.
- The `mounts` entry satisfies NFR-5 (repository root reflected on host filesystem).
- `ms-dotnettools.csdevkit` and `ms-dotnettools.csharp` are the mandatory extensions per FR-7.
- `ms-azuretools.vscode-docker` is a convenience addition (Docker view inside the container).
- `remoteUser: root` is required because the `Dockerfile` runs the server as root by default; if
  the Dockerfile is updated to use a non-root user, this must be updated to match.

### .vscode/launch.json

The launch configuration runs the MCP server natively (not in a container) with the .NET debugger
attached. The pre-launch task starts the BookStack dependency containers so that the native process
can reach `http://localhost:6875`.

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "BookStack MCP Server (local dev)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "Start dev dependencies",
      "program": "${workspaceFolder}/src/BookStack.Mcp.Server/bin/Debug/net10.0/BookStack.Mcp.Server.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/BookStack.Mcp.Server",
      "envFile": "${workspaceFolder}/.env.dev",
      "env": {
        "BOOKSTACK_MCP_TRANSPORT": "http",
        "BOOKSTACK_BASE_URL": "http://localhost:6875"
      },
      "stopAtEntry": false,
      "console": "internalConsole"
    }
  ]
}
```

**Design notes**:

- `envFile` loads all credentials from `.env.dev` so developers never need to set system env vars.
- The `env` block **overrides** two variables from `.env.dev`:
  - `BOOKSTACK_MCP_TRANSPORT: http` — enforces http mode per ADR-0009 (stdout must remain clean
    in stdio mode; http mode is the correct choice when a debugger is attached).
  - `BOOKSTACK_BASE_URL: http://localhost:6875` — overrides the container-internal default
    (`http://bookstack:6875`) with the host-accessible address exposed by the dev Compose file.
    The developer's `.env.dev` can keep the container-internal URL for full-stack Compose usage.
- `program` path assumes `net10.0` target framework moniker; the implementation task must verify
  this matches `<TargetFramework>` in `BookStack.Mcp.Server.csproj`.
- `preLaunchTask` must match the `label` in `tasks.json` exactly.

### .vscode/tasks.json

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Start dev dependencies",
      "type": "shell",
      "command": "docker compose -f docker-compose.dev.yml up -d bookstack-db bookstack",
      "isBackground": true,
      "problemMatcher": [],
      "presentation": {
        "reveal": "always",
        "panel": "shared",
        "showReuseMessage": false
      }
    },
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/BookStack.Mcp.Server.sln",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "group": "build",
      "problemMatcher": "$msCompile"
    },
    {
      "label": "Stop dev dependencies",
      "type": "shell",
      "command": "docker compose -f docker-compose.dev.yml down",
      "problemMatcher": [],
      "presentation": {
        "reveal": "always",
        "panel": "shared"
      }
    }
  ]
}
```

**Design notes**:

- `Start dev dependencies` starts only `bookstack-db` and `bookstack`; the `bookstack-mcp`
  container is intentionally excluded so VS Code runs the process natively for debugging.
- `isBackground: true` prevents VS Code from waiting for the shell command to exit before
  launching the debugger. The `problemMatcher: []` suppresses the "no problem matcher" warning.
- The `build` task is the standard VS Code .NET build task generated by the C# Dev Kit; including
  it here avoids conflicts if VS Code generates a `tasks.json` later.
- `Stop dev dependencies` is a convenience task accessible via the Command Palette.

### docs/developer-guide.md

The implementation task must author the full prose. The required section outline is:

1. **Overview** — purpose of the dev environment, link to the architecture diagram in the spec.
2. **Prerequisites** — Docker Engine 24+ (or Docker Desktop), .NET 10 SDK, VS Code with
   `ms-dotnettools.csdevkit` and `ms-dotnettools.csharp`, `pwsh` for the seed script.
3. **Quick Start** (numbered steps):
   1. Clone the repository.
   2. Copy `.env.dev.example` → `.env.dev`.
   3. Run `docker compose -f docker-compose.dev.yml up -d`.
   4. Wait for BookStack to be healthy: `docker compose -f docker-compose.dev.yml ps`.
   5. Open `http://localhost:6875` and complete the BookStack first-run setup (default credentials
      `admin@admin.com` / `password`; change the password immediately).
   6. Generate an API token: Profile → API Tokens → Add Token. Copy the Token ID and Secret into
      `.env.dev` as `BOOKSTACK_TOKEN_ID` and `BOOKSTACK_TOKEN_SECRET`.
   7. Restart the MCP server: `docker compose -f docker-compose.dev.yml restart bookstack-mcp`.
   8. Verify: `curl -H "Authorization: Token dev-token-replace-me" http://localhost:3001/health`.
4. **F5 Debug Launch** — open the repository in VS Code, ensure `.env.dev` is populated, press F5.
   Explain that `Start dev dependencies` starts BookStack containers automatically.
5. **VS Code Dev Container Setup** — Reopen in Container workflow; note that the full Compose
   stack starts including `bookstack-mcp` as the target service.
6. **Environment Variables Reference** — table matching the one in the spec.
7. **WSL2 → Windows Ollama Networking** — explain `host-gateway`, the `extra_hosts` entry, and how
   to set `OLLAMA_BASE_URL=http://host-gateway:11434` in `.env.dev`. Include a `curl` test command.
8. **Using the Ollama Profile** — how to start with `--profile ollama` and pull a model.
9. **Pre-populating Data** — how to run `scripts/Seed-BookStack.ps1 -Topic "Your Topic"`, including
   the `-DryRun` option and the Windows/WSL2 `pwsh` invocation.
10. **Common Tasks** — table of useful commands (rebuild MCP image, tail logs, open a DB shell,
    reset all volumes, stop the stack).
11. **Troubleshooting** — common issues: BookStack not ready (slow first-run migration), port
    conflicts, `host-gateway` not resolving, `.env.dev` missing variables.
12. **Security Warning** — `APP_URL` and port bindings are loopback-only; `.env.dev` must never be
    committed.

### scripts/Seed-BookStack.ps1

Already implemented. No design work required. The plan references it for documentation and
acceptance criteria traceability only.

---

## File Map

| File | Action | Notes |
|------|--------|-------|
| `docker-compose.dev.yml` | **Create** | New file; see Technical Design above for exact content |
| `.env.dev.example` | **Create** | New file; exact content in Technical Design above |
| `.env.dev` | **Do not create** | Developer-local; must be in `.gitignore` |
| `.gitignore` | **Edit** | Add `.env.dev` entry if not already present |
| `.devcontainer/devcontainer.json` | **Create** | New file and new directory; see Technical Design above |
| `.vscode/launch.json` | **Create** | New file and new directory; see Technical Design above |
| `.vscode/tasks.json` | **Create** | Same directory as launch.json; see Technical Design above |
| `docs/developer-guide.md` | **Create** | Full prose; section outline in Technical Design above |
| `scripts/Seed-BookStack.ps1` | **Already exists** | No changes required; referenced in developer guide |

---

## Commands

Executable commands for this feature (copy and run directly):

### Build

```bash
dotnet build BookStack.Mcp.Server.sln --configuration Debug
```

### Tests

```bash
dotnet test BookStack.Mcp.Server.sln --verbosity normal
```

### Lint / Formatting

```bash
dotnet format BookStack.Mcp.Server.sln --verify-no-changes
```

### Start dev stack

```bash
docker compose -f docker-compose.dev.yml up -d
```

### Start dev stack with Ollama

```bash
docker compose -f docker-compose.dev.yml --profile ollama up -d
```

### F5 local debug (VS Code)

Press **F5** with `BookStack MCP Server (local dev)` selected in the Run and Debug panel.

### Seed data

```bash
pwsh scripts/Seed-BookStack.ps1 -Topic "Docker Networking"
```

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| BookStack first-run migration takes > 90 s on slow hardware, causing the health check to fail and `bookstack-mcp` to exit | Medium | Medium | Increase `start_period` for `bookstack` health check; document the symptom and `docker compose ps` retry in the troubleshooting section |
| `.env.dev` accidentally committed to the repository, exposing local credentials | Low | High | Add `.env.dev` to `.gitignore`; verify the entry exists as part of the implementation task; `.env.dev.example` contains only placeholders |
| Port conflict: developer runs both dev and production Compose stacks on the same host simultaneously | Medium | Low | Dev MCP server defaults to port `3001`; document the assumption in the developer guide; remind developers to check `docker ps` |
| `host-gateway` special DNS name not available on older Docker Engine versions (< 20.10) | Low | Medium | Document the minimum Docker Engine version (20.10+) in prerequisites; provide the fallback of using the explicit host IP (`ip route show default \| awk '/default/ {print $3}'`) |
| Dev Container `remoteUser: root` mismatches a future non-root Dockerfile update | Low | Low | Add a comment in `devcontainer.json` linking it to the Dockerfile `USER` instruction; flag as a coupling risk |
| `lscr.io/linuxserver/bookstack` image tag `latest` drifts and breaks MariaDB compatibility | Low | Medium | Pin the BookStack image to a specific version tag in `docker-compose.dev.yml` after validating; document the pinning rationale |

---

## Open Items

- [ ] Verify `<TargetFramework>` in `src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj` is
  `net10.0` so that the `program` path in `launch.json` is correct.
- [ ] Confirm `.gitignore` already includes `.env.dev`; add if missing.
- [ ] Decide whether to pin the `lscr.io/linuxserver/bookstack` image to a specific version tag
  rather than `latest` to prevent silent upgrades breaking the dev environment.
- [ ] Verify the `mariadb:10.11` health check script (`healthcheck.sh`) is present in that image
  version; fall back to `mysqladmin ping` if not.
- [ ] After FEAT-0058 is merged, evaluate .NET Aspire integration as a follow-up feature
  (noted as deferred in the spec's Open Questions).
- [ ] Extend `docs/developer-guide.md` with macOS-specific instructions post-FEAT-0058 (deferred
  per spec Non-Goals).
