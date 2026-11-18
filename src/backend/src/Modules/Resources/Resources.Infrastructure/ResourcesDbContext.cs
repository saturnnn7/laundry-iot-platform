using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Laundry.Resources.Application;
using Microsoft.EntityFrameworkCore;

namespace Laundry.Resources.Infrastructure;

public sealed class ResourcesDbContext(
    DbContextOptions<ResourcesDbContext> options,
    ModuleContextServices services) : ModuleDbContext(options, services), IResourcesUnitOfWork
{
    public const string SchemaName = "resources";

    protected override string Schema => SchemaName;
}
