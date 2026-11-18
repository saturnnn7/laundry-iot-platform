namespace Laundry.BuildingBlocks.Application;

public sealed record Error(string Type, string Title, string? Detail = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
