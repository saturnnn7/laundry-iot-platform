using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;
using Laundry.BuildingBlocks.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Laundry.Tests.Unit;

public class DomainEventDispatcherTests
{
    private sealed class RecordingHandler(List<string> calls, string name) : IDomainEventHandler<SampleDomainEvent>
    {
        public Task HandleAsync(SampleDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            calls.Add($"{name}:{domainEvent.Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler : IDomainEventHandler<SampleDomainEvent>
    {
        public Task HandleAsync(SampleDomainEvent domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class FakeLogger : ILogger<DomainEventDispatcher>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Fact]
    public async Task CallsEveryHandlerInOrder()
    {
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<SampleDomainEvent>>(new RecordingHandler(calls, "first"));
        services.AddSingleton<IDomainEventHandler<SampleDomainEvent>>(new RecordingHandler(calls, "second"));
        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider(), new FakeLogger());

        await dispatcher.DispatchAsync([new SampleDomainEvent("x")], TestContext.Current.CancellationToken);

        Assert.Equal(["first:x", "second:x"], calls);
    }

    [Fact]
    public async Task KeepsGoingAfterFailingHandlerAndLogsIt()
    {
        var calls = new List<string>();
        var logger = new FakeLogger();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<SampleDomainEvent>>(new FailingHandler());
        services.AddSingleton<IDomainEventHandler<SampleDomainEvent>>(new RecordingHandler(calls, "after"));
        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider(), logger);

        await dispatcher.DispatchAsync([new SampleDomainEvent("x")], TestContext.Current.CancellationToken);

        Assert.Equal(["after:x"], calls);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(nameof(SampleDomainEvent), entry.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task ToleratesEventWithNoHandler()
    {
        var logger = new FakeLogger();
        var dispatcher = new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider(), logger);

        await dispatcher.DispatchAsync([new SampleDomainEvent("x")], TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }
}
