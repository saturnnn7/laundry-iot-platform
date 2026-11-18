using Microsoft.Extensions.Options;

namespace Laundry.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.RetentionPeriod <= TimeSpan.Zero)
        {
            failures.Add($"Outbox:RetentionPeriod must be greater than zero, but was '{options.RetentionPeriod}'.");
        }

        if (options.CleanupInterval <= TimeSpan.Zero)
        {
            failures.Add($"Outbox:CleanupInterval must be greater than zero, but was '{options.CleanupInterval}'.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
