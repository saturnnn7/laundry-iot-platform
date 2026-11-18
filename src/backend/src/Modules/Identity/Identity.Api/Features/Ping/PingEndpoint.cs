using Laundry.BuildingBlocks.Api;
using Laundry.Identity.Application.Features.Ping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Identity.Api.Features.Ping;

public sealed class PingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/identity/ping", HandleAsync)
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("IdentityPing");

    private static async Task<IResult> HandleAsync(PingHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return Results.Ok(result.Value);
    }
}
