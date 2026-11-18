using Laundry.Resources.Application.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class ResourcesApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddResourcesApplication(this IServiceCollection services)
    {
        services.AddScoped<PingHandler>();

        return services;
    }
}
