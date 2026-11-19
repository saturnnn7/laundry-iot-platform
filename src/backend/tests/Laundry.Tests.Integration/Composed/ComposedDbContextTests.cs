using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Laundry.Tests.Integration.Composed;

// Runs a context that does not derive from ModuleDbContext through the same module plumbing.
[Collection(PostgresCollection.Name)]
public sealed class ComposedDbContextTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan NegativeWait = TimeSpan.FromSeconds(1.5);

    private readonly RecordingHandler _integrationHandler = new();
    private readonly RecordingDomainHandler _domainHandler = new();
    private ServiceProvider _provider = null!;
    private IHostedService[] _hosted = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<BuildingBlocks.Infrastructure.Outbox.OutboxOptions>(options =>
        {
            options.PollInterval = TimeSpan.FromMilliseconds(100);
            options.MaxRetryDelay = TimeSpan.FromSeconds(1);
        });
        services.AddModuleDbContext<ComposedDbContext>(fixture.ConnectionString, ComposedDbContext.SchemaName);

        // The migration is written by hand, so there is no model snapshot to compare with.
        services.ConfigureDbContext<ComposedDbContext>(options =>
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(_integrationHandler);
        services.AddSingleton<IDomainEventHandler<ProbeRaised>>(_domainHandler);

        _provider = services.BuildServiceProvider();

        await using (var scope = _provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ComposedDbContext>().Database.MigrateAsync(Ct);
        }

        _hosted = [.. _provider.GetServices<IHostedService>()];
        foreach (var service in _hosted)
        {
            await service.StartAsync(Ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var service in _hosted)
        {
            await service.StopAsync(CancellationToken.None);
        }

        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ObjectsLiveInTheContextSchemaWithItsOwnHistoryTable()
    {
        var found = new HashSet<(string Schema, string Table)>();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "select table_schema, table_name from information_schema.tables " +
            "where table_name in ('outbox_messages', '__EFMigrationsHistory', 'probe_items') " +
            "and table_schema = 'composed'",
            connection);
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                found.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        Assert.Contains(("composed", "outbox_messages"), found);
        Assert.Contains(("composed", "probe_items"), found);
        Assert.Contains(("composed", "__EFMigrationsHistory"), found);
    }

    [Fact]
    public async Task StagedIntegrationEventIsStoredAndDeliveredByThePoller()
    {
        var integrationEvent = NewEvent();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ComposedDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            db.AddIntegrationEvent(integrationEvent);
            await db.SaveChangesAsync(Ct);

            Assert.Equal(1, await CountRowsAsync(integrationEvent.EventId, db));

            await Task.Delay(NegativeWait, Ct);
            Assert.Equal(0, _integrationHandler.CallCount(integrationEvent.EventId));

            await transaction.CommitAsync(Ct);
        }

        await _integrationHandler.WaitForAsync(integrationEvent.EventId, Deadline, Ct);
    }

    [Fact]
    public async Task RolledBackIntegrationEventIsNeverDelivered()
    {
        var integrationEvent = NewEvent();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ComposedDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            db.AddIntegrationEvent(integrationEvent);
            await db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        await Task.Delay(NegativeWait, Ct);

        Assert.Equal(0, _integrationHandler.CallCount(integrationEvent.EventId));
    }

    [Fact]
    public async Task DomainEventsAreDispatchedOnlyAfterASuccessfulSave()
    {
        var id = NextId();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ComposedDbContext>();
            var item = new ProbeItem(id);
            item.Touch();
            db.Items.Add(item);

            Assert.Empty(_domainHandler.Handled);

            await db.SaveChangesAsync(Ct);

            Assert.Equal([new ProbeRaised(id)], _domainHandler.Handled);
            Assert.Empty(item.DomainEvents);
        }

        // Saving the same key again fails, so nothing more is dispatched.
        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ComposedDbContext>();
            var duplicate = new ProbeItem(id);
            duplicate.Touch();
            db.Items.Add(duplicate);

            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync(Ct));
        }

        Assert.Equal([new ProbeRaised(id)], _domainHandler.Handled);
    }

    [Fact]
    public async Task DomainEventHandlerErrorIsLoggedAndNotRethrown()
    {
        _domainHandler.FailNext = true;

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ComposedDbContext>();
        var item = new ProbeItem(NextId());
        item.Touch();
        db.Items.Add(item);

        var saved = await db.SaveChangesAsync(Ct);

        Assert.Equal(1, saved);
    }

    [Fact]
    public async Task SynchronousSaveIsNotSupported()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ComposedDbContext>();

        Assert.Throws<NotSupportedException>(() => db.SaveChanges());
    }

    private static int _nextId = Environment.TickCount & 0x3FFFFFFF;

    private static int NextId() => Interlocked.Increment(ref _nextId);

    private static TestIntegrationEvent NewEvent() => new(Guid.NewGuid(), DateTimeOffset.UtcNow, "composed");

    private static Task<int> CountRowsAsync(Guid eventId, ComposedDbContext db) =>
        db.Set<BuildingBlocks.Infrastructure.Outbox.OutboxMessage>()
            .AsNoTracking()
            .CountAsync(m => m.Id == eventId, Ct);

    private sealed class RecordingDomainHandler : IDomainEventHandler<ProbeRaised>
    {
        private readonly List<ProbeRaised> _handled = [];

        public bool FailNext { get; set; }

        public IReadOnlyList<ProbeRaised> Handled
        {
            get
            {
                lock (_handled)
                {
                    return [.. _handled];
                }
            }
        }

        public Task HandleAsync(ProbeRaised domainEvent, CancellationToken cancellationToken)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("Simulated domain handler failure.");
            }

            lock (_handled)
            {
                _handled.Add(domainEvent);
            }

            return Task.CompletedTask;
        }
    }
}
