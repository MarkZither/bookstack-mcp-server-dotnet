using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<Attachment>> ListAttachmentsAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "attachments" + BuildQueryString(query);
        return SendAsync<ListResponse<Attachment>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<Attachment> CreateAttachmentAsync(
        CreateAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Attachment>(JsonRequest(HttpMethod.Post, "attachments", request), cancellationToken);
    }

    public Task<Attachment> GetAttachmentAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Attachment>(JsonRequest(HttpMethod.Get, $"attachments/{id}"), cancellationToken);
    }

    public Task<Attachment> UpdateAttachmentAsync(
        int id,
        UpdateAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Attachment>(JsonRequest(HttpMethod.Put, $"attachments/{id}", request), cancellationToken);
    }

    public Task DeleteAttachmentAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"attachments/{id}"), cancellationToken);
    }
}
