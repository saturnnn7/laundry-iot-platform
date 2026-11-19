using Laundry.BuildingBlocks.Application;

namespace Laundry.Identity.Application.Features.Auth.Login;

public sealed class LoginHandler(IUserAccounts accounts, IAccessTokenIssuer tokenIssuer)
{
    public const string TokenType = "Bearer";

    public async Task<Result<LoginResponse>> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = LoginValidator.Validate(request);
        if (errors.Build() is { } error)
        {
            return error;
        }

        var result = await accounts.VerifyCredentialsAsync(request.Email!.Trim(), request.Password!, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var token = tokenIssuer.Issue(result.Value);

        return new LoginResponse(token.Value, TokenType, token.ExpiresAtUtc);
    }
}
