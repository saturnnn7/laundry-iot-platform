using Laundry.BuildingBlocks.Infrastructure.Outbox;

namespace Laundry.Host;

internal static class ModuleRegistry
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Default");

        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));

        // modules:begin
        services.AddIdentityModule(connectionString);
        services.AddResourcesModule(connectionString);
        services.AddTelemetryModule(connectionString);
        services.AddBookingModule(connectionString);
        services.AddMaintenanceModule(connectionString);
        // modules:end

        return services;
    }
}
