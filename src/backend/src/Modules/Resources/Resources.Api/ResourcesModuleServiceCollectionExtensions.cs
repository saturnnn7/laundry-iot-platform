using Laundry.BuildingBlocks.Api;
using Laundry.Resources.Api.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class ResourcesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddResourcesModule(this IServiceCollection services, string? connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddResourcesApplication();
        services.AddResourcesInfrastructure(connectionString);
        services.AddEndpoints(typeof(PingEndpoint).Assembly);

        return services;
    }
}
