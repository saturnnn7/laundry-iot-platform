using System.Text.Json;
using Laundry.BuildingBlocks.Api.Security;
using Laundry.Identity.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Laundry.Identity.Api;

// Issues the HS256 access tokens from D-15 with the same Jwt settings and key the bearer validation uses.
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var settings = options.Value;

        // Whole seconds, so exp - iat is exactly the lifetime and the response matches the token.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var expires = issuedAt.AddMinutes(settings.AccessTokenMinutes);

        // The payload is written directly: the descriptor API adds an nbf claim, which D-15 does not list.
        var payload = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = account.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = account.Email,
            ["name"] = account.DisplayName,
            ["acct"] = account.AccountType.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [JwtRegisteredClaimNames.Iat] = issuedAt.ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Exp] = expires.ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Iss] = settings.Issuer,
            [JwtRegisteredClaimNames.Aud] = settings.Audience,
        };

        var credentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
        var token = _handler.CreateToken(JsonSerializer.Serialize(payload), credentials);

        return new AccessToken(token, expires);
    }
}
