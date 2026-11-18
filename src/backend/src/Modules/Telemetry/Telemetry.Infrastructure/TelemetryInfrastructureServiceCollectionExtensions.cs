using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.Telemetry.Application;
using Laundry.Telemetry.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class TelemetryInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTelemetryInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<TelemetryDbContext>(connectionString, TelemetryDbContext.SchemaName);
        services.AddScoped<ITelemetryUnitOfWork>(sp => sp.GetRequiredService<TelemetryDbContext>());

        return services;
    }
}
