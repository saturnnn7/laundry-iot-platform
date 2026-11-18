using Laundry.BuildingBlocks.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Laundry.Tests.Integration;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-signing-key-0123456789-abcdefghijklmnop";
    public const string OtherSigningKey = "another-signing-key-0123456789-abcdefghijklmn";
    public const string Issuer = "laundry-test";
    public const string Audience = "laundry-test-api";

    public HttpClient CreateClientWithTestEndpoints() =>
        WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddEndpoints(typeof(SampleEndpoint).Assembly)))
            .CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ConfigureDatabaseSettings(builder);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
    }

    protected virtual void ConfigureDatabaseSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=test");
        builder.UseSetting("Outbox:Enabled", "false");
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }
}
