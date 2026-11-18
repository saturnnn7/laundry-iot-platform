using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;
using Laundry.BuildingBlocks.Infrastructure.Events;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Laundry.Tests.Unit;

public class OutboxProcessorTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 2, 3, 0, 0, TimeSpan.Zero);

    private sealed class ProcContext(DbContextOptions options, ModuleContextServices services)
        : ModuleDbContext(options, services)
    {
        protected override string Schema => "proc";
    }

    private sealed class NoopDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class Probe
    {
        public List<string> Handled { get; } = [];

        public List<ProcContext> HandlerContexts { get; } = [];

        public List<ProcContext> CreatedContexts { get; } = [];

        public bool Fail { get; set; }
    }

    private sealed class ProbeHandler(Probe probe, ProcContext context) : IIntegrationEventHandler<SampleIntegrationEvent>
    {
        public Task HandleAsync(SampleIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            probe.Handled.Add(integrationEvent.Name);
            probe.HandlerContexts.Add(context);
            if (probe.Fail)
            {
                throw new InvalidOperationException("handler failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public Fixture(OutboxOptions? options = null)
        {
            Options = options ?? new OutboxOptions();
            Time = new ManualTimeProvider(Start);

            var dbOptions = new DbContextOptionsBuilder<ProcContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var services = new ServiceCollection();
            services.AddSingleton(Probe);
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton(new ModuleContextServices(new NoopDispatcher(), Time));
            services.AddScoped(sp =>
            {
                var context = new ProcContext(dbOptions, sp.GetRequiredService<ModuleContextServices>());
                Probe.CreatedContexts.Add(context);
                return context;
            });
            services.AddScoped<IIntegrationEventHandler<SampleIntegrationEvent>, ProbeHandler>();
            Provider = services.BuildServiceProvider();

            Processor = new OutboxProcessor<ProcContext>(
                Provider.GetRequiredService<IServiceScopeFactory>(),
                new IntegrationEventPublisher(),
                Microsoft.Extensions.Options.Options.Create(Options),
                Time,
                NullLogger<OutboxProcessor<ProcContext>>.Instance);

            Db = new ProcContext(dbOptions, Provider.GetRequiredService<ModuleContextServices>());
        }

        public OutboxOptions Options { get; }

        public ManualTimeProvider Time { get; }

        public Probe Probe { get; } = new();

        public ServiceProvider Provider { get; }

        public OutboxProcessor<ProcContext> Processor { get; }

        public ProcContext Db { get; }

        public async Task SeedAsync(string name, DateTimeOffset occurredAt)
        {
            Db.OutboxMessages.Add(OutboxMessage.Create(new SampleIntegrationEvent(Guid.NewGuid(), occurredAt, name), Time.GetUtcNow()));
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public Task<int> RunAsync() => Processor.ProcessPendingAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessesPendingMessagesInOccurredOrder()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync("late", Start.AddMinutes(2));
        await fixture.SeedAsync("early", Start.AddMinutes(1));

        var handled = await fixture.RunAsync();

        Assert.Equal(2, handled);
        Assert.Equal(["early", "late"], fixture.Probe.Handled);
        Assert.All(fixture.Db.OutboxMessages.AsNoTracking(), m => Assert.NotNull(m.ProcessedAtUtc));
    }

    [Fact]
    public async Task SkipsProcessedMessages()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync("a", Start);

        Assert.Equal(1, await fixture.RunAsync());
        Assert.Equal(0, await fixture.RunAsync());
        Assert.Single(fixture.Probe.Handled);
    }

    [Fact]
    public async Task FailedMessageIsRetriedOnlyAfterItsNextAttemptTime()
    {
        var fixture = new Fixture();
        fixture.Probe.Fail = true;
        await fixture.SeedAsync("a", Start);

        Assert.Equal(1, await fixture.RunAsync());
        Assert.Equal(0, await fixture.RunAsync());

        fixture.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await fixture.RunAsync());
        Assert.Equal(2, fixture.Probe.Handled.Count);

        var row = await fixture.Db.OutboxMessages.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, row.Attempts);
        Assert.Null(row.ProcessedAtUtc);
        Assert.NotNull(row.LastError);
    }

    [Fact]
    public async Task MessageBecomesDeadAtMaxAttemptsAndIsIgnored()
    {
        var fixture = new Fixture(new OutboxOptions { MaxAttempts = 2 });
        fixture.Probe.Fail = true;
        await fixture.SeedAsync("a", Start);

        await fixture.RunAsync();
        fixture.Time.Advance(TimeSpan.FromSeconds(2));
        await fixture.RunAsync();

        var row = await fixture.Db.OutboxMessages.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(row.DeadAtUtc);

        fixture.Time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, await fixture.RunAsync());
        Assert.Equal(2, fixture.Probe.Handled.Count);
    }

    [Fact]
    public async Task HandlersRunInADifferentScopeThanTheProcessor()
    {
        var fixture = new Fixture();
        await fixture.SeedAsync("a", Start);
        await fixture.SeedAsync("b", Start.AddSeconds(1));
        fixture.Probe.CreatedContexts.Clear();

        await fixture.RunAsync();

        var processorContext = fixture.Probe.CreatedContexts[0];
        Assert.Equal(2, fixture.Probe.HandlerContexts.Count);
        Assert.All(fixture.Probe.HandlerContexts, c => Assert.NotSame(processorContext, c));
        Assert.NotSame(fixture.Probe.HandlerContexts[0], fixture.Probe.HandlerContexts[1]);
    }
}
