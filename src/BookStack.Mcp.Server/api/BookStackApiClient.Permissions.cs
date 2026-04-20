using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ContentPermissions> GetContentPermissionsAsync(
        ContentType contentType,
        int contentId,
        CancellationToken cancellationToken = default)
    {
        var url = $"content-permissions/{GetContentTypePath(contentType)}/{contentId}";
        return SendAsync<ContentPermissions>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<ContentPermissions> UpdateContentPermissionsAsync(
        ContentType contentType,
        int contentId,
        UpdateContentPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"content-permissions/{GetContentTypePath(contentType)}/{contentId}";
        return SendAsync<ContentPermissions>(JsonRequest(HttpMethod.Put, url, request), cancellationToken);
    }
}
