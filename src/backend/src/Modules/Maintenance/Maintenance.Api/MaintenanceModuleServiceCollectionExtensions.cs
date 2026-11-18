using Laundry.BuildingBlocks.Api;
using Laundry.Maintenance.Api.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class MaintenanceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceModule(this IServiceCollection services, string? connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddMaintenanceApplication();
        services.AddMaintenanceInfrastructure(connectionString);
        services.AddEndpoints(typeof(PingEndpoint).Assembly);

        return services;
    }
}
