using Laundry.BuildingBlocks.Application;
using Laundry.Identity.Domain;

namespace Laundry.Identity.Application.Features.Auth.Register;

public static class RegisterValidator
{
    public static ValidationErrors Validate(RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new ValidationErrors();

        ValidateEmail(request.Email, errors);

        if (string.IsNullOrEmpty(request.Password))
        {
            errors.Add("password", "Password is required.");
        }
        else if (request.Password.Length > AuthLimits.PasswordMaxLength)
        {
            errors.Add("password", $"Password must be at most {AuthLimits.PasswordMaxLength} characters.");
        }

        ValidateDisplayName(request.DisplayName, errors);

        if (ParseAccountType(request.AccountType) is null)
        {
            errors.Add("accountType", "Account type must be Resident or Admin.");
        }

        return errors;
    }

    // Only the exact names are accepted, not other letter case and not numbers.
    public static AccountType? ParseAccountType(string? value) => value switch
    {
        nameof(AccountType.Resident) => AccountType.Resident,
        nameof(AccountType.Admin) => AccountType.Admin,
        _ => null,
    };

    private static void ValidateEmail(string? value, ValidationErrors errors)
    {
        var email = value?.Trim();

        if (string.IsNullOrEmpty(email))
        {
            errors.Add("email", "Email is required.");
            return;
        }

        if (email.Length > AuthLimits.EmailMaxLength)
        {
            errors.Add("email", $"Email must be at most {AuthLimits.EmailMaxLength} characters.");
            return;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        var valid = !email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
            && at > 0
            && at == email.LastIndexOf('@')
            && email[(at + 1)..].Contains('.', StringComparison.Ordinal);

        if (!valid)
        {
            errors.Add("email", "Email is not valid.");
        }
    }

    private static void ValidateDisplayName(string? value, ValidationErrors errors)
    {
        var name = value?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            errors.Add("displayName", "Display name is required.");
        }
        else if (name.Length > AuthLimits.DisplayNameMaxLength)
        {
            errors.Add("displayName", $"Display name must be at most {AuthLimits.DisplayNameMaxLength} characters.");
        }
        else if (name.Any(char.IsControl))
        {
            errors.Add("displayName", "Display name must not contain control characters.");
        }
    }
}
