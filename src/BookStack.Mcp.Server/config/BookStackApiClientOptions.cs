namespace BookStack.Mcp.Server.Config;

public sealed class BookStackApiClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string TokenId { get; set; } = string.Empty;
    public string TokenSecret { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
