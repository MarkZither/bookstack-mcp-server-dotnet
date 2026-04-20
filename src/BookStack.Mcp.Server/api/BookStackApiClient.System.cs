using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<SystemInfo>(JsonRequest(HttpMethod.Get, "system"), cancellationToken);
    }
}
