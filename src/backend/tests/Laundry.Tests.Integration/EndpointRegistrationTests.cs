using System.Net;
using Laundry.BuildingBlocks.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Laundry.Tests.Integration;

public class EndpointRegistrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task RegisteredEndpointIsMappedUnderApiV1()
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddEndpoints(typeof(SampleEndpoint).Assembly)))
            .CreateClient();

        var response = await client.GetAsync("/api/v1/sample", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("sample", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AddingTheSameAssemblyTwiceRegistersEachEndpointOnce()
    {
        var services = new ServiceCollection();
        var assembly = typeof(SampleEndpoint).Assembly;

        services.AddEndpoints(assembly);
        services.AddEndpoints(assembly);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IEndpoint));
        Assert.Equal(typeof(SampleEndpoint), descriptor.ImplementationType);
    }
}
