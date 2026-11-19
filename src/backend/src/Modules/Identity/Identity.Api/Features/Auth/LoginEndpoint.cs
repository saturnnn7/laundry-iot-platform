using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Api.Problems;
using Laundry.Identity.Application.Features.Auth.Login;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Identity.Api.Features.Auth;

public sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/auth/login", HandleAsync)
            .AllowAnonymous()
            .WithTags("Identity")
            .WithName("Login")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        LoginHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // Responses of this route must not be cached.
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        var result = await handler.HandleAsync(request, cancellationToken);

        return result.ToHttpResult(response => Results.Ok(response));
    }
}
