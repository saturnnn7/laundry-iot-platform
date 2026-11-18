using System.Collections.Concurrent;
using Laundry.BuildingBlocks.Application;

namespace Laundry.Tests.Integration;

public sealed class RecordingHandler : IIntegrationEventHandler<TestIntegrationEvent>
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public int CallCount(Guid eventId) => EntryFor(eventId).Calls;

    public void FailFirstCalls(Guid eventId, int count) => EntryFor(eventId).FailuresLeft = count;

    public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var entry = EntryFor(integrationEvent.EventId);
        entry.RegisterCall();

        if (entry.TryConsumeFailure())
        {
            throw new InvalidOperationException("Simulated handler failure.");
        }

        entry.Succeeded.TrySetResult();
        return Task.CompletedTask;
    }

    public async Task WaitForAsync(Guid eventId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            await EntryFor(eventId).Succeeded.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Handler did not succeed for event {eventId} within {timeout}.");
        }
    }

    private Entry EntryFor(Guid eventId) => _entries.GetOrAdd(eventId, _ => new Entry());

    private sealed class Entry
    {
        private readonly object _gate = new();
        private int _calls;
        private int _failuresLeft;

        public TaskCompletionSource Succeeded { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls
        {
            get
            {
                lock (_gate)
                {
                    return _calls;
                }
            }
        }

        public int FailuresLeft
        {
            set
            {
                lock (_gate)
                {
                    _failuresLeft = value;
                }
            }
        }

        public void RegisterCall()
        {
            lock (_gate)
            {
                _calls++;
            }
        }

        public bool TryConsumeFailure()
        {
            lock (_gate)
            {
                if (_failuresLeft <= 0)
                {
                    return false;
                }

                _failuresLeft--;
                return true;
            }
        }
    }
}
