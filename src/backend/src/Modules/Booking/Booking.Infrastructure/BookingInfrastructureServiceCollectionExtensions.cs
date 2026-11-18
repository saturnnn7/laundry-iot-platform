using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.Booking.Application;
using Laundry.Booking.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class BookingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBookingInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<BookingDbContext>(connectionString, BookingDbContext.SchemaName);
        services.AddScoped<IBookingUnitOfWork>(sp => sp.GetRequiredService<BookingDbContext>());

        return services;
    }
}
