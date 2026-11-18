namespace Laundry.Host;

internal static class ModuleRegistry
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Default");

        // modules:begin
        // modules:end

        return services;
    }
}
