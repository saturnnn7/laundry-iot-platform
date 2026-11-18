using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;
using Laundry.BuildingBlocks.Infrastructure.Events;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Laundry.Tests.Unit;

public class ModuleDbContextTests
{
    private sealed class Sample(int id) : AggregateRoot<int>(id)
    {
        public void Touch(string name) => Raise(new SampleDomainEvent(name));
    }

    private sealed class TestContext(DbContextOptions<TestContext> options, ModuleContextServices services)
        : ModuleDbContext(options, services)
    {
        public DbSet<Sample> Samples => Set<Sample>();

        protected override string Schema => "test";
    }

    private sealed class CapturingDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Dispatched { get; } = [];

        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            Dispatched.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }

    private static TestContext CreateContext(string database, CapturingDispatcher dispatcher)
    {
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(database)
            .Options;
        return new TestContext(options, new ModuleContextServices(dispatcher, TimeProvider.System));
    }

    [Fact]
    public async Task DispatchesEventsAfterSaveAndClearsThem()
    {
        var dispatcher = new CapturingDispatcher();
        await using var context = CreateContext(Guid.NewGuid().ToString(), dispatcher);
        var sample = new Sample(1);
        sample.Touch("a");
        context.Samples.Add(sample);

        await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([new SampleDomainEvent("a")], dispatcher.Dispatched);
        Assert.Empty(sample.DomainEvents);
    }

    [Fact]
    public async Task DispatchesNothingWhenSaveFails()
    {
        var database = Guid.NewGuid().ToString();
        var dispatcher = new CapturingDispatcher();
        await using (var first = CreateContext(database, dispatcher))
        {
            first.Samples.Add(new Sample(1));
            await first.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(database, dispatcher);
        var duplicate = new Sample(1);
        duplicate.Touch("a");
        context.Samples.Add(duplicate);

        await Assert.ThrowsAnyAsync<Exception>(
            () => context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken));

        Assert.Empty(dispatcher.Dispatched);
        Assert.Single(duplicate.DomainEvents);
    }

    [Fact]
    public async Task AddIntegrationEventStoresOneOutboxRowOnSave()
    {
        var database = Guid.NewGuid().ToString();
        var dispatcher = new CapturingDispatcher();
        var integrationEvent = new SampleIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "a");
        await using (var context = CreateContext(database, dispatcher))
        {
            context.AddIntegrationEvent(integrationEvent);
            await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        }

        await using var reader = CreateContext(database, dispatcher);
        var rows = await reader.OutboxMessages.ToListAsync(Xunit.TestContext.Current.CancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal(integrationEvent.EventId, row.Id);
    }

    [Fact]
    public void SynchronousSaveIsNotSupported()
    {
        using var context = CreateContext(Guid.NewGuid().ToString(), new CapturingDispatcher());

        Assert.Throws<NotSupportedException>(() => context.SaveChanges());
    }
}
