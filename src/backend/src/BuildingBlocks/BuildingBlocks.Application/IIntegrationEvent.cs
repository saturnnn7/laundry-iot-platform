namespace Laundry.BuildingBlocks.Application;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
