namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
}
