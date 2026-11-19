using System.Net;
using Microsoft.Extensions.Options;

namespace Laundry.BuildingBlocks.Api.Http;

public sealed class ForwardedHeadersSettingsValidator : IValidateOptions<ForwardedHeadersSettings>
{
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (options.KnownProxies.Count == 0 && options.KnownNetworks.Count == 0)
        {
            failures.Add(
                "ForwardedHeaders:Enabled is true but ForwardedHeaders:KnownProxies and ForwardedHeaders:KnownNetworks are both empty. "
                + "List the proxy addresses or networks that may set forwarded headers, or set ForwardedHeaders:Enabled to false.");
        }

        foreach (var proxy in options.KnownProxies.Where(p => !IPAddress.TryParse(p, out _)))
        {
            failures.Add($"ForwardedHeaders:KnownProxies contains '{proxy}', which is not a valid IP address.");
        }

        foreach (var network in options.KnownNetworks.Where(n => !IPNetwork.TryParse(n, out _)))
        {
            failures.Add($"ForwardedHeaders:KnownNetworks contains '{network}', which is not a valid network in CIDR form such as 172.18.0.0/16.");
        }

        if (options.ForwardLimit < 1)
        {
            failures.Add($"ForwardedHeaders:ForwardLimit must be at least 1, but was {options.ForwardLimit}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
