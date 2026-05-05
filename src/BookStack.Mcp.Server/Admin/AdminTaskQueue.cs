using System.Threading.Channels;

namespace BookStack.Mcp.Server.Admin;

internal sealed class AdminTaskQueue : IAdminTaskQueue
{
    private readonly Channel<AdminTask> _channel =
        Channel.CreateUnbounded<AdminTask>();

    public int PendingCount => _channel.Reader.Count;

    public ValueTask EnqueueAsync(AdminTask task, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(task, cancellationToken);

    public ValueTask<AdminTask> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
