namespace BookStack.Mcp.Server.Api.Models;

public sealed class ListResponse<T>
{
    public int Total { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public IReadOnlyList<T> Data { get; set; } = [];
}
