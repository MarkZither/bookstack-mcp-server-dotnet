using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<RecycleBinItem>> ListRecycleBinAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "recycle-bin" + BuildQueryString(query);
        return SendAsync<ListResponse<RecycleBinItem>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task RestoreFromRecycleBinAsync(
        int deletionId,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Put, $"recycle-bin/{deletionId}"), cancellationToken);
    }

    public Task PermanentlyDeleteAsync(
        int deletionId,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"recycle-bin/{deletionId}"), cancellationToken);
    }
}
