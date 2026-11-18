using Laundry.BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;

namespace Laundry.BuildingBlocks.Api.Problems;

public static class ProblemTypeStatus
{
    private static readonly Dictionary<string, int> Table = new(StringComparer.Ordinal)
    {
        [ProblemTypes.Validation] = StatusCodes.Status400BadRequest,
        [ProblemTypes.Unauthorized] = StatusCodes.Status401Unauthorized,
        [ProblemTypes.Forbidden] = StatusCodes.Status403Forbidden,
        [ProblemTypes.NotFound] = StatusCodes.Status404NotFound,
        [ProblemTypes.NotMember] = StatusCodes.Status403Forbidden,
        [ProblemTypes.InviteInvalid] = StatusCodes.Status400BadRequest,
        [ProblemTypes.SlotConflict] = StatusCodes.Status409Conflict,
        [ProblemTypes.LeadTime] = StatusCodes.Status422UnprocessableEntity,
        [ProblemTypes.Horizon] = StatusCodes.Status422UnprocessableEntity,
        [ProblemTypes.MachineUnavailable] = StatusCodes.Status409Conflict,
        [ProblemTypes.MachineBusy] = StatusCodes.Status409Conflict,
        [ProblemTypes.HoldLimit] = StatusCodes.Status409Conflict,
        [ProblemTypes.CodeInvalid] = StatusCodes.Status400BadRequest,
        [ProblemTypes.CodeExpired] = StatusCodes.Status400BadRequest,
        [ProblemTypes.TooManyAttempts] = StatusCodes.Status429TooManyRequests,
        [ProblemTypes.ProgramNotFit] = StatusCodes.Status409Conflict,
        [ProblemTypes.CommandFailed] = StatusCodes.Status502BadGateway,
        [ProblemTypes.IdempotencyConflict] = StatusCodes.Status422UnprocessableEntity,
    };

    public static IReadOnlyCollection<string> Types => Table.Keys;

    public static int For(string type) =>
        type is not null && Table.TryGetValue(type, out var status)
            ? status
            : StatusCodes.Status500InternalServerError;
}
