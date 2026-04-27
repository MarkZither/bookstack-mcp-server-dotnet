namespace BookStack.Mcp.Server.Data.Abstractions;

public sealed class VectorPageEntry
{
    public int PageId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
    public string ContentHash { get; init; } = string.Empty;
}
