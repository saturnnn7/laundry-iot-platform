using System.Collections.Concurrent;
using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Laundry.BuildingBlocks.Infrastructure.Events;

public sealed partial class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, Invoker> Invokers = new();

    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            var invoker = Invokers.GetOrAdd(domainEvent.GetType(), CreateInvoker);
            await invoker.InvokeAsync(serviceProvider, domainEvent, logger, cancellationToken);
        }
    }

    private static Invoker CreateInvoker(Type eventType) =>
        (Invoker)Activator.CreateInstance(typeof(Invoker<>).MakeGenericType(eventType))!;

    [LoggerMessage(Level = LogLevel.Error, Message = "Domain event handler failed for {EventType}")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception, string eventType);

    private abstract class Invoker
    {
        public abstract Task InvokeAsync(
            IServiceProvider serviceProvider,
            IDomainEvent domainEvent,
            ILogger logger,
            CancellationToken cancellationToken);
    }

    private sealed class Invoker<TEvent> : Invoker
        where TEvent : IDomainEvent
    {
        public override async Task InvokeAsync(
            IServiceProvider serviceProvider,
            IDomainEvent domainEvent,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var typed = (TEvent)domainEvent;

            foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
            {
                try
                {
                    await handler.HandleAsync(typed, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogHandlerFailed(logger, exception, typeof(TEvent).FullName ?? typeof(TEvent).Name);
                }
            }
        }
    }
}
