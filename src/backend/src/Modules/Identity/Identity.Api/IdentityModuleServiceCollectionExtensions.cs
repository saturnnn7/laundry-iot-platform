using Laundry.BuildingBlocks.Api;
using Laundry.Identity.Api;
using Laundry.Identity.Application;
using Laundry.Identity.Api.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string? connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(connectionString);
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddEndpoints(typeof(PingEndpoint).Assembly);

        return services;
    }
}
