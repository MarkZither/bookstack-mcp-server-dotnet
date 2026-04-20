# ADR-0006: System.Text.Json with SnakeCaseLower Naming Policy

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

BookStack REST API v25 returns JSON responses with **snake_case** field names (e.g., `created_at`, `book_id`, `updated_at`). The C# response models must use PascalCase properties to follow language conventions. Bridging the two naming styles requires a serialization strategy.

Three main approaches exist: a JSON serializer with an automatic naming policy, per-property `[JsonPropertyName]` attributes, or a third-party serializer. The choice affects compile-time safety, maintenance cost, and binary size.

The project already targets .NET 10 and `System.Text.Json` is included in the runtime; no additional NuGet reference is needed.

## Decision

We will configure **`System.Text.Json`** with **`JsonNamingPolicy.SnakeCaseLower`** as the global naming policy for all BookStack API response deserialization. A shared `JsonSerializerOptions` singleton is created once in `BookStackApiClient` and reused for all calls.

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
};
```

No `[JsonPropertyName]` attributes are required on response model types unless a field name does not follow the snake_case pattern.

## Rationale

`JsonNamingPolicy.SnakeCaseLower` was introduced in .NET 8 and maps PascalCase C# property names to lowercase snake_case JSON field names automatically in both directions. This eliminates the need to annotate every property in every response model class, which would number in the hundreds across all BookStack entity types. The policy is bidirectional, so it works for both serialization (request bodies) and deserialization (response parsing) without separate configuration.

`System.Text.Json` is the preferred serializer in .NET 10 and requires no additional package reference, keeping the dependency footprint minimal.

## Alternatives Considered

### Option A: Per-property `[JsonPropertyName]` attributes

- **Pros**: Explicit mapping; no surprises if BookStack field names deviate from snake_case.
- **Cons**: Hundreds of attributes across all model classes; high maintenance cost when adding new endpoint groups; error-prone to omit; clutters model code.
- **Why rejected**: The maintenance burden is disproportionate given that BookStack consistently uses snake_case throughout its API v25 surface.

### Option B: Newtonsoft.Json with `SnakeCaseNamingStrategy`

- **Pros**: Mature library; well-known `SnakeCaseNamingStrategy` available.
- **Cons**: Additional NuGet dependency (`Newtonsoft.Json`); slower than `System.Text.Json` for .NET 10 workloads; Microsoft guidance recommends `System.Text.Json` for new code; increases binary size.
- **Why rejected**: No benefit over `System.Text.Json` for this use case; adds an unnecessary dependency.

### Option C: Source-generated `System.Text.Json` with explicit property names

- **Pros**: Maximum performance via compile-time code generation; no reflection at runtime.
- **Cons**: Requires explicit `[JsonSerializable(typeof(T))]` and `[JsonSourceGenerationOptions]` for every model; adds significant boilerplate; complicates partial-type declarations for generated code.
- **Why rejected**: The performance gain is marginal for an HTTP client making individual API calls; complexity of maintaining source generation context outweighs the benefit.

## Consequences

### Positive

- Response model classes contain only properties and no serialization attributes, keeping them clean and readable.
- Adding new endpoint groups requires only writing C# model properties in PascalCase — no attribute annotations needed.
- Reuses the built-in .NET 10 runtime serializer; no additional package reference.
- `PropertyNameCaseInsensitive = true` absorbs any minor casing inconsistencies in BookStack API responses.

### Negative / Trade-offs

- If a BookStack field name does not strictly follow snake_case (e.g., `HTMLContent`), an explicit `[JsonPropertyName]` attribute will be required on that property as an exception.
- The `JsonSerializerOptions` instance must be treated as a singleton; creating it per-call causes allocations and degrades GC performance.

## Related ADRs

- [ADR-0005: IHttpClientFactory with Typed Client](ADR-0005-ihttpclientfactory-typed-client.md)
