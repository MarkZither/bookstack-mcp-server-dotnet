using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.RecycleBin;

[McpServerToolType]
internal sealed class RecycleBinToolHandler(IBookStackApiClient client, ILogger<RecycleBinToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<RecycleBinToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_recyclebin_list"), Description("List all items in the BookStack recycle bin")]
    public Task<string> ListRecycleBinAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");

    [McpServerTool(Name = "bookstack_recyclebin_restore"), Description("Restore an item from the recycle bin")]
    public Task<string> RestoreRecycleBinAsync(
        [Description("The recycle bin item ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");

    [McpServerTool(Name = "bookstack_recyclebin_delete_permanently"), Description("Permanently delete an item from the recycle bin")]
    public Task<string> DeletePermanentlyAsync(
        [Description("The recycle bin item ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");
}
