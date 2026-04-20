# ADR-0008: Typed Exception for HTTP Errors (BookStackApiException)

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The BookStack API HTTP client must communicate non-success HTTP responses (4xx, 5xx) to its callers (MCP tool and resource handlers). The coding guidelines recommend `Result<T>` or `OneOf` patterns for recoverable errors. However, the `BookStackApiClient` is an infrastructure-layer component analogous to `HttpClient` itself — it sits at the boundary between external HTTP and the application domain.

A decision is needed on whether HTTP errors should be surfaced as:

1. **Typed exceptions** (`BookStackApiException`) thrown on non-2xx responses.
2. **Result types** (`Result<T, BookStackApiError>`) returned from every method.
3. **Wrapped `HttpResponseMessage`** returned to callers for manual inspection.

## Decision

We will throw a **`BookStackApiException`** (extending `Exception`) for every non-2xx HTTP response. The exception exposes `int StatusCode`, `string? ErrorMessage`, and `string? ErrorCode` parsed from BookStack's JSON error envelope `{"error":{"message":"...","code":...}}`.

```csharp
public sealed class BookStackApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    public BookStackApiException(int statusCode, string? errorMessage, string? errorCode)
        : base($"BookStack API error {statusCode}: {errorMessage}")
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }
}
```

This matches the pattern established by `HttpRequestException` (which also throws on transport errors) and makes the interface of `IBookStackApiClient` clean: every method returns `T` on success or throws on failure. The `Result<T>` pattern is deferred to the tool handler layer, where business logic determines whether a `BookStackApiException` is recoverable and maps it to a structured MCP error response.

## Rationale

The `IBookStackApiClient` interface declares 47+ methods. Using `Result<T, BookStackApiError>` as the return type of each would require every caller to pattern-match or unwrap the result before using `T`, adding significant boilerplate across all tool handlers. The exception approach is consistent with how `HttpClient` and `EF Core` surface infrastructure failures — callers that care about specific error codes (e.g., 404 vs 403) can catch `BookStackApiException` and inspect `StatusCode`.

The coding guidelines' preference for `Result<T>` applies to domain-layer recoverable errors. An HTTP infrastructure client throwing exceptions on transport/protocol failures is the established .NET convention, not a violation of the guideline.

Tool handlers that wrap `IBookStackApiClient` calls will catch `BookStackApiException` and map it to an `McpError` (or return a typed error response), keeping the `Result<T>` convention at the domain boundary.

## Alternatives Considered

### Option A: `Result<T, BookStackApiError>` on every `IBookStackApiClient` method

- **Pros**: Forces callers to handle errors explicitly; aligns with coding guideline preference; composable with `OneOf`.
- **Cons**: All 47+ interface methods return `Result<T>` or similar; every caller must unwrap; significantly more boilerplate across all tool handlers; adds a library dependency (`OneOf` or custom `Result<T>`).
- **Why rejected**: The boilerplate cost across the entire tool handler layer outweighs the benefit for an infrastructure HTTP client. The exception pattern is the established .NET convention at this layer.

### Option B: Return raw `HttpResponseMessage` on non-2xx

- **Pros**: Zero loss of information; callers can inspect any header or body.
- **Cons**: Every caller is responsible for status-code inspection and error body parsing; error handling logic is duplicated across all tool handlers; `HttpResponseMessage` must be disposed correctly by callers.
- **Why rejected**: Pushes all error handling responsibility into consumers, creating duplication and divergence.

### Option C: `ProblemDetails` using `Microsoft.AspNetCore.Mvc.ProblemDetails`

- **Pros**: Standardised RFC 7807 format; well-known in ASP.NET ecosystem.
- **Cons**: BookStack returns its own `{"error":{"message":"...","code":...}}` envelope, not RFC 7807; parsing would require custom deserialization anyway; `ProblemDetails` is an ASP.NET model not designed for use in an HTTP client.
- **Why rejected**: Mismatch with BookStack's actual error format; unnecessary ASP.NET dependency.

## Consequences

### Positive

- `IBookStackApiClient` method signatures are clean: `Task<Book>`, `Task<ListResponse<Book>>`, etc.
- Error handling in tool handlers is opt-in: ignore if the error is truly unexpected, or catch `BookStackApiException` to map to a user-facing MCP error.
- `StatusCode`, `ErrorMessage`, and `ErrorCode` are available on the exception for structured logging and error mapping.
- `BookStackApiException.Message` must not include the `Authorization` header value (enforced by construction; the token is never available inside the client's response parsing path).

### Negative / Trade-offs

- Callers that forget to handle `BookStackApiException` will see unhandled exception behaviour rather than a compile-time reminder; this is partially mitigated by documentation and code reviews.
- Tool handlers that need to distinguish 404 from 403 must catch and inspect `StatusCode` in `catch` blocks, which is slightly more verbose than pattern-matching on a `Result<T>`.

## Related ADRs

- [ADR-0005: IHttpClientFactory with Typed Client](ADR-0005-ihttpclientfactory-typed-client.md)
- [ADR-0007: Server-Driven Rate Limiting via X-RateLimit Headers](ADR-0007-ratelimit-header-strategy.md)
