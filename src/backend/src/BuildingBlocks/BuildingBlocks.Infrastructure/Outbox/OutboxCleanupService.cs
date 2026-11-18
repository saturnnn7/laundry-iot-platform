using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxCleanupService<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxCleanupService<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    internal const int DefaultBatchSize = 1000;

    private static readonly string Module = typeof(TContext).Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            OutboxLog.CleanupDisabled(logger, Module);
            return;
        }

        using var timer = new PeriodicTimer(options.Value.CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(DefaultBatchSize, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    OutboxLog.CleanupFailed(logger, exception, Module);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown requested.
        }
    }

    // One pass: delete processed rows older than the retention period, batch by batch, and return the total.
    internal async Task<int> RunOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var cutoff = timeProvider.GetUtcNow() - options.Value.RetentionPeriod;
        var total = 0;
        int deleted;

        do
        {
            var batch = context.OutboxMessages
                .Where(m => m.ProcessedAtUtc != null && m.ProcessedAtUtc < cutoff && m.DeadAtUtc == null)
                .OrderBy(m => m.ProcessedAtUtc)
                .Select(m => m.Id)
                .Take(batchSize);

            deleted = await context.OutboxMessages
                .Where(m => batch.Contains(m.Id))
                .ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted > 0);

        if (total > 0)
        {
            OutboxLog.CleanupDeleted(logger, total, Module);
        }

        return total;
    }
}
