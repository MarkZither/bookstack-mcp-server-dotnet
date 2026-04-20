using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Pages;

[McpServerToolType]
internal sealed class PageToolHandler(IBookStackApiClient client, ILogger<PageToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<PageToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_pages_list"), Description("List all pages in BookStack")]
    public Task<string> ListPagesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_pages_read"), Description("Get a page by ID")]
    public Task<string> ReadPageAsync(
        [Description("The page ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_pages_create"), Description("Create a new page")]
    public Task<string> CreatePageAsync(
        [Description("The page name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_pages_update"), Description("Update an existing page")]
    public Task<string> UpdatePageAsync(
        [Description("The page ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_pages_delete"), Description("Delete a page by ID")]
    public Task<string> DeletePageAsync(
        [Description("The page ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_pages_export"), Description("Export a page in a given format")]
    public Task<string> ExportPageAsync(
        [Description("The page ID")] int id,
        [Description("Export format: html, pdf, plaintext, markdown")] string format,
        CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");
}
