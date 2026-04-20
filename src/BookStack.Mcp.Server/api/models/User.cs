namespace BookStack.Mcp.Server.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? ExternalAuthId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
}

public sealed class UserWithRoles : User
{
    public IReadOnlyList<Role> Roles { get; set; } = [];
}
