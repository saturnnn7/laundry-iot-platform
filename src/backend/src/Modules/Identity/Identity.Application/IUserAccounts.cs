using Laundry.BuildingBlocks.Application;
using Laundry.Identity.Domain;

namespace Laundry.Identity.Application;

public interface IUserAccounts
{
    Task<Result<UserAccount>> RegisterAsync(
        string email,
        string password,
        string displayName,
        AccountType accountType,
        CancellationToken cancellationToken = default);

    // Wrong password and unknown email give the same unauthorized error; a locked account gives too-many-attempts.
    Task<Result<UserAccount>> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result<UserAccount>> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
