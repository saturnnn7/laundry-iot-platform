namespace Laundry.BuildingBlocks.Infrastructure.Paging;

// Projection target of the paging query: the sort keys travel with the item so the cursor can be built.
internal sealed class PageRow<T>
{
    public DateTimeOffset Timestamp { get; set; }

    public Guid Id { get; set; }

    public T Item { get; set; } = default!;
}
