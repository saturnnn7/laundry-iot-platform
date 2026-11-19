using Laundry.BuildingBlocks.Application;

namespace Laundry.Identity.Application.Features.Auth.Register;

public sealed class RegisterHandler(IUserAccounts accounts)
{
    public async Task<Result<RegisterResponse>> HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = RegisterValidator.Validate(request);
        if (errors.Build() is { } error)
        {
            return error;
        }

        var accountType = RegisterValidator.ParseAccountType(request.AccountType)!.Value;

        var result = await accounts.RegisterAsync(
            request.Email!.Trim(),
            request.Password!,
            request.DisplayName!.Trim(),
            accountType,
            cancellationToken);

        return result.IsFailure
            ? result.Error
            : new RegisterResponse(
                result.Value.Id,
                result.Value.Email,
                result.Value.DisplayName,
                result.Value.AccountType.ToString());
    }
}
