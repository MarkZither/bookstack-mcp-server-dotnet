using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<Book>> ListBooksAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "books" + BuildQueryString(query);
        return SendAsync<ListResponse<Book>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<Book> CreateBookAsync(
        CreateBookRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Book>(JsonRequest(HttpMethod.Post, "books", request), cancellationToken);
    }

    public Task<BookWithContents> GetBookAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<BookWithContents>(JsonRequest(HttpMethod.Get, $"books/{id}"), cancellationToken);
    }

    public Task<Book> UpdateBookAsync(
        int id,
        UpdateBookRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Book>(JsonRequest(HttpMethod.Put, $"books/{id}", request), cancellationToken);
    }

    public Task DeleteBookAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"books/{id}"), cancellationToken);
    }

    public Task<string> ExportBookAsync(
        int id,
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        return SendRawAsync(JsonRequest(HttpMethod.Get, $"books/{id}/export/{GetExportUrlSegment(format)}"), cancellationToken);
    }
}
