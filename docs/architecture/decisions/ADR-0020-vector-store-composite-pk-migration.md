# ADR-0020: Vector Store Composite PK Migration Strategy

**Status**: Accepted
**Date**: 2026-05-19
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

FEAT-0060 Phase 2 changes vector indexing from one row per page to multiple chunks per page. The
current schema keys `page_vectors` by `page_id`, which cannot store multiple chunks for the same
page safely.

The migration must define one clear strategy across provider projects so implementation does not
split into incompatible paths. Current and near-term provider scope includes:

- Postgres provider in [src/BookStack.Mcp.Server.Data.Postgres](../../src/BookStack.Mcp.Server.Data.Postgres)
- SQLite provider in [src/BookStack.Mcp.Server.Data.Sqlite](../../src/BookStack.Mcp.Server.Data.Sqlite)
- SQL Server provider project in
  [src/BookStack.Mcp.Server.Data.SqlServer](../../src/BookStack.Mcp.Server.Data.SqlServer)

The project is pre-v1, and maintainers have accepted that this is a breaking schema change.
Downtime budgeting is not a design constraint for this phase.

## Decision

We will migrate `page_vectors` to a composite primary key `(page_id, chunk_index)` and add
`chunk_index` and `total_chunks` columns in all provider projects that own vector schema.

We will use a provider-native, destructive migration strategy for pre-v1 environments:

1. Create the new schema shape with composite PK and required columns.
2. Rebuild or reinitialize vector rows where required by the provider migration path.
3. Treat vector rows as rebuildable cache data; operational recovery is re-index, not row-preserving
   transformation.

Provider rules:

- Postgres: implement EF Core migration to add `chunk_index` and `total_chunks`, switch PK to
  `(page_id, chunk_index)`, and keep `chunk_index=0` and `total_chunks=1` as backward-compatible
  defaults for single-chunk indexing mode.
- SQLite: implement equivalent migration, including PK change to `(page_id, chunk_index)`.
- SQL Server: do not skip the provider project. If vector store support is active in this phase,
  implement an equivalent migration. If support is not yet active, add an explicit tracking step and
  migration placeholder so SQL Server cannot be forgotten when provider implementation is enabled.

Versioning rule:

- This migration family is considered a breaking data-model milestone and is planned to align with a
  package/application version bump to `0.5.0`.

## Rationale

- Composite PK is required to represent multiple chunks per page without duplicate-key conflicts.
- A destructive migration is simpler, lower risk, and acceptable pre-v1 because vector rows are
  derived from source content and can be regenerated.
- Enforcing provider parity in the ADR avoids a hidden backlog where SQL Server diverges from
  Postgres/SQLite assumptions.
- Explicit defaults (`chunk_index=0`, `total_chunks=1`) preserve behavior for `ChunkSize=0` fallback
  and legacy single-chunk paths.

## Alternatives Considered

### Option A: In-place data-preserving PK conversion for all providers

- **Pros**: no vector data reset after upgrade.
- **Cons**: high migration complexity across providers, especially around PK replacement and index
  rebuild ordering.
- **Why rejected**: unnecessary complexity for pre-v1 where re-index is acceptable.

### Option B: Keep `page_id` as PK and store chunks in a separate table

- **Pros**: avoids PK change in the existing table.
- **Cons**: adds schema complexity, query joins, and duplicated mapping logic across providers.
- **Why rejected**: adds permanent complexity for a temporary migration concern.

### Option C: Postgres and SQLite only, defer SQL Server indefinitely

- **Pros**: fastest short-term implementation.
- **Cons**: creates provider drift and increases future retrofit risk.
- **Why rejected**: maintainers explicitly require SQL Server project tracking to avoid omission.

## Consequences

### Positive

- Chunked indexing is unblocked with a clear, enforceable key strategy.
- Migration logic remains straightforward because vector rows are treated as rebuildable cache.
- Provider expectations are explicit, including SQL Server tracking.
- Future implementation tasks can reference one accepted migration contract.

### Negative / Trade-offs

- Existing vector rows may be dropped and re-indexed during upgrade.
- Consumers must accept a breaking schema change before v1.
- SQL Server may need placeholder work now even if full vector support is delivered later.

## Related ADRs

- [ADR-0015: Vector Store Abstraction](ADR-0015-vector-store-abstraction.md)
- [ADR-0016: Embedding Provider Abstraction](ADR-0016-embedding-provider-abstraction.md)
