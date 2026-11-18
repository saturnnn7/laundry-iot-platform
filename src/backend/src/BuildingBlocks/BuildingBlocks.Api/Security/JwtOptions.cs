namespace Laundry.BuildingBlocks.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinSigningKeyBytes = 32;

    public string Issuer { get; set; } = "laundry";

    public string Audience { get; set; } = "laundry-api";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;
}
