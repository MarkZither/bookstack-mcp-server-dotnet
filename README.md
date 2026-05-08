# bookstack-mcp-server-dotnet

[![CI](https://github.com/MarkZither/bookstack-mcp-server-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/MarkZither/bookstack-mcp-server-dotnet/actions/workflows/ci.yml)
[![VS Code Marketplace](https://img.shields.io/visual-studio-marketplace/v/MarkZither.bookstack-mcp-server.svg?label=VS%20Code%20Marketplace)](https://marketplace.visualstudio.com/items?itemName=MarkZither.bookstack-mcp-server)
[![Docker Pulls](https://img.shields.io/docker/pulls/markzither/bookstack-mcp-server.svg)](https://hub.docker.com/r/markzither/bookstack-mcp-server)
[![Docker Image Version](https://img.shields.io/docker/v/markzither/bookstack-mcp-server/latest?label=docker)](https://hub.docker.com/r/markzither/bookstack-mcp-server)

A [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server providing full access to BookStack's knowledge management API in .NET 10. Gives AI assistants (GitHub Copilot, Claude, and others) direct access to your BookStack instance.

Inspired by <https://github.com/pnocera/bookstack-mcp-server>.

---

## Installation

### VS Code Extension (recommended — no .NET required)

Install from the VS Code Marketplace — the server binary is bundled:

[![Install in VS Code](https://img.shields.io/badge/Install%20in%20VS%20Code-007ACC?logo=visualstudiocode&logoColor=white)](https://marketplace.visualstudio.com/items?itemName=MarkZither.bookstack-mcp-server)

Then set three settings (`Ctrl+,` → search `bookstack`):

| Setting | Value |
|---------|-------|
| `bookstack.url` | `https://your-bookstack/` |
| `bookstack.tokenId` | Your BookStack API token ID |
| `bookstack.tokenSecret` | Your BookStack API token secret |

See [vscode-extension/README.md](vscode-extension/README.md) for full configuration reference.

### Docker (Streamable HTTP transport)

Run the server as a container — ideal for self-hosted AI assistants, Claude Desktop, or any client that supports Streamable HTTP MCP.

```bash
docker run -d \
  -e BOOKSTACK_BASE_URL=https://your-bookstack/ \
  -e BOOKSTACK_TOKEN_SECRET=your-token-id:your-token-secret \
  -e BOOKSTACK_MCP_TRANSPORT=http \
  -e BOOKSTACK_MCP_HTTP_AUTH_TOKEN=change-me \
  -p 3000:3000 \
  ghcr.io/markzither/bookstack-mcp-server:latest
```

Health check: `GET http://localhost:3000/health`

MCP endpoint: `http://localhost:3000/mcp` (Bearer token required if `BOOKSTACK_MCP_HTTP_AUTH_TOKEN` is set)

#### Docker Compose

Sample compose files are provided for common configurations:

| File | Description |
|------|-------------|
| [`docker-compose.yml`](docker-compose.yml) | HTTP transport, no vector search (simplest) |
| [`docker-compose.sqlite.yml`](docker-compose.sqlite.yml) | SQLite vector search + Ollama embeddings |
| [`docker-compose.postgres.yml`](docker-compose.postgres.yml) | PostgreSQL + pgvector + Ollama (all in containers) |
| [`docker-compose.postgres-external.yml`](docker-compose.postgres-external.yml) | PostgreSQL external connection string + Ollama |

Copy and edit the relevant file, then:

```bash
cp docker-compose.yml docker-compose.override.yml
# edit docker-compose.override.yml with your values
docker compose up -d
```

#### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `BOOKSTACK_BASE_URL` | ✅ | Full URL of your BookStack instance, e.g. `https://demo.bookstackapp.com/` |
| `BOOKSTACK_TOKEN_SECRET` | ✅ | `tokenId:tokenSecret` from BookStack → Settings → API Tokens |
| `BOOKSTACK_MCP_TRANSPORT` | — | `stdio` (default) \| `http` \| `both` |
| `BOOKSTACK_MCP_HTTP_PORT` | — | HTTP listen port (default: `3000`) |
| `BOOKSTACK_MCP_HTTP_AUTH_TOKEN` | — | Bearer token for HTTP endpoint auth (recommended for HTTP transport) |
| `BOOKSTACK_SCOPED_BOOKS` | — | Comma-separated book IDs or slugs to restrict access |
| `BOOKSTACK_SCOPED_SHELVES` | — | Comma-separated shelf IDs or slugs to restrict access |
| `ConnectionStrings__VectorDb` | — | Connection string for vector database (default: `Data Source=bookstack-vectors.db`) |
| `VectorSearch__Enabled` | — | `true` to enable semantic search (default: `false`) |
| `VectorSearch__Database` | — | `Sqlite` (default) \| `Postgres` |
| `VectorSearch__EmbeddingProvider` | — | `Ollama` (default) \| `AzureOpenAI` |
| `VectorSearch__Ollama__BaseUrl` | — | Ollama base URL (default: `http://localhost:11434`) |
| `VectorSearch__Ollama__Model` | — | Ollama embedding model (default: `nomic-embed-text`) |
| `VectorSearch__AzureOpenAI__Endpoint` | — | Azure OpenAI endpoint (when using AzureOpenAI provider) |
| `VectorSearch__AzureOpenAI__DeploymentName` | — | Azure OpenAI deployment name |
| `VectorSearch__AzureOpenAI__ApiKey` | — | Azure OpenAI API key |
| `VectorSearch__Sync__IntervalHours` | — | How often to sync embeddings (default: `24`) |
| `VectorSearch__Sync__BatchSize` | — | Pages per sync batch (default: `50`) |

### Local Development (self-hosted BookStack + F5 debugging)

Run a full local stack — BookStack, MariaDB, and the MCP server — with no external dependencies.

**Prerequisites**: Docker Engine 20.10+, .NET 10 SDK, VS Code

```bash
git clone https://github.com/MarkZither/bookstack-mcp-server-dotnet.git
cd bookstack-mcp-server-dotnet

# Required: create your local env file (gitignored — never committed)
cp .env.dev.example .env.dev

# Required: generate a BookStack app key and append it to .env.dev
docker run --rm --entrypoint /bin/bash lscr.io/linuxserver/bookstack:latest appkey \
  | sed 's/^/APP_KEY=/' >> .env.dev
```

Then press **F5** in VS Code. The pre-launch task starts BookStack and MariaDB, waits until they are healthy, and then launches the MCP server with the debugger attached.

On first run, open <http://localhost:6875>, log in with `admin@admin.com` / `password`, generate an API token (Profile → API Tokens → Add Token), and paste the token ID and secret into `.env.dev`.

See [docs/developer-guide.md](docs/developer-guide.md) for the full setup walkthrough including Ollama integration and the data seed script.

### Build from Source

**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/MarkZither/bookstack-mcp-server-dotnet.git
cd bookstack-mcp-server-dotnet
dotnet build
dotnet test
```

---

## Available MCP Tools

### Content

| Category | Operations |
|----------|-----------|
| **Books** | list, create, read, update, delete, export |
| **Chapters** | list, create, read, update, delete |
| **Pages** | list, create, read, update, delete, export |
| **Shelves** | list, create, read, update, delete |

### Search

| Tool | Description |
|------|-------------|
| **Search** | Full-text search across all BookStack content |
| **Semantic Search** | Natural language vector search using AI embeddings (requires `VectorSearch__Enabled=true`) |

### Administration

| Category | Operations |
|----------|-----------|
| **Users** | list, create, read, update, delete |
| **Roles** | list, create, read, update, delete |
| **Attachments** | list, create, read, update, delete |
| **Images** | list, read, update, delete |
| **Permissions** | read and update content-level permissions |
| **Audit Logs** | list audit log entries |
| **Recycle Bin** | list, restore, and permanently delete items |
| **Server Info** | read BookStack server version and instance information |

---

## Vector Search

Semantic search lets AI assistants find pages by meaning rather than exact keywords. It is disabled by default and requires:

1. An embedding provider (Ollama with `nomic-embed-text`, or Azure OpenAI)
2. A vector database (SQLite with sqlite-vec, or PostgreSQL with pgvector)

The server syncs page content to the vector database on a configurable schedule (`VectorSearch__Sync__IntervalHours`).

See the [docker-compose.sqlite.yml](docker-compose.sqlite.yml) and [docker-compose.postgres.yml](docker-compose.postgres.yml) files for ready-to-run examples.

---

## Scoped Access

Restrict the MCP server to specific books or shelves to limit what the AI can see and modify:

```bash
# Via environment variable (comma-separated IDs or slugs)
BOOKSTACK_SCOPED_BOOKS=architecture-decisions,runbooks
BOOKSTACK_SCOPED_SHELVES=engineering
```

```json
// Via VS Code settings
{
  "bookstack.scopedBooks": ["architecture-decisions", "runbooks"],
  "bookstack.scopedShelves": ["engineering"]
}
```

---

## Transports

| Transport | Use case |
|-----------|----------|
| `stdio` | VS Code extension, Claude Desktop (default) |
| `http` | Docker / self-hosted, remote AI clients |
| `both` | Run both simultaneously |

HTTP transport exposes a Streamable HTTP MCP endpoint at `/mcp` and a health check at `/health`. Protect it with `BOOKSTACK_MCP_HTTP_AUTH_TOKEN` (Bearer token).

---

## Development

```bash
dotnet build
dotnet test --configuration Release
dotnet format --verify-no-changes
```

See [docs/](docs/) for architecture decisions, feature specs, and migration guides.

## Contributing

- [GitHub Issues](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues)
- Pull requests welcome — please open an issue first for significant changes.

