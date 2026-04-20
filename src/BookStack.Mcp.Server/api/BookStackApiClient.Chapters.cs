using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<Chapter>> ListChaptersAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "chapters" + BuildQueryString(query);
        return SendAsync<ListResponse<Chapter>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<Chapter> CreateChapterAsync(
        CreateChapterRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Chapter>(JsonRequest(HttpMethod.Post, "chapters", request), cancellationToken);
    }

    public Task<ChapterWithPages> GetChapterAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ChapterWithPages>(JsonRequest(HttpMethod.Get, $"chapters/{id}"), cancellationToken);
    }

    public Task<Chapter> UpdateChapterAsync(
        int id,
        UpdateChapterRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Chapter>(JsonRequest(HttpMethod.Put, $"chapters/{id}", request), cancellationToken);
    }

    public Task DeleteChapterAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"chapters/{id}"), cancellationToken);
    }

    public Task<string> ExportChapterAsync(
        int id,
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        return SendRawAsync(JsonRequest(HttpMethod.Get, $"chapters/{id}/export/{GetExportUrlSegment(format)}"), cancellationToken);
    }
}
