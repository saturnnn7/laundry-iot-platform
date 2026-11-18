using System.Net;
using System.Text.Json;
using Xunit;

namespace Laundry.Tests.Integration;

public class IdentityPingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task PingReturnsModuleName()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/identity/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Identity", json.RootElement.GetProperty("module").GetString());
    }
}
