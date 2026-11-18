using Laundry.BuildingBlocks.Api;
using Laundry.Booking.Api.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class BookingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services, string? connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddBookingApplication();
        services.AddBookingInfrastructure(connectionString);
        services.AddEndpoints(typeof(PingEndpoint).Assembly);

        return services;
    }
}
