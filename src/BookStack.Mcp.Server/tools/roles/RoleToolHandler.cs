using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Roles;

// [McpServerToolType] — hidden until #9 is implemented
internal sealed class RoleToolHandler(IBookStackApiClient client, ILogger<RoleToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<RoleToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_roles_list"), Description("List all roles in BookStack")]
    public Task<string> ListRolesAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_roles_read"), Description("Get a role by ID")]
    public Task<string> ReadRoleAsync(
        [Description("The role ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_roles_create"), Description("Create a new role")]
    public Task<string> CreateRoleAsync(
        [Description("The role display name")] string name, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_roles_update"), Description("Update an existing role")]
    public Task<string> UpdateRoleAsync(
        [Description("The role ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");

    [McpServerTool(Name = "bookstack_roles_delete"), Description("Delete a role by ID")]
    public Task<string> DeleteRoleAsync(
        [Description("The role ID")] int id, CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #16");
}
