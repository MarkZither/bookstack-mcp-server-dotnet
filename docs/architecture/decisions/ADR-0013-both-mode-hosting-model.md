# ADR-0013: Both-Mode Hosting — stdio as IHostedService inside WebApplication

**Status**: Accepted
**Date**: 2026-04-24
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

FEAT-0017 introduces a `both` value for `BOOKSTACK_MCP_TRANSPORT` that must simultaneously serve
stdio clients and HTTP clients from a single process. ADR-0009 established that `stdio` mode uses
`Host.CreateApplicationBuilder` and `http` mode uses `WebApplication.CreateBuilder`. These two
hosts are structurally different:

- `Host.CreateApplicationBuilder` produces a generic `IHost` with no HTTP pipeline; it starts the
  MCP stdio pump via `WithStdioServerTransport()` and runs until the process exits.
- `WebApplication.CreateBuilder` produces an `IWebHost` (backed by Kestrel) with a full HTTP
  middleware pipeline; `WithHttpTransport()` registers the Streamable HTTP handler.

For `both` mode a single startup path must activate both transports. Three strategies exist:

1. **Two independent hosts in parallel** — build and start an `IHost` (stdio) and a `WebApplication`
   (HTTP) concurrently, each with its own DI container and handler registrations. The two hosts run
   as concurrent `Task`s inside `Task.WhenAll`.
2. **stdio pump as an `IHostedService` inside `WebApplication`** — build only the `WebApplication`
   host; register a thin `IHostedService` that starts the MCP SDK's stdio transport. Both transports
   share the same DI container, so tool and resource handlers are registered once.
3. **HTTP server as an `IHostedService` inside the generic `IHost`** — build only the generic host;
   register ASP.NET Core's `GenericWebHostService` (internal) as a hosted service. This is the
   approach used by the `.NET Generic Host + Kestrel` pattern for non-web projects that want HTTP.

The stdio transport in the MCP SDK (`WithStdioServerTransport`) ultimately runs a background loop
that reads from `Console.OpenStandardInput()` and writes to `Console.OpenStandardOutput()`. For
`both` mode this loop must run alongside the HTTP Kestrel listener without blocking the host
shutdown pipeline.

An additional constraint: in `both` mode all diagnostic output (from both the stdio MCP channel
and from ASP.NET Core) must go to `stderr`; `stdout` carries MCP frames and must remain
unpolluted, which means ASP.NET Core's default console logger must be reconfigured with
`LogToStandardErrorThreshold = LogLevel.Trace` exactly as it is in `stdio` mode.

## Decision

We will implement `both` mode by building a single **`WebApplication`** host and registering the
MCP SDK's stdio transport handler as a background `IHostedService` within that host. Specifically:

1. A new class `StdioTransportHostedService : BackgroundService` is added to the server project.
   Its `ExecuteAsync` override calls the MCP SDK's stdio server run method (obtained via DI from
   the `IMcpServer` or equivalent SDK entry point) and awaits it until `stoppingToken` is cancelled.
2. `Program.cs` is updated so that the `both` branch:
   a. Builds a `WebApplication` with `WithHttpTransport()` (identical to the `http` branch).
   b. Configures `LogToStandardErrorThreshold = LogLevel.Trace` on the console logger
      (identical to the `stdio` branch) so stdout remains clean for MCP frames.
   c. Additionally registers `WithStdioServerTransport()` on the `IMcpServerBuilder` and
      registers `StdioTransportHostedService` as a hosted service.
   d. Adds `both` to the transport validation allow-list alongside `stdio` and `http`.
3. Tool and resource handlers are registered once via
   `WithToolsFromAssembly` / `WithResourcesFromAssembly` on the shared `IMcpServerBuilder`; both
   transports resolve handlers from the same DI container.
4. Graceful shutdown: when the `WebApplication` receives a shutdown signal (SIGTERM, Ctrl+C), the
   hosted service's `stoppingToken` is cancelled, which causes `StdioTransportHostedService` to
   complete and the host to shut down cleanly.

> **Note**: If the MCP SDK does not expose a direct "run stdio" entry point suitable for
> `BackgroundService`, the `StdioTransportHostedService` will instead start the stdio message loop
> by directly invoking `McpServerFactory.Create(...).RunAsync(stoppingToken)` or equivalent SDK
> surface. The exact API call is resolved during implementation against the SDK version in use.

## Rationale

- **Single DI container** is the primary driver: tool handlers, `IBookStackApiClient`, logging, and
  configuration are registered once. Two independent hosts would require duplicating all service
  registrations and risk subtle divergence.
- **`WebApplication` as the outer host** is the natural choice because it owns Kestrel and the
  HTTP pipeline. Embedding HTTP inside the generic host via `GenericWebHostService` is an internal
  ASP.NET Core implementation detail not intended for direct use; it is undocumented and subject to
  breaking changes.
- **`BackgroundService`** is the idiomatic .NET pattern for a long-running background loop within
  a hosted application. It integrates naturally with the host's `IHostApplicationLifetime` and
  cancellation token propagation.
- **Stdout cleanliness**: re-applying `LogToStandardErrorThreshold` in `both` mode ensures that
  ASP.NET Core's startup banner, routing logs, and Kestrel binding messages do not corrupt the
  stdio MCP channel.

## Alternatives Considered

### Option A: Two independent hosts in parallel (`Task.WhenAll`)

- **Pros**: cleanest separation — each host is configured exactly as it would be in single-transport
  mode; no cross-host concerns.
- **Cons**: all service registrations (BookStack client, MCP handlers) must be duplicated across
  two DI containers; configuration divergence is a maintenance risk; two hosts competing for the
  same `Console.In` / `Console.Out` / `Console.Error` streams requires careful coordination;
  shutdown sequencing across two hosts is complex.
- **Why rejected**: duplication of DI registration is the key deal-breaker. A single container is
  simpler and aligns with the existing single-binary strategy from ADR-0009.

### Option B: HTTP server as `IHostedService` inside generic `IHost`

- **Pros**: generic host is already used for stdio mode; extending it with an HTTP hosted service
  keeps the entry point symmetrical.
- **Cons**: `GenericWebHostService` is an internal ASP.NET Core class; using it directly is
  unsupported and fragile. The alternative — using `Microsoft.AspNetCore.Hosting.WebHostBuilder`
  manually — adds significant boilerplate and does not benefit from `WebApplication.CreateBuilder`
  convenience APIs (minimal API, route groups, CORS, health checks).
- **Why rejected**: reliance on internal types violates the project's stability requirements.
  `WebApplication.CreateBuilder` is the documented, supported entry point for HTTP workloads.

### Option C: stdio as `IHostedService` inside `WebApplication` (chosen)

- **Pros**: single DI container; `WebApplication` is the documented HTTP host; `BackgroundService`
  is the idiomatic long-running background worker pattern; shutdown integration is free via
  `IHostedService` cancellation.
- **Cons**: the MCP SDK's stdio transport API must be callable from a hosted service rather than
  being the top-level `IHost.RunAsync()` call; requires verifying the SDK surface during
  implementation.
- **Why chosen**: best balance of simplicity, correctness, and maintainability.

## Consequences

### Positive

- Tool and resource handlers, the `IBookStackApiClient`, and all shared services are registered
  once and used by both transports.
- `StdioTransportHostedService` is independently unit-testable.
- Graceful shutdown propagates uniformly through the standard `IHostedService` cancellation token.
- No internal ASP.NET Core types are used; the implementation is fully supported and stable.

### Negative / Trade-offs

- The `both` mode adds a branch in `Program.cs` and a new `StdioTransportHostedService` class,
  increasing the startup code surface.
- The exact MCP SDK API for invoking the stdio transport from a `BackgroundService` must be
  verified during implementation; the SDK may need to be consulted or extended if this pattern is
  not directly supported.
- In `both` mode, a crash in the stdio loop (e.g., the parent process closing stdin unexpectedly)
  will surface as an unhandled exception in the background service, which the host will log and
  may or may not re-raise depending on host configuration.

## Related ADRs

- [ADR-0009: Dual-Transport Entry-Point Strategy](ADR-0009-dual-transport-entry-point.md)
- [ADR-0012: HTTP Bearer Token Authentication Middleware Strategy](ADR-0012-http-bearer-token-auth-middleware.md)
