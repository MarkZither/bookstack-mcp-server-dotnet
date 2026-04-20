using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<Role>> ListRolesAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "roles" + BuildQueryString(query);
        return SendAsync<ListResponse<Role>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<Role> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Role>(JsonRequest(HttpMethod.Post, "roles", request), cancellationToken);
    }

    public Task<RoleWithPermissions> GetRoleAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<RoleWithPermissions>(JsonRequest(HttpMethod.Get, $"roles/{id}"), cancellationToken);
    }

    public Task<Role> UpdateRoleAsync(
        int id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<Role>(JsonRequest(HttpMethod.Put, $"roles/{id}", request), cancellationToken);
    }

    public Task DeleteRoleAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"roles/{id}"), cancellationToken);
    }
}
