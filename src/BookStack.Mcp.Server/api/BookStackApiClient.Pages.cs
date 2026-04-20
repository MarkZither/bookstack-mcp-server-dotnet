using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<Page>> ListPagesAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "pages" + BuildQueryString(query);
        return SendAsync<ListResponse<Page>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<Page> CreatePageAsync(
        CreatePageRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Page>(JsonRequest(HttpMethod.Post, "pages", request), cancellationToken);
    }

    public Task<PageWithContent> GetPageAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PageWithContent>(JsonRequest(HttpMethod.Get, $"pages/{id}"), cancellationToken);
    }

    public Task<Page> UpdatePageAsync(
        int id,
        UpdatePageRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Page>(JsonRequest(HttpMethod.Put, $"pages/{id}", request), cancellationToken);
    }

    public Task DeletePageAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"pages/{id}"), cancellationToken);
    }

    public Task<string> ExportPageAsync(
        int id,
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        return SendRawAsync(JsonRequest(HttpMethod.Get, $"pages/{id}/export/{GetExportUrlSegment(format)}"), cancellationToken);
    }
}
