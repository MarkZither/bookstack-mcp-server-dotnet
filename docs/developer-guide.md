# Developer Guide

This guide gets you from a fresh clone to a running, debuggable MCP server backed by your own
local BookStack instance — no shared credentials, no 15-minute token resets.

---

## Overview

The local dev environment consists of three Docker containers managed by `docker-compose.dev.yml`:

| Container | Image | Host Port |
|---|---|---|
| `bookstack-db` | `mariadb:10.11` | not published (Docker bridge only) |
| `bookstack` | `lscr.io/linuxserver/bookstack` | `6875` |
| `bookstack-mcp` | local build (Dockerfile) | `3001` |

An optional `ollama` service is available via the `ollama` Compose profile (see
[Using the Ollama Profile](#using-the-ollama-profile)).

For a full architecture diagram see the
[FEAT-0058 spec](features/local-dev-environment/spec.md#component-diagram).

---

## Prerequisites

| Tool | Minimum version | Notes |
|---|---|---|
| Docker Engine or Docker Desktop | 20.10+ | Docker Desktop includes Compose v2 |
| Docker Compose v2 | 2.5+ | `docker compose` (no hyphen) |
| .NET 10 SDK | 10.0+ | [download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| VS Code | any recent | with Remote WSL if on Windows |
| `ms-dotnettools.csdevkit` | any | install from VS Code Extensions |
| `ms-dotnettools.csharp` | any | install from VS Code Extensions |
| `pwsh` (PowerShell 7+) | 7.0+ | only required for the seed script |

---

## Quick Start

### 1. Clone and configure

```bash
git clone https://github.com/MarkZither/bookstack-mcp-server-dotnet.git
cd bookstack-mcp-server-dotnet
cp .env.dev.example .env.dev
```

Generate a BookStack application key and add it to `.env.dev`:

```bash
docker run --rm --entrypoint /bin/bash lscr.io/linuxserver/bookstack:latest appkey
# outputs: base64:xxxxxxxx...
echo 'APP_KEY=base64:xxxxxxxx...' >> .env.dev   # paste the actual output
```

Open `.env.dev` and review the values. You can leave everything as-is for now — you will fill in
the BookStack API token in step 4.

### 2. Start the dev stack

```bash
docker compose -f docker-compose.dev.yml up -d
```

This starts MariaDB and then waits for it to be healthy before starting BookStack.
BookStack runs a database migration on first start which takes 60–120 seconds.

Watch the status:

```bash
docker compose -f docker-compose.dev.yml ps
```

Wait until both `bookstack-db` and `bookstack` show `healthy`.

### 3. Complete BookStack first-run setup

Open <http://localhost:6875> in your browser.

Default credentials:

- **Email**: `admin@admin.com`
- **Password**: `password`

Change the password immediately after logging in.

### 4. Generate a persistent API token

1. Click your avatar (top-right) → **Edit Profile**.
2. Scroll to **API Tokens** → **Add Token**.
3. Give the token a name (e.g., `dev`), leave expiry blank for a non-expiring token.
4. Copy the **Token ID** and **Token Secret**.
5. Open `.env.dev` and set:

   ```dotenv
   BOOKSTACK_TOKEN_ID=<your token id>
   BOOKSTACK_TOKEN_SECRET=<your token secret>
   ```

### 5. Restart the MCP server

```bash
docker compose -f docker-compose.dev.yml restart bookstack-mcp
```

### 6. Verify

```bash
curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer dev-token-replace-me" \
  http://localhost:3001/health
```

Expected output: `200`

> Replace `dev-token-replace-me` with the value of `BOOKSTACK_MCP_HTTP_AUTH_TOKEN` from
> your `.env.dev` if you changed it.

---

## F5 Debug Launch

The VS Code launch configuration runs the MCP server **natively** (not in a container) with the
.NET debugger attached. The pre-launch task automatically starts the BookStack dependency
containers.

1. Open the repository in VS Code (or VS Code with Remote WSL).
2. Ensure `.env.dev` is populated with a valid BookStack API token (see Quick Start steps 3–4).
3. Press **F5** (or open the Run and Debug panel and select **BookStack MCP Server (local dev)**).

VS Code will:

1. Run the `Start dev dependencies` task — starts `bookstack-db` and `bookstack` if not already
   running.
2. Build the project.
3. Launch `BookStack.Mcp.Server` with the .NET debugger attached.

Breakpoints in any C# file under `src/` will be hit normally.

> The launch configuration overrides `BOOKSTACK_BASE_URL` to `http://localhost:6875` so the
> native process reaches the Docker-exposed BookStack port, and forces
> `BOOKSTACK_MCP_TRANSPORT=http` so stdout stays clean for the debugger.

---

## VS Code Dev Container Setup

The `.devcontainer/devcontainer.json` attaches VS Code to the `bookstack-mcp` container using the
same `docker-compose.dev.yml`.

1. Install the **Dev Containers** extension (`ms-vscode-remote.remote-containers`).
2. Open the Command Palette → **Dev Containers: Reopen in Container**.
3. VS Code builds the MCP server image (if not cached) and starts the full stack.
4. The C# Dev Kit and C# extensions are installed automatically inside the container.
5. The repository root is mounted at `/workspace` so edits are immediately reflected on the host.

---

## Environment Variables Reference

All variables are defined in `.env.dev.example`. Copy to `.env.dev` and fill in real values.

| Variable | Default (example) | Description |
|---|---|---|
| `BOOKSTACK_BASE_URL` | `http://bookstack:6875` | BookStack URL from MCP server. Override to `http://localhost:6875` for F5 launch (done automatically by `launch.json`). |
| `BOOKSTACK_TOKEN_ID` | `replace-me` | BookStack API token ID (generated in BookStack UI). |
| `BOOKSTACK_TOKEN_SECRET` | `replace-me` | BookStack API token secret. |
| `BOOKSTACK_MCP_HTTP_AUTH_TOKEN` | `dev-token-replace-me` | Bearer token protecting the MCP HTTP endpoint. Replace with any random string. |
| `BOOKSTACK_MCP_HTTP_PORT` | `3001` | Port the MCP server listens on. Avoids collision with production Compose (port 3000). |
| `BOOKSTACK_ADMIN_PORT` | `0` | Port for the admin sidecar (VS Code extension backend). Set to `0` to disable; required for F5 dev launch to avoid port conflicts. |
| `DB_ROOT_PASSWORD` | `dev-root-password` | MariaDB root password. |
| `DB_DATABASE` | `bookstack` | BookStack database name. |
| `DB_USERNAME` | `bookstack` | MariaDB user for BookStack. |
| `DB_PASSWORD` | `bookstack-dev-password` | MariaDB user password. |
| `APP_URL` | `http://localhost:6875` | Public URL BookStack uses for generated links. |
| `OLLAMA_BASE_URL` | `http://ollama:11434` | Ollama endpoint. See [WSL2 → Windows Ollama Networking](#wsl2--windows-ollama-networking). |
| `VECTOR_SEARCH_ENABLED` | `false` | Set to `true` to enable semantic/vector search (requires Ollama). |

---

## WSL2 → Windows Ollama Networking

If you run Ollama on Windows (`ollama serve` with `OLLAMA_HOST=0.0.0.0`) and develop inside
WSL2, the Docker containers can reach the Windows host via the special DNS name `host-gateway`.

The `docker-compose.dev.yml` already adds `host-gateway` to the `bookstack-mcp` container's
`/etc/hosts`.

To use your Windows Ollama from the dev stack:

1. In `.env.dev`, set:

   ```dotenv
   OLLAMA_BASE_URL=http://host-gateway:11434
   ```

2. Restart the MCP server:

   ```bash
   docker compose -f docker-compose.dev.yml restart bookstack-mcp
   ```

3. Verify the connection from inside the container:

   ```bash
   docker compose -f docker-compose.dev.yml exec bookstack-mcp \
     curl -s http://host-gateway:11434/api/tags | head -c 200
   ```

> **Docker Engine version**: `host-gateway` requires Docker Engine 20.10 or later.
> If it does not resolve, fall back to the explicit Windows host IP:
>
> ```bash
> ip route show default | awk '/default/ {print $3}'
> ```
>
> Use that IP in place of `host-gateway` in `OLLAMA_BASE_URL`.

---

## Using the Ollama Profile

To run Ollama as a container instead of using a host installation:

```bash
docker compose -f docker-compose.dev.yml --profile ollama up -d
```

On first run, pull the embedding model:

```bash
docker compose -f docker-compose.dev.yml exec ollama ollama pull nomic-embed-text
```

The model is stored in the `dev_ollama_data` named volume and survives container restarts.

Enable vector search in `.env.dev`:

```dotenv
OLLAMA_BASE_URL=http://ollama:11434
VECTOR_SEARCH_ENABLED=true
```

Restart the MCP server:

```bash
docker compose -f docker-compose.dev.yml restart bookstack-mcp
```

---

## Pre-populating Data with the Seed Script

`scripts/Seed-BookStack.ps1` generates a complete book (chapters + pages) about any topic using
an Ollama LLM and creates it in BookStack via the REST API.

### Prerequisites

- The dev stack must be running with a valid API token in `.env.dev`.
- `pwsh` (PowerShell 7+) must be installed.
- Ollama must be accessible (either via the `ollama` profile or the host).

### Usage

```powershell
# Source your credentials from .env.dev first (bash/zsh):
export $(grep -v '^#' .env.dev | xargs)

# Generate a book about a topic:
pwsh scripts/Seed-BookStack.ps1 -Topic "Docker Networking"

# Preview without creating anything in BookStack:
pwsh scripts/Seed-BookStack.ps1 -Topic "Kubernetes" -DryRun

# Use a different model:
pwsh scripts/Seed-BookStack.ps1 -Topic "Rust Programming" -OllamaModel "mistral"

# Use Ollama on the Windows host from WSL2:
pwsh scripts/Seed-BookStack.ps1 -Topic "Medieval History" -OllamaBaseUrl "http://host-gateway:11434"
```

The script reads `BOOKSTACK_TOKEN_ID`, `BOOKSTACK_TOKEN_SECRET`, `BOOKSTACK_BASE_URL`, and
`OLLAMA_BASE_URL` from environment variables automatically, so if you source `.env.dev` first
you do not need to pass any parameters except `-Topic`.

---

## Common Tasks

| Task | Command |
|---|---|
| Start the full dev stack | `docker compose -f docker-compose.dev.yml up -d` |
| Start with embedded Ollama | `docker compose -f docker-compose.dev.yml --profile ollama up -d` |
| Stop the stack | `docker compose -f docker-compose.dev.yml down` |
| Stop and delete all dev volumes | `docker compose -f docker-compose.dev.yml down -v` |
| Check container health | `docker compose -f docker-compose.dev.yml ps` |
| Tail MCP server logs | `docker compose -f docker-compose.dev.yml logs -f bookstack-mcp` |
| Rebuild the MCP server image | `docker compose -f docker-compose.dev.yml build bookstack-mcp` |
| Open a DB shell | `docker compose -f docker-compose.dev.yml exec bookstack-db mysql -u bookstack -p bookstack` |
| Restart MCP server only | `docker compose -f docker-compose.dev.yml restart bookstack-mcp` |

---

## Troubleshooting

### BookStack does not become healthy

The linuxserver/bookstack image runs a database migration on first start that can take 60–120
seconds on slow hardware. Check the logs:

```bash
docker compose -f docker-compose.dev.yml logs bookstack
```

If the health check fails repeatedly, increase `start_period` for the `bookstack` service in
`docker-compose.dev.yml` (e.g., change `start_period: 90s` to `start_period: 180s`).

### Port conflicts

If port `6875` or `3001` is already in use:

```bash
# Check what is using the port:
ss -tlnp | grep 6875
```

Either stop the conflicting process or change the port mappings in `docker-compose.dev.yml`
and `BOOKSTACK_MCP_HTTP_PORT` in `.env.dev`.

### `host-gateway` does not resolve

Requires Docker Engine 20.10+. Check your version:

```bash
docker version --format '{{.Server.Version}}'
```

As a fallback, find the Windows host IP and use it directly:

```bash
ip route show default | awk '/default/ {print $3}'
# e.g., 172.23.96.1
```

Then set `OLLAMA_BASE_URL=http://172.23.96.1:11434` in `.env.dev`.

### MCP server exits immediately on F5

Check that `.env.dev` exists in the repository root and contains valid values for
`BOOKSTACK_TOKEN_ID` and `BOOKSTACK_TOKEN_SECRET`. The server exits at startup if required
configuration is missing.

### `.env.dev` variables not picked up

`.env.dev` is loaded by `docker-compose.dev.yml` via `env_file` and by `launch.json` via
`envFile`. It is **not** sourced into your shell automatically. If running the seed script or
ad-hoc `curl` commands, source it manually:

```bash
export $(grep -v '^#' .env.dev | xargs)
```

---

## Security

> **Warning**: The port bindings and `APP_URL` in `docker-compose.dev.yml` are intended for
> loopback access only (`localhost`). Do **not** expose this stack on a network interface
> accessible from outside your machine.

- `.env.dev` is listed in `.gitignore` (`*.env`). Never commit it.
- `.env.dev.example` contains only placeholder values — never real credentials.
- Replace `BOOKSTACK_MCP_HTTP_AUTH_TOKEN=dev-token-replace-me` with a random string before
  using the dev environment, even locally.
- The MariaDB port (`3306`) is not published to the host by default.
