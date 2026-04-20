// BookStack MCP Server — entry point (stub)
// Full implementation tracked in https://github.com/MarkZither/bookstack-mcp-server-dotnet/issues/14

using System.Reflection;
using BookStack.Mcp.Server.Api;
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
