using System.Net;
using System.Net.Http.Json;
using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Api.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Laundry.Tests.Integration;

public class ForwardedHeadersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CallerAddress = "172.18.0.5";
    private const string ClientAddress = "203.0.113.9";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The test server sets no remote address, so a first-in-pipeline filter sets one.
    private sealed class RemoteAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private HttpClient CreateClient(IReadOnlyDictionary<string, string> settings, string caller = CallerAddress) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddEndpoints(typeof(SampleEndpoint).Assembly);
                services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(IPAddress.Parse(caller)));
            });
        }).CreateClient();

    private static async Task<ConnectionInfo> ReadAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/connection-info");
        request.Headers.Add("X-Forwarded-For", ClientAddress);
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConnectionInfo>(Ct))!;
    }

    [Fact]
    public async Task DisabledIgnoresForwardedHeaders()
    {
        var client = CreateClient(new Dictionary<string, string>());

        var info = await ReadAsync(client);

        Assert.Equal("http", info.Scheme);
        Assert.Equal(CallerAddress, info.RemoteIp);
    }

    [Fact]
    public async Task TrustedNetworkAppliesForwardedHeaders()
    {
        var client = CreateClient(new Dictionary<string, string>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownNetworks:0"] = "172.18.0.0/16",
        });

        var info = await ReadAsync(client);

        Assert.Equal("https", info.Scheme);
        Assert.Equal(ClientAddress, info.RemoteIp);
    }

    [Fact]
    public async Task TrustedProxyAppliesForwardedHeaders()
    {
        var client = CreateClient(new Dictionary<string, string>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownProxies:0"] = CallerAddress,
        });

        var info = await ReadAsync(client);

        Assert.Equal("https", info.Scheme);
        Assert.Equal(ClientAddress, info.RemoteIp);
    }

    [Fact]
    public async Task UntrustedCallerIsIgnored()
    {
        var client = CreateClient(
            new Dictionary<string, string>
            {
                ["ForwardedHeaders:Enabled"] = "true",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8",
                ["ForwardedHeaders:KnownProxies:0"] = "10.9.9.9",
            });

        var info = await ReadAsync(client);

        Assert.Equal("http", info.Scheme);
        Assert.Equal(CallerAddress, info.RemoteIp);
    }

    [Fact]
    public void EnabledWithoutAnyListFailsStartup()
    {
        var exception = Record.Exception(() => CreateClient(new Dictionary<string, string> { ["ForwardedHeaders:Enabled"] = "true" }));

        Assert.NotNull(exception);
        Assert.Contains("ForwardedHeaders:KnownProxies and ForwardedHeaders:KnownNetworks are both empty", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidNetworkFailsStartup()
    {
        var exception = Record.Exception(() => CreateClient(new Dictionary<string, string>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownNetworks:0"] = "172.18.0.0/99",
        }));

        Assert.NotNull(exception);
        Assert.Contains("KnownNetworks contains '172.18.0.0/99', which is not a valid network", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidProxyFailsStartup()
    {
        var exception = Record.Exception(() => CreateClient(new Dictionary<string, string>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownProxies:0"] = "not-an-ip",
        }));

        Assert.NotNull(exception);
        Assert.Contains("KnownProxies contains 'not-an-ip', which is not a valid IP address", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    public void ValidatorChecksOnlyWhenEnabledAndRequiresForwardLimit(bool enabled, int forwardLimit, bool expectedValid)
    {
        var settings = new ForwardedHeadersSettings { Enabled = enabled, ForwardLimit = forwardLimit };
        if (enabled)
        {
            settings.KnownProxies.Add("10.0.0.1");
        }

        var result = new ForwardedHeadersSettingsValidator().Validate(null, settings);

        Assert.Equal(expectedValid, result.Succeeded);
    }
}
