using System.Security.Claims;
using Laundry.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Laundry.Tests.Unit;

public class CurrentUserTests
{
    private sealed class Accessor(HttpContext? context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private static HttpContextCurrentUser For(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new HttpContextCurrentUser(new Accessor(context));
    }

    [Fact]
    public void ValidSubIsTheUserId()
    {
        var id = Guid.NewGuid();

        var user = For(("sub", id.ToString()), ("acct", "Admin"));

        Assert.True(user.IsAuthenticated);
        Assert.Equal(id, user.UserId);
        Assert.Equal("Admin", user.AccountType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("user-1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void MissingOrInvalidSubMeansUnauthenticated(string? sub)
    {
        var user = sub is null ? For(("acct", "Resident")) : For(("sub", sub));

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
    }

    [Fact]
    public void MissingAcctGivesNoAccountType()
    {
        var user = For(("sub", Guid.NewGuid().ToString()));

        Assert.True(user.IsAuthenticated);
        Assert.Null(user.AccountType);
    }

    [Fact]
    public void NoHttpContextMeansUnauthenticated()
    {
        var user = new HttpContextCurrentUser(new Accessor(null));

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.AccountType);
    }
}
