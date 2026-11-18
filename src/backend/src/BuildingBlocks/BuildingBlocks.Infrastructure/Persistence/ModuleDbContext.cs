using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;
using Laundry.BuildingBlocks.Infrastructure.Events;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Laundry.BuildingBlocks.Infrastructure.Persistence;

public abstract class ModuleDbContext : DbContext, IUnitOfWork
{
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    protected ModuleDbContext(DbContextOptions options, ModuleContextServices services)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(services);

        _dispatcher = services.DomainEventDispatcher;
        _timeProvider = services.TimeProvider;
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected abstract string Schema { get; }

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) =>
        OutboxMessages.Add(OutboxMessage.Create(integrationEvent, _timeProvider.GetUtcNow()));

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync.");

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();
        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        await _dispatcher.DispatchAsync(domainEvents, cancellationToken);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
