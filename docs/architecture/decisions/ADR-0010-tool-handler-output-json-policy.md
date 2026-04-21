# ADR-0010: Tool and Resource Handler JSON Output Naming Policy

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

MCP tool and resource handlers serialize their results as JSON strings that are returned to MCP
clients (LLM agents, Claude Desktop, etc.). The C# models use PascalCase properties
(`BookId`, `CreatedAt`), while BookStack's own REST API returns snake_case JSON (`book_id`,
`created_at`). When re-serializing deserialized models for MCP output, a naming policy must be
selected.

ADR-0006 already established `JsonNamingPolicy.SnakeCaseLower` for the `BookStackApiClient`'s
deserialization of BookStack HTTP responses. This ADR governs the **output** side: what naming
policy tool and resource handler methods use when serializing results back to MCP clients.

The naming policy must be consistent across all 23 tools and 8 resources in FEAT-0007 and all
future handler implementations.

## Decision

We will use **`JsonNamingPolicy.CamelCase`** for all tool and resource handler JSON output.

Each handler class declares a `private static readonly JsonSerializerOptions _jsonOptions`
field that is shared across all methods of that handler. No cross-handler singleton is introduced;
the field is local to each class.

```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false,
};
```

This field is the **only** serializer options instance used for output within a given handler
class. The API client's `JsonOptions` (SnakeCaseLower) are not exposed or reused in handler code.

## Rationale

The MCP protocol originated in the JavaScript/TypeScript ecosystem, and most MCP clients — including
Claude Desktop and the TypeScript MCP SDK — use camelCase JSON as the convention for tool
argument objects and results. Using camelCase output aligns with the expectations of the clients
that consume this server's tools.

`SnakeCaseLower` would produce output that mirrors BookStack's API format, which could be useful
for callers who already know the BookStack schema. However, it couples the MCP output format to
BookStack's implementation detail rather than the MCP ecosystem convention. If BookStack changes
a field name, callers would need to update their consumption logic regardless of the naming policy.

A shared cross-handler singleton was considered but rejected (see Alternatives) to avoid an
artificial shared-state dependency between otherwise stateless handler classes.

## Alternatives Considered

### Option A: `JsonNamingPolicy.SnakeCaseLower` (mirror BookStack format)

- **Pros**: Output format matches BookStack API documentation exactly; no mismatch for callers
  familiar with BookStack REST API field names (`book_id`, `created_at`, etc.).
- **Cons**: Diverges from the MCP/JSON-API camelCase convention; couples MCP output to BookStack
  naming conventions; inconsistent with the direction of most MCP implementations.
- **Why rejected**: CamelCase is more appropriate for the MCP ecosystem and is the convention used
  by the TypeScript reference implementation when it re-serializes API responses.

### Option B: Shared static singleton in a utility class

- **Pros**: Single instance allocation; avoids per-class field declarations.
- **Cons**: Introduces a shared-state dependency between otherwise stateless handler classes;
  any change to the shared options affects all handlers simultaneously; harder to override for
  a specific handler if needed in the future.
- **Why rejected**: The allocation cost of a `static readonly` field is negligible (one instance
  per class, created once at first use). Keeping the field local to each handler class preserves
  handler independence.

### Option C: Per-handler options with varying policies

- **Pros**: Maximum flexibility.
- **Cons**: Inconsistent output format across tools; breaks MCP client expectations.
- **Why rejected**: Consistency is more valuable than flexibility here; all tools must produce
  uniform output.

## Consequences

### Positive

- All 23 tools and 8 resources produce camelCase JSON output, consistent with MCP ecosystem
  conventions.
- Each handler class is self-contained; no dependency on a shared serialization utility.
- `WriteIndented = false` keeps response payloads compact.
- The policy is explicit and documented, preventing future contributors from accidentally using
  the API client's SnakeCaseLower options for handler output.

### Negative / Trade-offs

- Output field names (`bookId`, `createdAt`) differ from BookStack's own API field names
  (`book_id`, `created_at`). MCP clients that parse tool results expecting BookStack field names
  will need to use the camelCase variants.
- Each handler class repeats the `_jsonOptions` field declaration. This is intentional (see
  rejected Option B) but adds a small amount of boilerplate per class.

## Related ADRs

- [ADR-0006: System.Text.Json with SnakeCaseLower Naming Policy](ADR-0006-systemtextjson-snakecase.md)
- [ADR-0008: Typed Exception for HTTP Errors (BookStackApiException)](ADR-0008-bookstackapiexception.md)
