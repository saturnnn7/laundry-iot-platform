using System.Net;
using Xunit;

namespace Laundry.Tests.Integration;

public class HealthEndpointTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task LiveReturnsOkWithoutToken()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessReturns503AndDoesNotLeakTheConnectionString()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Unhealthy", body);
        Assert.DoesNotContain("localhost", body, StringComparison.Ordinal);
    }
}
