# ADR-0019: Admin Sidecar Hosting — Always-WebApplication with Second Kestrel Listener

**Status**: Accepted
**Date**: 2026-05-05
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

FEAT-0055 introduces a local-only HTTP admin sidecar that must run in all three transport modes
(`stdio`, `http`, `both`). The sidecar binds exclusively to `127.0.0.1:{BOOKSTACK_ADMIN_PORT}`
(default `5174`) and exposes three REST endpoints: `GET /admin/status`, `POST /admin/sync`, and
`POST /admin/index`. When `BOOKSTACK_ADMIN_PORT=0`, the sidecar must not start.

The hosting challenge is that the current entry point (ADR-0009, ADR-0013) uses two structurally
distinct host types:

- **`stdio` mode**: `Host.CreateApplicationBuilder` → generic `IHost`, no HTTP pipeline.
- **`http` / `both` modes**: `WebApplication.CreateBuilder` → Kestrel-backed `IWebHost`.

ADR-0013 already migrated `both` mode away from a two-host model by running the stdio transport
alongside the HTTP transport within a single `WebApplication`. The admin sidecar creates the same
pressure on the `stdio` mode: it requires an HTTP listener that the generic `IHost` cannot provide
without significant workarounds.

Four options were evaluated for hosting the admin sidecar while satisfying the constraint that all
three transport modes support it.

---

## Decision

We will implement **Option 4**: migrate the `stdio` mode entry point to use `WebApplication`
when the admin sidecar is enabled (`adminPort > 0`), following the pattern already established
in ADR-0013 for `both` mode. Kestrel is configured with explicit listen addresses that vary by
transport mode:

| Transport | Kestrel listeners |
|-----------|-------------------|
| `stdio` (adminPort > 0) | `127.0.0.1:{adminPort}` only |
| `http` | `0.0.0.0:{mcpPort}` + `127.0.0.1:{adminPort}` |
| `both` | `0.0.0.0:{mcpPort}` + `127.0.0.1:{adminPort}` |
| any mode, adminPort = 0 | unchanged from current behaviour |

Explicit Kestrel `Listen` calls via `builder.WebHost.ConfigureKestrel(...)` replace the
`app.RunAsync(url)` URL-passing pattern for port assignment. Admin routes are registered under a
`MapGroup("/admin").RequireHost($"127.0.0.1:{adminPort}")` constraint so they are only reachable
on the admin listener.

**Exception**: when `BOOKSTACK_ADMIN_PORT=0` and `transport == "stdio"`, the existing
`Host.CreateApplicationBuilder` path is retained unchanged. Migrating to `WebApplication` solely
to configure Kestrel with zero listeners would introduce Kestrel startup warnings with no benefit;
the headless/CI path has no HTTP requirements.

---

## Rationale

- **ADR-0013 created the precedent**: `both` mode already uses `WebApplication` with
  `WithStdioServerTransport()` registered alongside `WithHttpTransport()` within the same host.
  Extending this to the `stdio` branch when admin is enabled is a natural, consistent progression
  rather than a new pattern.
- **Single DI container**: all three transport modes share one `WebApplication` host, so
  `IVectorStore`, `IBookStackApiClient`, `IAdminTaskQueue`, and tool handlers are registered once.
  Shared state (e.g., the in-memory `IAdminTaskQueue`) is accessible to both the admin pipeline
  and the background sync worker without cross-container bridging.
- **No nested hosts**: Option 2 (a `WebApplication` inside a `BackgroundService`) inverts the
  containment relationship. The inner application's DI container is isolated from the outer host's
  services unless the outer container is explicitly passed in, which is fragile and untestable.
- **No two parallel applications**: Option 1 requires duplicated service registrations and complex
  shutdown coordination across two independent hosts — the same design ADR-0013 rejected for
  `both` mode.
- **Supported APIs only**: `ConfigureKestrel` / `Listen` is the fully documented, stable API for
  multi-port binding in ASP.NET Core. No internal types are used.
- **`RequireHost` constraint**: minimal APIs' `RequireHost` ties an endpoint to a specific
  host:port combination, ensuring admin endpoints return `404` on the MCP listener even if path
  prefixes match — defence-in-depth beyond the TCP binding.

---

## Alternatives Considered

### Option 1: Independent `WebApplication` for admin, launched separately per mode

A minimal `WebApplication` is created exclusively for the admin sidecar and run in parallel with
the primary host via `Task.WhenAll`.

- **Pros**: complete isolation between admin and MCP pipelines; no change to existing transport
  entry points in `Program.cs`.
- **Cons**: two separate DI containers require duplicating `IVectorStore`, `IBookStackApiClient`,
  logging, and configuration registrations. Shutdown must be coordinated across two hosts.
  `Task.WhenAll` across two `RunAsync` calls is the same design ADR-0013 rejected for `both` mode
  (Option A: two independent hosts). Shared mutable state (e.g., `IAdminTaskQueue`) cannot be
  passed between containers without a service locator pattern.
- **Why rejected**: duplication of DI registration is the primary deal-breaker. A single DI
  container is the stated preference from ADR-0009 and ADR-0013.

### Option 2: Second `IHostedService` running ASP.NET Core inside the primary host

An `IHostedService` is registered in the primary `IHost` (or `WebApplication`); its
`ExecuteAsync` override creates and starts a second `WebApplication` for admin operations.

- **Pros**: does not require changing the `stdio` host type for the primary transport; admin is
  self-contained within the background service.
- **Cons**: a `WebApplication` running inside a `BackgroundService` is an undocumented and poorly
  supported pattern. The inner application's lifetime, DI scoping, and cancellation are not
  integrated with the outer host's `IHostApplicationLifetime`. Sharing services between inner and
  outer containers requires explicit factory delegation that is fragile and difficult to test in
  isolation. The inner application must be started and stopped manually, bypassing the standard
  hosted lifecycle.
- **Why rejected**: the resulting architecture is harder to reason about than a single
  `WebApplication` with multiple Kestrel listeners, and it does not align with any documented
  .NET hosting guidance.

### Option 3: Single `WebApplication` with multiple Kestrel listen addresses, routing by port

Configure Kestrel with two `Listen` calls on the same `WebApplication` and use `RequireHost` or
`context.Connection.LocalPort` middleware to route requests to admin vs. MCP handlers.

- **Pros**: clean single-host model; port-based routing via `RequireHost` is fully supported in
  minimal APIs.
- **Cons**: requires `stdio` mode to also use `WebApplication`. Without that migration, `stdio`
  mode still lacks a `WebApplication` host and this option cannot apply in all transport modes.
  This option describes only the multi-port routing mechanism, not the necessary hosting model
  change for the `stdio` path.
- **Why not chosen as a standalone option**: it is a subset of Option 4. The decision to migrate
  `stdio` to `WebApplication` when admin is enabled must be made explicitly. Option 3's routing
  mechanism is adopted as the implementation detail within Option 4.

### Option 4: Migrate stdio to `WebApplication` when admin is enabled (chosen)

When the admin sidecar is enabled (`adminPort > 0`), the `stdio` branch uses
`WebApplication.CreateBuilder` and registers the stdio MCP transport alongside Kestrel's admin
listener. When `adminPort == 0`, the existing `Host.CreateApplicationBuilder` path is retained.

- **Pros**: uniform `WebApplication` startup path when admin is active; single DI container;
  admin listener is a first-class Kestrel endpoint with no special hosting concerns; aligns
  directly with the pattern validated by ADR-0013 for `both` mode.
- **Cons**: the `adminPort == 0` exception leaves a residual conditional in `Program.cs`, keeping
  two host types in the codebase. The migration must be verified against the MCP SDK to confirm
  `WithStdioServerTransport()` functions correctly when Kestrel is also running but has no MCP
  HTTP routes mapped.
- **Why chosen**: best balance of simplicity, consistency, and maintainability. The `both` mode
  in ADR-0013 already validated that `WithStdioServerTransport()` and Kestrel can coexist within
  a single `WebApplication`, establishing confidence that the pattern is sound.

---

## Consequences

### Positive

- All modes share a single `WebApplication` host and DI container when admin is enabled.
- The admin sidecar's lifetime is managed by `IHostApplicationLifetime`; no custom shutdown
  coordination is required.
- `RequireHost` provides defence-in-depth: admin endpoints are unreachable on the MCP listener.
- `Program.cs` loses the `Host.CreateApplicationBuilder` branch for the common `stdio` + admin
  case, reducing startup code divergence across modes.
- Kestrel multi-port binding is straightforward to test via `WebApplicationFactory<Program>`.
- `IAdminTaskQueue` is a singleton in the shared DI container, accessible to both the HTTP handler
  and the background `AdminIndexWorkerService` without any cross-container plumbing.

### Negative / Trade-offs

- Kestrel starts in `stdio` mode when `adminPort > 0`, opening a TCP listener that was absent
  before. The `127.0.0.1` binding limits the attack surface to loopback only.
- A residual conditional remains in `Program.cs` for the `stdio` + `adminPort == 0` case,
  retaining the `Host.CreateApplicationBuilder` path. Full unification is deferred.
- The migration must be verified against the MCP SDK to confirm `WithStdioServerTransport()`
  functions correctly when Kestrel is also present. This is the known unknown identified during
  option analysis.
- When `ASPNETCORE_URLS` is set by an operator alongside explicit `ConfigureKestrel` `Listen`
  calls, both sets of addresses are active. Operational documentation must note this interaction.

---

## Implementation Note — VS Code Extension Hosting

During implementation of FEAT-0056 it became apparent that VS Code starts `McpStdioServerDefinition` processes **lazily** — only on the first chat interaction. Because the admin sidecar is embedded in the MCP process, this meant the status bar and WebviewPanel always showed "unreachable" until the user initiated a chat.

To resolve this without changing the .NET server design, the VS Code extension (`extension.ts`) now:

1. Spawns the binary as its **own eager child process** at extension activation, with `BOOKSTACK_ADMIN_PORT` set to the configured port. This process binds the admin listener immediately.
2. Passes `BOOKSTACK_ADMIN_PORT=0` to the `McpStdioServerDefinition` env vars so the lazily-started MCP process does **not** attempt to bind the same port.

This is an extension-side concern only; the .NET server hosting model described in this ADR is unchanged. The `stdio` + `adminPort > 0` path using `WebApplication` is still the correct choice — it is exercised by the eager child process, not by the VS Code MCP host process.

---

## Related ADRs

- [ADR-0009: Dual-Transport Entry-Point Strategy](ADR-0009-dual-transport-entry-point.md)
- [ADR-0013: Both-Mode Hosting Model](ADR-0013-both-mode-hosting-model.md)
- [ADR-0015: Vector Store Abstraction](ADR-0015-vector-store-abstraction.md)
