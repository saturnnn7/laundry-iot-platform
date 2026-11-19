namespace Laundry.Identity.Application.Features.Auth.Login;

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc);
