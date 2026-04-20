# Implementation Plan: BookStack API v25 HTTP Client

**Feature**: FEAT-0018
**Spec**: [docs/features/bookstack-api-client/spec.md](spec.md)
**GitHub Issue**: [#18](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/18)
**Status**: Ready for implementation

---

## Architecture Decisions

All ADRs for this feature have been accepted. Implementation must follow them without deviation.

| ADR | Title | Decision Summary |
| --- | --- | --- |
| [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) | Solution and Project Structure | `src/` + `tests/` split; namespaces mirror directories |
| [ADR-0004](../../architecture/decisions/ADR-0004-test-framework.md) | Test Framework Selection | TUnit + Moq + FluentAssertions; mock `HttpMessageHandler` for HTTP tests |
| [ADR-0005](../../architecture/decisions/ADR-0005-ihttpclientfactory-typed-client.md) | IHttpClientFactory with Typed Client | `AddHttpClient<IBookStackApiClient, BookStackApiClient>()` with delegating handler pipeline |
| [ADR-0006](../../architecture/decisions/ADR-0006-systemtextjson-snakecase.md) | System.Text.Json SnakeCaseLower | `JsonNamingPolicy.SnakeCaseLower`; no `[JsonPropertyName]` attributes on models |
| [ADR-0007](../../architecture/decisions/ADR-0007-ratelimit-header-strategy.md) | Server-Driven Rate Limiting | `RateLimitHandler` reads `X-RateLimit-Remaining`/`X-RateLimit-Reset` headers |
| [ADR-0008](../../architecture/decisions/ADR-0008-bookstackapiexception.md) | Typed Exception for HTTP Errors | `BookStackApiException` thrown on non-2xx; `Result<T>` pattern deferred to tool handler layer |

---

## Component Design

### Handler Pipeline

```
IBookStackApiClient (interface)
    └── BookStackApiClient (typed HttpClient consumer)
            └── AuthenticationHandler (injects Authorization header)
                    └── RateLimitHandler (delays on X-RateLimit-Remaining = 0)
                            └── SocketsHttpHandler / MockHttpMessageHandler (in tests)
```

### Key Types

| Type | Namespace | Purpose |
| --- | --- | --- |
| `IBookStackApiClient` | `BookStack.Mcp.Server.Api` | Public interface — 47+ typed async methods |
| `BookStackApiClient` | `BookStack.Mcp.Server.Api` | Typed `HttpClient` wrapper; implements interface |
| `AuthenticationHandler` | `BookStack.Mcp.Server.Api` | `DelegatingHandler`; injects `Authorization: Token id:secret` |
| `RateLimitHandler` | `BookStack.Mcp.Server.Api` | `DelegatingHandler`; delays on `X-RateLimit-Remaining = 0` |
| `BookStackApiException` | `BookStack.Mcp.Server.Api` | Typed exception with `StatusCode`, `ErrorMessage`, `ErrorCode` |
| `BookStackApiClientOptions` | `BookStack.Mcp.Server.Config` | `IOptions<T>` POCO bound from `IConfiguration` section `"BookStack"` |
| `ListResponse<T>` | `BookStack.Mcp.Server.Api.Models` | Paginated envelope: `Total`, `From`, `To`, `Data` |
| `ExportFormat` | `BookStack.Mcp.Server.Api.Models` | Enum: `Html`, `Pdf`, `Plaintext`, `Markdown` |
| `ContentType` | `BookStack.Mcp.Server.Api.Models` | Enum: `Book`, `Chapter`, `Page`, `Bookshelf` |
| Response model classes | `BookStack.Mcp.Server.Api.Models` | `Book`, `Chapter`, `Page`, `Bookshelf`, `User`, `Role`, `Attachment`, `Image`, `SearchResult`, `RecycleBinItem`, `ContentPermissions`, `AuditLogEntry`, `SystemInfo`, and `*WithContents` variants |

### File Layout

```
src/BookStack.Mcp.Server/
  api/
    IBookStackApiClient.cs            ← Interface (all endpoint groups)
    BookStackApiClient.cs             ← Implementation (constructor + shared helpers)
    BookStackApiClient.Books.cs       ← Partial: books endpoints
    BookStackApiClient.Chapters.cs    ← Partial: chapters endpoints
    BookStackApiClient.Pages.cs       ← Partial: pages endpoints
    BookStackApiClient.Shelves.cs     ← Partial: shelves endpoints
    BookStackApiClient.Users.cs       ← Partial: users endpoints
    BookStackApiClient.Roles.cs       ← Partial: roles endpoints
    BookStackApiClient.Attachments.cs ← Partial: attachments endpoints
    BookStackApiClient.Images.cs      ← Partial: image gallery endpoints
    BookStackApiClient.Search.cs      ← Partial: search endpoint
    BookStackApiClient.RecycleBin.cs  ← Partial: recycle bin endpoints
    BookStackApiClient.Permissions.cs ← Partial: content permissions endpoints
    BookStackApiClient.AuditLog.cs    ← Partial: audit log endpoint
    BookStackApiClient.System.cs      ← Partial: system info endpoint
    AuthenticationHandler.cs          ← DelegatingHandler: auth header injection
    RateLimitHandler.cs               ← DelegatingHandler: rate limit delay
    BookStackApiException.cs          ← Typed exception
    BookStackServiceCollectionExtensions.cs  ← AddBookStackApiClient extension
    models/
      ListResponse.cs
      ExportFormat.cs
      ContentType.cs
      Book.cs
      Chapter.cs
      Page.cs
      Bookshelf.cs
      User.cs
      Role.cs
      Attachment.cs
      Image.cs
      SearchResult.cs
      RecycleBinItem.cs
      ContentPermissions.cs
      AuditLogEntry.cs
      SystemInfo.cs
  config/
    BookStackApiClientOptions.cs      ← IOptions<T> POCO

tests/BookStack.Mcp.Server.Tests/
  api/
    Helpers/
      MockHttpMessageHandler.cs       ← Reusable handler stub for TUnit tests
    BookStackApiClientBooksTests.cs
    BookStackApiClientChaptersTests.cs
    BookStackApiClientPagesTests.cs
    BookStackApiClientShelvesTests.cs
    BookStackApiClientUsersTests.cs
    BookStackApiClientRolesTests.cs
    BookStackApiClientAttachmentsTests.cs
    BookStackApiClientImagesTests.cs
    BookStackApiClientSearchTests.cs
    BookStackApiClientRecycleBinTests.cs
    BookStackApiClientPermissionsTests.cs
    BookStackApiClientAuditLogTests.cs
    BookStackApiClientSystemTests.cs
    AuthenticationHandlerTests.cs
    RateLimitHandlerTests.cs
    BookStackApiExceptionTests.cs
    BookStackApiClientDiTests.cs
```

> **Note**: `BookStackApiClient` is split into partial classes by endpoint group to keep each
> file under 40 lines of meaningful logic, per the coding guidelines.

### DI Registration

```csharp
// BookStackServiceCollectionExtensions.cs
public static IServiceCollection AddBookStackApiClient(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.Configure<BookStackApiClientOptions>(
        configuration.GetSection("BookStack"));

    services.AddTransient<AuthenticationHandler>();
    services.AddTransient<RateLimitHandler>();

    services.AddHttpClient<IBookStackApiClient, BookStackApiClient>()
            .AddHttpMessageHandler<AuthenticationHandler>()
            .AddHttpMessageHandler<RateLimitHandler>()
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                });

    return services;
}
```

### Configuration Binding

`BookStackApiClientOptions` is bound from `IConfiguration` section `"BookStack"`:

| Property | Config key | Env variable | Required | Default |
| --- | --- | --- | --- | --- |
| `BaseUrl` | `BookStack:BaseUrl` | `BOOKSTACK_BASE_URL` | No | `http://localhost:8080` |
| `TokenId` | `BookStack:TokenId` | `BOOKSTACK_TOKEN_ID` | Yes | — |
| `TokenSecret` | `BookStack:TokenSecret` | `BOOKSTACK_TOKEN_SECRET` | Yes | — |
| `TimeoutSeconds` | `BookStack:TimeoutSeconds` | `BOOKSTACK_TIMEOUT_SECONDS` | No | `30` |

`BaseUrl` is validated at startup via `IValidateOptions<BookStackApiClientOptions>` to be a well-formed HTTP or HTTPS URI; an `InvalidOperationException` is thrown if it is empty or malformed.

### Test Strategy

All tests use a `MockHttpMessageHandler` helper that captures the outbound `HttpRequestMessage`
and returns a pre-configured `HttpResponseMessage`. No real network connection is needed.

```csharp
// MockHttpMessageHandler helper shape
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public HttpResponseMessage Response { get; init; } = new(HttpStatusCode.OK);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(Response);
    }
}
```

Tests build an `HttpClient` with the mock as the primary handler and pass it directly to
`BookStackApiClient` (bypassing `IHttpClientFactory`), or use a `TestServer` for DI tests.

Test naming convention (per ADR-0004): `MethodName_Scenario_ExpectedResult`.

---

## Implementation Tasks

Tasks are ordered by dependency. Each task is independently committable.

### Task 1 — `BookStackApiClientOptions` configuration POCO

Create `src/BookStack.Mcp.Server/config/BookStackApiClientOptions.cs`.

Required properties: `BaseUrl` (default `"http://localhost:8080"`), `TokenId`, `TokenSecret`,
`TimeoutSeconds` (default `30`). The class must be a plain `public sealed class` with public
setters so `IConfiguration.Bind` can populate it.

Also create `BookStackApiClientOptionsValidator` implementing
`IValidateOptions<BookStackApiClientOptions>` that validates `BaseUrl` is a well-formed HTTP
or HTTPS URI and that `TokenId` and `TokenSecret` are non-empty.

**Acceptance**: `IOptions<BookStackApiClientOptions>` resolves correctly from a test
`IConfiguration` built from an in-memory dictionary; validation throws
`OptionsValidationException` when `BaseUrl` is empty or `TokenId` is blank.

---

### Task 2 — `BookStackApiException` typed exception

Create `src/BookStack.Mcp.Server/api/BookStackApiException.cs`.

The class must extend `Exception`, be `sealed`, and expose:

```csharp
public int StatusCode { get; }
public string? ErrorMessage { get; }
public string? ErrorCode { get; }
```

The `Message` property (inherited from `Exception`) must be
`$"BookStack API error {statusCode}: {errorMessage}"`. The `Authorization` header value must
never appear in `Message`, `Data`, or any inner exception.

**Acceptance**: Constructor sets all properties; `Message` matches expected format; no
serialization attributes needed.

---

### Task 3 — Shared response envelope and enum types

Create under `src/BookStack.Mcp.Server/api/models/`:

- `ListResponse.cs` — generic `ListResponse<T>` with `Total`, `From`, `To`,
  `IReadOnlyList<T> Data`.
- `ExportFormat.cs` — enum `ExportFormat { Html, Pdf, Plaintext, Markdown }`.
- `ContentType.cs` — enum `ContentType { Book, Chapter, Page, Bookshelf }`.

These types are referenced by the interface and all endpoint implementations.

**Acceptance**: Types compile; `ListResponse<Book>` can be deserialized from a JSON string
matching BookStack's paginated envelope format using the configured `JsonSerializerOptions`.

---

### Task 4 — Response model types

Create one file per entity under `src/BookStack.Mcp.Server/api/models/`:

| File | Types |
| --- | --- |
| `Book.cs` | `Book`, `BookWithContents` |
| `Chapter.cs` | `Chapter`, `ChapterWithPages` |
| `Page.cs` | `Page`, `PageWithContent` |
| `Bookshelf.cs` | `Bookshelf`, `BookshelfWithBooks` |
| `User.cs` | `User`, `UserWithRoles` |
| `Role.cs` | `Role`, `RoleWithPermissions` |
| `Attachment.cs` | `Attachment` |
| `Image.cs` | `Image` |
| `SearchResult.cs` | `SearchResult`, `SearchResultItem` |
| `RecycleBinItem.cs` | `RecycleBinItem` |
| `ContentPermissions.cs` | `ContentPermissions`, `ContentPermissionEntry` |
| `AuditLogEntry.cs` | `AuditLogEntry` |
| `SystemInfo.cs` | `SystemInfo` |

All properties must be PascalCase; no `[JsonPropertyName]` attributes unless the BookStack
field name deviates from snake_case (e.g., a field named `HTMLContent`). Nullable reference
types must be correctly annotated: required fields `string`, optional fields `string?`.

**Acceptance**: Each model deserializes correctly from a representative BookStack JSON fixture
using `JsonNamingPolicy.SnakeCaseLower`.

---

### Task 5 — `IBookStackApiClient` interface

Create `src/BookStack.Mcp.Server/api/IBookStackApiClient.cs`.

Declare all 47 typed async methods grouped by endpoint group (Books, Chapters, Pages, Shelves,
Users, Roles, Attachments, Images, Search, Recycle Bin, Content Permissions, Audit Log,
System). Every method must accept `CancellationToken cancellationToken = default` as its last
parameter.

Refer to the Requirements section of the spec (FR-1) for the complete method list.

**Acceptance**: Interface compiles; a mock implementation using Moq can be created from it
without reflection tricks.

---

### Task 6 — `AuthenticationHandler`

Create `src/BookStack.Mcp.Server/api/AuthenticationHandler.cs`.

Requirements:

- Extend `DelegatingHandler`.
- Inject `IOptions<BookStackApiClientOptions>` in the constructor.
- In `SendAsync`, clone the request (or add a header to the existing request if not already sent)
  and set `Authorization: Token {options.TokenId}:{options.TokenSecret}`.
- **Must not** log `TokenId`, `TokenSecret`, or the full `Authorization` header value at any
  log level. The header name alone may be logged at `Debug` level.
- Accept `ILogger<AuthenticationHandler>` for the debug-level log of the header name.

**Acceptance**: Given a mock inner handler, the outbound `HttpRequestMessage` contains an
`Authorization` header with the correct `Token id:secret` format; no log sink receives a
message containing the token value.

---

### Task 7 — `RateLimitHandler`

Create `src/BookStack.Mcp.Server/api/RateLimitHandler.cs`.

Requirements:

- Extend `DelegatingHandler`.
- After receiving every `HttpResponseMessage`, read `X-RateLimit-Remaining` and
  `X-RateLimit-Reset` headers.
- When `X-RateLimit-Remaining == 0`, compute the delay as the epoch in `X-RateLimit-Reset`
  minus `DateTimeOffset.UtcNow` and call `await Task.Delay(delay, cancellationToken)`.
- Use a `SemaphoreSlim(1, 1)` to serialise concurrent requests through the delay gate.
- If the headers are absent or malformed, log a `Warning` and continue without delay.
- Store the reset deadline in a field so a subsequent request after the delay is checked
  before sending (avoid re-entering the delay for concurrent requests already past the reset).

**Acceptance**: Given a mock handler that returns `X-RateLimit-Remaining: 0` and
`X-RateLimit-Reset: {nowPlusOneSecond}`, the handler delays by approximately one second before
the next call returns; given a response without rate-limit headers, no delay occurs.

---

### Task 8 — `BookStackApiClient` core + Books partial

Create `src/BookStack.Mcp.Server/api/BookStackApiClient.cs` (core partial) and
`src/BookStack.Mcp.Server/api/BookStackApiClient.Books.cs`.

**Core partial** must contain:

- Constructor accepting `HttpClient httpClient`, `IOptions<BookStackApiClientOptions> options`,
  `ILogger<BookStackApiClient> logger`.
- Static `JsonSerializerOptions` singleton with `JsonNamingPolicy.SnakeCaseLower` and
  `PropertyNameCaseInsensitive = true`.
- Private `SendAsync<T>` helper that sends a request, calls `EnsureSuccessOrThrow` (see below),
  and deserializes the response.
- Private `EnsureSuccessOrThrow` helper that reads the response; if non-2xx, deserializes the
  BookStack error envelope (`{"error":{"message":"...","code":...}}`) and throws
  `BookStackApiException`.
- Private `GetExportUrlSegment(ExportFormat format)` helper.
- `IBookStackApiClient` declaration on the class.

**Books partial** must implement: `ListBooksAsync`, `CreateBookAsync`, `GetBookAsync`,
`UpdateBookAsync`, `DeleteBookAsync`, `ExportBookAsync`.

**Acceptance**: `ListBooksAsync` with a mock handler returning a valid JSON books list returns
a populated `ListResponse<Book>`; `DeleteBookAsync` with a 404 mock throws
`BookStackApiException` with `StatusCode == 404`.

---

### Task 9 — Chapters, Pages, Shelves partials

Create:

- `src/BookStack.Mcp.Server/api/BookStackApiClient.Chapters.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.Pages.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.Shelves.cs`

Implement the corresponding methods from `IBookStackApiClient`.

Export methods (`ExportChapterAsync`, `ExportPageAsync`) must use `GetExportUrlSegment` and
return the raw response body string without JSON deserialization.

**Acceptance**: Each export method returns the raw string from the mock response body; each
CRUD method maps to the correct HTTP verb and path.

---

### Task 10 — Users, Roles partials

Create:

- `src/BookStack.Mcp.Server/api/BookStackApiClient.Users.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.Roles.cs`

Implement the corresponding methods from `IBookStackApiClient`.

**Acceptance**: Each method targets the correct BookStack API path; response models deserialize
correctly from representative JSON fixtures.

---

### Task 11 — Attachments, Images partials

Create:

- `src/BookStack.Mcp.Server/api/BookStackApiClient.Attachments.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.Images.cs`

> **Note**: Multipart form-data upload is out of scope for this feature (deferred to #6, #12).
> `CreateAttachmentAsync` and `CreateImageAsync` in this task accept only JSON-serializable
> request parameter types; multipart overloads are deferred.

**Acceptance**: Each method targets the correct BookStack API path; `DeleteImageAsync` with a
404 mock throws `BookStackApiException`.

---

### Task 12 — Search, Recycle Bin, Permissions, Audit Log, System partials

Create:

- `src/BookStack.Mcp.Server/api/BookStackApiClient.Search.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.RecycleBin.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.Permissions.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.AuditLog.cs`
- `src/BookStack.Mcp.Server/api/BookStackApiClient.System.cs`

**Acceptance**: `SearchAsync` targets `GET /api/search`; `GetSystemInfoAsync` targets
`GET /api/system`; all methods accept and forward `CancellationToken`.

---

### Task 13 — `AddBookStackApiClient` DI extension method

Create `src/BookStack.Mcp.Server/api/BookStackServiceCollectionExtensions.cs`.

Implement `AddBookStackApiClient(this IServiceCollection services, IConfiguration configuration)`
as shown in the DI Registration section above.

Also register `BookStackApiClientOptionsValidator` (from Task 1) as
`IValidateOptions<BookStackApiClientOptions>` so startup validation is triggered automatically.

**Acceptance**: `services.AddBookStackApiClient(configuration)` followed by
`services.BuildServiceProvider().GetRequiredService<IBookStackApiClient>()` resolves without
error when `IConfiguration` contains valid `BookStack:*` keys; an
`OptionsValidationException` is thrown when `TokenId` is absent.

---

### Task 14 — Test infrastructure: `MockHttpMessageHandler`

Create `tests/BookStack.Mcp.Server.Tests/api/Helpers/MockHttpMessageHandler.cs`.

The helper must:

- Extend `HttpMessageHandler`.
- Expose a `Func<HttpRequestMessage, HttpResponseMessage>` delegate that tests configure to
  return specific responses.
- Capture the last `HttpRequestMessage` for assertion in tests.
- Support both a single-response constructor and a delegate-based constructor.

**Acceptance**: `MockHttpMessageHandler` can be used as the primary handler for a directly
constructed `HttpClient`; tests can assert headers, method, and URI on `LastRequest`.

---

### Task 15 — Unit tests: Books, Chapters, Pages

Create:

- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientBooksTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientChaptersTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientPagesTests.cs`

Cover per spec acceptance criteria:

- `ListBooksAsync_MockReturns200WithValidJson_ReturnsPopulatedListResponse`
- `GetBookAsync_MockReturns404_ThrowsBookStackApiExceptionWithStatusCode404`
- `ExportBookAsync_MockReturnsNonJsonBody_ReturnsRawString`
- Snake_case deserialization: `created_at` → `CreatedAt` (and equivalents for chapters/pages).
- `CancellationToken` is forwarded to `HttpClient.SendAsync` (cancel mid-flight).

**Acceptance**: All tests pass with `dotnet test`; no network connection required.

---

### Task 16 — Unit tests: Shelves, Users, Roles

Create:

- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientShelvesTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientUsersTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientRolesTests.cs`

Cover CRUD happy-path and 404/403 error cases per the spec acceptance criteria.

**Acceptance**: All tests pass without network access.

---

### Task 17 — Unit tests: Attachments, Images, Search

Create:

- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientAttachmentsTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientImagesTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientSearchTests.cs`

**Acceptance**: All tests pass without network access.

---

### Task 18 — Unit tests: Recycle Bin, Permissions, Audit Log, System

Create:

- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientRecycleBinTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientPermissionsTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientAuditLogTests.cs`
- `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientSystemTests.cs`

**Acceptance**: All tests pass without network access.

---

### Task 19 — Unit tests: `AuthenticationHandler`

Create `tests/BookStack.Mcp.Server.Tests/api/AuthenticationHandlerTests.cs`.

Required test cases:

- `SendAsync_AnyRequest_InjectsAuthorizationHeader`
- `SendAsync_AnyRequest_AuthorizationHeaderContainsTokenIdAndSecret`
- `SendAsync_AnyRequest_TokenValueNotPresentInAnyLogOutput`

For the no-logging test, use an in-memory `ILogger` sink that captures all log messages and
asserts the token value does not appear in any of them.

**Acceptance**: Tests pass; the no-log assertion covers all log levels (Trace through Critical).

---

### Task 20 — Unit tests: `RateLimitHandler`

Create `tests/BookStack.Mcp.Server.Tests/api/RateLimitHandlerTests.cs`.

Required test cases:

- `SendAsync_ResponseWithRateLimitRemainingGreaterThanZero_NoDelay`
- `SendAsync_ResponseWithRateLimitRemainingZeroAndFutureReset_DelaysUntilReset`
- `SendAsync_ResponseWithMissingRateLimitHeaders_NoDelayAndWarningLogged`
- `SendAsync_DelayedByRateLimitHandler_CancellationTokenAbortsDelay`

Use a `FakeTimeProvider` (or `DateTimeOffset` injection) to avoid real clock sleeps in tests.

**Acceptance**: Delay tests complete in milliseconds (with fake time); cancellation test throws
`OperationCanceledException`.

---

### Task 21 — Unit tests: `BookStackApiException`

Create `tests/BookStack.Mcp.Server.Tests/api/BookStackApiExceptionTests.cs`.

Required test cases:

- `Constructor_SetsAllProperties`
- `Message_ContainsStatusCodeAndErrorMessage`
- `SendAsync_MockReturns401_ThrowsExceptionWithoutTokenInMessage`
- `SendAsync_MockReturns422WithValidationErrorBody_ExceptionParsesErrorMessage`

**Acceptance**: All tests pass; the 401 test asserts the token value is absent from
`exception.Message`, `exception.Data`, and `exception.InnerException?.Message`.

---

### Task 22 — Unit tests: DI registration

Create `tests/BookStack.Mcp.Server.Tests/api/BookStackApiClientDiTests.cs`.

Required test cases:

- `AddBookStackApiClient_ValidConfiguration_ResolvesIBookStackApiClient`
- `AddBookStackApiClient_MissingTokenId_ThrowsOptionsValidationException`
- `AddBookStackApiClient_InvalidBaseUrl_ThrowsOptionsValidationException`
- `AddBookStackApiClient_HandlerPipelineContainsAuthenticationHandlerThenRateLimitHandler`

**Acceptance**: All tests pass; pipeline-order test confirms `AuthenticationHandler` precedes
`RateLimitHandler` in the registered handler chain.

---

## Engineering Practices

| Practice | Decision | Reference |
| --- | --- | --- |
| Async | `async`/`await` throughout; no `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` | [Coding Guidelines](../../.github/docs/coding-guidelines.md) |
| `CancellationToken` | Every public async method accepts and forwards `CancellationToken` | Spec FR-NFR-3 |
| Logging | `ILogger<T>`; never `Console.WriteLine`; tokens and secrets are never logged | Spec Security + [Coding Guidelines](../../.github/docs/coding-guidelines.md) |
| `ConfigureAwait` | `ConfigureAwait(false)` in all library code | [Coding Guidelines](../../.github/docs/coding-guidelines.md) |
| One type per file | Each class/record/enum in its own file; filename matches type name | [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) |
| `TreatWarningsAsErrors` | Enabled in all projects; CI will fail on any compiler warning | [ADR-0003](../../architecture/decisions/ADR-0003-cicd-github-actions.md) |

---

## Commands

Executable commands for this project (copy and run directly):

### Build

```bash
dotnet build --configuration Release
```

### Tests

```bash
dotnet test --verbosity normal
```

### Lint / Formatting

```bash
dotnet format --verify-no-changes
```

### Local Execution

```bash
dotnet run --project src/BookStack.Mcp.Server
```
