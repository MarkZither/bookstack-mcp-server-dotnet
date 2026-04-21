using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.ServerInfo;

// [McpServerToolType] — hidden until #10 is implemented
internal sealed class ServerInfoToolHandler(IBookStackApiClient client, ILogger<ServerInfoToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ServerInfoToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_server_info"), Description("Get information about the BookStack MCP server")]
    public Task<string> GetServerInfoAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");

    [McpServerTool(Name = "bookstack_help"), Description("Get help and usage information for available tools")]
    public Task<string> GetHelpAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");

    [McpServerTool(Name = "bookstack_error_guides"), Description("Get error guides and troubleshooting information")]
    public Task<string> GetErrorGuidesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");

    [McpServerTool(Name = "bookstack_tool_categories"), Description("Get a categorized list of available tools")]
    public Task<string> GetToolCategoriesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");

    [McpServerTool(Name = "bookstack_usage_examples"), Description("Get usage examples for common workflows")]
    public Task<string> GetUsageExamplesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");
}
