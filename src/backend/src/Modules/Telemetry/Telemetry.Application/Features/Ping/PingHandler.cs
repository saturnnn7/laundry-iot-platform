using Laundry.BuildingBlocks.Application;

namespace Laundry.Telemetry.Application.Features.Ping;

public sealed class PingHandler(TimeProvider timeProvider)
{
    public Task<Result<PingResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var response = new PingResponse("Telemetry", timeProvider.GetUtcNow());

        return Task.FromResult(Result.Success(response));
    }
}
