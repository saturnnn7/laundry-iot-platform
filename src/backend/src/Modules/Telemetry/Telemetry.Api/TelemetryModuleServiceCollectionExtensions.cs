using Laundry.BuildingBlocks.Api;
using Laundry.Telemetry.Api.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class TelemetryModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTelemetryModule(this IServiceCollection services, string? connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddTelemetryApplication();
        services.AddTelemetryInfrastructure(connectionString);
        services.AddEndpoints(typeof(PingEndpoint).Assembly);

        return services;
    }
}
