namespace BookStack.Mcp.Server.Admin;

internal sealed record AdminTask(AdminTaskKind Kind, string? PageUrl = null);

internal enum AdminTaskKind
{
    FullSync,
    IndexPage,
}
