using Laundry.BuildingBlocks.Api;
using Laundry.Telemetry.Application.Features.Ping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Telemetry.Api.Features.Ping;

public sealed class PingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/telemetry/ping", HandleAsync)
            .AllowAnonymous()
            .WithTags("Telemetry")
            .WithName("TelemetryPing");

    private static async Task<IResult> HandleAsync(PingHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return Results.Ok(result.Value);
    }
}
