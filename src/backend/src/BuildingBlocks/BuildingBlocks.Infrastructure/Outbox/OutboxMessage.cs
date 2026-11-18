using System.Text.Json;
using Laundry.BuildingBlocks.Application;

namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    private const int MaxErrorLength = 2000;

    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? DeadAtUtc { get; private set; }

    public static OutboxMessage Create(IIntegrationEvent integrationEvent, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();

        return new OutboxMessage
        {
            Id = integrationEvent.EventId,
            Type = $"{eventType.FullName}, {eventType.Assembly.GetName().Name}",
            Payload = JsonSerializer.Serialize(integrationEvent, eventType),
            OccurredAtUtc = integrationEvent.OccurredAtUtc.ToUniversalTime(),
            NextAttemptAtUtc = utcNow.ToUniversalTime(),
        };
    }

    public void MarkProcessed(DateTimeOffset utcNow)
    {
        ProcessedAtUtc = utcNow.ToUniversalTime();
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset utcNow, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(options);

        var now = utcNow.ToUniversalTime();

        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        NextAttemptAtUtc = now + OutboxRetryPolicy.Delay(Attempts, options.MaxRetryDelay);

        if (Attempts >= options.MaxAttempts)
        {
            DeadAtUtc = now;
        }
    }

    public IIntegrationEvent Deserialize()
    {
        var eventType = System.Type.GetType(Type, throwOnError: false);
        if (eventType is null)
        {
            throw new InvalidOperationException($"Cannot resolve outbox event type '{Type}'.");
        }

        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
        {
            throw new InvalidOperationException($"Type '{Type}' is not an integration event.");
        }

        return (IIntegrationEvent?)JsonSerializer.Deserialize(Payload, eventType)
            ?? throw new InvalidOperationException($"Outbox payload of '{Type}' is empty.");
    }
}
