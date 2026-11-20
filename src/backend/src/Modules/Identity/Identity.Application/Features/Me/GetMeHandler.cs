using Laundry.BuildingBlocks.Application;

namespace Laundry.Identity.Application.Features.Me;

public sealed class GetMeHandler(IUserAccounts accounts)
{
    public async Task<Result<MeResponse>> HandleAsync(GetMeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await accounts.FindByIdAsync(query.UserId, cancellationToken);
        if (result.IsFailure)
        {
            // A valid token for a user that no longer exists is not a signed-in caller.
            return result.Error.Type == ProblemTypes.NotFound
                ? new Error(ProblemTypes.Unauthorized, "Unauthorized", "The account no longer exists.")
                : result.Error;
        }

        var account = result.Value;

        // Memberships arrive with locations; the shape is fixed now.
        return new MeResponse(account.Id, account.Email, account.DisplayName, account.AccountType.ToString(), []);
    }
}
