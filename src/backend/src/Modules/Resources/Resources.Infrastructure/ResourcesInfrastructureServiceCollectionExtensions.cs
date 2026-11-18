using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.Resources.Application;
using Laundry.Resources.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class ResourcesInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddResourcesInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<ResourcesDbContext>(connectionString, ResourcesDbContext.SchemaName);
        services.AddScoped<IResourcesUnitOfWork>(sp => sp.GetRequiredService<ResourcesDbContext>());

        return services;
    }
}
