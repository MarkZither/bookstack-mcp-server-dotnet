using BookStack.Mcp.Server.Api.Models;

namespace BookStack.Mcp.Server.Api;

public sealed partial class BookStackApiClient
{
    public Task<ListResponse<User>> ListUsersAsync(
        ListQueryParams? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = "users" + BuildQueryString(query);
        return SendAsync<ListResponse<User>>(JsonRequest(HttpMethod.Get, url), cancellationToken);
    }

    public Task<User> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<User>(JsonRequest(HttpMethod.Post, "users", request), cancellationToken);
    }

    public Task<UserWithRoles> GetUserAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<UserWithRoles>(JsonRequest(HttpMethod.Get, $"users/{id}"), cancellationToken);
    }

    public Task<User> UpdateUserAsync(
        int id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<User>(JsonRequest(HttpMethod.Put, $"users/{id}", request), cancellationToken);
    }

    public async Task DeleteUserAsync(
        int id,
        int? migrateOwnershipId = null,
        CancellationToken cancellationToken = default)
    {
        if (migrateOwnershipId.HasValue)
        {
            var body = new { migrate_ownership_id = migrateOwnershipId.Value };
            await SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"users/{id}", body), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await SendNoContentAsync(JsonRequest(HttpMethod.Delete, $"users/{id}"), cancellationToken).ConfigureAwait(false);
        }
    }
}
