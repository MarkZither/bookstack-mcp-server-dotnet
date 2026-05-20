# ADR-0021: MarkZither.Rag.Chunking NuGet Package — Repository and Versioning Policy

**Status**: Accepted
**Date**: 2026-05-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

FEAT-0060 Phase 2 extracts token-aware sliding-window chunking logic into a reusable library
(`MarkZither.Rag.Chunking`) so that both `bookstack-mcp-server-dotnet` and `DeepWikiOpenDotnet`
can consume the same implementation.

The extraction creates a cross-repo versioning dependency: a breaking change in the package
propagates to at least two consumers. There is no established policy for where the package lives,
how it is versioned, whether it is signed, or how consumers pin and update the dependency.

Key constraints:

- The algorithm must match `DeepWiki.Rag.Core.Ingestion.DocumentIngestionService` (commit `8b5887b`)
  to allow `DeepWikiOpenDotnet` to replace its inline implementation with the package.
- The package must remain lightweight: no `HtmlAgilityPack`, uses `System.Text.RegularExpressions`
  for HTML stripping.
- Both `net9.0` and `net10.0` must be targeted; future TFM additions should be additive, not
  breaking.
- Both consumers are pre-v1 and have accepted that schema / API changes may occur with a minor
  version bump; the package versioning policy must reflect this.

## Decision

We will host the package in a dedicated repository at `github.com/MarkZither/rag-chunking-dotnet`
and publish it to NuGet.org as `MarkZither.Rag.Chunking`.

### Repository

- Dedicated repo `MarkZither/rag-chunking-dotnet`; not a sub-folder of either consumer repo.
- Reason: independent release cadence, clean dependency graph, no circular project references.

### Versioning

We will use **SemVer 2.0** with the following interpretation for pre-v1 packages:

| Change type | Version bump |
|---|---|
| New public API (additive) | minor (`0.x+1.0`) |
| Breaking public API change (rename, remove, signature change) | minor (`0.x+1.0`) |
| Bug fix, non-breaking implementation change | patch (`0.x.y+1`) |
| Targets new TFM additively (no TFM removed) | minor |
| Removes a TFM | major if ≥ v1; minor if pre-v1 |

Pre-v1 policy: while the package version is `0.*`, **minor bumps may include breaking changes**
and consumers must review the changelog before upgrading. This matches the policy applied to
`bookstack-mcp-server-dotnet` itself (milestone `0.5.0` for the composite-PK data-model change).

The first publish to NuGet.org will be `0.1.0`.

### Breaking Change Policy

A change is **breaking** if it:

- Renames, removes, or changes the signature of any `public` or `protected` type, method,
  property, or interface member in the `MarkZither.Rag.Chunking` namespace.
- Removes or renames a configuration key that maps to `ChunkOptions` or `TextChunk`.
- Changes chunking output in a way that alters the number or content of chunks produced by the
  same input under the same `ChunkOptions` (algorithm behaviour change).

Breaking changes must be:

1. Documented in `CHANGELOG.md` under the releasing version with a `BREAKING CHANGE:` prefix.
2. Accompanied by a migration note explaining what consumers must change.

### Package Signing

NuGet package signing is **deferred to v1.0.0**. Pre-v1 releases (`0.*`) will be published
unsigned to NuGet.org, which is permitted by NuGet.org policy. When signing is enabled it will
use the same code-signing certificate used for the VS Code extension VSIX (tracked in
[ADR-0018](ADR-0018-vsix-project-layout.md)).

### Consuming Projects — Pinning and Update Policy

| Consumer | How to pin |
|---|---|
| `bookstack-mcp-server-dotnet` | `<PackageReference Include="MarkZither.Rag.Chunking" Version="[0.1.0,0.2.0)" />` (minor-pinned) |
| `DeepWikiOpenDotnet` | Same pattern, reviewed independently |

- **Updating**: consumers MUST read the changelog before widening the version range.
- **Renovate / Dependabot**: version range updates MUST be treated as `minor` PRs and reviewed
  manually (not auto-merged) until the package reaches v1.
- A `Directory.Packages.props` entry (Central Package Management) MUST be used in
  `bookstack-mcp-server-dotnet` once the package is first consumed.

### Publish Target

NuGet.org (`https://api.nuget.org/v3/index.json`) is the sole publish target. No private feed
is used; the package has no proprietary dependencies.

## Rationale

- A dedicated repo gives the package its own issue tracker, CI, and release pipeline without
  coupling to either consumer's release schedule.
- SemVer minor-for-breaking under pre-v1 is consistent with SemVer spec §4 ("Major version zero
  is for initial development. Anything may change at any time.") and matches how both consumers
  are already versioned.
- Minor-pinned ranges (`[0.1.0, 0.2.0)`) protect consumers from surprise breaking changes while
  still receiving patch fixes automatically.
- NuGet.org is preferred over a private feed because both consumers are open-source and the
  package has no proprietary code; a private feed adds infrastructure cost for no benefit.

## Alternatives Considered

### Option A: Sub-folder / project reference within bookstack-mcp-server-dotnet

- **Pros**: no cross-repo coordination; simple `<ProjectReference>`.
- **Cons**: `DeepWikiOpenDotnet` cannot consume a project reference; defeats the sharing goal.
- **Why rejected**: does not satisfy Req 7–10 of the spec.

### Option B: Monorepo — add the package to an existing consumer repo

- **Pros**: single CI pipeline.
- **Cons**: ties the package release cadence to one consumer; the other consumer still needs a
  published package, so a NuGet publish step is required anyway.
- **Why rejected**: adds coupling without eliminating the packaging step.

### Option C: GitHub Packages (private NuGet feed)

- **Pros**: free for public repos, integrated with GitHub Actions.
- **Cons**: consumers must configure an authenticated feed even for a public package;
  worse discovery than NuGet.org.
- **Why rejected**: unnecessary friction for an open-source package.

### Option D: Float on `0.*` with no pinning (always latest)

- **Pros**: consumers always get the latest fixes without PR noise.
- **Cons**: a breaking minor bump silently breaks consumer builds on next restore.
- **Why rejected**: CI becomes unreliable; violates the principle that the build is always green.

## Consequences

### Positive

- Clear, documented contract for when a version bump is required.
- Consumers are protected from surprise breaking changes via minor-pinned ranges.
- Package is discoverable and consumable by the broader .NET community on NuGet.org.
- Dedicated repo means the package can be developed, reviewed, and released independently.

### Negative / Trade-offs

- Cross-repo coordination is required when breaking changes are needed (open issue in both
  consumer repos, update both before widening the version range).
- Maintaining a second repo adds surface area (CI, dependabot, releases).

## Related ADRs

- [ADR-0015: Vector Store Abstraction](ADR-0015-vector-store-abstraction.md)
- [ADR-0016: Embedding Provider Abstraction](ADR-0016-embedding-provider-abstraction.md)
- [ADR-0018: VSIX Project Layout and Signing](ADR-0018-vsix-project-layout.md)
- [ADR-0020: Vector Store Composite PK Migration Strategy](ADR-0020-vector-store-composite-pk-migration.md)
