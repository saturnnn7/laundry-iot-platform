using Laundry.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Laundry.Identity.Infrastructure;

// A persistence model owned by ASP.NET Core Identity, not a rich aggregate.
public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string DisplayName { get; set; } = string.Empty;

    public AccountType AccountType { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
