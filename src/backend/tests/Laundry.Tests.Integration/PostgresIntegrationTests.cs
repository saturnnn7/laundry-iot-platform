using System.Net;
using Laundry.Booking.Infrastructure;
using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Laundry.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class PostgresIntegrationTests(PostgresFixture fixture)
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan NegativeWait = TimeSpan.FromSeconds(1.5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MigrationsCreateAllModuleSchemas()
    {
        string[] schemas = ["identity", "resources", "telemetry", "booking", "maintenance"];
        var found = new HashSet<(string Schema, string Table)>();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "select table_schema, table_name from information_schema.tables " +
            "where table_schema = any(@schemas) and table_name in ('outbox_messages', '__EFMigrationsHistory')",
            connection);
        command.Parameters.AddWithValue("schemas", schemas);
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                found.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        foreach (var schema in schemas)
        {
            Assert.Contains((schema, "outbox_messages"), found);
            Assert.Contains((schema, "__EFMigrationsHistory"), found);
        }

        await fixture.Factory.Services.MigrateModulesAsync(Ct);
    }

    [Fact]
    public async Task HealthIsOkWithRealDatabase()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task EventIsDeliveredOnlyAfterCommit()
    {
        var integrationEvent = NewEvent("commit");

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            db.AddIntegrationEvent(integrationEvent);
            await db.SaveChangesAsync(Ct);

            await Task.Delay(NegativeWait, Ct);
            Assert.Equal(0, fixture.Handler.CallCount(integrationEvent.EventId));

            await transaction.CommitAsync(Ct);
        }

        await fixture.Handler.WaitForAsync(integrationEvent.EventId, Deadline, Ct);

        var row = await WaitForProcessedRowAsync(integrationEvent.EventId);
        Assert.Equal(0, row.Attempts);
    }

    [Fact]
    public async Task RolledBackEventIsNeverDelivered()
    {
        var integrationEvent = NewEvent("rollback");

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            db.AddIntegrationEvent(integrationEvent);
            await db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        await Task.Delay(NegativeWait, Ct);

        Assert.Equal(0, fixture.Handler.CallCount(integrationEvent.EventId));
        Assert.Null(await FindRowAsync(integrationEvent.EventId));
    }

    [Fact]
    public async Task FailingHandlerIsRetriedUntilItSucceeds()
    {
        var integrationEvent = NewEvent("retry");
        fixture.Handler.FailFirstCalls(integrationEvent.EventId, 1);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            db.AddIntegrationEvent(integrationEvent);
            await db.SaveChangesAsync(Ct);
        }

        await fixture.Handler.WaitForAsync(integrationEvent.EventId, Deadline, Ct);

        Assert.Equal(2, fixture.Handler.CallCount(integrationEvent.EventId));
        var row = await WaitForProcessedRowAsync(integrationEvent.EventId);
        Assert.Equal(1, row.Attempts);
        Assert.Null(row.LastError);
    }

    private static TestIntegrationEvent NewEvent(string note) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, note);

    private async Task<OutboxMessage?> FindRowAsync(Guid eventId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        return await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == eventId, Ct);
    }

    // The handler signals before the processor saves the row, so the row state is read with a bounded wait.
    private async Task<OutboxMessage> WaitForProcessedRowAsync(Guid eventId)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(Deadline);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

        try
        {
            do
            {
                var row = await FindRowAsync(eventId);
                if (row?.ProcessedAtUtc is not null)
                {
                    return row;
                }
            }
            while (await timer.WaitForNextTickAsync(deadline.Token));
        }
        catch (OperationCanceledException) when (!Ct.IsCancellationRequested)
        {
            // Fall through to the timeout below.
        }

        throw new TimeoutException($"Outbox row {eventId} was not marked processed within {Deadline}.");
    }
}
