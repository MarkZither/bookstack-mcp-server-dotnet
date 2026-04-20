# ADR-0004: Test Framework Selection — TUnit

**Status**: Accepted
**Date**: 2026-04-20
**Author**: GitHub Copilot
**Deciders**: bookstack-mcp-server-dotnet maintainers

---

## Context

The project requires a test framework for `BookStack.Mcp.Server.Tests`. The .NET testing ecosystem offers several mature options. The key criteria for this project are:

- Native `async`/`await` support (all server logic is async; tests must be able to `await` without workarounds)
- Parallel test execution out of the box (47+ tools means a large test surface; fast feedback loops matter)
- First-class IDE support in VS Code (via the .NET Test Explorer) and Visual Studio
- Active development on .NET 10
- Compatibility with FluentAssertions for readable assertions

## Decision

We will use **TUnit** as the test framework for all test projects.

TUnit replaces `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`. Tests are discovered by `TUnit.Engine` and run via `dotnet test`. **Moq** and **FluentAssertions** remain as companion libraries for mocking and assertions respectively.

Test project packages:

```xml
<ItemGroup>
  <PackageReference Include="TUnit" Version="1.*" />
  <PackageReference Include="Moq" Version="4.*" />
  <PackageReference Include="FluentAssertions" Version="7.*" />
</ItemGroup>
```

Test method shape:

```csharp
[Test]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    // Act
    // Assert — using TUnit built-ins or FluentAssertions
    await Assert.That(result).IsNotNull();
    result.Should().Be(expected);
}
```

## Rationale

- **Native async**: TUnit is designed from the ground up for `async`/`await`; every test is async by default. xUnit requires explicit `Task`-returning signatures and does not support `async void` safely. nUnit has similar limitations.
- **Parallel by default**: TUnit runs tests in parallel at the test-class and test-method level without opt-in configuration. This aligns with the project's cutting-edge stance and reduces CI time as the test suite grows.
- **Modern .NET 10 design**: TUnit targets current .NET runtimes and is actively developed alongside .NET 10. xUnit 2.x carries pre-.NET 6 design constraints; xUnit v3 is still stabilising.
- **IDE support**: TUnit integrates with the standard `dotnet test` CLI and VS Code Test Explorer via `Microsoft.Testing.Platform`, the same platform used by MSTest and xUnit v3.
- **FluentAssertions compatibility**: TUnit's built-in assertions (`Assert.That(…).IsTrue()`) and FluentAssertions (`.Should().Be(…)`) can be used side-by-side; contributors can choose the style that reads most naturally for a given test.

## Alternatives Considered

### Option A: xUnit 2.x

- **Pros**: Industry standard; extremely well-documented; massive community; Moq and FluentAssertions both target it.
- **Cons**: Pre-`async`-native design requires `Task`-returning signatures; no built-in parallelism across methods in the same class without `[Collection]` workarounds; xUnit 2.x is in maintenance mode.
- **Why rejected**: The project targets cutting-edge .NET 10; xUnit 2.x's async and parallelism limitations would create friction as the async-heavy server logic is tested.

### Option B: xUnit v3 (preview)

- **Pros**: Modern redesign; improved async support; `Microsoft.Testing.Platform` integration.
- **Cons**: Still in pre-release as of the project start date; API surface is unstable; some ecosystem tooling (coverage, mutation testing) lags behind.
- **Why rejected**: Too unstable for a project baseline; risks breaking changes mid-development.

### Option C: NUnit 4.x

- **Pros**: Mature; parallel test execution; good IDE support.
- **Cons**: Async support requires `[Timeout]` attribute for truly parallel async tests; boilerplate heavier than TUnit; less aligned with cutting-edge .NET 10 development style.
- **Why rejected**: TUnit provides cleaner async-native design with less boilerplate.

### Option D: MSTest v3

- **Pros**: First-party Microsoft support; `Microsoft.Testing.Platform` native.
- **Cons**: Historically verbose; attribute-heavy; less expressive than TUnit for unit tests.
- **Why rejected**: TUnit's expressiveness and async-first design are preferred for a greenfield project.

## Consequences

### Positive

- All tests are async by default — no impedance mismatch with the `async`/`await` server code under test.
- Parallel execution reduces CI feedback time as the test suite grows to cover 47+ tool handlers.
- `dotnet test` works unchanged in CI; no additional tooling required.
- TUnit's `Assert.That(…)` and FluentAssertions `.Should()` can coexist, giving contributors flexibility.

### Negative / Trade-offs

- TUnit is at `1.x` (reached stable 1.0 shortly after the project started). The `Version="1.*"` wildcard in the `csproj` picks the latest compatible minor version.
- `dotnet test` on .NET 10 SDK requires opting in to the Microsoft.Testing.Platform runner via `global.json`: `"test": { "runner": "Microsoft.Testing.Platform" }`. Without this, `dotnet test` falls back to the removed VSTest integration and errors. This is a one-time repo-level setting documented in `global.json` and ADR-0003.

## Related ADRs

- [ADR-0002: Solution and Project Structure](ADR-0002-solution-structure.md)
- [ADR-0003: CI/CD GitHub Actions Strategy](ADR-0003-cicd-github-actions.md)
