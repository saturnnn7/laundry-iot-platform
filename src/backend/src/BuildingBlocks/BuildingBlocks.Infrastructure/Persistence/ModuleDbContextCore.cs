using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;
using Laundry.BuildingBlocks.Infrastructure.Events;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Laundry.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// The shared per-module database behaviour, for a DbContext to compose: schema and outbox mapping,
/// staging of integration events and dispatch of domain events after a successful save.
/// </summary>
public sealed class ModuleDbContextCore
{
    private readonly DbContext _context;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    public ModuleDbContextCore(DbContext context, ModuleContextServices services)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        _context = context;
        _dispatcher = services.DomainEventDispatcher;
        _timeProvider = services.TimeProvider;
    }

    // Call from OnModelCreating before base.OnModelCreating.
    public static void ConfigureModel(ModelBuilder modelBuilder, string schemaName)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(schemaName);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }

    public static NotSupportedException SynchronousSaveNotSupported() => new("Use SaveChangesAsync.");

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) =>
        _context.Set<OutboxMessage>().Add(OutboxMessage.Create(integrationEvent, _timeProvider.GetUtcNow()));

    // The context passes its own save (base.SaveChangesAsync) as saveAsync.
    public async Task<int> SaveChangesAsync(Func<Task<int>> saveAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(saveAsync);

        var aggregates = _context.ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();
        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        var result = await saveAsync();

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        await _dispatcher.DispatchAsync(domainEvents, cancellationToken);

        return result;
    }
}
