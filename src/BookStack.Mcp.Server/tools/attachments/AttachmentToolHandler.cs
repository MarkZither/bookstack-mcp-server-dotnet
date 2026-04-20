using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Attachments;

[McpServerToolType]
internal sealed class AttachmentToolHandler(IBookStackApiClient client, ILogger<AttachmentToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<AttachmentToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_attachments_list"), Description("List all attachments in BookStack")]
    public Task<string> ListAttachmentsAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_attachments_read"), Description("Get an attachment by ID")]
    public Task<string> ReadAttachmentAsync(
        [Description("The attachment ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_attachments_create"), Description("Create a new attachment")]
    public Task<string> CreateAttachmentAsync(
        [Description("The attachment name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_attachments_update"), Description("Update an existing attachment")]
    public Task<string> UpdateAttachmentAsync(
        [Description("The attachment ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_attachments_delete"), Description("Delete an attachment by ID")]
    public Task<string> DeleteAttachmentAsync(
        [Description("The attachment ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");
}
