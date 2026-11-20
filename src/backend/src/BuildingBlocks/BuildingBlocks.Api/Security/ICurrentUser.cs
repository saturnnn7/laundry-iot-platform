namespace Laundry.BuildingBlocks.Api.Security;

// The caller of the current request, read from the JWT claims (sub and acct).
public interface ICurrentUser
{
    // True when the token carries a sub claim that is a valid Guid.
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    // Resident or Admin, or null when the claim is missing.
    string? AccountType { get; }
}
