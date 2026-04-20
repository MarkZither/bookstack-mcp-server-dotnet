# Implementation Plan: MCP Server Infrastructure — stdio + Streamable HTTP

**Feature**: FEAT-0008
**Spec**: [docs/features/mcp-server-infrastructure/spec.md](spec.md)
**GitHub Issue**: [#8](https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/8)
**Status**: Ready for implementation

---

## Architecture Decisions

All ADRs for this feature have been accepted. Implementation must follow them without deviation.

| ADR | Title | Decision Summary |
| --- | --- | --- |
| [ADR-0001](../../architecture/decisions/ADR-0001-mcp-sdk-selection.md) | MCP SDK Selection | Use `ModelContextProtocol` + `ModelContextProtocol.AspNetCore`; no hand-rolled protocol layer |
| [ADR-0002](../../architecture/decisions/ADR-0002-solution-structure.md) | Solution and Project Structure | Single server project; `tools/` and `resources/` subdirectories; namespaces mirror directories |
| [ADR-0005](../../architecture/decisions/ADR-0005-ihttpclientfactory-typed-client.md) | IHttpClientFactory Typed Client | `AddBookStackApiClient(IConfiguration)` already implemented; wire it into the composition root |
| [ADR-0009](../../architecture/decisions/ADR-0009-dual-transport-entry-point.md) | Dual-Transport Entry-Point Strategy | Single binary; `BOOKSTACK_MCP_TRANSPORT` env var selects `stdio` (default) or `http` at startup |

---

## Implementation Tasks

Tasks are ordered by dependency. Each task is independently committable.

---

### Task 1 — Update `BookStack.Mcp.Server.csproj`

Change the project SDK to `Microsoft.NET.Sdk.Web` (required for `WebApplication` in the HTTP
transport branch) and add the `ModelContextProtocol` NuGet packages.

**File**: `src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RootNamespace>BookStack.Mcp.Server</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="1.2.0" />
    <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.6" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.6" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.6" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.6" />
  </ItemGroup>

</Project>
```

**Acceptance**: `dotnet build src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj` succeeds with
zero errors and zero warnings.

---

### Task 2 — Implement `Program.cs`

Replace the stub entry point with the full composition root. `Program.cs` must:

1. Read `BOOKSTACK_MCP_TRANSPORT` before building any host.
2. Reject invalid values with a `Console.Error.WriteLine` message and exit code `1` (ILogger is not
   yet available at this point; this is the sole acceptable use of `Console.Error` in the codebase).
3. Build the appropriate host based on the validated transport value.
4. Register `AddBookStackApiClient(configuration)` and `AddMcpServer()` in both branches.
5. Use `WithToolsFromAssembly` and `WithResourcesFromAssembly` for handler discovery (see ADR-0009).
6. Route all logging to `stderr` in the stdio branch so that `stdout` carries only MCP frames.
7. Return exit code `0` on clean shutdown.

**File**: `src/BookStack.Mcp.Server/Program.cs`

```csharp
using System.Reflection;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var transport = Environment.GetEnvironmentVariable("BOOKSTACK_MCP_TRANSPORT") ?? "stdio";

if (transport is not ("stdio" or "http"))
{
    Console.Error.WriteLine(
        $"Invalid BOOKSTACK_MCP_TRANSPORT value: '{transport}'. Valid values: stdio, http.");
    return 1;
}

if (transport == "stdio")
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(options =>
        options.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddBookStackApiClient(builder.Configuration);
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(Assembly.GetExecutingAssembly())
        .WithResourcesFromAssembly(Assembly.GetExecutingAssembly());

    await builder.Build().RunAsync();
}
else
{
    var port = int.TryParse(
        Environment.GetEnvironmentVariable("BOOKSTACK_MCP_HTTP_PORT"), out var p) ? p : 3000;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddBookStackApiClient(builder.Configuration);
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly(Assembly.GetExecutingAssembly())
        .WithResourcesFromAssembly(Assembly.GetExecutingAssembly());

    var app = builder.Build();
    app.MapMcp();
    await app.RunAsync($"http://0.0.0.0:{port}");
}

return 0;
```

**Acceptance**:

- `dotnet run --project src/BookStack.Mcp.Server` (with `BOOKSTACK_TOKEN_ID` and
  `BOOKSTACK_TOKEN_SECRET` set) starts the server and completes the MCP `initialize` handshake
  over stdio within five seconds.
- Running with `BOOKSTACK_MCP_TRANSPORT=invalid` exits with code `1` and prints a message to
  `stderr`.
- No content other than MCP JSON-RPC frames appears on `stdout` when the stdio transport is active.

---

### Task 3 — Stub tool handler classes

Create one stub handler class per tool category under `src/BookStack.Mcp.Server/tools/`. Each class:

- Is decorated with `[McpServerToolType]` so `WithToolsFromAssembly` discovers it automatically.
- Receives `IBookStackApiClient` and `ILogger<T>` via primary constructor injection.
- Has one stub method per tool (throwing `NotImplementedException`) decorated with
  `[McpServerTool(Name = "...")]` and `[Description("...")]`.
- Uses the `internal sealed` access modifier.
- Lives in a file-scoped namespace matching its directory path.

**Reference stub — `src/BookStack.Mcp.Server/tools/books/BookToolHandler.cs`**:

```csharp
using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Books;

[McpServerToolType]
internal sealed class BookToolHandler(IBookStackApiClient client, ILogger<BookToolHandler> logger)
{
    [McpServerTool(Name = "bookstack_books_list"), Description("List all books in BookStack")]
    public Task<string> ListBooksAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_read"), Description("Get a book by ID")]
    public Task<string> ReadBookAsync(
        [Description("The book ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_create"), Description("Create a new book")]
    public Task<string> CreateBookAsync(
        [Description("The book name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_update"), Description("Update an existing book")]
    public Task<string> UpdateBookAsync(
        [Description("The book ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_delete"), Description("Delete a book by ID")]
    public Task<string> DeleteBookAsync(
        [Description("The book ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");

    [McpServerTool(Name = "bookstack_books_export"), Description("Export a book in a given format")]
    public Task<string> ExportBookAsync(
        [Description("The book ID")] int id,
        [Description("Export format: html, pdf, plaintext, markdown")] string format,
        CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #7");
}
```

**Files to create** (one per row; all follow the reference pattern above):

| File path | Class name | Tool names | Implements |
| --- | --- | --- | --- |
| `tools/books/BookToolHandler.cs` | `BookToolHandler` | `bookstack_books_list/read/create/update/delete/export` | Issue #7 |
| `tools/chapters/ChapterToolHandler.cs` | `ChapterToolHandler` | `bookstack_chapters_list/read/create/update/delete/export` | Issue #9 |
| `tools/pages/PageToolHandler.cs` | `PageToolHandler` | `bookstack_pages_list/read/create/update/delete/export` | Issue #9 |
| `tools/shelves/ShelfToolHandler.cs` | `ShelfToolHandler` | `bookstack_shelves_list/read/create/update/delete` | Issue #9 |
| `tools/users/UserToolHandler.cs` | `UserToolHandler` | `bookstack_users_list/read/create/update/delete` | Issue #16 |
| `tools/roles/RoleToolHandler.cs` | `RoleToolHandler` | `bookstack_roles_list/read/create/update/delete` | Issue #16 |
| `tools/attachments/AttachmentToolHandler.cs` | `AttachmentToolHandler` | `bookstack_attachments_list/read/create/update/delete` | Issue #6 |
| `tools/images/ImageToolHandler.cs` | `ImageToolHandler` | `bookstack_images_list/read/create/update/delete` | Issue #6 |
| `tools/search/SearchToolHandler.cs` | `SearchToolHandler` | `bookstack_search` | Issue #12 |
| `tools/recyclebin/RecycleBinToolHandler.cs` | `RecycleBinToolHandler` | `bookstack_recyclebin_list/restore/delete_permanently` | Issue #10 |
| `tools/permissions/PermissionToolHandler.cs` | `PermissionToolHandler` | `bookstack_permissions_read/update` | Issue #10 |
| `tools/audit/AuditToolHandler.cs` | `AuditToolHandler` | `bookstack_audit_log_list` | Issue #10 |
| `tools/system/SystemToolHandler.cs` | `SystemToolHandler` | `bookstack_system_info` | Issue #10 |
| `tools/server-info/ServerInfoToolHandler.cs` | `ServerInfoToolHandler` | `bookstack_server_info/help/error_guides/tool_categories/usage_examples` | Issue #10 |

**Acceptance**: `dotnet build` succeeds; each handler class is decorated with `[McpServerToolType]`
and each tool method is decorated with `[McpServerTool]`.

---

### Task 4 — Stub resource handler classes

Create one stub handler class per resource category under `src/BookStack.Mcp.Server/resources/`.
Each class:

- Is decorated with `[McpServerResourceType]` so `WithResourcesFromAssembly` discovers it.
- Receives `IBookStackApiClient` and `ILogger<T>` via primary constructor injection.
- Has one stub method per resource URI throwing `NotImplementedException`.
- Uses the `internal sealed` access modifier and a file-scoped namespace.

**Reference stub — `src/BookStack.Mcp.Server/resources/books/BookResourceHandler.cs`**:

```csharp
using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Books;

[McpServerResourceType]
internal sealed class BookResourceHandler(
    IBookStackApiClient client, ILogger<BookResourceHandler> logger)
{
    [McpServerResource(Uri = "bookstack://books", Name = "Books")]
    [Description("All books in the BookStack instance")]
    public Task<string> GetBooksAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in a future issue");
}
```

**Files to create**:

| File path | Class name | Resource URI |
| --- | --- | --- |
| `resources/books/BookResourceHandler.cs` | `BookResourceHandler` | `bookstack://books` |
| `resources/chapters/ChapterResourceHandler.cs` | `ChapterResourceHandler` | `bookstack://chapters` |
| `resources/pages/PageResourceHandler.cs` | `PageResourceHandler` | `bookstack://pages` |
| `resources/shelves/ShelfResourceHandler.cs` | `ShelfResourceHandler` | `bookstack://shelves` |
| `resources/users/UserResourceHandler.cs` | `UserResourceHandler` | `bookstack://users` |
| `resources/search/SearchResourceHandler.cs` | `SearchResourceHandler` | `bookstack://search` |

**Acceptance**: `dotnet build` succeeds; each handler class is decorated with
`[McpServerResourceType]` and each resource method is decorated with `[McpServerResource]`.

---

### Task 5 — Tests

Add tests in `tests/BookStack.Mcp.Server.Tests/` covering three areas: attribute verification,
assembly scan discovery, and transport environment variable validation.

**File**: `tests/BookStack.Mcp.Server.Tests/server/McpHandlerAttributeTests.cs`

```csharp
using System.Reflection;
using BookStack.Mcp.Server.Tools.Books;
using FluentAssertions;
using ModelContextProtocol.Server;
using TUnit.Core;

namespace BookStack.Mcp.Server.Tests.Server;

public class McpHandlerAttributeTests
{
    [Test]
    public void BookToolHandler_IsDecoratedWithMcpServerToolTypeAttribute()
    {
        typeof(BookToolHandler)
            .Should().BeDecoratedWith<McpServerToolTypeAttribute>();
    }

    [Test]
    public void ServerAssembly_ContainsAtLeastFourteenToolHandlerTypes()
    {
        var assembly = typeof(BookToolHandler).Assembly;
        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .ToList();

        toolTypes.Count.Should().BeGreaterThanOrEqualTo(14);
    }

    [Test]
    public void ServerAssembly_ContainsAtLeastSixResourceHandlerTypes()
    {
        var assembly = typeof(BookToolHandler).Assembly;
        var resourceTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerResourceTypeAttribute>() is not null)
            .ToList();

        resourceTypes.Count.Should().BeGreaterThanOrEqualTo(6);
    }

    [Test]
    public void AllToolMethods_HaveMcpServerToolAttributeWithNonEmptyName()
    {
        var assembly = typeof(BookToolHandler).Assembly;
        var toolHandlerTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null);

        foreach (var type in toolHandlerTypes)
        {
            var toolMethods = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .ToList();

            toolMethods.Should().NotBeEmpty(
                because: $"{type.Name} is marked [McpServerToolType] but has no [McpServerTool] methods");

            foreach (var method in toolMethods)
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;
                attr.Name.Should().NotBeNullOrWhiteSpace(
                    because: $"{type.Name}.{method.Name} must have a non-empty tool Name");
            }
        }
    }
}
```

**Acceptance**: `dotnet test` passes with zero failures; no real HTTP calls are made.

---

## Commands

Executable commands for this project (copy and run directly):

### Build

```bash
dotnet build src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj --configuration Release
```

### Tests

```bash
dotnet test tests/BookStack.Mcp.Server.Tests/BookStack.Mcp.Server.Tests.csproj --verbosity normal
```

### Lint / Formatting

```bash
dotnet format --verify-no-changes
```

### Local Execution — stdio (default)

```bash
BOOKSTACK_BASE_URL=http://localhost:8080 \
BOOKSTACK_TOKEN_ID=<token-id> \
BOOKSTACK_TOKEN_SECRET=<token-secret> \
dotnet run --project src/BookStack.Mcp.Server
```

### Local Execution — HTTP transport

```bash
BOOKSTACK_MCP_TRANSPORT=http \
BOOKSTACK_MCP_HTTP_PORT=3000 \
BOOKSTACK_BASE_URL=http://localhost:8080 \
BOOKSTACK_TOKEN_ID=<token-id> \
BOOKSTACK_TOKEN_SECRET=<token-secret> \
dotnet run --project src/BookStack.Mcp.Server
```
