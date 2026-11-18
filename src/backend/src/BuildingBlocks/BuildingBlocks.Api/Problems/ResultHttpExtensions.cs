using Laundry.BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;

namespace Laundry.BuildingBlocks.Api.Problems;

public static class ResultHttpExtensions
{
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Results.Problem(
            detail: error.Detail,
            statusCode: ProblemTypeStatus.For(error.Type),
            title: error.Title,
            type: error.Type);
    }

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess() : result.Error.ToProblem();
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
    }
}
