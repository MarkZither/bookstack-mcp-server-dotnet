using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.System;

// [McpServerToolType] — hidden until #10 is implemented
internal sealed class SystemToolHandler(IBookStackApiClient client, ILogger<SystemToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<SystemToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_system_info"), Description("Get system information from the BookStack instance")]
    public Task<string> GetSystemInfoAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");
}
