namespace Laundry.BuildingBlocks.Application.Paging;

public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
