using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Laundry.Maintenance.Application;
using Microsoft.EntityFrameworkCore;

namespace Laundry.Maintenance.Infrastructure;

public sealed class MaintenanceDbContext(
    DbContextOptions<MaintenanceDbContext> options,
    ModuleContextServices services) : ModuleDbContext(options, services), IMaintenanceUnitOfWork
{
    public const string SchemaName = "maintenance";

    protected override string Schema => SchemaName;
}
