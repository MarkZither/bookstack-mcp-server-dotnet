namespace BookStack.Mcp.Server.Api.Models;

public sealed class Image
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int UpdatedBy { get; set; }
}
