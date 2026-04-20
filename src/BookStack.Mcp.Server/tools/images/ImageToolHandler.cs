using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Images;

[McpServerToolType]
internal sealed class ImageToolHandler(IBookStackApiClient client, ILogger<ImageToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ImageToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_images_list"), Description("List all images in BookStack")]
    public Task<string> ListImagesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_images_read"), Description("Get an image by ID")]
    public Task<string> ReadImageAsync(
        [Description("The image ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_images_create"), Description("Create a new image")]
    public Task<string> CreateImageAsync(
        [Description("The image name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_images_update"), Description("Update an existing image")]
    public Task<string> UpdateImageAsync(
        [Description("The image ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");

    [McpServerTool(Name = "bookstack_images_delete"), Description("Delete an image by ID")]
    public Task<string> DeleteImageAsync(
        [Description("The image ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #6");
}
