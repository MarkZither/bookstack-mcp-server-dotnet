# Implementation Plan: .NET 10 Solution Scaffold & CI/CD

**Feature**: FEAT-0014
**Spec**: [docs/features/scaffold-and-cicd/spec.md](spec.md)
**GitHub Issue**: [#14](https://github.com/pnocera/bookstack-mcp-server-dotnet/issues/14)
**Status**: Ready for implementation

---

## Architecture Decisions

All ADRs for this feature have been accepted. Implementation must follow them without deviation.

| ADR | Title | Decision Summary |
| --- | --- | --- |
| [ADR-0001](../../architecture/decisions/ADR-0001-mcp-sdk-selection.md) | MCP SDK Selection | Use `ModelContextProtocol` NuGet package; no hand-rolled protocol layer |
| [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) | Solution and Project Structure | `src/` + `tests/` split; solution file at root; 4 data layer projects; namespaces mirror directories |
| [ADR-0003](../../architecture/decisions/ADR-0003-cicd-github-actions.md) | CI/CD GitHub Actions Strategy | `ubuntu-latest`; `global.json` SDK pin; all Actions pinned to commit SHA |
| [ADR-0004](../../architecture/decisions/ADR-0004-test-framework.md) | Test Framework Selection | TUnit — async-native, parallel by default; Moq + FluentAssertions as companions |

---

## Implementation Tasks

Tasks are ordered by dependency. Each task is independently committable.

### Task 1 — `global.json` SDK pin

Create `global.json` at the repository root pinning the .NET 10 SDK and opting in to the Microsoft.Testing.Platform runner.

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestPatch"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

The `"test": { "runner": "Microsoft.Testing.Platform" }` entry is required for `dotnet test` to use the MTP runner (TUnit) instead of the removed VSTest integration on .NET 10 SDK.

**Acceptance**: `dotnet --version` at the repository root outputs `10.0.1xx`; `dotnet test` runs without VSTest errors.

---

### Task 2 — `.editorconfig`

Create `.editorconfig` at the repository root encoding all rules from `.github/docs/coding-guidelines.md`.

Required rules (non-exhaustive — all must be present):

| Rule | Value |
| --- | --- |
| `indent_style` | `space` |
| `indent_size` | `4` |
| `end_of_line` | `lf` |
| `insert_final_newline` | `true` |
| `trim_trailing_whitespace` | `true` |
| `charset` | `utf-8` |
| `dotnet_style_namespace_declarations` | `file_scoped` |
| `csharp_new_line_before_open_brace` | `all` (Allman style) |
| `csharp_prefer_braces` | `true` |
| `dotnet_sort_system_directives_first` | `true` |
| `dotnet_separate_import_directive_groups` | `false` |

**Acceptance**: `dotnet format --verify-no-changes` passes on an empty solution.

---

### Task 3 — Solution file and project files

1. Create `BookStack.Mcp.Server.sln` at the repository root.
2. Create `src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj` as a `console` project targeting `net10.0`.
3. Create `src/BookStack.Mcp.Server.Data.Abstractions/BookStack.Mcp.Server.Data.Abstractions.csproj` as a `classlib` targeting `net10.0`.
4. Create `src/BookStack.Mcp.Server.Data.SqlServer/BookStack.Mcp.Server.Data.SqlServer.csproj` as a `classlib` targeting `net10.0`.
5. Create `src/BookStack.Mcp.Server.Data.Postgres/BookStack.Mcp.Server.Data.Postgres.csproj` as a `classlib` targeting `net10.0`.
6. Create `src/BookStack.Mcp.Server.Data.Sqlite/BookStack.Mcp.Server.Data.Sqlite.csproj` as a `classlib` targeting `net10.0`.
7. Create `tests/BookStack.Mcp.Server.Tests/BookStack.Mcp.Server.Tests.csproj` as a test project targeting `net10.0`.
8. Add all projects to the solution file.

**`BookStack.Mcp.Server.csproj` required properties**:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <RootNamespace>BookStack.Mcp.Server</RootNamespace>
</PropertyGroup>
```

**`BookStack.Mcp.Server.Data.Abstractions.csproj` required properties and packages**:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <RootNamespace>BookStack.Mcp.Server.Data.Abstractions</RootNamespace>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.*" />
</ItemGroup>
```

**`BookStack.Mcp.Server.Data.SqlServer.csproj` required properties and packages**:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <RootNamespace>BookStack.Mcp.Server.Data.SqlServer</RootNamespace>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.*" />
  <ProjectReference Include="..\BookStack.Mcp.Server.Data.Abstractions\BookStack.Mcp.Server.Data.Abstractions.csproj" />
</ItemGroup>
```

**`BookStack.Mcp.Server.Data.Postgres.csproj` required properties and packages**:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <RootNamespace>BookStack.Mcp.Server.Data.Postgres</RootNamespace>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
  <ProjectReference Include="..\BookStack.Mcp.Server.Data.Abstractions\BookStack.Mcp.Server.Data.Abstractions.csproj" />
</ItemGroup>
```

**`BookStack.Mcp.Server.Data.Sqlite.csproj` required properties and packages**:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <RootNamespace>BookStack.Mcp.Server.Data.Sqlite</RootNamespace>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.*" />
  <ProjectReference Include="..\BookStack.Mcp.Server.Data.Abstractions\BookStack.Mcp.Server.Data.Abstractions.csproj" />
</ItemGroup>
```

**`BookStack.Mcp.Server.Tests.csproj` required properties and packages**:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <IsPackable>false</IsPackable>
  <RootNamespace>BookStack.Mcp.Server.Tests</RootNamespace>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="TUnit" Version="1.*" />
  <PackageReference Include="Moq" Version="4.*" />
  <PackageReference Include="FluentAssertions" Version="7.*" />
  <ProjectReference Include="..\..\src\BookStack.Mcp.Server\BookStack.Mcp.Server.csproj" />
</ItemGroup>
```

**Acceptance**: `dotnet build` at the repository root produces zero errors and zero warnings.

---

### Task 4 — Stub source files

1. Create `src/BookStack.Mcp.Server/Program.cs` with a minimal entry point comment stub (no functional code).
2. Create `tests/BookStack.Mcp.Server.Tests/PlaceholderTest.cs` with a single `[Fact]` that asserts `true`.

**`PlaceholderTest.cs` shape** (TUnit — `[Test]`, async):

```csharp
namespace BookStack.Mcp.Server.Tests;

public sealed class PlaceholderTest
{
    [Test]
    public async Task Placeholder_AlwaysPasses()
    {
        await Assert.That(true).IsTrue();
    }
}
```

**Acceptance**: `dotnet test` at the repository root reports `1 passed, 0 failed`.

---

### Task 5 — GitHub Actions CI workflow

Create `.github/workflows/ci.yml`.

Requirements (per ADR-0003):
- Trigger: `push` to `main`; `pull_request` targeting `main`.
- Job permissions: `contents: read`.
- Runner: `ubuntu-latest`.
- All `uses:` references pinned to **commit SHA** with a human-readable tag comment.
- Steps in order: `checkout` → `setup-dotnet` (reads `global.json`) → `dotnet restore` → `dotnet build --no-restore --configuration Release` → `dotnet test --no-build --configuration Release` → `dotnet format --verify-no-changes`.

**Workflow skeleton**:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA>  # v4
      - uses: actions/setup-dotnet@<SHA>  # v4
        with:
          global-json-file: global.json
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release
      - run: dotnet format --verify-no-changes
```

> **Note**: Replace `<SHA>` placeholders with the current immutable commit SHA for `actions/checkout@v4` and `actions/setup-dotnet@v4` at the time of implementation. Verify the SHAs from the official action repositories before committing.

**Acceptance**: A push to `main` with the scaffold committed results in a green CI run with all four steps passing.

---

### Task 6 — README update

Update `README.md` to add:

1. A CI build badge immediately below the project title, linked to the GitHub Actions workflow URL.
2. A **Quickstart** section with the exact steps a contributor needs to clone, build, and test on a machine with only the .NET 10 SDK installed.

**Badge format**:

```markdown
[![CI](https://github.com/pnocera/bookstack-mcp-server-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/pnocera/bookstack-mcp-server-dotnet/actions/workflows/ci.yml)
```

**Quickstart content** (minimum):

```markdown
## Quickstart

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/pnocera/bookstack-mcp-server-dotnet.git
cd bookstack-mcp-server-dotnet
dotnet build
dotnet test
```
```

**Acceptance**: README renders the badge and quickstart section correctly on GitHub; badge reflects current CI state.

---

## Test Conventions

These are project-wide conventions, not implementation tasks. All future tests must follow them.

| Convention | Rule |
| --- | --- |
| Framework | TUnit 1.x (see [ADR-0004](../../architecture/decisions/ADR-0004-test-framework.md)) |
| Assertion library | TUnit built-ins (`Assert.That`) or FluentAssertions 7.x — both are acceptable |
| Mocking library | Moq 4.x |
| Test method naming | `MethodName_Scenario_ExpectedResult` |
| Test attribute | `[Test]` (not `[Fact]`); parameterised tests use `[Arguments]` |
| HTTP mocking | `MockHttpMessageHandler` or `Moq` — never real HTTP |
| Coverage target | ≥ 80% line coverage on `tools/`, `api/`, `validation/` |
| Test isolation | No shared mutable state between tests; use `ClassHook` / `AssemblyHook` for expensive setup |

---

## Engineering Practices

| Practice | Decision | Reference |
| --- | --- | --- |
| Branch strategy | Feature branches off `main`; PRs required to merge | Project convention |
| CI/CD | GitHub Actions on `ubuntu-latest`; restore → build → test → format | [ADR-0003](../../architecture/decisions/ADR-0003-cicd-github-actions.md) |
| SDK version | Pinned via `global.json` with `rollForward: latestPatch` | [ADR-0003](../../architecture/decisions/ADR-0003-cicd-github-actions.md) |
| Formatting | `dotnet format` enforced in CI via `--verify-no-changes` | [ADR-0003](../../architecture/decisions/ADR-0003-cicd-github-actions.md) |
| Code style | Allman braces; 4-space indent; file-scoped namespaces | `.editorconfig` + coding guidelines |
| Warnings | `TreatWarningsAsErrors=true` in all `csproj` files | Coding guidelines |

---

## Commands

Executable commands for this project (copy and run directly from the repository root):

### Build

```bash
dotnet build --configuration Release
```

### Tests

```bash
dotnet test --configuration Release --verbosity normal
```

### Lint / Formatting Check

```bash
dotnet format --verify-no-changes
```

### Apply Formatting

```bash
dotnet format
```

### Local Execution

```bash
dotnet run --project src/BookStack.Mcp.Server
```

---

## Acceptance Summary

All acceptance criteria from the spec must pass before the feature is considered complete:

- [ ] `dotnet build` from repo root: zero errors, zero warnings
- [ ] `dotnet test` from repo root: `1 passed, 0 failed`
- [ ] CI workflow passes all four stages on push to `main`
- [ ] `dotnet format --verify-no-changes` fails on a file with a formatting violation
- [ ] README displays a live CI badge
- [ ] New contributor following the quickstart succeeds with only the .NET 10 SDK installed
