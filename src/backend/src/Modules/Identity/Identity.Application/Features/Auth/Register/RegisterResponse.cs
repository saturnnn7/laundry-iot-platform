namespace Laundry.Identity.Application.Features.Auth.Register;

public sealed record RegisterResponse(Guid Id, string Email, string DisplayName, string AccountType);
