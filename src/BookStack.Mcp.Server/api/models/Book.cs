namespace BookStack.Mcp.Server.Api.Models;

public class Book
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionHtml { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public UserSummary? CreatedBy { get; set; }
    public UserSummary? UpdatedBy { get; set; }
    public UserSummary? OwnedBy { get; set; }
    public int? ImageId { get; set; }
    public int? DefaultTemplateId { get; set; }
    public IReadOnlyList<Tag> Tags { get; set; } = [];
    public Image? Cover { get; set; }
}

public sealed class BookWithContents : Book
{
    public IReadOnlyList<object> Contents { get; set; } = [];
}
