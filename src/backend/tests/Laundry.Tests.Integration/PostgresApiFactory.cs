using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Laundry.Tests.Integration;

public sealed class PostgresApiFactory(string connectionString, RecordingHandler handler) : ApiFactory
{
    protected override void ConfigureDatabaseSettings(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Outbox:Enabled", "true");
        builder.UseSetting("Outbox:PollInterval", "00:00:00.100");
        builder.UseSetting("Outbox:MaxRetryDelay", "00:00:01");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(handler);
            services.AddEndpoints(typeof(SampleEndpoint).Assembly);
        });
    }
}
