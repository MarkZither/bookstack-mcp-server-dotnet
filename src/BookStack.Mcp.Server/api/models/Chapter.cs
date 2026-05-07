namespace BookStack.Mcp.Server.Api.Models;

public class Chapter
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int UpdatedBy { get; set; }
    public int OwnedBy { get; set; }
    public IReadOnlyList<Tag> Tags { get; set; } = [];
}

public sealed class ChapterWithPages : Chapter
{
    public IReadOnlyList<Page> Pages { get; set; } = [];
}
