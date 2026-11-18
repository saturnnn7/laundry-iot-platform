using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;

namespace Laundry.Tests.Unit;

public sealed record SampleDomainEvent(string Name) : IDomainEvent;

public sealed record SampleIntegrationEvent(Guid EventId, DateTimeOffset OccurredAtUtc, string Name) : IIntegrationEvent;
