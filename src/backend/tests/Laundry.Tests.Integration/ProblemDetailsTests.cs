using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Laundry.BuildingBlocks.Api.Problems;
using Laundry.BuildingBlocks.Application;
using Xunit;

namespace Laundry.Tests.Integration;

public class ProblemDetailsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(Ct);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static HttpRequestMessage Get(string path, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    [Fact]
    public async Task SecureEndpointWithoutTokenReturnsUnauthorizedProblem()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/secure-sample"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("unauthorized", problem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task SecureEndpointWithValidTokenReturnsOk()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/secure-sample", TestTokens.Valid()), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("other-key")]
    [InlineData("wrong-audience")]
    public async Task SecureEndpointRejectsBadTokens(string kind)
    {
        var token = kind switch
        {
            "expired" => TestTokens.Expired(),
            "other-key" => TestTokens.SignedWithOtherKey(),
            _ => TestTokens.WrongAudience(),
        };
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/secure-sample", token), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/sample")]
    [InlineData("/health/live")]
    [InlineData("/api/v1/identity/ping")]
    [InlineData("/api/v1/resources/ping")]
    [InlineData("/api/v1/telemetry/ping")]
    [InlineData("/api/v1/booking/ping")]
    [InlineData("/api/v1/maintenance/ping")]
    public async Task AnonymousRoutesWorkWithoutToken(string path)
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get(path), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FailedResultMapsToProblemWithTableStatus()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/fail/slot-conflict"), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("slot-conflict", problem.GetProperty("type").GetString());
        Assert.Equal(FailureEndpoints.SlotConflictTitle, problem.GetProperty("title").GetString());
        Assert.Equal(FailureEndpoints.SlotConflictDetail, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnknownTypeMapsToInternalServerError()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/fail/unknown"), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRouteReturnsNotFoundProblem()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Get("/api/v1/nothing-here"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("not-found", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task UnhandledExceptionReturnsProblemWithoutMessage()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/fail/throw"), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(FailureEndpoints.ThrowMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidationErrorWithFieldsListsMessagesPerCamelCaseField()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/fail/validation-fields"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("validation", problem.GetProperty("type").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
        Assert.Equal(["name", "startsAt"], errors.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["Name is required.", "Name is too long."],
            errors.GetProperty("name").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(
            ["Start must be in the future."],
            errors.GetProperty("startsAt").EnumerateArray().Select(m => m.GetString()));
    }

    [Fact]
    public async Task ValidationErrorWithoutFieldsHasNoErrorsMember()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.SendAsync(Get("/api/v1/fail/validation-plain"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("validation", problem.GetProperty("type").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("{ \"name\": \"x\", \"count\": ", "application/json")]
    [InlineData("{ \"name\": \"x\", \"count\": \"not-a-number\" }", "application/json")]
    [InlineData("", "application/json")]
    [InlineData("", null)]
    public async Task BadRequestBodyReturnsValidationProblemWithoutInternalDetails(string body, string? contentType)
    {
        var client = factory.CreateClientWithTestEndpoints();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/body/echo");
        if (contentType is not null)
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, contentType);
        }

        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("validation", problem.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));

        var text = problem.GetRawText();
        foreach (var forbidden in new[] { "Exception", "   at ", "System.", "Laundry.", "BodySample", "JsonReaderException", "not-a-number", "LineNumber", "Path:" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StatusTableCoversEveryProblemType()
    {
        var constants = typeof(ProblemTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(constants, ProblemTypeStatus.Types.Order(StringComparer.Ordinal).ToArray());
        Assert.All(constants, type => Assert.NotEqual(500, ProblemTypeStatus.For(type)));
    }

    [Fact]
    public void EmptySigningKeyFailsStartup()
    {
        var broken = factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningKey", string.Empty));

        var exception = Record.Exception(() => broken.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Jwt:SigningKey", exception.ToString(), StringComparison.Ordinal);
    }
}
