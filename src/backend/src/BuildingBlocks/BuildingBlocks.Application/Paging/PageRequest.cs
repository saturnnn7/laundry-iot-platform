namespace Laundry.BuildingBlocks.Application.Paging;

public sealed record PageRequest
{
    public const int DefaultLimit = 50;
    public const int MinLimit = 1;
    public const int MaxLimit = 100;

    private PageRequest(PageCursor? after, int limit)
    {
        After = after;
        Limit = limit;
    }

    public PageCursor? After { get; }

    public int Limit { get; }

    public static Result<PageRequest> Create(string? cursor, int? limit)
    {
        var errors = new ValidationErrors();
        var effectiveLimit = limit ?? DefaultLimit;

        if (effectiveLimit is < MinLimit or > MaxLimit)
        {
            errors.Add("limit", $"Limit must be between {MinLimit} and {MaxLimit}.");
        }

        PageCursor? after = null;
        if (!string.IsNullOrEmpty(cursor) && !PageCursor.TryDecode(cursor, out after))
        {
            errors.Add("cursor", "Cursor is not valid.");
        }

        return errors.Build() is { } error
            ? Result.Failure<PageRequest>(error)
            : Result.Success(new PageRequest(after, effectiveLimit));
    }
}
