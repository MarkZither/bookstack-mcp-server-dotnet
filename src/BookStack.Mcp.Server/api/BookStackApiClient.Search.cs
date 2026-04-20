using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<SearchResult> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"query={Uri.EscapeDataString(request.Query)}",
        };

        if (request.Page.HasValue)
        {
            parts.Add($"page={request.Page.Value}");
        }

        if (request.Count.HasValue)
        {
            parts.Add($"count={request.Count.Value}");
        }

        var url = "search?" + string.Join("&", parts);
        return SendAsync<SearchResult>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }
}
