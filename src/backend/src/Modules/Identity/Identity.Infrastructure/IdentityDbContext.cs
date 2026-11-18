using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Laundry.Identity.Application;
using Microsoft.EntityFrameworkCore;

namespace Laundry.Identity.Infrastructure;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    ModuleContextServices services) : ModuleDbContext(options, services), IIdentityUnitOfWork
{
    public const string SchemaName = "identity";

    protected override string Schema => SchemaName;
}
