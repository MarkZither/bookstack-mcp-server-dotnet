# Tasks: Local Admin HTTP Sidecar (FEAT-0055)

**Feature**: Local Admin HTTP Sidecar
**Spec**: [docs/features/local-admin-sidecar/spec.md](spec.md)
**Plan**: [docs/features/local-admin-sidecar/plan.md](plan.md)
**GitHub Issue**: [#80](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/80)
**Status**: Decomposed
**Date**: 2026-05-05

---

## Phase 1 — stdio mode migration to WebApplication

- [ ] Read `BOOKSTACK_ADMIN_PORT` env var at startup (default `5174`; `0` = disabled); add conditional `WebApplication` path for `stdio` transport when `adminPort > 0` in `src/BookStack.Mcp.Server/Program.cs`

## Phase 2 — Kestrel second-listener configuration

- [ ] Replace `app.RunAsync(url)` conditional with explicit `ConfigureKestrel` that adds `IPAddress.Loopback:{adminPort}` alongside the MCP HTTP listener in all http/both startup paths in `src/BookStack.Mcp.Server/Program.cs`
- [ ] Log `Information` when admin sidecar starts (port) and when it is disabled (`BOOKSTACK_ADMIN_PORT=0`) in `src/BookStack.Mcp.Server/Program.cs`

## Phase 3 — Admin service abstractions

- [ ] Create `IAdminTaskQueue`, `AdminTask`, `AdminTaskKind` in `src/BookStack.Mcp.Server/Admin/IAdminTaskQueue.cs` and `src/BookStack.Mcp.Server/Admin/AdminTask.cs`
- [ ] Implement `AdminTaskQueue` backed by `Channel<AdminTask>` (unbounded, single-reader) in `src/BookStack.Mcp.Server/Admin/AdminTaskQueue.cs`
- [ ] Implement `AdminIndexWorkerService : BackgroundService` that drains the queue and dispatches to `VectorIndexSyncService` in `src/BookStack.Mcp.Server/Admin/AdminIndexWorkerService.cs`
- [ ] Rename `VectorIndexSyncService.RunSyncCycleAsync` → `RunFullSyncAsync` (internal) and add `SyncPageByUrlAsync(string url, CancellationToken)` in `src/BookStack.Mcp.Server/` (VectorIndexSyncService)
- [ ] Register `IAdminTaskQueue` (singleton) and `AdminIndexWorkerService` (hosted service) in both the stdio `WebApplication` branch and the http/both branch of `Program.cs`

## Phase 4 — Admin endpoint handlers

- [ ] Create `AdminStatusResponse`, `AdminAcceptedResponse`, `AdminErrorResponse`, `IndexPageRequest` record types in `src/BookStack.Mcp.Server/Admin/AdminModels.cs`
- [ ] Implement `AdminHandlers.GetStatusAsync`, `PostSyncAsync`, `PostIndexAsync`, and `ValidatePageUrl` (SSRF guard — absolute URL, http/https only, host must match BookStack base URL) in `src/BookStack.Mcp.Server/Admin/AdminHandlers.cs`
- [ ] Map `/admin` route group with `.RequireHost($"127.0.0.1:{adminPort}")` and register the three handler delegates in `src/BookStack.Mcp.Server/Program.cs`

## Phase 5 — IVectorStore.GetTotalCountAsync

- [ ] Add `Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)` to `src/BookStack.Mcp.Server.Data.Abstractions/IVectorStore.cs`
- [ ] Implement `GetTotalCountAsync` via EF Core `CountAsync` in `src/BookStack.Mcp.Server.Data.Sqlite/SqliteVectorStore.cs`
- [ ] Implement `GetTotalCountAsync` via EF Core `CountAsync` in `src/BookStack.Mcp.Server.Data.Postgres/PostgresVectorStore.cs`
- [ ] Implement `GetTotalCountAsync` via EF Core `CountAsync` in `src/BookStack.Mcp.Server.Data.SqlServer/SqlServerVectorStore.cs`

## Phase 6 — Integration & unit tests

- [ ] Create `AdminSidecarTestFactory : WebApplicationFactory<Program>` with `http` transport, fixed test admin port, and in-memory `IVectorStore`/`IBookStackApiClient` stubs in `tests/BookStack.Mcp.Server.Tests/Admin/AdminSidecarTestFactory.cs`
- [ ] Add `AdminStatusEndpointTests`: `Status_Returns200_WithCorrectSchema`, `Status_NeverSynced_ReturnsNullLastSyncTimeAndZeroTotal` in `tests/BookStack.Mcp.Server.Tests/Admin/AdminStatusEndpointTests.cs`
- [ ] Add `AdminSyncEndpointTests`: `Sync_Returns202_WithAcceptedStatus` in `tests/BookStack.Mcp.Server.Tests/Admin/AdminSyncEndpointTests.cs`
- [ ] Add `AdminIndexEndpointTests`: `Index_WithValidUrl_Returns202`, `Index_WithInvalidUrl_NotAbsolute_Returns400`, `Index_WithSchemeFile_Returns400`, `Index_WithUrlFromDifferentHost_Returns400`, `Index_WithMissingUrlField_Returns400`, `Index_WithEmptyBody_Returns400` in `tests/BookStack.Mcp.Server.Tests/Admin/AdminIndexEndpointTests.cs`
- [ ] Add `AdminPortRoutingTests`: `AdminPort0_SidecarNotRegistered_Returns404`, `AdminPort0_InformationLogConfirmsDisabled`, `AdminPort_CustomPort_BindsCorrectly`, `AdminRoutes_NotAccessibleOnMcpPort` in `tests/BookStack.Mcp.Server.Tests/Admin/AdminPortRoutingTests.cs`
- [ ] Add `AdminHandlersUrlValidationTests` (unit): `ValidatePageUrl_NullUrl_ReturnsError`, `ValidatePageUrl_RelativeUrl_ReturnsError`, `ValidatePageUrl_FileScheme_ReturnsError`, `ValidatePageUrl_MatchingHost_ReturnsNull` in `tests/BookStack.Mcp.Server.Tests/Admin/AdminHandlersUrlValidationTests.cs`
