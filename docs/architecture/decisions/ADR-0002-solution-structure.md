# ADR-0002: Solution and Project Structure

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

Before any code can be written, the repository must have a stable directory layout, solution file, project naming convention, and namespace strategy. These choices affect every contributor and every future feature: changing the layout after multiple features are implemented is expensive and creates divergence between the file system and namespace declarations.

The project has three first-class concerns: a runtime server (`BookStack.Mcp.Server`), a pluggable data access layer (EF Core providers for SQL Server, PostgreSQL, and SQLite), and automated tests. The data layer follows the repository pattern with a shared abstractions project and per-provider implementations — mirroring the approach used in [DeepWikiOpenDotnet](https://github.com/MarkZither/DeepWikiOpenDotnet/tree/main/src). The structure must scale to 47+ tool handlers and multiple database providers without becoming a flat, unnavigable folder.

## Decision

We will use a **`src/` + `tests/` split** with a root-level solution file, where namespaces mirror directory paths exactly. All projects target `net10.0`. The layout is:

```
BookStack.Mcp.Server.sln                        # Solution file at repository root
src/
  BookStack.Mcp.Server/                         # Console application (MCP server entry point)
    BookStack.Mcp.Server.csproj
    Program.cs
    api/                                        # Namespace: BookStack.Mcp.Server.Api
    config/                                     # Namespace: BookStack.Mcp.Server.Config
    resources/                                  # Namespace: BookStack.Mcp.Server.Resources
    tools/                                      # Namespace: BookStack.Mcp.Server.Tools
    utils/                                      # Namespace: BookStack.Mcp.Server.Utils
    validation/                                 # Namespace: BookStack.Mcp.Server.Validation
  BookStack.Mcp.Server.Data.Abstractions/       # EF Core interfaces, models, repository contracts
    BookStack.Mcp.Server.Data.Abstractions.csproj
  BookStack.Mcp.Server.Data.SqlServer/          # EF Core SQL Server provider
    BookStack.Mcp.Server.Data.SqlServer.csproj
  BookStack.Mcp.Server.Data.Postgres/           # EF Core PostgreSQL provider (pgvector for vector search)
    BookStack.Mcp.Server.Data.Postgres.csproj
  BookStack.Mcp.Server.Data.Sqlite/             # EF Core SQLite provider (lightweight / dev)
    BookStack.Mcp.Server.Data.Sqlite.csproj
tests/
  BookStack.Mcp.Server.Tests/                   # TUnit test project (see ADR-0004)
    BookStack.Mcp.Server.Tests.csproj
```

One public type per file; filename matches the type name. Sub-namespaces use PascalCase matching the directory name.

The `Data.Abstractions` project defines repository interfaces and EF Core entity models shared by all providers. Each provider project (`Data.SqlServer`, `Data.Postgres`, `Data.Sqlite`) references `Data.Abstractions` and provides `DbContext`, migrations, and `IServiceCollection` extension methods. `BookStack.Mcp.Server` references `Data.Abstractions` only — the concrete provider is wired via DI at startup based on configuration.

## Rationale

- The `src/` + `tests/` split is the de facto convention for .NET OSS projects, making the repository immediately legible to any .NET contributor without explanation.
- Placing the solution file at the repository root means `dotnet build`, `dotnet test`, and `dotnet format` can all be run without specifying a path, which simplifies CI commands and contributor onboarding.
- Mirroring namespaces to directory paths (the `<RootNamespace>` default in .NET) eliminates the cognitive overhead of translating between file paths and fully qualified type names.
- Keeping test projects under `tests/` (not alongside source in `src/`) prevents test assemblies from being accidentally included in production builds and keeps the `src/` tree clean for static analysis tooling.
- The sub-directory structure (`api/`, `tools/`, `resources/`, etc.) matches the TypeScript reference implementation's layout, reducing friction for contributors familiar with the original project.
- The `Data.Abstractions` / per-provider split follows established .NET OSS practice (EF Core itself, Dapper, MediatR) and allows the Vector Search epic (Epic 3) to plug in pgvector via `Data.Postgres` without changing the server core.
- The pattern mirrors [DeepWikiOpenDotnet](https://github.com/MarkZither/DeepWikiOpenDotnet/tree/main/src) which the maintainer is already familiar with, reducing ramp-up time.

## Alternatives Considered

### Option A: Flat structure — all projects at repository root

- **Pros**: Fewer directories; simpler for very small projects.
- **Cons**: Does not scale to 47+ tool handler files; source and test code are interleaved; namespace roots diverge from directory structure; violates .NET OSS conventions.
- **Why rejected**: The project is planned to grow to 47+ tool handlers across multiple sub-namespaces; a flat structure would become unnavigable.

### Option B: Separate repository per concern (server + tests as sibling repos)

- **Pros**: Independent versioning of test scaffolding.
- **Cons**: Breaks the single-clone developer workflow; CI must coordinate across repositories; significantly higher operational overhead for an OSS project.
- **Why rejected**: Overcomplicated for a single logical project; no benefit at this scale.

### Option C: Multi-TFM project targeting both `net10.0` and `net8.0` LTS

- **Pros**: Wider runtime compatibility.
- **Cons**: `ModelContextProtocol` SDK targets `net9.0`/`net10.0`; supporting older TFMs would require conditional compilation or a separate compatibility shim; CI complexity doubles; the envisioning document explicitly targets .NET 10.
- **Why rejected**: Contradicts the stated goal of targeting cutting-edge .NET 10 and adds maintenance burden with no identified consumer need.

## Consequences

### Positive

- Any .NET contributor can run `dotnet build` and `dotnet test` at the repository root immediately after cloning.
- IDE project explorers (VS Code, Visual Studio, Rider) render the solution tree in a logical, navigable hierarchy.
- Adding new tool handlers requires only creating a new `.cs` file in `src/BookStack.Mcp.Server/tools/`; no project or solution changes are needed.
- `dotnet format` at the root applies consistently across all projects in the solution.
- The `BookStack.Mcp.Server` project is decoupled from any specific database provider; swapping or adding a provider is a DI configuration change only.
- SQLite provider enables zero-infrastructure local development and testing without SQL Server or PostgreSQL.

### Negative / Trade-offs

- Four data layer projects add solution complexity at startup; most of this complexity is deferred to the Vector Search epic (Epic 3).
- Solution file must be updated manually if new projects are added (e.g., a future benchmarks project); this is a one-time cost per new project.
- EF Core migrations live per-provider, meaning schema changes must be applied independently to each provider's `Migrations/` folder.

## Related ADRs

- [ADR-0001: MCP SDK Selection](ADR-0001-mcp-sdk-selection.md)
- [ADR-0003: CI/CD GitHub Actions Strategy](ADR-0003-cicd-github-actions.md)
- [ADR-0004: Test Framework Selection — TUnit](ADR-0004-test-framework.md)
