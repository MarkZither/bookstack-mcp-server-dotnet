using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Users;

// [McpServerToolType] — hidden until #9 is implemented
internal sealed class UserToolHandler(IBookStackApiClient client, ILogger<UserToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<UserToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_users_list"), Description("List all users in BookStack")]
    public Task<string> ListUsersAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_users_read"), Description("Get a user by ID")]
    public Task<string> ReadUserAsync(
        [Description("The user ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_users_create"), Description("Create a new user")]
    public Task<string> CreateUserAsync(
        [Description("The user display name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_users_update"), Description("Update an existing user")]
    public Task<string> UpdateUserAsync(
        [Description("The user ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_users_delete"), Description("Delete a user by ID")]
    public Task<string> DeleteUserAsync(
        [Description("The user ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");
}
