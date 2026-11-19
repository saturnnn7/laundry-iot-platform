using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Laundry.Identity.Application;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Laundry.Identity.Infrastructure;

public sealed class IdentityDbContext : IdentityUserContext<AppUser, Guid>, IIdentityUnitOfWork, IModuleDbContext
{
    public const string SchemaName = "identity";

    private readonly ModuleDbContextCore _core;

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ModuleContextServices services)
        : base(options)
    {
        _core = new ModuleDbContextCore(this, services);
    }

    string IModuleDbContext.SchemaName => SchemaName;

    public void AddIntegrationEvent(IIntegrationEvent integrationEvent) =>
        _core.AddIntegrationEvent(integrationEvent);

    // SaveChanges() and SaveChangesAsync(ct) call these two.
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw ModuleDbContextCore.SynchronousSaveNotSupported();

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        _core.SaveChangesAsync(() => base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken), cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ModuleDbContextCore.ConfigureModel(builder, SchemaName);

        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new AppUserConfiguration());
        IdentityModelNaming.Apply(builder);
    }
}
