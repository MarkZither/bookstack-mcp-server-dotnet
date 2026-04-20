namespace BookStack.Mcp.Server.Api.Models;

public class Role
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool MfaEnforced { get; set; }
    public string? ExternalAuthId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RoleWithPermissions : Role
{
    public IReadOnlyList<string> Permissions { get; set; } = [];
}
