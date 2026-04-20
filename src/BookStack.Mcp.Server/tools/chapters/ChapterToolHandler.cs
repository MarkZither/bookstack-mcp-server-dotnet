using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Chapters;

[McpServerToolType]
internal sealed class ChapterToolHandler(IBookStackApiClient client, ILogger<ChapterToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ChapterToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_chapters_list"), Description("List all chapters in BookStack")]
    public Task<string> ListChaptersAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_chapters_read"), Description("Get a chapter by ID")]
    public Task<string> ReadChapterAsync(
        [Description("The chapter ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_chapters_create"), Description("Create a new chapter")]
    public Task<string> CreateChapterAsync(
        [Description("The chapter name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_chapters_update"), Description("Update an existing chapter")]
    public Task<string> UpdateChapterAsync(
        [Description("The chapter ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_chapters_delete"), Description("Delete a chapter by ID")]
    public Task<string> DeleteChapterAsync(
        [Description("The chapter ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");

    [McpServerTool(Name = "bookstack_chapters_export"), Description("Export a chapter in a given format")]
    public Task<string> ExportChapterAsync(
        [Description("The chapter ID")] int id,
        [Description("Export format: html, pdf, plaintext, markdown")] string format,
        CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #9");
}
