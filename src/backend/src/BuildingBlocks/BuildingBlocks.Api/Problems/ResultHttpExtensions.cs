using System.Text.Json;
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
            type: error.Type,
            extensions: FieldExtensions(error));
    }

    private static Dictionary<string, object?>? FieldExtensions(Error error)
    {
        if (error.Fields is not { Count: > 0 } fields)
        {
            return null;
        }

        // Field names are camelCase, as in the request JSON. Names that collapse to the same key are merged.
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (field, messages) in fields)
        {
            var name = CamelCase(field);
            errors[name] = errors.TryGetValue(name, out var existing) ? [.. existing, .. messages] : [.. messages];
        }

        return new Dictionary<string, object?> { ["errors"] = errors };
    }

    private static string CamelCase(string path) =>
        string.Join('.', path.Split('.').Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment)));

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
