namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public static class OutboxRetryPolicy
{
    public static TimeSpan Delay(int attempts, TimeSpan max)
    {
        var seconds = Math.Min(Math.Pow(2, attempts), max.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
