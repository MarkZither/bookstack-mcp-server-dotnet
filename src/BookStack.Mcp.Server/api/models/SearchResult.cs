namespace BookStack.Mcp.Server.Api.Models;

public sealed class SearchResultPreviewHtml
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class SearchResultItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public SearchResultPreviewHtml PreviewHtml { get; set; } = new();
    public IReadOnlyList<Tag> Tags { get; set; } = [];
    public Book? Book { get; set; }
    public Chapter? Chapter { get; set; }
}

public sealed class SearchResult
{
    public int Total { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public IReadOnlyList<SearchResultItem> Data { get; set; } = [];
}
