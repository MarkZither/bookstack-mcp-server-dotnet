using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Resources.Users;

[McpServerResourceType]
internal sealed class UserResourceHandler(
    IBookStackApiClient client, ILogger<UserResourceHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<UserResourceHandler> _logger = logger;

    [McpServerResource(UriTemplate = "bookstack://users", Name = "Users")]
    [Description("All users in the BookStack instance")]
    public Task<string> GetUsersAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in a future issue");
}
