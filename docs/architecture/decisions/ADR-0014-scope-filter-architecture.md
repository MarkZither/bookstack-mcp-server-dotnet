# ADR-0014: Scope Filter Architecture for Book/Shelf Scope Filtering

**Status**: Accepted
**Date**: 2026-04-25
**Author**: GitHub Copilot
**Deciders**: Engineering team

---

## Context

Feature FEAT-0054 introduces `BOOKSTACK_SCOPED_BOOKS` and `BOOKSTACK_SCOPED_SHELVES` environment
variables that restrict list and search results to a configured subset of books/shelves. Four
architectural decisions need to be made before implementation:

1. Where does filtering logic execute — in the MCP tool layer or passed to the BookStack API as
   query parameters?
2. How is the scope configuration (`ScopeFilterOptions`) registered in the DI container?
3. Where does the shared filter predicate live — inline in each handler or in a shared helper?
4. How are the comma-separated env var values parsed into a form that `IConfiguration` can bind to
   `IReadOnlyList<string>`?

These decisions must be consistent with existing patterns: `BookStackApiClientOptions` registered
via `IOptions<T>`, `MapBookStackEnvVars()` building an in-memory `IConfiguration` dictionary, and
primary-constructor DI injection in tool and resource handlers.

## Decision

**We will implement scope filtering as application-side post-fetch filtering in the MCP tool
layer, with `IOptions<ScopeFilterOptions>` DI registration, a shared static `ScopeFilter` helper,
and indexed `IConfiguration` key generation in `MapBookStackEnvVars()`.**

Specifically:

- `ScopeFilterOptions` is a new `sealed` class in `BookStack.Mcp.Server.Config`, registered with
  `services.Configure<ScopeFilterOptions>(configuration.GetSection("BookStack"))` inside
  `AddBookStackApiClient()`.
- `IOptions<ScopeFilterOptions>` is injected via primary constructor into `BookToolHandler`,
  `ShelfToolHandler`, `SearchToolHandler`, `BookResourceHandler`, and `ShelfResourceHandler` — the
  only five handlers affected by scope.
- A `static internal class ScopeFilter` in `src/BookStack.Mcp.Server/config/ScopeFilter.cs`
  provides a single `MatchesScope(int id, string slug, IReadOnlyList<string> scope)` method.
  Filtering is bypassed entirely when the scope list is empty.
- `MapBookStackEnvVars()` splits `BOOKSTACK_SCOPED_BOOKS` and `BOOKSTACK_SCOPED_SHELVES` on
  commas, trims whitespace, validates each entry against `^[a-zA-Z0-9_-]+$`, discards invalid
  entries with a logged warning, and writes the valid entries as indexed keys
  (`BookStack:ScopedBooks:0`, `BookStack:ScopedBooks:1`, …) so that `Configure<ScopeFilterOptions>`
  binds them directly as list elements.
- Slug comparison is case-insensitive (`StringComparison.OrdinalIgnoreCase`).
- `BookStack:ScopedBooks` and `BookStack:ScopedShelves` are the configuration section keys, so
  they bind to `ScopeFilterOptions.ScopedBooks` and `ScopeFilterOptions.ScopedShelves` via the
  existing `GetSection("BookStack")` binding.

## Rationale

### Filter placement: MCP tool layer, not BookStack API

BookStack's `/api/books` and `/api/shelves` endpoints have no filtering by shelf membership or
arbitrary ID list. The search endpoint accepts `{filter:book_id=N}` for a **single** book ID only;
supporting multiple IDs would require undocumented query construction that may silently fail.
Slug-to-ID resolution (required for API-side filtering) would add extra HTTP round-trips at query
time, increasing latency and coupling. Post-fetch filtering is simple, transparent, and consistent
across all five affected handlers.

The known trade-off — that `count`/`offset` pagination is applied by the API before scope
filtering, so filtered pages may contain fewer items than requested — is documented in the spec as
an acceptable limitation given expected scope sizes of 1–10 books per deployment.

### DI registration: `IOptions<ScopeFilterOptions>` via `Configure<T>`

This follows the existing pattern for `BookStackApiClientOptions` (registered in
`AddBookStackApiClient`, bound from `configuration.GetSection("BookStack")`). Using `IOptions<T>`
makes the options injectable with `Options.Create(new ScopeFilterOptions { ... })` in unit tests
with no running host, which is the pattern already used in the test suite. A `Singleton` manual
registration would require more boilerplate and would not integrate with the `IOptions` validation
pipeline if validation is added in the future.

### Filter logic: static helper, not service

`ScopeFilter.MatchesScope` is a pure function with no state and no I/O. Registering it as a DI
service would require an interface and a registration call for something that has no alternative
implementation and never needs to be mocked. A `static internal` class is simpler and easier to
test directly. The `ScopeFilter` class is co-located with `ScopeFilterOptions` in the `config/`
folder to indicate it is configuration-layer infrastructure.

### Env var parsing: indexed `IConfiguration` keys

`IConfiguration.GetSection("BookStack").Bind(options)` only maps a list from indexed keys
(`BookStack:ScopedBooks:0`, `BookStack:ScopedBooks:1`, …), not from a comma-separated string
value. Splitting and indexing in `MapBookStackEnvVars()` keeps the conversion in one place and
avoids a custom `IConfigurationProvider` or a post-`Bind` parsing step. This is the idiomatic
approach for populating `IReadOnlyList<string>` from environment variables in the existing
configuration architecture.

### Input validation: reject-and-warn

Scope entries are validated against `^[a-zA-Z0-9_-]+$` at parsing time in `MapBookStackEnvVars()`.
This is the character set for BookStack slugs (lowercase alphanumeric plus hyphen) and positive
integers. Rejecting entries that do not match this pattern prevents unexpected comparison behaviour
and satisfies the OWASP A03 (Injection) requirement in the spec. Invalid entries are discarded
with a `LogWarning` rather than a hard failure, so a misconfigured entry does not prevent the
server from starting.

## Alternatives Considered

### Option A: Pass scope IDs as BookStack API query parameters

- **Pros**: Server-side filtering; no data returned for out-of-scope items; accurate pagination.
- **Cons**: Only single `book_id` filter supported; slugs require extra API round-trips; complex
  query string construction for multiple IDs; behaviour may differ between BookStack versions.
- **Why rejected**: The BookStack API does not expose the required filtering surface.

### Option B: Dedicated `IScopeFilterService` DI service

- **Pros**: Mockable in tests; extensible for future caching or async resolution of slugs to IDs.
- **Cons**: No alternative implementation exists or is planned; injecting a no-op interface for a
  pure function adds indirection without value; violates YAGNI.
- **Why rejected**: `ScopeFilter` is a pure function with no testability advantage from a DI
  interface.

### Option C: Separate `AddScopeFilter()` extension method

- **Pros**: Explicit separation from API client registration.
- **Cons**: Requires a second DI call in `Program.cs`; all scope filtering is directly tied to the
  BookStack API client configuration — they belong in the same registration.
- **Why rejected**: Unnecessary split for a small co-dependent configuration.

### Option D: Parse comma-separated string in `ScopeFilterOptions` setter

- **Pros**: Keeps `MapBookStackEnvVars()` simple.
- **Cons**: Business logic in a configuration class; the class would need a special-cased binding
  path; `IReadOnlyList<string>` is not the right type for a string property.
- **Why rejected**: Non-idiomatic; violates single-responsibility for a configuration model.

## Consequences

### Positive

- No changes to `IBookStackApiClient`, `BookStackApiClient`, or any model.
- `ScopeFilter` is directly unit-testable without DI setup.
- Handler injection pattern is consistent across the five affected handlers.
- `IOptions<ScopeFilterOptions>` integrates with existing test helper pattern (`Options.Create`).
- Empty scope is a zero-cost path — the filter guard returns immediately, preserving the existing
  default behaviour exactly.

### Negative / Trade-offs

- Pagination accuracy: `count`/`offset` apply to the unfiltered API result; filtered pages may be
  smaller than requested. Documented in spec as a known limitation.
- `SearchResult` filtering is by book association only — items without a `Book` property (e.g.,
  shelf results in search) are excluded when `ScopedBooks` is set. Shelf-based search filtering is
  deferred.
- The five affected handlers each add one constructor parameter and two lines of filter code — a
  small but real increase in handler complexity.

## Related ADRs

- [ADR-0005: IHttpClientFactory Typed Client](ADR-0005-ihttpclientfactory-typed-client.md)
- [ADR-0009: Dual-Transport Entry-Point Strategy](ADR-0009-dual-transport-entry-point.md)
