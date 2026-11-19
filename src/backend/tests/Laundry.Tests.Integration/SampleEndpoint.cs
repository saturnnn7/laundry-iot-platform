using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Api.Problems;
using Laundry.BuildingBlocks.Api.Http;
using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Application.Paging;
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

        app.MapGet("/fail/validation-fields", () =>
        {
            var errors = new ValidationErrors()
                .Add("Name", "Name is required.")
                .Add("Name", "Name is too long.")
                .Add("startsAt", "Start must be in the future.");

            return Result.Failure(errors.Build()!).ToHttpResult(() => Results.Ok());
        }).AllowAnonymous();

        app.MapGet("/fail/validation-plain", () =>
            Result.Failure(new Error(ProblemTypes.Validation, "Validation failed", "Nothing to show."))
                .ToHttpResult(() => Results.Ok())).AllowAnonymous();

        app.MapPost("/body/echo", (BodySample body) => Results.Ok(body)).AllowAnonymous();

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

public sealed record BodySample(string Name, int Count);

public sealed class ConnectionInfoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/connection-info", (HttpContext context) => new ConnectionInfo(
            context.Request.Scheme,
            context.Connection.RemoteIpAddress?.ToString())).AllowAnonymous();
}

public sealed record ConnectionInfo(string Scheme, string? RemoteIp);

public sealed class PageSampleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/page-sample", ([AsParameters] PageQuery query) =>
            query.ToRequest().ToHttpResult(request => Results.Ok(new Page<string>([], null)))).AllowAnonymous();
}
