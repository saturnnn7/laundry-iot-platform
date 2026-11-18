using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.Maintenance.Application;
using Laundry.Maintenance.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class MaintenanceInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<MaintenanceDbContext>(connectionString, MaintenanceDbContext.SchemaName);
        services.AddScoped<IMaintenanceUnitOfWork>(sp => sp.GetRequiredService<MaintenanceDbContext>());

        return services;
    }
}
