using Laundry.Identity.Application.Features.Auth.Login;
using Laundry.Identity.Application.Features.Auth.Register;
using Laundry.Identity.Application.Features.Me;
using Laundry.Identity.Application.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<PingHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GetMeHandler>();

        return services;
    }
}
