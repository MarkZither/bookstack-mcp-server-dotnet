using BookStack.Mcp.Server.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookStack.Mcp.Server.Admin;

internal sealed class AdminIndexWorkerService(
    IAdminTaskQueue queue,
    VectorIndexSyncService syncService,
    ILogger<AdminIndexWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            AdminTask task;
            try
            {
                task = await queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                switch (task.Kind)
                {
                    case AdminTaskKind.FullSync:
                        await syncService.RunFullSyncAsync(stoppingToken).ConfigureAwait(false);
                        break;
                    case AdminTaskKind.IndexPage when task.PageUrl is not null:
                        await syncService
                            .SyncPageByUrlAsync(task.PageUrl, stoppingToken)
                            .ConfigureAwait(false);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Admin task {Kind} failed.", task.Kind);
            }
        }
    }
}
