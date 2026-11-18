using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Laundry.Tests.Unit;

public class OutboxOptionsValidatorTests
{
    private sealed class ValidatorContext(DbContextOptions options, ModuleContextServices services)
        : ModuleDbContext(options, services)
    {
        protected override string Schema => "validator";
    }

    private static readonly OutboxOptionsValidator Validator = new();

    [Fact]
    public void DefaultsAreValid()
    {
        var options = new OutboxOptions();

        Assert.Equal(TimeSpan.FromDays(7), options.RetentionPeriod);
        Assert.Equal(TimeSpan.FromHours(1), options.CleanupInterval);
        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-01:00:00")]
    public void NonPositiveRetentionPeriodFails(string value)
    {
        var result = Validator.Validate(null, new OutboxOptions { RetentionPeriod = TimeSpan.Parse(value, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.True(result.Failed);
        Assert.Contains("Outbox:RetentionPeriod", result.FailureMessage);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:05:00")]
    public void NonPositiveCleanupIntervalFails(string value)
    {
        var result = Validator.Validate(null, new OutboxOptions { CleanupInterval = TimeSpan.Parse(value, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.True(result.Failed);
        Assert.Contains("Outbox:CleanupInterval", result.FailureMessage);
    }

    [Fact]
    public void InvalidOptionsAreRejectedWhenResolved()
    {
        var services = new ServiceCollection();
        services.AddModuleDbContext<ValidatorContext>("Host=localhost;Database=x", "validator");
        services.Configure<OutboxOptions>(o => o.RetentionPeriod = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxOptions>>().Value);

        Assert.Contains("Outbox:RetentionPeriod", exception.Message);
    }
}
