using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<Bookshelf>> ListShelvesAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "shelves" + BuildQueryString(query);
        return SendAsync<ListResponse<Bookshelf>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<Bookshelf> CreateShelfAsync(
        CreateShelfRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Bookshelf>(JsonRequest(HttpMethod.Post, "shelves", request), cancellationToken);
    }

    public Task<BookshelfWithBooks> GetShelfAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<BookshelfWithBooks>(JsonRequest(HttpMethod.Get, $"shelves/{id}"), cancellationToken);
    }

    public Task<Bookshelf> UpdateShelfAsync(
        int id,
        UpdateShelfRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Bookshelf>(JsonRequest(HttpMethod.Put, $"shelves/{id}", request), cancellationToken);
    }

    public Task DeleteShelfAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"shelves/{id}"), cancellationToken);
    }
}
