using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Xunit;

namespace Laundry.Tests.Unit;

public class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void CreateUsesEventIdAndStartsPending()
    {
        var integrationEvent = new SampleIntegrationEvent(Guid.NewGuid(), Now, "a");

        var message = OutboxMessage.Create(integrationEvent, Now);

        Assert.Equal(integrationEvent.EventId, message.Id);
        Assert.Equal(Now, message.NextAttemptAtUtc);
        Assert.Equal(TimeSpan.Zero, message.OccurredAtUtc.Offset);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Null(message.DeadAtUtc);
        Assert.Equal(0, message.Attempts);
        Assert.StartsWith("Laundry.Tests.Unit.SampleIntegrationEvent, Laundry.Tests.Unit", message.Type, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateNormalizesOffsetsToUtc()
    {
        var local = new DateTimeOffset(2026, 1, 2, 5, 4, 5, TimeSpan.FromHours(2));
        var integrationEvent = new SampleIntegrationEvent(Guid.NewGuid(), local, "a");

        var message = OutboxMessage.Create(integrationEvent, local);

        Assert.Equal(TimeSpan.Zero, message.OccurredAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, message.NextAttemptAtUtc.Offset);
        Assert.Equal(Now, message.OccurredAtUtc);
    }

    [Fact]
    public void DeserializeRoundTrips()
    {
        var integrationEvent = new SampleIntegrationEvent(Guid.NewGuid(), Now, "washer-3");

        var restored = OutboxMessage.Create(integrationEvent, Now).Deserialize();

        Assert.Equal(integrationEvent, restored);
    }

    [Fact]
    public void DeserializeRejectsUnknownType()
    {
        var message = OutboxMessage.Create(new SampleIntegrationEvent(Guid.NewGuid(), Now, "a"), Now);
        SetType(message, "No.Such.Type, Laundry.Tests.Unit");

        Assert.Throws<InvalidOperationException>(message.Deserialize);
    }

    [Fact]
    public void DeserializeRejectsNonIntegrationType()
    {
        var message = OutboxMessage.Create(new SampleIntegrationEvent(Guid.NewGuid(), Now, "a"), Now);
        SetType(message, typeof(SampleDomainEvent).FullName + ", Laundry.Tests.Unit");

        Assert.Throws<InvalidOperationException>(message.Deserialize);
    }

    private static void SetType(OutboxMessage message, string type) =>
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.Type))!.SetValue(message, type);
}
