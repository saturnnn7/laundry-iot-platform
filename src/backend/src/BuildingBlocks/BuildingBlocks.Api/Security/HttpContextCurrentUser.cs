using Microsoft.AspNetCore.Http;

namespace Laundry.BuildingBlocks.Api.Security;

// Claim names are not remapped (MapInboundClaims is false), so the raw JWT names are used.
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string SubjectClaim = "sub";
    public const string AccountTypeClaim = "acct";

    public bool IsAuthenticated => UserId is not null;

    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(SubjectClaim)?.Value, out var id) && id != Guid.Empty
            ? id
            : null;

    public string? AccountType => accessor.HttpContext?.User.FindFirst(AccountTypeClaim)?.Value;
}
