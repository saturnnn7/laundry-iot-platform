using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.Identity.Application;
using Laundry.Identity.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<IdentityDbContext>(connectionString, IdentityDbContext.SchemaName);
        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());

        return services;
    }
}
