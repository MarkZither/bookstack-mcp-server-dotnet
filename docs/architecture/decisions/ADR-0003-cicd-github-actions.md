# ADR-0003: CI/CD GitHub Actions Strategy

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The project requires a CI pipeline that runs on every push to `main` and every pull request targeting `main`. The pipeline must enforce four quality gates in order: restore dependencies, build, run tests, and verify formatting. Two security-relevant choices must be made explicitly:

1. **SDK version pinning**: How the .NET 10 SDK version is locked to prevent silent upgrades that break builds or introduce behavioral changes.
2. **Action version pinning**: How third-party GitHub Actions (e.g., `actions/checkout`, `actions/setup-dotnet`) are referenced to prevent supply-chain substitution attacks.

The spec's security requirements explicitly mandate that all third-party Actions be pinned to a specific commit SHA.

## Decision

We will use **GitHub Actions on `ubuntu-latest`** with the following pinning strategy:

- **SDK version**: A `global.json` file at the repository root pins the .NET SDK to a specific `10.0.x` patch version with `"rollForward": "latestPatch"` to allow patch-level security updates without manual intervention.
- **Action versions**: All `uses:` references in workflow files are pinned to an immutable **commit SHA** (not a mutable tag like `v4`). A comment on the same line records the human-readable tag for maintainability.

The CI workflow (`.github/workflows/ci.yml`) runs a single job with four sequential steps that each fail fast:

```
restore → build --no-restore → test --no-build → format --verify-no-changes
```

Job-level permissions are scoped to `contents: read`. No secrets are required or referenced.

## Rationale

- `ubuntu-latest` provides the fastest cold-start time among GitHub-hosted runners and has the .NET 10 SDK available without additional setup overhead.
- `global.json` with `"rollForward": "latestPatch"` is the .NET team's recommended approach for reproducible builds: it locks the major/minor version (preventing surprise upgrades to .NET 11) while allowing security patch updates automatically.
- Pinning Actions to commit SHAs is the industry-standard mitigation for supply-chain attacks targeting CI pipelines (SLSA L3 / GitHub hardened runner guidance). A mutable tag like `v4` can be repointed by a compromised upstream maintainer; an immutable SHA cannot.
- `dotnet format --verify-no-changes` as the formatting gate is simpler and more reliable than a separate linting tool: it uses Roslyn's formatting engine, which is the same engine that applies fixes, so there is never a discrepancy between what the formatter produces and what the gate checks.
- Sequential steps with `--no-restore` / `--no-build` flags eliminate redundant work and produce precise failure messages: if the build fails, the test step never runs, so CI feedback is unambiguous.
- `contents: read` is the minimum permission needed for a read-only build job; no write permissions are granted, limiting blast radius if the workflow is compromised.

## Alternatives Considered

### Option A: Pin Actions to mutable version tags (e.g., `actions/checkout@v4`)

- **Pros**: Human-readable workflow files; simpler to update when a new major version is released.
- **Cons**: A compromised or malicious upstream maintainer can repoint `v4` to a different commit containing malicious code; this is a documented supply-chain attack vector.
- **Why rejected**: Explicitly prohibited by the spec's security requirements.

### Option B: Use a self-hosted runner

- **Pros**: Consistent hardware; ability to cache .NET SDK and NuGet packages persistently.
- **Cons**: Infrastructure maintenance burden; runner security (isolation between workflow runs) requires additional hardening; unnecessary complexity for an OSS project at this scale; the spec's build-time target (< 60 s on a standard runner) is achievable on GitHub-hosted runners.
- **Why rejected**: Operational overhead not justified at current scale; the project is open source and GitHub-hosted runners provide adequate isolation.

### Option C: Use `dotnet-version: '10.x'` floating version in `setup-dotnet` without `global.json`

- **Pros**: Zero extra files; always uses the latest .NET 10 patch automatically.
- **Cons**: `setup-dotnet` interprets `'10.x'` as "latest available on the runner image," which can differ between runner image updates; without `global.json`, `dotnet --version` may produce different results locally vs. in CI; reproducibility is reduced.
- **Why rejected**: `global.json` provides deterministic local/CI parity, which is more valuable than avoiding a single file.

### Option D: Add a separate lint job using a third-party C# linter (e.g., SonarQube, Roslyn Analyzers job)

- **Pros**: Richer static analysis beyond formatting.
- **Cons**: Additional CI complexity and potential costs; `dotnet format` already covers formatting, which is the only lint gate required by the spec at this stage; Roslyn Analyzers run as part of the build step anyway via `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- **Why rejected**: Out of scope for this scaffold feature; can be added as a separate CI enhancement later.

## Consequences

### Positive

- The pipeline is reproducible: the same SDK version and Action versions run in CI and locally.
- SHA pinning eliminates the supply-chain attack surface for all third-party Actions.
- `TreatWarningsAsErrors` in `csproj` combined with `dotnet format --verify-no-changes` creates two independent quality gates that together enforce both semantic correctness and stylistic consistency.
- The `global.json` `rollForward: latestPatch` policy means security patch updates to the .NET 10 SDK do not require a manual PR to update `global.json`.

### Negative / Trade-offs

- SHA-pinned Action references must be updated periodically (e.g., via Dependabot for GitHub Actions) to pick up security fixes in upstream Actions; this creates a small ongoing maintenance obligation.
- `global.json` must be updated manually when the project intentionally upgrades to a new .NET 10 SDK feature band (e.g., `10.0.200` → `10.0.300`); this is a low-frequency, intentional action.
- `ubuntu-latest` is a mutable image tag; if a runner image update breaks the build, the failure appears as an infrastructure issue rather than a code issue. Mitigated by the fact that `ubuntu-latest` updates are infrequent and well-announced.

## Related ADRs

- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
