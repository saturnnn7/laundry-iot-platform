using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Laundry.BuildingBlocks.Api.Problems;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;

            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            if (string.IsNullOrEmpty(problem.Instance))
            {
                problem.Instance = context.HttpContext.Request.Path.Value;
            }

            if (IsDefaultType(problem.Type) && SlugForStatus(problem.Status ?? context.HttpContext.Response.StatusCode) is { } slug)
            {
                problem.Type = slug;
            }
        });

        return services;
    }

    public static WebApplication UseApiProblemDetails(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }

    private static bool IsDefaultType(string? type) =>
        string.IsNullOrEmpty(type)
        || type == "about:blank"
        || type.StartsWith("https://tools.ietf.org/html/rfc", StringComparison.Ordinal);

    private static string? SlugForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "validation",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not-found",
        _ => null,
    };
}
