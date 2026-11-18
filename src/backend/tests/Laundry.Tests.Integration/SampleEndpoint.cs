using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Api.Problems;
using Laundry.BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Tests.Integration;

public sealed class SampleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/sample", () => "sample").AllowAnonymous();
}

public sealed class SecureSampleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/secure-sample", () => "secure");
}

public sealed class FailureEndpoints : IEndpoint
{
    public const string SlotConflictTitle = "Slot taken";
    public const string SlotConflictDetail = "The slot overlaps another booking.";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/fail/slot-conflict", () =>
            Result.Failure(new Error(ProblemTypes.SlotConflict, SlotConflictTitle, SlotConflictDetail))
                .ToHttpResult(() => Results.Ok())).AllowAnonymous();

        app.MapGet("/fail/unknown", () =>
            Result.Failure<string>(new Error("not-in-the-table", "Unknown", "No status for this type."))
                .ToHttpResult(value => Results.Ok(value))).AllowAnonymous();

        app.MapGet("/fail/throw", () =>
        {
            throw new InvalidOperationException(ThrowMessage);
#pragma warning disable CS0162
            return "unreachable";
#pragma warning restore CS0162
        }).AllowAnonymous();
    }

    public const string ThrowMessage = "secret-failure-message-12345";
}
