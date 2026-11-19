using Laundry.Identity.Domain;

namespace Laundry.Identity.Application;

public sealed record UserAccount(Guid Id, string Email, string DisplayName, AccountType AccountType);
