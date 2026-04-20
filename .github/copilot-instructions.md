# GitHub Copilot Instructions — bookstack-mcp-server-dotnet

## Project Overview

**bookstack-mcp-server-dotnet** is a Model Context Protocol (MCP) server implemented in .NET 10 / C# that provides full access to BookStack's knowledge management API. It is inspired by the TypeScript reference implementation at <https://github.com/pnocera/bookstack-mcp-server>.

The server exposes 47+ MCP tools and resources covering books, chapters, pages, shelves, users, roles, attachments, images, audit logs, recycle bin, permissions, and server administration.

## Tech Stack

- **Runtime**: .NET 10, C#
- **Protocol**: Model Context Protocol (MCP) — `stdio` and Streamable HTTP transports
- **API Client**: `HttpClient` with typed clients, `System.Text.Json`
- **Testing**: TUnit, Moq, FluentAssertions
- **Linting / formatting**: `dotnet format`, EditorConfig

## Project Structure

```
src/
  BookStack.Mcp.Server/                        # Main server project
    Server.cs                                  # MCP server entry point
    Program.cs
    api/                                       # BookStack API client
    config/                                    # Configuration manager
    resources/                                 # MCP resource handlers
    tools/                                     # MCP tool handlers
    utils/                                     # Shared utilities (errors, logger, rate-limit)
    validation/                                # Input validators
  BookStack.Mcp.Server.Data.Abstractions/      # EF Core interfaces & entity models
  BookStack.Mcp.Server.Data.SqlServer/         # SQL Server EF Core provider
  BookStack.Mcp.Server.Data.Postgres/          # PostgreSQL EF Core provider (pgvector)
  BookStack.Mcp.Server.Data.Sqlite/            # SQLite EF Core provider (dev/lightweight)
tests/
  BookStack.Mcp.Server.Tests/                  # TUnit test project
docs/
  architecture/decisions/                      # ADRs
  features/                                    # Feature specs
  migrations/                                  # Migration specs
  envisioning/                # Envisioning docs
```

## Code Conventions

- Follow `.github/docs/coding-guidelines.md` for all C# code.
- Use `async`/`await` throughout; avoid `.Result` or `.Wait()`.
- Validate all external inputs at system boundaries (API responses, tool arguments).
- Use structured logging (`ILogger<T>`) — never `Console.WriteLine` in production code.
- Keep tools and resources stateless; inject dependencies via constructor.
- Each tool handler lives in its own file under `tools/`.
- Each resource handler lives in its own file under `resources/`.

## Documentation

- Write specs using `docs/features/TEMPLATE.md`.
- Record architecture decisions using `docs/architecture/decisions/ADR-TEMPLATE.md`.
- Follow the style guide in `.github/instructions/documentation-style.instructions.md`.

## Agent Mode Instructions

- Use `@workspace` references when citing files.
- Prefer editing existing files over creating new ones.
- Do not add comments, docstrings, or type annotations to unchanged code.
- Security: follow OWASP Top 10; never log secrets or API tokens.
