using Laundry.BuildingBlocks.Application;
using Laundry.Identity.Application;
using Laundry.Identity.Domain;
using Laundry.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Laundry.Tests.Integration.Identity;

[Collection(PostgresCollection.Name)]
public sealed class UserAccountsTests(PostgresFixture fixture)
{
    private const string GoodPassword = "correct horse battery";

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RegisterStoresHashedPasswordAndAccountTypeAsText()
    {
        var email = NewEmail();

        var result = await RegisterAsync(email, GoodPassword, AccountType.Admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(email, result.Value.Email);
        Assert.Equal(AccountType.Admin, result.Value.AccountType);

        var row = await ReadUserAsync(email);
        Assert.NotEqual(GoodPassword, row.PasswordHash);
        Assert.DoesNotContain(GoodPassword, row.PasswordHash, StringComparison.Ordinal);
        Assert.StartsWith("AQAAAA", row.PasswordHash, StringComparison.Ordinal);
        Assert.Equal("Admin", row.AccountType);
        Assert.Equal(result.Value.Id, row.Id);
    }

    [Fact]
    public async Task UsersLiveInTheIdentitySchemaWithSnakeCaseNames()
    {
        var tables = await QueryStringsAsync(
            "select table_name from information_schema.tables where table_schema = 'identity'");
        Assert.Contains("users", tables);
        Assert.Contains("user_claims", tables);
        Assert.Contains("user_logins", tables);
        Assert.Contains("user_tokens", tables);
        Assert.Contains("outbox_messages", tables);
        Assert.DoesNotContain(tables, t => t.StartsWith("AspNet", StringComparison.Ordinal));

        var columns = await QueryStringsAsync(
            "select column_name from information_schema.columns where table_schema = 'identity' and table_name = 'users'");
        string[] expected =
        [
            "id", "user_name", "normalized_user_name", "email", "normalized_email", "password_hash",
            "display_name", "account_type", "created_at_utc", "lockout_end", "access_failed_count",
        ];
        foreach (var column in expected)
        {
            Assert.Contains(column, columns);
        }
    }

    [Fact]
    public async Task NormalizedEmailAndUserNameIndexesAreUnique()
    {
        var definitions = await QueryStringsAsync(
            "select indexdef from pg_indexes where schemaname = 'identity' and tablename = 'users'");

        Assert.Contains(definitions, d => d.Contains("UNIQUE", StringComparison.Ordinal)
            && d.Contains("(normalized_email)", StringComparison.Ordinal));
        Assert.Contains(definitions, d => d.Contains("UNIQUE", StringComparison.Ordinal)
            && d.Contains("(normalized_user_name)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateEmailInAnotherLetterCaseIsAValidationErrorOnEmail()
    {
        var email = NewEmail();
        Assert.True((await RegisterAsync(email, GoodPassword)).IsSuccess);

        var result = await RegisterAsync(email.ToUpperInvariant(), GoodPassword);

        AssertFieldError(result, "email");
    }

    [Fact]
    public async Task ParallelRegistrationsOfTheSameEmailGiveOneSuccessAndOneValidationError()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var email = NewEmail();
            using var gate = new Barrier(2);

            Task<Result<UserAccount>> Run() => Task.Run(async () =>
            {
                await using var scope = fixture.Factory.Services.CreateAsyncScope();
                var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
                gate.SignalAndWait(Ct);
                return await accounts.RegisterAsync(email, GoodPassword, "Race", AccountType.Resident, Ct);
            }, Ct);

            var results = await Task.WhenAll(Run(), Run());

            Assert.Single(results, r => r.IsSuccess);
            AssertFieldError(Assert.Single(results, r => r.IsFailure), "email");
        }
    }

    [Fact]
    public async Task NinePasswordCharactersAreRejectedAndTenAreAccepted()
    {
        var rejected = await RegisterAsync(NewEmail(), new string('a', 9));
        var accepted = await RegisterAsync(NewEmail(), "abcdefghij");

        AssertFieldError(rejected, "password");
        Assert.True(accepted.IsSuccess);
    }

    [Fact]
    public async Task EmailWithPlusAndApostropheIsAccepted()
    {
        var email = $"o'brien+{Guid.NewGuid():N}@example.com";

        var result = await RegisterAsync(email, GoodPassword);

        Assert.True(result.IsSuccess);
        Assert.True((await VerifyAsync(email, GoodPassword)).IsSuccess);
    }

    [Fact]
    public async Task CorrectPasswordSucceedsAndFindByIdReturnsTheAccount()
    {
        var email = NewEmail();
        var registered = await RegisterAsync(email, GoodPassword);

        var verified = await VerifyAsync(email.ToUpperInvariant(), GoodPassword);

        Assert.True(verified.IsSuccess);
        Assert.Equal(registered.Value.Id, verified.Value.Id);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var found = await scope.ServiceProvider.GetRequiredService<IUserAccounts>()
            .FindByIdAsync(registered.Value.Id, Ct);
        Assert.Equal(registered.Value, found.Value);

        var missing = await scope.ServiceProvider.GetRequiredService<IUserAccounts>()
            .FindByIdAsync(Guid.NewGuid(), Ct);
        Assert.Equal(ProblemTypes.NotFound, missing.Error.Type);
    }

    [Fact]
    public async Task WrongPasswordAndUnknownEmailGiveTheSameError()
    {
        var email = NewEmail();
        await RegisterAsync(email, GoodPassword);

        var wrongPassword = await VerifyAsync(email, "not the password");
        var unknownEmail = await VerifyAsync(NewEmail(), GoodPassword);

        Assert.Equal(ProblemTypes.Unauthorized, wrongPassword.Error.Type);
        Assert.Equal(wrongPassword.Error, unknownEmail.Error);
    }

    [Fact]
    public async Task FiveWrongAttemptsLockTheAccount()
    {
        var email = NewEmail();
        await RegisterAsync(email, GoodPassword);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(ProblemTypes.Unauthorized, (await VerifyAsync(email, "wrong password")).Error.Type);
        }

        var sixth = await VerifyAsync(email, GoodPassword);

        Assert.Equal(ProblemTypes.TooManyAttempts, sixth.Error.Type);
    }

    [Fact]
    public async Task SuccessBeforeTheLimitResetsTheCounter()
    {
        var email = NewEmail();
        await RegisterAsync(email, GoodPassword);

        for (var i = 0; i < 4; i++)
        {
            await VerifyAsync(email, "wrong password");
        }

        Assert.True((await VerifyAsync(email, GoodPassword)).IsSuccess);

        for (var i = 0; i < 4; i++)
        {
            await VerifyAsync(email, "wrong password");
        }

        Assert.True((await VerifyAsync(email, GoodPassword)).IsSuccess);
    }

    [Fact]
    public async Task UserSaveThroughUserManagerStagesTheIntegrationEventInTheSameTransaction()
    {
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "identity");
        var email = NewEmail();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            db.AddIntegrationEvent(integrationEvent);

            var result = await users.CreateAsync(
                new AppUser { UserName = email, Email = email, DisplayName = "Staged", CreatedAtUtc = DateTimeOffset.UtcNow },
                GoodPassword);

            Assert.True(result.Succeeded);
        }

        await fixture.Handler.WaitForAsync(integrationEvent.EventId, Deadline, Ct);
        Assert.Equal(1, await CountOutboxRowsAsync(integrationEvent.EventId));
    }

    [Fact]
    public async Task FailedUserSaveDoesNotStoreTheStagedIntegrationEvent()
    {
        var email = NewEmail();
        Assert.True((await RegisterAsync(email, GoodPassword)).IsSuccess);
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "identity");

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.AddIntegrationEvent(integrationEvent);
            db.Users.Add(new AppUser
            {
                UserName = "other-" + email,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = "OTHER-" + email.ToUpperInvariant(),
                DisplayName = "Dup",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }

        Assert.Equal(0, await CountOutboxRowsAsync(integrationEvent.EventId));
        Assert.Equal(0, fixture.Handler.CallCount(integrationEvent.EventId));
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static void AssertFieldError(Result<UserAccount> result, string field)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ProblemTypes.Validation, result.Error.Type);
        Assert.NotNull(result.Error.Fields);
        Assert.Contains(field, result.Error.Fields.Keys);
    }

    private async Task<Result<UserAccount>> RegisterAsync(
        string email,
        string password,
        AccountType accountType = AccountType.Resident)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserAccounts>()
            .RegisterAsync(email, password, "Test User", accountType, Ct);
    }

    private async Task<Result<UserAccount>> VerifyAsync(string email, string password)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserAccounts>()
            .VerifyCredentialsAsync(email, password, Ct);
    }

    private async Task<(Guid Id, string PasswordHash, string AccountType)> ReadUserAsync(string email)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "select id, password_hash, account_type from identity.users where normalized_email = @email", connection);
        command.Parameters.AddWithValue("email", email.ToUpperInvariant());
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }

    private async Task<List<string>> QueryStringsAsync(string sql)
    {
        var values = new List<string>();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private async Task<int> CountOutboxRowsAsync(Guid eventId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("select count(*) from identity.outbox_messages where id = @id", connection);
        command.Parameters.AddWithValue("id", eventId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}
