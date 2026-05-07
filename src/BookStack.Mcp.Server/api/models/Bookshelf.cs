namespace BookStack.Mcp.Server.Api.Models;

public class Bookshelf
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int UpdatedBy { get; set; }
    public int OwnedBy { get; set; }
    public int? ImageId { get; set; }
    public IReadOnlyList<Tag> Tags { get; set; } = [];
    public Image? Cover { get; set; }
}

public sealed class BookshelfWithBooks : Bookshelf
{
    public new UserSummary? CreatedBy { get; set; }
    public new UserSummary? UpdatedBy { get; set; }
    public new UserSummary? OwnedBy { get; set; }
    public IReadOnlyList<Book> Books { get; set; } = [];
}
