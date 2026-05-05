namespace BookStack.Mcp.Server.Admin;

internal sealed record AdminStatusResponse(int TotalPages, string? LastSyncTime, int PendingCount);

internal sealed record AdminAcceptedResponse(string Status = "accepted");

internal sealed record AdminErrorResponse(string Error);

internal sealed record IndexPageRequest(string? Url);
