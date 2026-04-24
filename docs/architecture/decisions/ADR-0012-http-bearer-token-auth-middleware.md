# ADR-0012: HTTP Bearer Token Authentication Middleware Strategy

**Status**: Accepted
**Date**: 2026-04-24
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The Streamable HTTP transport (FEAT-0017) requires that the `POST /mcp` endpoint be protected by a
pre-shared Bearer token. Three mainstream ASP.NET Core mechanisms exist for validating an incoming
`Authorization: Bearer <token>` header:

1. **Full `AddAuthentication` / `AddAuthorization` pipeline** — registers an authentication scheme
   (e.g., a custom `IAuthenticationHandler`), uses `[Authorize]` attributes or policy-based
   authorization, and participates in the standard ASP.NET Core security middleware stack.
2. **Endpoint filter** — an `IEndpointFilter` or `AddEndpointFilter<T>()` attached directly to the
   `MapMcp()` endpoint that reads the header and short-circuits the request.
3. **Minimal custom `RequestDelegate` middleware** — a small inline `IApplicationBuilder.Use(...)`
   block (or a thin class implementing `IMiddleware`) inserted into the pipeline before `MapMcp()`.
   It reads the `Authorization` header, performs a constant-time comparison, and either short-circuits
   with 401 or calls `next`.

The `GET /health` endpoint must be exempt from authentication regardless of the chosen mechanism.
The auth token is a server-side secret: it must never appear in log output, error responses, or
diagnostic pages.

The MCP SDK's `app.MapMcp()` call registers its own route group; attaching middleware at the
application level is straightforward, but attaching policies to the route group requires SDK
knowledge that may change between versions.

## Decision

We will implement authentication as a **minimal custom `RequestDelegate` middleware** inserted into
the `IApplicationBuilder` pipeline via `app.Use(...)` before `app.MapMcp()`. The middleware will:

1. Bypass authentication for any request whose path does not start with `/mcp` (this covers
   `GET /health` and any future unauthenticated routes).
2. When `BOOKSTACK_MCP_HTTP_AUTH_TOKEN` is non-empty, read the `Authorization` header and extract
   the token after `Bearer `.
3. Compare the provided token to the configured token using
   `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals` (constant-time byte
   comparison) to prevent timing-attack token enumeration.
4. If the comparison fails or the header is absent, respond with HTTP 401 and a plain-text body
   `Unauthorized` and return without calling `next`.
5. When `BOOKSTACK_MCP_HTTP_AUTH_TOKEN` is empty or not set, log a `Warning`-level message at
   startup and allow all requests through without any token check.

The token is read once from configuration at startup and stored as a `ReadOnlyMemory<byte>` of its
UTF-8 encoding so that `FixedTimeEquals` can be called on the byte representation.

## Rationale

- **Minimal surface area**: the pre-shared token model is inherently simple — one secret, one
  comparison. Bringing in the full `AddAuthentication` stack (scheme handlers, claims transforms,
  `HttpContext.User`, policy evaluation) adds complexity that provides no value for a single-secret
  scenario.
- **No `[Authorize]` attribute coupling to the MCP SDK**: attaching authorization policies to
  `app.MapMcp()` requires using Minimal API route metadata APIs. These are SDK-version-sensitive;
  a pipeline-level middleware is decoupled from the SDK's internal route registration.
- **Endpoint filter alternative works but is less visible**: a filter is invoked after route
  matching, which means the framework still allocates endpoint metadata and executes route matching
  before the 401 is returned. Pipeline-level middleware exits earlier.
- **Constant-time comparison is mandatory**: the token is a secret credential. A naive
  `string.Equals` or `==` comparison leaks timing information proportional to the length of the
  common prefix. `CryptographicOperations.FixedTimeEquals` eliminates this side-channel.
- **Path-based bypass is simpler than policy exemptions**: exempting `/health` via
  `AllowAnonymous` metadata or a policy exception requires either touching route registration or
  knowing the SDK's metadata model. A path prefix check in the middleware is one line of code and
  does not depend on any external contract.

## Alternatives Considered

### Option A: Full `AddAuthentication` with a custom scheme handler

- **Pros**: integrates with `HttpContext.User`, `IAuthorizationService`, and the standard
  ASP.NET Core security model; enables future extension to OAuth2/OIDC without architectural change.
- **Cons**: requires implementing `IAuthenticationHandler` (three methods, `AuthenticateResult`,
  `ChallengeAsync`, `ForbidAsync`); adds `AddAuthentication`, `AddAuthorization`, and
  `UseAuthentication`, `UseAuthorization` middleware registrations; `[Authorize]` must be applied
  to the MCP route group (SDK coupling); significant complexity overhead for a single pre-shared
  secret.
- **Why rejected**: over-engineered for the requirement. OAuth2/OIDC is explicitly listed as a
  non-goal in the spec. The added abstraction provides no value today and complicates future readers.

### Option B: Endpoint filter on `MapMcp()`

- **Pros**: scoped directly to the MCP endpoint; no need for path-based bypass logic; filter API is
  idiomatic for Minimal API.
- **Cons**: route matching and endpoint resolution happen before the filter executes, which is
  unnecessary overhead for unauthenticated requests; `app.MapMcp()` returns a
  `RouteHandlerBuilder` — calling `AddEndpointFilter` on it may not be supported or may break if
  the SDK internally uses route groups rather than individual route handlers; creates SDK-version
  coupling.
- **Why rejected**: the SDK coupling risk and the uncertainty around `RouteHandlerBuilder` support
  make this option fragile. Pipeline-level middleware is more stable.

### Option C: Minimal custom `RequestDelegate` middleware (chosen)

- **Pros**: single responsibility, ~30 lines of code, path-agnostic bypass logic, no SDK coupling,
  constant-time comparison, easily unit-tested by constructing an `HttpContext` directly.
- **Cons**: must explicitly enumerate bypassed paths (or use a prefix); does not integrate with
  ASP.NET Core's claims/policy model (acceptable — we do not need claims).
- **Why chosen**: lowest complexity, highest stability, meets all security requirements.

## Consequences

### Positive

- Authentication logic is isolated in a single, easily auditable location.
- `FixedTimeEquals` eliminates timing-attack surface on the token comparison.
- No `[Authorize]` attributes or policy registrations; the middleware is self-contained.
- Future addition of a second unauthenticated route (e.g., `/metrics`) requires only a one-line
  path-bypass condition.
- Unit-testable without starting a full host: construct `DefaultHttpContext`, set headers, invoke
  the middleware delegate.

### Negative / Trade-offs

- The path-based bypass list is implicit in the middleware code; adding new unauthenticated
  routes requires a code change to the middleware condition.
- Does not populate `HttpContext.User`; if a future feature needs claims-based authorization it
  will require a migration to the full authentication stack.
- A developer unfamiliar with the codebase may not immediately recognise that authentication is
  handled here rather than via the standard `UseAuthentication()` / `UseAuthorization()` calls.

## Related ADRs

- [ADR-0009: Dual-Transport Entry-Point Strategy](ADR-0009-dual-transport-entry-point.md)
- [ADR-0013: Both-Mode Hosting — stdio as IHostedService inside WebApplication](ADR-0013-both-mode-hosting-model.md)
