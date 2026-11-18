using Laundry.BuildingBlocks.Application;

namespace Laundry.Maintenance.Application.Features.Ping;

public sealed class PingHandler(TimeProvider timeProvider)
{
    public Task<Result<PingResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var response = new PingResponse("Maintenance", timeProvider.GetUtcNow());

        return Task.FromResult(Result.Success(response));
    }
}
