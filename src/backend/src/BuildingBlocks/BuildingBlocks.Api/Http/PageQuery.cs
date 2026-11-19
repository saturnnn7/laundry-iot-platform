using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Application.Paging;
using Microsoft.AspNetCore.Mvc;

namespace Laundry.BuildingBlocks.Api.Http;

// Query-string binding for list endpoints: take it with [AsParameters].
public sealed record PageQuery(
    [FromQuery(Name = "cursor")] string? Cursor = null,
    [FromQuery(Name = "limit")] int? Limit = null)
{
    public Result<PageRequest> ToRequest() => PageRequest.Create(Cursor, Limit);
}
