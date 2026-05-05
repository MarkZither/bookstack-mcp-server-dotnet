using BookStack.Mcp.Server.Config;
using BookStack.Mcp.Server.Data.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BookStack.Mcp.Server.Admin;

internal static class AdminHandlers
{
    internal static async Task<IResult> GetStatusAsync(
        IVectorStore vectorStore,
        IAdminTaskQueue queue,
        CancellationToken ct)
    {
        var totalPages = await vectorStore.GetTotalCountAsync(ct).ConfigureAwait(false);
        var lastSync = await vectorStore.GetLastSyncAtAsync(ct).ConfigureAwait(false);
        var lastSyncTime = lastSync?.ToString("o");
        return Results.Ok(new AdminStatusResponse(totalPages, lastSyncTime, queue.PendingCount));
    }

    internal static async Task<IResult> PostSyncAsync(
        IAdminTaskQueue queue,
        CancellationToken ct)
    {
        await queue.EnqueueAsync(new AdminTask(AdminTaskKind.FullSync), ct).ConfigureAwait(false);
        return Results.Accepted(value: new AdminAcceptedResponse());
    }

    internal static async Task<IResult> PostIndexAsync(
        IndexPageRequest request,
        IAdminTaskQueue queue,
        IOptions<BookStackApiClientOptions> options,
        CancellationToken ct)
    {
        var error = ValidatePageUrl(request.Url, options.Value.BaseUrl);
        if (error is not null)
        {
            return Results.BadRequest(new AdminErrorResponse(error));
        }

        await queue.EnqueueAsync(new AdminTask(AdminTaskKind.IndexPage, request.Url), ct).ConfigureAwait(false);
        return Results.Accepted(value: new AdminAcceptedResponse());
    }

    internal static string? ValidatePageUrl(string? url, string? bookStackBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "url is required";
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "url must be an absolute URI";
        }

        if (uri.Scheme is not "http" and not "https")
        {
            return "url scheme must be http or https";
        }

        if (!string.IsNullOrEmpty(bookStackBaseUrl)
            && Uri.TryCreate(bookStackBaseUrl, UriKind.Absolute, out var baseUri))
        {
            if (!uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
                || uri.Port != baseUri.Port)
            {
                return $"url host must match the configured BookStack base URL ({baseUri.Host}:{baseUri.Port})";
            }
        }

        return null;
    }
}
