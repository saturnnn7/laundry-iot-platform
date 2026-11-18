using Laundry.BuildingBlocks.Api;
using Laundry.Maintenance.Application.Features.Ping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Maintenance.Api.Features.Ping;

public sealed class PingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/maintenance/ping", HandleAsync)
            .AllowAnonymous()
            .WithTags("Maintenance")
            .WithName("MaintenancePing");

    private static async Task<IResult> HandleAsync(PingHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return Results.Ok(result.Value);
    }
}
