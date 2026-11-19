using Laundry.BuildingBlocks.Application;

namespace Laundry.Identity.Application.Features.Auth.Login;

// Only presence and length are checked, so the answer gives no hint about the account format.
public static class LoginValidator
{
    public static ValidationErrors Validate(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("email", "Email is required.");
        }
        else if (request.Email.Length > AuthLimits.EmailMaxLength)
        {
            errors.Add("email", $"Email must be at most {AuthLimits.EmailMaxLength} characters.");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors.Add("password", "Password is required.");
        }
        else if (request.Password.Length > AuthLimits.PasswordMaxLength)
        {
            errors.Add("password", $"Password must be at most {AuthLimits.PasswordMaxLength} characters.");
        }

        return errors;
    }
}
