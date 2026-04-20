# ADR-0007: Server-Driven Rate Limiting via X-RateLimit Response Headers

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

BookStack API v25 enforces rate limits and communicates them via HTTP response headers:

- `X-RateLimit-Remaining` — number of requests remaining in the current window.
- `X-RateLimit-Reset` — UTC epoch timestamp at which the window resets.

When the client exhausts the limit, BookStack returns HTTP 429. The client must avoid sending requests after the limit is exhausted, or handle the delay gracefully.

Two broad strategies exist: **client-side proactive limiting** (predict the request rate and throttle before sending) or **server-driven reactive limiting** (read the server's own remaining-count header and delay when it reaches zero). The TypeScript reference implementation uses a client-side token bucket that refills at a configured rate, independent of server feedback.

## Decision

We will implement a **server-driven `RateLimitHandler`** (`DelegatingHandler`) that:

1. Reads `X-RateLimit-Remaining` and `X-RateLimit-Reset` from every HTTP response.
2. When `X-RateLimit-Remaining` is `0`, calculates the delay as `DateTimeOffset.UtcNow` until the epoch in `X-RateLimit-Reset`, then calls `Task.Delay(delay, cancellationToken)` before the next outbound request.
3. Uses a `SemaphoreSlim(1,1)` to ensure that concurrent requests pause correctly and do not race past the delay.

The handler stores the reset time in a `volatile` field (or via `Interlocked`) to avoid locking on the read path in the common case (remaining > 0).

## Rationale

The server is the authoritative source of the rate limit window. A client-side token bucket that is not calibrated to the server's actual window will either under-throttle (causing 429s) or over-throttle (reducing throughput unnecessarily). The server-driven approach responds to the actual remaining capacity signal and does not require configuration of requests-per-minute values that may differ across BookStack deployments.

The TypeScript reference implementation's token bucket is a reasonable fallback when the server does not expose rate-limit headers, but BookStack v25 explicitly exposes them, making a server-driven approach strictly more accurate.

Retry logic on 429 is explicitly out of scope per the spec; the handler prevents 429s proactively by delaying before `Remaining` reaches zero.

## Alternatives Considered

### Option A: Client-side token bucket (TypeScript reference pattern)

- **Pros**: Works even when the server does not provide rate-limit headers; predictable throughput.
- **Cons**: Must be configured with the server's actual limit (varies per BookStack deployment); can under-throttle if configured too high; ignores server feedback.
- **Why rejected**: Less accurate than server feedback for BookStack v25, which provides explicit `X-RateLimit-Remaining` and `X-RateLimit-Reset` headers.

### Option B: No client-side rate limiting; rely on 429 retry

- **Pros**: Zero implementation cost.
- **Cons**: Hammers the server until 429 is returned; retry logic is deferred, so 429s would surface as `BookStackApiException` to consumers.
- **Why rejected**: The spec explicitly requires the handler to self-throttle before the server returns 429. Retry logic is out of scope and would leave a gap in protection.

### Option C: Polly `RateLimiter` policy

- **Pros**: Production-grade; configurable sliding window, fixed window, or token bucket; built-in `IHttpClientFactory` integration via `AddResilienceHandler`.
- **Cons**: Adds a `Microsoft.Extensions.Http.Resilience` package dependency; its policies are client-side and do not read server-provided rate-limit headers out of the box.
- **Why rejected**: Polly's rate limiter is client-side; adapting it to consume `X-RateLimit-Reset` would require custom policy logic equivalent to writing the `RateLimitHandler` anyway. Deferred to a follow-up resilience ADR once retry/circuit-breaker requirements are defined.

## Consequences

### Positive

- The handler automatically adapts to any BookStack deployment's rate-limit configuration without client-side tuning.
- Proactive delay before exhaustion prevents 429 responses in the common case.
- `CancellationToken` propagation through `Task.Delay` ensures the delay is abandoned cleanly on server shutdown.

### Negative / Trade-offs

- The handler introduces a small per-response overhead to parse two header values on every response.
- Concurrent requests under a shared handler instance require synchronisation (via `SemaphoreSlim`) to avoid a race between reading `Remaining` and entering the delay; this adds complexity.
- If `X-RateLimit-Reset` is absent or malformed, the handler must degrade gracefully (log a warning; do not delay).

## Related ADRs

- [ADR-0005: IHttpClientFactory with Typed Client](ADR-0005-ihttpclientfactory-typed-client.md)
- [ADR-0008: Typed Exception for HTTP Errors](ADR-0008-bookstackapiexception.md)
