using Laundry.BuildingBlocks.Domain;

namespace Laundry.BuildingBlocks.Application;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
