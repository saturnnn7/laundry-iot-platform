using Laundry.BuildingBlocks.Application;
using Laundry.Identity.Application;
using Laundry.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Laundry.Identity.Infrastructure;

internal sealed partial class UserAccounts(
    UserManager<AppUser> userManager,
    IPasswordHasher<AppUser> passwordHasher,
    IdentityDbContext context,
    TimeProvider timeProvider,
    ILogger<UserAccounts> logger) : IUserAccounts
{
    // A valid hash of a random value. Checked for unknown emails so the response time does not reveal the account.
    private const string DummyHash = "AQAAAAEAAYagAAAAEGKL9/AF5RRwXV6MS+Rl/jHxv66QUDwCKO8zSNa+2w75hUR4Jk3a43HeJEMlUshc0g==";

    private const string PostgresUniqueViolation = "23505";

    private static readonly AppUser DummyUser = new();

    private static readonly Error InvalidCredentials =
        new(ProblemTypes.Unauthorized, "Invalid credentials", "The email or password is incorrect.");

    private static readonly Error LockedOut =
        new(ProblemTypes.TooManyAttempts, "Too many attempts", "The account is temporarily locked. Try again later.");

    public async Task<Result<UserAccount>> RegisterAsync(
        string email,
        string password,
        string displayName,
        AccountType accountType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            AccountType = accountType,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // A concurrent request registered the same email between the validation and the insert.
            context.ChangeTracker.Clear();
            LogRegistrationRejected(logger, "RaceDuplicate");
            return Error.Validation(DuplicateEmailFields());
        }

        if (!result.Succeeded)
        {
            var codes = string.Join(',', result.Errors.Select(e => e.Code));
            LogRegistrationRejected(logger, codes);
            return MapRegistrationErrors(result.Errors);
        }

        return ToAccount(user);
    }

    public async Task<Result<UserAccount>> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(DummyUser, DummyHash, password);
            return InvalidCredentials;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            LogLockedOut(logger, user.Id);
            return LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return ToAccount(user);
    }

    public async Task<Result<UserAccount>> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        return user is null
            ? new Error(ProblemTypes.NotFound, "User not found")
            : ToAccount(user);
    }

    private static UserAccount ToAccount(AppUser user) =>
        new(user.Id, user.Email!, user.DisplayName, user.AccountType);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresUniqueViolation };

    private static Dictionary<string, IReadOnlyList<string>> DuplicateEmailFields() =>
        new(StringComparer.Ordinal) { ["email"] = ["This email is already registered."] };

    private static Error MapRegistrationErrors(IEnumerable<IdentityError> errors)
    {
        var fields = new ValidationErrors();

        foreach (var error in errors)
        {
            switch (error.Code)
            {
                case nameof(IdentityErrorDescriber.DuplicateEmail):
                case nameof(IdentityErrorDescriber.DuplicateUserName):
                    fields.Add("email", "This email is already registered.");
                    break;
                case nameof(IdentityErrorDescriber.InvalidEmail):
                case nameof(IdentityErrorDescriber.InvalidUserName):
                    fields.Add("email", "The email is not valid.");
                    break;
                case var code when code.StartsWith("Password", StringComparison.Ordinal):
                    fields.Add("password", error.Description);
                    break;
                default:
                    fields.Add("email", "The account could not be created.");
                    break;
            }
        }

        return fields.Build()!;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registration rejected: {Codes}")]
    private static partial void LogRegistrationRejected(ILogger logger, string codes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in refused, account {UserId} is locked out")]
    private static partial void LogLockedOut(ILogger logger, Guid userId);
}
