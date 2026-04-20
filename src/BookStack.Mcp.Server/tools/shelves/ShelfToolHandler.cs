using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Shelves;

[McpServerToolType]
internal sealed class ShelfToolHandler(IBookStackApiClient client, ILogger<ShelfToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ShelfToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_shelves_list"), Description("List all shelves in BookStack")]
    public Task<string> ListShelvesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_shelves_read"), Description("Get a shelf by ID")]
    public Task<string> ReadShelfAsync(
        [Description("The shelf ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_shelves_create"), Description("Create a new shelf")]
    public Task<string> CreateShelfAsync(
        [Description("The shelf name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_shelves_update"), Description("Update an existing shelf")]
    public Task<string> UpdateShelfAsync(
        [Description("The shelf ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_shelves_delete"), Description("Delete a shelf by ID")]
    public Task<string> DeleteShelfAsync(
        [Description("The shelf ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");
}
