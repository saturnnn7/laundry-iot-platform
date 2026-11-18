using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Laundry.BuildingBlocks.Infrastructure.DependencyInjection;

public static class ModuleMigrationExtensions
{
    private static readonly Action<ILogger, string, Exception?> LogApplying =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, "ApplyingMigrations"), "Applying migrations for {ContextName}");

    public static async Task MigrateModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Laundry.ModuleMigrations");

        foreach (var descriptor in services.GetServices<ModuleContextDescriptor>())
        {
            await using var scope = services.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(descriptor.ContextType);

            LogApplying(logger, descriptor.ContextType.Name, null);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
