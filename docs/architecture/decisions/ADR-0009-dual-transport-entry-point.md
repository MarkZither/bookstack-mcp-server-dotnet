# ADR-0009: Dual-Transport Entry-Point Strategy

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The MCP server must be reachable by two distinct classes of client:

1. **stdio clients** (Claude Desktop, CLI-based agents) — communicate over the process's standard
   input/output streams using MCP JSON-RPC framing. This is the primary transport for most MCP
   clients today.
2. **HTTP clients** (web agents, networked tools) — communicate over HTTP using the Streamable HTTP
   transport provided by `ModelContextProtocol.AspNetCore`.

A hosting strategy must be chosen: a single executable that selects its transport at runtime, or
separate executables for each transport. The choice affects project structure, build artefacts,
deployment model, and DI container composition.

The stdio transport imposes a hard constraint: **all diagnostic output — logs, startup messages,
unhandled exception details — must go to `stderr`**. Anything written to `stdout` is interpreted
as MCP JSON-RPC frames and corrupts the protocol channel.

## Decision

We will ship a **single executable** (`BookStack.Mcp.Server`) that selects its active transport at
startup from the `BOOKSTACK_MCP_TRANSPORT` environment variable (`stdio` | `http`, default `stdio`).

- **`stdio`**: build an `IHost` via `Host.CreateApplicationBuilder`, register
  `WithStdioServerTransport()`, and redirect all console logging to `stderr` by setting
  `LogToStandardErrorThreshold = LogLevel.Trace`.
- **`http`**: build a `WebApplication` via `WebApplication.CreateBuilder`, register
  `WithHttpTransport()`, and mount the MCP endpoint with `app.MapMcp()`. The listen port is
  controlled by `BOOKSTACK_MCP_HTTP_PORT` (default `3000`).

The `BookStack.Mcp.Server.csproj` SDK will be changed from `Microsoft.NET.Sdk` to
`Microsoft.NET.Sdk.Web` so that `WebApplication` and the `Microsoft.AspNetCore.App` framework
reference are available without an explicit `<FrameworkReference>` declaration.

Tool and resource handler classes are discovered at startup via
`WithToolsFromAssembly(Assembly.GetExecutingAssembly())` and
`WithResourcesFromAssembly(Assembly.GetExecutingAssembly())`, eliminating explicit per-class
registration calls.

## Rationale

- A single binary simplifies distribution: container images, PATH installations, and CI artefacts
  remain a single file regardless of the transport in use.
- The env-var selection pattern mirrors the TypeScript reference implementation and follows
  twelve-factor app practices for configuration.
- `Microsoft.NET.Sdk.Web` is the canonical SDK for projects that use `WebApplication`; using it
  avoids a manual `<FrameworkReference Include="Microsoft.AspNetCore.App" />` declaration and
  aligns with standard ASP.NET Core conventions.
- Both `Host.CreateApplicationBuilder` (stdio) and `WebApplication.CreateBuilder` (HTTP) integrate
  directly with the `ModelContextProtocol` SDK (ADR-0001); no adapter layer is required.
- Assembly scanning via `WithToolsFromAssembly` / `WithResourcesFromAssembly` means new handler
  classes decorated with `[McpServerToolType]` / `[McpServerResourceType]` are picked up
  automatically without touching `Program.cs`.

## Alternatives Considered

### Option A: Separate executables for each transport

- **Pros**: Each entry point uses the optimal host type with no conditional branching; the stdio
  binary excludes ASP.NET Core dependencies.
- **Cons**: Two build artefacts to version, package, and deploy; tool and resource registrations
  must be kept in sync between two projects; container image and CI configuration become more
  complex.
- **Why rejected**: Doubles deployment complexity without meaningful benefit. Sharing handlers
  across two executables contradicts the single-project structure in ADR-0002.

### Option B: HTTP-only transport (no stdio)

- **Pros**: Simplest host model — one `WebApplication.CreateBuilder` path, no branching.
- **Cons**: Incompatible with Claude Desktop and virtually all current MCP clients, which require
  stdio. Drops the primary use case entirely.
- **Why rejected**: stdio is the dominant transport for MCP clients today.

### Option C: Always use `WebApplication` for both transports

- **Pros**: Single host type, no conditional branching; `WebApplication` can host both transport
  modes via different middleware.
- **Cons**: The `StdioServerTransport` in `ModelContextProtocol` is designed for the Generic Host
  model; routing it through `WebApplication` introduces unnecessary ASP.NET Core request-pipeline
  overhead and risk of stdout pollution from middleware.
- **Why rejected**: The official SDK samples use `Host.CreateApplicationBuilder` for stdio
  explicitly to avoid this risk.

## Consequences

### Positive

- Single-binary distribution; the same artefact works for Claude Desktop (stdio) and server
  environments (HTTP) by changing one environment variable.
- All tool and resource handlers are registered once and shared by both transport branches.
- Assembly scanning removes the need to update `Program.cs` when new handler classes are added.
- No separate project or solution structure change required beyond the SDK switch.

### Negative / Trade-offs

- `BookStack.Mcp.Server.csproj` changes to `Microsoft.NET.Sdk.Web`, which includes the full
  ASP.NET Core framework reference in all builds, marginally increasing binary size even when
  the stdio transport is selected.
- `Program.cs` contains a runtime branch (`if transport == "stdio"`) that increases startup-path
  complexity slightly; mitigated by keeping each branch under ten lines.
- Assembly scanning is not Native AOT-compatible; if AOT publishing is required in the future,
  the registration must switch to explicit `WithTools<T>()` calls.

## Related ADRs

- [ADR-0001: MCP SDK Selection](ADR-0001-mcp-sdk-selection.md)
- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
