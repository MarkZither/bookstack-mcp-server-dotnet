namespace BookStack.Mcp.Server.Data.Abstractions;

public sealed class VectorSearchResult
{
    public int PageId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public float Score { get; init; }
}
