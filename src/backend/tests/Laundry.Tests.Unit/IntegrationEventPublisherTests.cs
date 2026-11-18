using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Laundry.Tests.Unit;

public class IntegrationEventPublisherTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private sealed class Recorder(List<string> calls, string name) : IIntegrationEventHandler<SampleIntegrationEvent>
    {
        public Task HandleAsync(SampleIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            calls.Add($"{name}:{integrationEvent.Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class Failing(string message) : IIntegrationEventHandler<SampleIntegrationEvent>
    {
        public Task HandleAsync(SampleIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message);
    }

    private static OutboxMessage NewMessage() =>
        OutboxMessage.Create(new SampleIntegrationEvent(Guid.NewGuid(), Now, "x"), Now);

    [Fact]
    public async Task CallsAllHandlersInOrder()
    {
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<SampleIntegrationEvent>>(new Recorder(calls, "one"));
        services.AddSingleton<IIntegrationEventHandler<SampleIntegrationEvent>>(new Recorder(calls, "two"));

        await new IntegrationEventPublisher().PublishAsync(
            NewMessage(), services.BuildServiceProvider(), TestContext.Current.CancellationToken);

        Assert.Equal(["one:x", "two:x"], calls);
    }

    [Fact]
    public async Task FailingHandlerDoesNotStopOthersAndIsReported()
    {
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<SampleIntegrationEvent>>(new Failing("boom"));
        services.AddSingleton<IIntegrationEventHandler<SampleIntegrationEvent>>(new Recorder(calls, "after"));

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            new IntegrationEventPublisher().PublishAsync(
                NewMessage(), services.BuildServiceProvider(), TestContext.Current.CancellationToken));

        Assert.Equal(["after:x"], calls);
        var inner = Assert.Single(exception.InnerExceptions);
        Assert.Equal("boom", inner.Message);
    }

    [Fact]
    public async Task NoHandlersIsSuccess()
    {
        await new IntegrationEventPublisher().PublishAsync(
            NewMessage(), new ServiceCollection().BuildServiceProvider(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnknownTypeThrows()
    {
        var message = NewMessage();
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.Type))!.SetValue(message, "No.Such.Type, Nowhere");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new IntegrationEventPublisher().PublishAsync(
                message, new ServiceCollection().BuildServiceProvider(), TestContext.Current.CancellationToken));
    }
}
