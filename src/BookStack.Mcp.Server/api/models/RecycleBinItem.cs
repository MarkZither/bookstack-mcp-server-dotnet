using System.Text.Json;

namespace BookStack.Mcp.Server.Api.Models;

public sealed class RecycleBinItem
{
    public int Id { get; set; }
    public DateTimeOffset DeletedAt { get; set; }
    public string DeletableType { get; set; } = string.Empty;
    public int DeletableId { get; set; }
    public int DeletedBy { get; set; }
    public JsonElement Deletable { get; set; }
}
