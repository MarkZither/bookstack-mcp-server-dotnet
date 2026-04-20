using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Chapters;

[McpServerResourceType]
internal sealed class ChapterResourceHandler(
    IBookStackApiClient client, ILogger<ChapterResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<ChapterResourceHandler> _logger = logger;

    [McpServerResource(UriTemplate = "bookstack://chapters", Name = "Chapters")]
    [Description("All chapters in the BookStack instance")]
    public Task<string> GetChaptersAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in a future issue");
}
