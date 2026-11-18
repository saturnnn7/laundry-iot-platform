using Laundry.Identity.Application.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<PingHandler>();

        return services;
    }
}
