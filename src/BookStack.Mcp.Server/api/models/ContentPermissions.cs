namespace BookStack.Mcp.Server.Api.Models;

public sealed class ContentPermissionEntry
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool View { get; set; }
    public bool Create { get; set; }
    public bool Update { get; set; }
    public bool Delete { get; set; }
}

public sealed class ContentPermissions
{
    public bool Inheriting { get; set; }
    public IReadOnlyList<ContentPermissionEntry> Permissions { get; set; } = [];
}
