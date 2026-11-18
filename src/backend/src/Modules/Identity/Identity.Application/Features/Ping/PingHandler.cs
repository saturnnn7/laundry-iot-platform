using Laundry.BuildingBlocks.Application;

namespace Laundry.Identity.Application.Features.Ping;

public sealed class PingHandler(TimeProvider timeProvider)
{
    public Task<Result<PingResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var response = new PingResponse("Identity", timeProvider.GetUtcNow());

        return Task.FromResult(Result.Success(response));
    }
}
