using Laundry.Maintenance.Application.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class MaintenanceApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceApplication(this IServiceCollection services)
    {
        services.AddScoped<PingHandler>();

        return services;
    }
}
