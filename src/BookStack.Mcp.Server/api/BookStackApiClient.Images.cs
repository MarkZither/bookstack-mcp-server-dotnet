using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<Image>> ListImagesAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "image-gallery" + BuildQueryString(query);
        return SendAsync<ListResponse<Image>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<Image> CreateImageAsync(
        CreateImageRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Image>(JsonRequest(HttpMethod.Post, "image-gallery", request), cancellationToken);
    }

    public Task<Image> GetImageAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Image>(JsonRequest(HttpMethod.Get, $"image-gallery/{id}"), cancellationToken);
    }

    public Task<Image> UpdateImageAsync(
        int id,
        UpdateImageRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Image>(JsonRequest(HttpMethod.Put, $"image-gallery/{id}", request), cancellationToken);
    }

    public Task DeleteImageAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"image-gallery/{id}"), cancellationToken);
    }
}
