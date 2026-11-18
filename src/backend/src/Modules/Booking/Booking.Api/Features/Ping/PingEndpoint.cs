using Laundry.BuildingBlocks.Api;
using Laundry.Booking.Application.Features.Ping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Booking.Api.Features.Ping;

public sealed class PingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/booking/ping", HandleAsync)
            .AllowAnonymous()
            .WithTags("Booking")
            .WithName("BookingPing");

    private static async Task<IResult> HandleAsync(PingHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return Results.Ok(result.Value);
    }
}
