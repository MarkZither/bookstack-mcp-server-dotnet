namespace BookStack.Mcp.Server.Admin;

internal interface IAdminTaskQueue
{
    int PendingCount { get; }
    ValueTask EnqueueAsync(AdminTask task, CancellationToken cancellationToken = default);
    ValueTask<AdminTask> DequeueAsync(CancellationToken cancellationToken);
}
