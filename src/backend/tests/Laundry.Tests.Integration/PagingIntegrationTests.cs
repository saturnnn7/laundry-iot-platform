using Laundry.Booking.Infrastructure;
using Laundry.BuildingBlocks.Application.Paging;
using Laundry.BuildingBlocks.Infrastructure.Outbox;
using Laundry.BuildingBlocks.Infrastructure.Paging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Laundry.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class PagingIntegrationTests(PostgresFixture fixture)
{
    private const int RowCount = 25;
    private const int PageSize = 10;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task WalkingAllPagesReturnsEveryRowOnceInOrder(SortDirection direction)
    {
        var ids = await InsertRowsAsync(RowCount, DateTimeOffset.Parse("2001-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var expected = await ExpectedOrderAsync(ids, direction);

        var (walked, pages) = await WalkAsync(ids, direction);

        Assert.Equal(expected, walked);
        Assert.Equal(3, pages.Count);
        Assert.Equal([PageSize, PageSize, 5], pages.Select(p => p.Items.Count));
        Assert.NotNull(pages[0].NextCursor);
        Assert.NotNull(pages[1].NextCursor);
        Assert.Null(pages[2].NextCursor);
    }

    [Fact]
    public async Task ExactMultipleOfLimitEndsWithNullCursor()
    {
        var ids = await InsertRowsAsync(PageSize * 2, DateTimeOffset.Parse("2002-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var (walked, pages) = await WalkAsync(ids, SortDirection.Ascending);

        Assert.Equal(PageSize * 2, walked.Count);
        Assert.Equal(2, pages.Count);
        Assert.Null(pages[^1].NextCursor);
    }

    [Fact]
    public async Task RowsInsertedBetweenRequestsDoNotCauseDuplicates()
    {
        var ids = await InsertRowsAsync(RowCount, DateTimeOffset.Parse("2003-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var walked = new List<Guid>();

        var first = await FetchAsync(ids, Request(null), SortDirection.Ascending);
        walked.AddRange(first.Items);

        // New rows sort before the cursor; offset paging would now repeat rows already returned.
        ids.AddRange(await InsertRowsAsync(3, DateTimeOffset.Parse("2000-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));

        var next = first.NextCursor;
        while (next is not null)
        {
            var page = await FetchAsync(ids, Request(next), SortDirection.Ascending);
            walked.AddRange(page.Items);
            next = page.NextCursor;
        }

        Assert.Equal(walked.Count, walked.Distinct().Count());
        Assert.Equal(RowCount, walked.Count);
    }

    private static PageRequest Request(string? cursor) => PageRequest.Create(cursor, PageSize).Value;

    private async Task<(List<Guid> Walked, List<Page<Guid>> Pages)> WalkAsync(List<Guid> ids, SortDirection direction)
    {
        var walked = new List<Guid>();
        var pages = new List<Page<Guid>>();
        string? cursor = null;

        do
        {
            var page = await FetchAsync(ids, Request(cursor), direction);
            pages.Add(page);
            walked.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return (walked, pages);
    }

    private async Task<Page<Guid>> FetchAsync(List<Guid> ids, PageRequest request, SortDirection direction)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        return await db.OutboxMessages.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToPageAsync(request, m => m.OccurredAtUtc, m => m.Id, direction, m => m.Id, Ct);
    }

    // The database defines the order of uuid values, so the expectation comes from a plain ordered query.
    private async Task<List<Guid>> ExpectedOrderAsync(List<Guid> ids, SortDirection direction)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var query = db.OutboxMessages.AsNoTracking().Where(m => ids.Contains(m.Id));

        var ordered = direction == SortDirection.Ascending
            ? query.OrderBy(m => m.OccurredAtUtc).ThenBy(m => m.Id)
            : query.OrderByDescending(m => m.OccurredAtUtc).ThenByDescending(m => m.Id);

        return await ordered.Select(m => m.Id).ToListAsync(Ct);
    }

    // Five rows share each timestamp, so the id decides the order inside a group.
    private async Task<List<Guid>> InsertRowsAsync(int count, DateTimeOffset start)
    {
        var messages = Enumerable.Range(0, count)
            .Select(i => OutboxMessage.Create(
                new TestIntegrationEvent(Guid.NewGuid(), start.AddSeconds(i / 5), "paging"),
                DateTimeOffset.UtcNow))
            .ToList();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.OutboxMessages.AddRange(messages);
        await db.SaveChangesAsync(Ct);

        return messages.Select(m => m.Id).ToList();
    }
}
