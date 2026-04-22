// BookStack MCP Server — entry point (stub)
// Full implementation tracked in https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/14

using System.Reflection;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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

    builder.Configuration.AddInMemoryCollection(MapBookStackEnvVars());
    builder.Services.AddBookStackApiClient(builder.Configuration);
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
    builder.Configuration.AddInMemoryCollection(MapBookStackEnvVars());
    builder.Services.AddBookStackApiClient(builder.Configuration);
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly(Assembly.GetExecutingAssembly())
        .WithResourcesFromAssembly(Assembly.GetExecutingAssembly());

    var app = builder.Build();
    app.MapMcp();
    await app.RunAsync($"http://0.0.0.0:{port}").ConfigureAwait(false);
}

return 0;

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

    return map;
}
