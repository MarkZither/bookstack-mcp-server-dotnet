using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Permissions;

[McpServerToolType]
internal sealed class PermissionToolHandler(IBookStackApiClient client, ILogger<PermissionToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<PermissionToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_permissions_read"), Description("Read content permissions for an item")]
    public Task<string> ReadPermissionsAsync(
        [Description("The content type (book, chapter, page, bookshelf)")] string contentType,
        [Description("The content item ID")] int id,
        CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");

    [McpServerTool(Name = "bookstack_permissions_update"), Description("Update content permissions for an item")]
    public Task<string> UpdatePermissionsAsync(
        [Description("The content type (book, chapter, page, bookshelf)")] string contentType,
        [Description("The content item ID")] int id,
        CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");
}
