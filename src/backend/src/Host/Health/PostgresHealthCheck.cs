using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Laundry.Host.Health;

internal sealed class PostgresHealthCheck(string connectionString) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(timeout.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(timeout.Token);

            return HealthCheckResult.Healthy();
        }
#pragma warning disable CA1031 // any failure means unhealthy; details must not leak
        catch (Exception)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("Database is not reachable.");
        }
    }
}
