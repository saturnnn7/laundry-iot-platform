using System.Net;
using System.Text.Json;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Laundry.Tests.Integration;

public class OpenApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DocumentIsPublicAndDescribesRoutesAndBearerScheme()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = json.RootElement;
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/v1/identity/ping", out _));
        Assert.Equal("Laundry IoT Platform API", root.GetProperty("info").GetProperty("title").GetString());
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
    }

    [Theory]
    [InlineData("/api/v1/me", "get", true)]
    [InlineData("/api/v1/auth/login", "post", false)]
    [InlineData("/api/v1/auth/register", "post", false)]
    [InlineData("/api/v1/identity/ping", "get", false)]
    public async Task OnlyProtectedOperationsCarryTheBearerRequirement(string path, string method, bool protectedRoute)
    {
        var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", Ct));
        var operation = json.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

        var hasSecurity = operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0;
        Assert.Equal(protectedRoute, hasSecurity);
        if (protectedRoute)
        {
            Assert.True(security[0].TryGetProperty("Bearer", out _));
        }
    }

    [Fact]
    public async Task SwaggerUiIsPublic()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task DisabledSwaggerReturnsNotFound(string path)
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("Swagger:Enabled", "false"))
            .CreateClient();

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void OneContextDescriptorPerModuleIsRegistered()
    {
        var descriptors = factory.Services.GetServices<ModuleContextDescriptor>().ToList();

        Assert.Equal(5, descriptors.Count);
        Assert.Equal(5, descriptors.Select(d => d.ContextType).Distinct().Count());
    }
}
