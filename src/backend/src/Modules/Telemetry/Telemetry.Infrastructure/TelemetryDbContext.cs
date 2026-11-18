using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Laundry.Telemetry.Application;
using Microsoft.EntityFrameworkCore;

namespace Laundry.Telemetry.Infrastructure;

public sealed class TelemetryDbContext(
    DbContextOptions<TelemetryDbContext> options,
    ModuleContextServices services) : ModuleDbContext(options, services), ITelemetryUnitOfWork
{
    public const string SchemaName = "telemetry";

    protected override string Schema => SchemaName;
}
