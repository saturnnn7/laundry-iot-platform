using Laundry.BuildingBlocks.Application;

namespace Laundry.Tests.Integration;

public sealed record TestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAtUtc, string Note)
    : IIntegrationEvent;
