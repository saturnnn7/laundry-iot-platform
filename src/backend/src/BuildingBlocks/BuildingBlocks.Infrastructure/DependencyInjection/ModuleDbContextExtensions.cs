using Laundry.BuildingBlocks.Infrastructure.Events;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Laundry.BuildingBlocks.Infrastructure.DependencyInjection;

public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string schema)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.TryAddScoped<ModuleContextServices>();
        services.TryAddSingleton<IntegrationEventPublisher>();
        services.AddOptions<OutboxOptions>();

        services.AddDbContext<TContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema)));

        services.AddHostedService<OutboxProcessor<TContext>>();

        return services;
    }
}
