using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var transport = Environment.GetEnvironmentVariable("BOOKSTACK_MCP_TRANSPORT") ?? "stdio";

if (transport is not ("stdio" or "http" or "both"))
{
    Console.Error.WriteLine(
        $"Invalid BOOKSTACK_MCP_TRANSPORT value: '{transport}'. Valid values: stdio, http, both.");
    return 1;
}

if (transport == "stdio")
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(options =>
        options.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Configuration.AddInMemoryCollection(MapBookStackEnvVars());
    builder.Services.AddBookStackApiClient(builder.Configuration);
    builder.Services.AddVectorSearch(builder.Configuration);
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(Assembly.GetExecutingAssembly())
        .WithResourcesFromAssembly(Assembly.GetExecutingAssembly());

    await builder.Build().RunAsync().ConfigureAwait(false);
}
else
{
    var port = int.TryParse(
        Environment.GetEnvironmentVariable("BOOKSTACK_MCP_HTTP_PORT"), out var p) ? p : 3000;

    var builder = WebApplication.CreateBuilder(args);

    if (transport == "both")
    {
        builder.Logging.AddConsole(options =>
            options.LogToStandardErrorThreshold = LogLevel.Trace);
    }

    builder.Configuration.AddInMemoryCollection(MapBookStackEnvVars());
    builder.Services.AddBookStackApiClient(builder.Configuration);
    builder.Services.AddVectorSearch(builder.Configuration);

    var mcpBuilder = builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly(Assembly.GetExecutingAssembly())
        .WithResourcesFromAssembly(Assembly.GetExecutingAssembly());

    if (transport == "both")
    {
        mcpBuilder.WithStdioServerTransport();
    }

    var app = builder.Build();

    var authToken = app.Configuration["BOOKSTACK_MCP_HTTP_AUTH_TOKEN"]
                    ?? Environment.GetEnvironmentVariable("BOOKSTACK_MCP_HTTP_AUTH_TOKEN");

    if (string.IsNullOrEmpty(authToken))
    {
        app.Logger.LogWarning(
            "HTTP authentication is disabled. Set BOOKSTACK_MCP_HTTP_AUTH_TOKEN to enable.");

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapMcp();
    }
    else
    {
        var authTokenBytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(authToken));

        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/mcp"))
            {
                var header = ctx.Request.Headers.Authorization.ToString();
                if (!IsAuthorized(header, authTokenBytes))
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }
            }

            await next(ctx).ConfigureAwait(false);
        });

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapMcp();
    }

    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    {
        await app.RunAsync($"http://0.0.0.0:{port}").ConfigureAwait(false);
    }
    else
    {
        await app.RunAsync().ConfigureAwait(false);
    }
}

return 0;

static bool IsAuthorized(string authorizationHeader, ReadOnlyMemory<byte> expected)
{
    const string bearerPrefix = "Bearer ";
    if (!authorizationHeader.StartsWith(bearerPrefix, StringComparison.Ordinal))
    {
        return false;
    }

    var provided = Encoding.UTF8.GetBytes(authorizationHeader[bearerPrefix.Length..]);
    return provided.Length == expected.Length
        && CryptographicOperations.FixedTimeEquals(expected.Span, provided);
}

static Dictionary<string, string?> MapBookStackEnvVars()
{
    var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    var baseUrl = Environment.GetEnvironmentVariable("BOOKSTACK_BASE_URL");
    if (baseUrl is not null)
    {
        map["BookStack:BaseUrl"] = baseUrl;
    }

    var tokenSecret = Environment.GetEnvironmentVariable("BOOKSTACK_TOKEN_SECRET");
    if (tokenSecret is not null)
    {
        var colonIndex = tokenSecret.IndexOf(':');
        if (colonIndex > 0)
        {
            map["BookStack:TokenId"] = tokenSecret[..colonIndex];
            map["BookStack:TokenSecret"] = tokenSecret[(colonIndex + 1)..];
        }
    }

    AddScopeEntries(map, "BOOKSTACK_SCOPED_BOOKS", "BookStack:ScopedBooks");
    AddScopeEntries(map, "BOOKSTACK_SCOPED_SHELVES", "BookStack:ScopedShelves");

    return map;
}

static void AddScopeEntries(Dictionary<string, string?> map, string envVar, string configPrefix)
{
    var raw = Environment.GetEnvironmentVariable(envVar);
    if (raw is null)
    {
        return;
    }

    var index = 0;
    foreach (var entry in raw.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0))
    {
        if (!_scopeEntryRegex.IsMatch(entry))
        {
            continue;
        }

        map[$"{configPrefix}:{index++}"] = entry;
    }
}

public partial class Program
{
    private static readonly Regex _scopeEntryRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
}
