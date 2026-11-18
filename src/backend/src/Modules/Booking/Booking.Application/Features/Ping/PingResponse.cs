namespace Laundry.Booking.Application.Features.Ping;

public sealed record PingResponse(string Module, DateTimeOffset UtcNow);
