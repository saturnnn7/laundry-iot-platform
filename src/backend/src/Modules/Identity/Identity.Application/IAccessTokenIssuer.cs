namespace Laundry.Identity.Application;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount account);
}
