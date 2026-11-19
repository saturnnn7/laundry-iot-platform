using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Domain;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Laundry.Tests.Integration.Composed;

public sealed record ProbeRaised(int Id) : IDomainEvent;

public sealed class ProbeItem : AggregateRoot<int>
{
    public ProbeItem(int id)
        : base(id)
    {
    }

    public void Touch() => Raise(new ProbeRaised(Id));
}

// Derives from DbContext directly and composes the shared module behaviour.
public sealed class ComposedDbContext : DbContext, IUnitOfWork, IModuleDbContext
{
    public const string SchemaName = "composed";

    private readonly ModuleDbContextCore _core;

    public ComposedDbContext(DbContextOptions<ComposedDbContext> options, ModuleContextServices services)
        : base(options)
    {
        _core = new ModuleDbContextCore(this, services);
    }

    string IModuleDbContext.SchemaName => SchemaName;

    public DbSet<ProbeItem> Items => Set<ProbeItem>();

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
        ModuleDbContextCore.ConfigureModel(modelBuilder, SchemaName);

        modelBuilder.Entity<ProbeItem>(builder =>
        {
            builder.ToTable("probe_items");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
        });

        base.OnModelCreating(modelBuilder);
    }
}
