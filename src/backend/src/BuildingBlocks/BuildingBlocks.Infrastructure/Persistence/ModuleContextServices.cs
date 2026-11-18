using Laundry.BuildingBlocks.Infrastructure.Events;

namespace Laundry.BuildingBlocks.Infrastructure.Persistence;

public sealed record ModuleContextServices(IDomainEventDispatcher DomainEventDispatcher, TimeProvider TimeProvider);
