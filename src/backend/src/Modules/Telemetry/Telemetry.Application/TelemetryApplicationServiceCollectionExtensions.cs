using Laundry.Telemetry.Application.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class TelemetryApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTelemetryApplication(this IServiceCollection services)
    {
        services.AddScoped<PingHandler>();

        return services;
    }
}
