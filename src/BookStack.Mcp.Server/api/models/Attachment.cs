namespace BookStack.Mcp.Server.Api.Models;

public sealed class AttachmentLinks
{
    public string Html { get; set; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
}

public sealed class Attachment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public int UploadedTo { get; set; }
    public bool External { get; set; }
    public int Order { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int UpdatedBy { get; set; }
    public AttachmentLinks Links { get; set; } = new();
}
