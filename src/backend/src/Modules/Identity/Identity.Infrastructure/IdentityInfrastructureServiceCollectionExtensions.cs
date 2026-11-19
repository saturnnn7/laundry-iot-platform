using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.Identity.Application;
using Laundry.Identity.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<IdentityDbContext>(connectionString, IdentityDbContext.SchemaName);
        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());

        // Core only: no cookies, no roles, no sign-in manager.
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<IdentityDbContext>();

        services.AddScoped<IUserAccounts, UserAccounts>();

        return services;
    }
}
