using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<AuditLogEntry>> ListAuditLogAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "audit-log" + BuildQueryString(query);
        return SendAsync<ListResponse<AuditLogEntry>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }
}
