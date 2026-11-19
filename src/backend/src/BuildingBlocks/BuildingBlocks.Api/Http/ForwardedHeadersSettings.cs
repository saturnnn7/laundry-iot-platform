namespace Laundry.BuildingBlocks.Api.Http;

public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    public List<string> KnownProxies { get; set; } = [];

    public List<string> KnownNetworks { get; set; } = [];

    public int ForwardLimit { get; set; } = 1;
}
