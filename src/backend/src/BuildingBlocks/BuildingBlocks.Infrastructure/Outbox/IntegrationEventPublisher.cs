using System.Collections.Concurrent;
using Laundry.BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public sealed class IntegrationEventPublisher
{
    private readonly ConcurrentDictionary<Type, Invoker> _invokers = new();

    public async Task PublishAsync(
        OutboxMessage message,
        IServiceProvider scopedProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(scopedProvider);

        var integrationEvent = message.Deserialize();
        var invoker = _invokers.GetOrAdd(integrationEvent.GetType(), CreateInvoker);

        await invoker.InvokeAsync(scopedProvider, integrationEvent, cancellationToken);
    }

    private static Invoker CreateInvoker(Type eventType) =>
        (Invoker)Activator.CreateInstance(typeof(Invoker<>).MakeGenericType(eventType))!;

    private abstract class Invoker
    {
        public abstract Task InvokeAsync(
            IServiceProvider serviceProvider,
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken);
    }

    private sealed class Invoker<TEvent> : Invoker
        where TEvent : IIntegrationEvent
    {
        public override async Task InvokeAsync(
            IServiceProvider serviceProvider,
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            var typed = (TEvent)integrationEvent;
            List<Exception>? failures = null;

            foreach (var handler in serviceProvider.GetServices<IIntegrationEventHandler<TEvent>>())
            {
                try
                {
                    await handler.HandleAsync(typed, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failures ??= [];
                    failures.Add(exception);
                }
            }

            if (failures is not null)
            {
                throw new AggregateException(failures);
            }
        }
    }
}
