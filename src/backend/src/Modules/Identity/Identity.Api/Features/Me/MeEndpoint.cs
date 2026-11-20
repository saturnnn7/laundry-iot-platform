using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Api.Problems;
using Laundry.BuildingBlocks.Api.Security;
using Laundry.BuildingBlocks.Application;
using Laundry.Identity.Application.Features.Me;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Identity.Api.Features.Me;

public sealed class MeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me", HandleAsync)
            .WithTags("Identity")
            .WithName("Me")
            .WithSummary("The signed-in user")
            .WithDescription(
                "Profile of the caller. memberships lists the Locations the user belongs to "
                + "(locationId, locationName, role); it is empty until Locations are available.")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(
        ICurrentUser currentUser,
        GetMeHandler handler,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return new Error(ProblemTypes.Unauthorized, "Unauthorized", "The token has no valid subject.").ToProblem();
        }

        var result = await handler.HandleAsync(new GetMeQuery(userId), cancellationToken);

        return result.ToHttpResult(response => Results.Ok(response));
    }
}
