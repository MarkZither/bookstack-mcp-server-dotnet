# Tasks: Streamable HTTP Transport (FEAT-0017)

**Feature**: Streamable HTTP Transport
**Spec**: [docs/features/streamable-http-transport/spec.md](spec.md)
**Plan**: [docs/features/streamable-http-transport/plan.md](plan.md)
**GitHub Issue**: [#17](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/17)
**Status**: Decomposed
**Date**: 2026-04-24

---

## Phase 1 — Transport Validation

- [ ] Extend transport validation to accept `"both"` and improve error message in `src/BookStack.Mcp.Server/Program.cs` → [#68](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/68)

## Phase 2 — HTTP Middleware Pipeline

- [ ] Add CORS middleware and `GET /health` endpoint to HTTP branch in `src/BookStack.Mcp.Server/Program.cs` → [#69](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/69)
- [ ] Implement `HttpBearerAuthMiddleware` with `CryptographicOperations.FixedTimeEquals`, path bypass for `/health`, and startup warning in `src/BookStack.Mcp.Server/HttpBearerAuthMiddleware.cs` → [#70](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/70)

## Phase 3 — Both-Mode Hosting

- [ ] Implement `StdioTransportHostedService : BackgroundService` and `"both"` mode branch in `src/BookStack.Mcp.Server/StdioTransportHostedService.cs` → [#71](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/71)

## Phase 4 — Integration Tests

- [ ] [P] Add integration tests via `WebApplicationFactory<Program>` covering all 8 test cases in `tests/BookStack.Mcp.Server.Tests/Http/` → [#72](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/72)
