# ADR-0005: IHttpClientFactory with Typed Client for BookStack HTTP Client

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The `BookStackApiClient` must issue HTTP requests to a BookStack instance. .NET offers several patterns for managing `HttpClient` lifetime: direct instantiation, `IHttpClientFactory` with named clients, and `IHttpClientFactory` with typed clients. The choice governs handler pipeline composition, connection pooling behaviour, and testability.

Key constraints from the spec and coding guidelines:

- `DelegatingHandler` pipeline (`AuthenticationHandler` → `RateLimitHandler`) must be attached without leaking into consumer code.
- `SocketsHttpHandler` with `PooledConnectionLifetime` of two minutes must be used to prevent stale DNS entries.
- The coding guidelines mandate `IHttpClientFactory`; direct instantiation of `HttpClient` in business logic is forbidden.
- The client must be resolvable as `IBookStackApiClient` from the DI container so tool handlers depend on the interface.

## Decision

We will register `BookStackApiClient` as a **typed `HttpClient`** using `IHttpClientFactory` via `services.AddHttpClient<IBookStackApiClient, BookStackApiClient>()`. Handler pipeline members (`AuthenticationHandler`, `RateLimitHandler`) are attached via `AddHttpMessageHandler<T>()`. The primary handler is a `SocketsHttpHandler` with `PooledConnectionLifetime = TimeSpan.FromMinutes(2)`, configured via `ConfigurePrimaryHttpMessageHandler`.

A single `IServiceCollection` extension method `AddBookStackApiClient(IConfiguration)` encapsulates the full registration.

## Rationale

The typed client pattern binds the `HttpClient` lifetime to a concrete class and exposes it under an interface, which is exactly what `IBookStackApiClient` requires. Named clients return an untyped `HttpClient` and require callers to know the name string, coupling consumers to infrastructure details. The typed client eliminates that coupling and allows the handler pipeline to be declared once at registration time.

`IHttpClientFactory` automatically manages the underlying `SocketsHttpHandler` pool; setting `PooledConnectionLifetime` prevents stale DNS without requiring manual `HttpClient` disposal.

## Alternatives Considered

### Option A: Named `HttpClient` via `IHttpClientFactory`

- **Pros**: Flexible; multiple named clients can coexist for different base URLs.
- **Cons**: Callers must resolve by name string; interface abstraction is lost; DI cannot inject `IBookStackApiClient` directly.
- **Why rejected**: Consumers would depend on a string name rather than an interface, violating the DI convention and the interface contract requirement.

### Option B: Direct `HttpClient` instantiation

- **Pros**: Simplest code path; no DI wiring.
- **Cons**: `HttpClient` instances are not pooled; handler pipeline must be composed manually; explicitly forbidden by coding guidelines.
- **Why rejected**: Violates the coding guidelines' `IHttpClientFactory` mandate and produces socket exhaustion risks in long-running processes.

### Option C: `HttpClientFactory.Create()` (static factory, no DI)

- **Pros**: Usable outside DI.
- **Cons**: Bypasses `IHttpClientFactory` lifecycle management; incompatible with `AddHttpMessageHandler` pipeline; not testable via DI substitution.
- **Why rejected**: Incompatible with the handler pipeline requirements and DI-first design.

## Consequences

### Positive

- `IBookStackApiClient` is resolvable directly from the DI container; no service-locator pattern needed.
- `AuthenticationHandler` and `RateLimitHandler` are added once at registration and are invisible to consumers.
- Connection pooling with DNS refresh is handled automatically by `SocketsHttpHandler`.
- Tests can override the primary handler with a mock `HttpMessageHandler` without touching the DI wiring for `AuthenticationHandler` or `RateLimitHandler`.

### Negative / Trade-offs

- The typed client approach means `BookStackApiClient` is constructed with an `HttpClient` injected by the factory; the constructor signature is constrained to accept `HttpClient` as its first parameter.
- Misconfiguration of `PooledConnectionLifetime` (e.g., setting it too short) can cause connection churn under high load.

## Related ADRs

- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
- [ADR-0006: System.Text.Json with SnakeCaseLower Naming Policy](ADR-0006-systemtextjson-snakecase.md)
- [ADR-0007: Server-Driven Rate Limiting via X-RateLimit Headers](ADR-0007-ratelimit-header-strategy.md)
- [ADR-0008: Typed Exception for HTTP Errors](ADR-0008-bookstackapiexception.md)
