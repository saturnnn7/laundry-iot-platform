using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Xunit;

namespace Laundry.Tests.Unit;

public class OutboxMessageStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static OutboxMessage NewMessage() =>
        OutboxMessage.Create(new SampleIntegrationEvent(Guid.NewGuid(), Now, "a"), Now);

    [Fact]
    public void MarkProcessedSetsTimestamp()
    {
        var message = NewMessage();

        message.MarkProcessed(Now.AddSeconds(1));

        Assert.Equal(Now.AddSeconds(1), message.ProcessedAtUtc);
        Assert.Null(message.DeadAtUtc);
    }

    [Fact]
    public void MarkFailedCountsAttemptAndBacksOff()
    {
        var message = NewMessage();
        var options = new OutboxOptions();

        message.MarkFailed("first", Now, options);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("first", message.LastError);
        Assert.Equal(Now.AddSeconds(2), message.NextAttemptAtUtc);

        message.MarkFailed("second", Now, options);
        Assert.Equal(2, message.Attempts);
        Assert.Equal(Now.AddSeconds(4), message.NextAttemptAtUtc);
        Assert.Null(message.DeadAtUtc);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public void MarkFailedTruncatesError()
    {
        var message = NewMessage();

        message.MarkFailed(new string('x', 5000), Now, new OutboxOptions());

        Assert.Equal(2000, message.LastError!.Length);
    }

    [Fact]
    public void MarkFailedMakesMessageDeadAtMaxAttempts()
    {
        var message = NewMessage();
        var options = new OutboxOptions { MaxAttempts = 2 };

        message.MarkFailed("e", Now, options);
        Assert.Null(message.DeadAtUtc);

        message.MarkFailed("e", Now.AddSeconds(5), options);
        Assert.Equal(Now.AddSeconds(5), message.DeadAtUtc);
    }
}
