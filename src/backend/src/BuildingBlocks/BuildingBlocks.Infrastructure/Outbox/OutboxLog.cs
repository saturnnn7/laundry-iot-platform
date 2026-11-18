using Microsoft.Extensions.Logging;

namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

internal static partial class OutboxLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed for {Module}")]
    public static partial void BatchFailed(ILogger logger, Exception exception, string module);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} in {Module} is dead after {Attempts} attempts")]
    public static partial void MessageDead(ILogger logger, Exception exception, Guid messageId, string module, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} in {Module} failed, attempt {Attempts}")]
    public static partial void MessageFailed(ILogger logger, Exception exception, Guid messageId, string module, int attempts);
}
