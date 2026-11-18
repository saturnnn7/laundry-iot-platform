using Laundry.BuildingBlocks.Domain;

namespace Laundry.BuildingBlocks.Infrastructure.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
