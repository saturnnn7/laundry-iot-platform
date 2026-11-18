using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Laundry.Tests.Integration;

public static class TestTokens
{
    public static string Valid() => Create(ApiFactory.SigningKey, ApiFactory.Audience, expires: DateTime.UtcNow.AddMinutes(10));

    public static string Expired() => Create(
        ApiFactory.SigningKey,
        ApiFactory.Audience,
        notBefore: DateTime.UtcNow.AddMinutes(-20),
        expires: DateTime.UtcNow.AddMinutes(-10));

    public static string SignedWithOtherKey() => Create(ApiFactory.OtherSigningKey, ApiFactory.Audience, expires: DateTime.UtcNow.AddMinutes(10));

    public static string WrongAudience() => Create(ApiFactory.SigningKey, "someone-else", expires: DateTime.UtcNow.AddMinutes(10));

    private static string Create(string key, string audience, DateTime expires, DateTime? notBefore = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ApiFactory.Issuer,
            Audience = audience,
            Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", "user-1")]),
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
