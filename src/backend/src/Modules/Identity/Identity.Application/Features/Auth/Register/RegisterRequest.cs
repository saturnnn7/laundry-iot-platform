namespace Laundry.Identity.Application.Features.Auth.Register;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName, string? AccountType);
