using Laundry.BuildingBlocks.Api.Security;
using Laundry.Identity.Api;
using Laundry.Identity.Application;
using Laundry.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Laundry.Tests.Unit;

public class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 10, 0, 0, 123, TimeSpan.Zero);

    private static readonly JwtOptions Options = new()
    {
        SigningKey = "unit-test-signing-key-0123456789-abcdefghij",
    };

    private static readonly UserAccount Account =
        new(Guid.Parse("0197a1f0-0000-7000-8000-000000000001"), "jane@example.com", "Jane Doe", AccountType.Admin);

    private static JwtAccessTokenIssuer CreateIssuer(TimeProvider time) =>
        new(Microsoft.Extensions.Options.Options.Create(Options), time);

    [Fact]
    public void TokenCarriesExactlyTheDecidedClaims()
    {
        var token = CreateIssuer(new ManualTimeProvider(Start)).Issue(Account);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

        string[] expected = ["sub", "email", "name", "acct", "jti", "iat", "exp", "iss", "aud"];
        Assert.Equal(expected.Order(), jwt.Claims.Select(c => c.Type).Distinct().Order());
        Assert.Equal(Account.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("jane@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("Jane Doe", jwt.GetClaim("name").Value);
        Assert.Equal("Admin", jwt.GetClaim("acct").Value);
        Assert.Equal("laundry", jwt.Issuer);
        Assert.Equal(["laundry-api"], jwt.Audiences);
        Assert.Equal("HS256", jwt.Alg);
        Assert.True(Guid.TryParse(jwt.GetClaim("jti").Value, out _));
    }

    [Fact]
    public void LifetimeIsSixtyMinutesAndMatchesTheResponse()
    {
        var token = CreateIssuer(new ManualTimeProvider(Start)).Issue(Account);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);
        var iat = long.Parse(jwt.GetClaim("iat").Value, System.Globalization.CultureInfo.InvariantCulture);
        var exp = long.Parse(jwt.GetClaim("exp").Value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(3600, exp - iat);
        Assert.Equal(iat, Start.ToUnixTimeSeconds());
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(exp), token.ExpiresAtUtc);
    }

    [Fact]
    public void EachTokenGetsANewId()
    {
        var issuer = CreateIssuer(new ManualTimeProvider(Start));
        var handler = new JsonWebTokenHandler();

        var first = handler.ReadJsonWebToken(issuer.Issue(Account).Value).GetClaim("jti").Value;
        var second = handler.ReadJsonWebToken(issuer.Issue(Account).Value).GetClaim("jti").Value;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task TokenValidatesWithTheSharedSigningKey()
    {
        var token = CreateIssuer(new ManualTimeProvider(DateTimeOffset.UtcNow)).Issue(Account);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = Options.CreateSigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        });

        Assert.True(result.IsValid);
    }
}
