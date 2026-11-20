using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Laundry.BuildingBlocks.Api.Security;
using Laundry.Identity.Api;
using Laundry.Identity.Application;
using Laundry.Identity.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace Laundry.Tests.Integration.Identity;

[Collection(PostgresCollection.Name)]
public sealed class AuthEndpointsTests(PostgresFixture fixture)
{
    private const string Password = "correct horse battery";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Client => fixture.Factory.CreateClient();

    [Fact]
    public async Task RegisterReturns201WithTheAccountAndNoLocationOrToken()
    {
        var email = NewEmail();

        var response = await RegisterAsync(email, accountType: "Admin");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await ReadJsonAsync(response);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Jane Doe", body.GetProperty("displayName").GetString());
        Assert.Equal("Admin", body.GetProperty("accountType").GetString());
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.False(body.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task DuplicateEmailInAnotherLetterCaseGivesValidationErrorOnEmail()
    {
        var email = NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(email)).StatusCode);

        var response = await RegisterAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("validation", body.GetProperty("type").GetString());
        Assert.Equal(1, body.GetProperty("errors").GetProperty("email").GetArrayLength());
    }

    [Fact]
    public async Task SeveralBadFieldsAreReportedTogether()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "nope", password = "short", displayName = " ", accountType = "1" },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        foreach (var field in new[] { "email", "displayName", "accountType" })
        {
            Assert.True(errors.TryGetProperty(field, out _), field);
        }
    }

    [Fact]
    public async Task ShortPasswordIsReportedOnThePasswordField()
    {
        var response = await RegisterAsync(NewEmail(), password: "123456789");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadJsonAsync(response)).GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task MalformedBodyGivesValidationProblem()
    {
        var response = await Client.PostAsync(
            "/api/v1/auth/login",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The request could not be read.", (await ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task LoginReturnsATokenTheBearerPipelineAcceptsWithNoStoreHeaders()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await LoginAsync(email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("no-cache", response.Headers.Pragma.ToString());
        var body = await ReadJsonAsync(response);
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.True(body.GetProperty("expiresAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(55));

        var secured = await GetSecureAsync(body.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, secured.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedCallToAProtectedRouteStillGives401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetSecureAsync(null)).StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKeyIsRejected()
    {
        var token = IssueToken(ApiFactory.OtherSigningKey, DateTimeOffset.UtcNow);

        await AssertUnauthorizedAsync(await GetSecureAsync(token));
    }

    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        var token = IssueToken(ApiFactory.SigningKey, DateTimeOffset.UtcNow.AddHours(-2));

        await AssertUnauthorizedAsync(await GetSecureAsync(token));
    }

    [Fact]
    public async Task TamperedTokenIsRejected()
    {
        var token = IssueToken(ApiFactory.SigningKey, DateTimeOffset.UtcNow);
        var parts = token.Split('.');
        parts[1] = parts[1].Length > 10 ? parts[1][..5] + (parts[1][5] == 'A' ? 'B' : 'A') + parts[1][6..] : parts[1] + "A";

        await AssertUnauthorizedAsync(await GetSecureAsync(string.Join('.', parts)));
    }

    [Fact]
    public async Task WrongPasswordAndUnknownEmailGiveIdenticalBodies()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var wrong = await LoginAsync(email, "not the password");
        var unknown = await LoginAsync(NewEmail(), Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await StableBodyAsync(wrong), await StableBodyAsync(unknown));
    }

    [Fact]
    public async Task FiveWrongAttemptsThenTheRightPasswordGives429()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email, "wrong password")).StatusCode);
        }

        var response = await LoginAsync(email, Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("too-many-attempts", (await ReadJsonAsync(response)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task MeReturnsTheProfileWithEmptyMemberships()
    {
        var email = NewEmail();
        var registered = await ReadJsonAsync(await RegisterAsync(email, accountType: "Admin"));
        var token = (await ReadJsonAsync(await LoginAsync(email, Password))).GetProperty("accessToken").GetString();

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(registered.GetProperty("id").GetString(), body.GetProperty("id").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Jane Doe", body.GetProperty("displayName").GetString());
        Assert.Equal("Admin", body.GetProperty("accountType").GetString());
        var memberships = body.GetProperty("memberships");
        Assert.Equal(JsonValueKind.Array, memberships.ValueKind);
        Assert.Equal(0, memberships.GetArrayLength());
    }

    [Fact]
    public async Task MeWithoutTokenGives401()
    {
        await AssertUnauthorizedAsync(await GetMeAsync(null));
    }

    [Fact]
    public async Task MeWithExpiredTokenGives401()
    {
        await AssertUnauthorizedAsync(await GetMeAsync(IssueToken(ApiFactory.SigningKey, DateTimeOffset.UtcNow.AddHours(-2))));
    }

    [Fact]
    public async Task MeWithValidTokenForUnknownUserGives401()
    {
        await AssertUnauthorizedAsync(await GetMeAsync(IssueToken(ApiFactory.SigningKey, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task OpenApiDescribesBothRoutes()
    {
        var document = await Client.GetStringAsync("/openapi/v1.json", Ct);

        Assert.Contains("/api/v1/auth/register", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/auth/login", document, StringComparison.Ordinal);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();

    // The trace id differs per request, everything else must match.
    private static async Task<string> StableBodyAsync(HttpResponseMessage response)
    {
        var body = await ReadJsonAsync(response);
        return string.Join(
            '|',
            body.EnumerateObject().Where(p => p.Name != "traceId").OrderBy(p => p.Name).Select(p => $"{p.Name}={p.Value}"));
    }

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", (await ReadJsonAsync(response)).GetProperty("type").GetString());
    }

    private Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password = Password,
        string accountType = "Resident") =>
        Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password, displayName = "Jane Doe", accountType },
            Ct);

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Ct);

    private Task<HttpResponseMessage> GetMeAsync(string? token) => GetAsync("/api/v1/me", token);

    private Task<HttpResponseMessage> GetSecureAsync(string? token) => GetAsync("/api/v1/secure-sample", token);

    private Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return Client.SendAsync(request, Ct);
    }

    private static string IssueToken(string signingKey, DateTimeOffset now)
    {
        var options = new JwtOptions
        {
            Issuer = ApiFactory.Issuer,
            Audience = ApiFactory.Audience,
            SigningKey = signingKey,
        };
        var issuer = new JwtAccessTokenIssuer(Options.Create(options), new FixedTimeProvider(now));
        var account = new UserAccount(Guid.NewGuid(), "someone@example.com", "Someone", AccountType.Resident);

        return issuer.Issue(account).Value;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
