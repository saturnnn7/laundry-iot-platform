using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Laundry.BuildingBlocks.Infrastructure.Persistence;

public abstract class ModuleDbContext : DbContext, IUnitOfWork, IModuleDbContext
{
    private readonly ModuleDbContextCore _core;

    protected ModuleDbContext(DbContextOptions options, ModuleContextServices services)
        : base(options)
    {
        _core = new ModuleDbContextCore(this, services);
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    string IModuleDbContext.SchemaName => Schema;

    protected abstract string Schema { get; }

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) =>
        _core.AddIntegrationEvent(integrationEvent);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw ModuleDbContextCore.SynchronousSaveNotSupported();

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        _core.SaveChangesAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken), cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModuleDbContextCore.ConfigureModel(modelBuilder, Schema);

        base.OnModelCreating(modelBuilder);
    }
}
