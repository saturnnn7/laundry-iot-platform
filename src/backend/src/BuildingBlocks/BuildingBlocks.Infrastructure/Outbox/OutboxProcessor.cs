using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IntegrationEventPublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    private static readonly string Module = typeof(TContext).Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessPendingAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    OutboxLog.BatchFailed(logger, exception, Module);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown requested.
        }
    }

    internal async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var batchScope = scopeFactory.CreateScope();
        var context = batchScope.ServiceProvider.GetRequiredService<TContext>();

        var now = timeProvider.GetUtcNow();
        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.DeadAtUtc == null && m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await HandleAsync(context, message, settings, cancellationToken);
        }

        return messages.Count;
    }

    private async Task HandleAsync(
        TContext context,
        OutboxMessage message,
        OutboxOptions settings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var handlerScope = scopeFactory.CreateScope();
            await publisher.PublishAsync(message, handlerScope.ServiceProvider, cancellationToken);

            message.MarkProcessed(timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            message.MarkFailed(exception.Message, timeProvider.GetUtcNow(), settings);

            if (message.DeadAtUtc is not null)
            {
                OutboxLog.MessageDead(logger, exception, message.Id, Module, message.Attempts);
            }
            else
            {
                OutboxLog.MessageFailed(logger, exception, message.Id, Module, message.Attempts);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
