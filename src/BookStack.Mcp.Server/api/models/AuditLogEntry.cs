namespace BookStack.Mcp.Server.Api.Models;

public sealed class AuditLogEntry
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string Ip { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public User User { get; set; } = new();
}
