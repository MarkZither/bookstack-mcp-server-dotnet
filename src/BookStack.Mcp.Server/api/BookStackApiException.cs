namespace BookStack.Mcp.Server.Api;

public sealed class BookStackApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    public BookStackApiException(int statusCode, string? errorMessage, string? errorCode)
        : base($"BookStack API error {statusCode}: {errorMessage}")
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }
}
