using System.Net;
using System.Text.Json;
using Xunit;

namespace Laundry.Tests.Integration;

public class ModulePingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("identity", "Identity")]
    [InlineData("resources", "Resources")]
    [InlineData("telemetry", "Telemetry")]
    [InlineData("booking", "Booking")]
    [InlineData("maintenance", "Maintenance")]
    public async Task PingReturnsModuleName(string route, string expectedModule)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/{route}/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(expectedModule, json.RootElement.GetProperty("module").GetString());
    }
}
