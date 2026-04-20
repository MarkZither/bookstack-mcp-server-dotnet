namespace BookStack.Mcp.Server.Api.Models;

public sealed class Tag
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Order { get; set; }
}
