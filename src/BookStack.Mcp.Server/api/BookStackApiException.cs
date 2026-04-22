namespace BookStack.Mcp.Server.Api;

public sealed class BookStackApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    public BookStackApiException(int statusCode, string? errorMessage, string? errorCode)
        : base(BuildMessage(statusCode, errorMessage))
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    private static string BuildMessage(int statusCode, string? errorMessage) => statusCode switch
    {
        401 => "Authentication failed (HTTP 401) — check that bookstack.tokenId and bookstack.tokenSecret are correct and the token has not expired.",
        403 => "Permission denied (HTTP 403) — the API token does not have access to this resource.",
        404 => $"Resource not found (HTTP 404){(errorMessage is null ? "." : $": {errorMessage}")}",
        422 => $"Validation error (HTTP 422){(errorMessage is null ? "." : $": {errorMessage}")}",
        _   => $"BookStack API error {statusCode}{(errorMessage is null ? "." : $": {errorMessage}")}",
    };
}
