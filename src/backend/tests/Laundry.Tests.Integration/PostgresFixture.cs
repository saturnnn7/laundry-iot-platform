using Testcontainers.PostgreSql;
using Xunit;

namespace Laundry.Tests.Integration;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Image = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    public RecordingHandler Handler { get; } = new();

    public PostgresApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Could not start the {Image} test container. The integration tests need a running Docker daemon.",
                exception);
        }

        Factory = new PostgresApiFactory(ConnectionString, Handler);

        // Building the server runs the startup migrations.
        using var client = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
