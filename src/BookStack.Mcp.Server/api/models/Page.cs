namespace BookStack.Mcp.Server.Api.Models;

public class Page
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool Draft { get; set; }
    public bool Template { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int UpdatedBy { get; set; }
    public int OwnedBy { get; set; }
    public int RevisionCount { get; set; }
    public string Editor { get; set; } = string.Empty;
    public IReadOnlyList<Tag> Tags { get; set; } = [];
}

public sealed class PageWithContent : Page
{
    public new UserSummary? CreatedBy { get; set; }
    public new UserSummary? UpdatedBy { get; set; }
    public new UserSummary? OwnedBy { get; set; }
    public string Html { get; set; } = string.Empty;
    public string RawHtml { get; set; } = string.Empty;
    public string? Markdown { get; set; }
}
