using Laundry.BuildingBlocks.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Laundry.BuildingBlocks.Api.Problems;

// Malformed JSON, a wrong value type and a missing body surface as BadHttpRequestException.
// The response is a plain validation problem: the exception text can name internal types.
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Type = ProblemTypes.Validation,
                Title = "Validation failed",
                Detail = "The request body is missing or malformed.",
            },
        });
    }
}
