# Coding Guidelines — bookstack-mcp-server-dotnet

## Language and Runtime

- Target **.NET 10** and **C# 13** (or latest available with .NET 10).
- Enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in all project files.
- Use `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in CI builds.

## Naming Conventions

| Symbol | Convention | Example |
|---|---|---|
| Classes, records, enums | PascalCase | `BookApiClient` |
| Interfaces | `I` + PascalCase | `IBookApiClient` |
| Methods | PascalCase | `GetBooksAsync` |
| Properties | PascalCase | `BaseUrl` |
| Fields (private) | `_camelCase` | `_httpClient` |
| Constants | PascalCase | `DefaultPageSize` |
| Parameters, locals | camelCase | `bookId` |
| Async methods | suffix `Async` | `CreatePageAsync` |

## Async / Await

- All I/O operations **must** be `async`/`await`; never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Accept and forward `CancellationToken` in every public async method.
- Use `ConfigureAwait(false)` in library code (non-UI projects).

## Dependency Injection

- Register all services in `Program.cs` using the built-in `Microsoft.Extensions.DependencyInjection`.
- Prefer constructor injection; avoid service locator pattern.
- Scope: use `Singleton` for stateless services (API client, tools), `Transient` for validators.

## HTTP Client

- Use `IHttpClientFactory` or typed `HttpClient`; never instantiate `HttpClient` directly in business logic.
- Set `Timeout` and base address in DI registration.
- Handle `HttpRequestException` and surface structured `McpError` responses.

## Error Handling

- Define domain errors as `sealed record` types or a discriminated-union-style hierarchy.
- Never swallow exceptions silently; log and rethrow or convert to structured errors.
- Return `Result<T>` / `OneOf` patterns for recoverable errors; throw only for programming errors.

## Logging

- Use `ILogger<T>` throughout; never use `Console.WriteLine` in production code.
- Use structured logging with message templates: `_logger.LogInformation("Fetching book {BookId}", bookId)`.
- **Never log** API tokens, passwords, or other secrets — use `[Redacted]` placeholders.
- Log levels: `Debug` for verbose traces, `Information` for key operations, `Warning` for recoverable issues, `Error` for failures.

## Validation

- Validate all external inputs (tool arguments, API responses) at system boundaries.
- Use FluentValidation or hand-written validators in the `validation/` folder.
- Return descriptive validation errors; do not throw raw exceptions for user input issues.

## Testing

- Test project: `tests/BookStack.Mcp.Server.Tests/` using **xUnit**, **Moq**, and **FluentAssertions**.
- Naming: `MethodName_Scenario_ExpectedResult` (e.g., `GetBookAsync_ValidId_ReturnsBook`).
- Unit tests must not make real HTTP calls; use `MockHttpMessageHandler` or `Moq`.
- Aim for ≥ 80% line coverage on `tools/`, `api/`, and `validation/`.

## Security

- Follow OWASP Top 10 mitigations.
- Do not construct HTTP URLs from user input without validation/sanitization.
- Store configuration (base URL, token) via `IConfiguration` / environment variables; never hardcode.
- Sanitize error messages returned to MCP clients — do not leak stack traces.

## Code Organization

- One public type per file; filename matches the type name.
- Folders mirror namespaces: `BookStack.Mcp.Server.Tools`, `BookStack.Mcp.Server.Api`, etc.
- Keep tool handlers stateless: no mutable instance fields.
- Limit methods to ≤ 40 lines; extract helpers for longer logic.

## Formatting

- Use `dotnet format` (Roslyn formatter) with the `.editorconfig` at the repo root.
- 4-space indentation; no tabs.
- Trailing newline at end of every file.
- Braces on their own lines (Allman style) for classes and methods; same-line for single-line lambdas.
