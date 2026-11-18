using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Xunit;

namespace Laundry.Tests.Unit;

public class OutboxRetryPolicyTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void DelayDoublesEachAttempt(int attempts, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), OutboxRetryPolicy.Delay(attempts, TimeSpan.FromMinutes(5)));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(30)]
    [InlineData(5000)]
    public void DelayIsCapped(int attempts)
    {
        var max = TimeSpan.FromMinutes(5);

        Assert.Equal(max, OutboxRetryPolicy.Delay(attempts, max));
    }
}
