using Laundry.BuildingBlocks.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Laundry.Tests.Integration;

public sealed class SampleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet("/sample", () => "sample");
}
