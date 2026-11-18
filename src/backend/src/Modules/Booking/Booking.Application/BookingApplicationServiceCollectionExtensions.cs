using Laundry.Booking.Application.Features.Ping;

namespace Microsoft.Extensions.DependencyInjection;

public static class BookingApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBookingApplication(this IServiceCollection services)
    {
        services.AddScoped<PingHandler>();

        return services;
    }
}
