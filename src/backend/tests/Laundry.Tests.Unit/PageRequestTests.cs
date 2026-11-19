using Laundry.BuildingBlocks.Application;
using Laundry.BuildingBlocks.Application.Paging;
using Xunit;

namespace Laundry.Tests.Unit;

public class PageRequestTests
{
    private static readonly PageCursor Sample = new(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero).AddTicks(1234), Guid.NewGuid());

    [Fact]
    public void DefaultsToFifty()
    {
        var result = PageRequest.Create(null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.Limit);
        Assert.Null(result.Value.After);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void AcceptsLimitBoundaries(int limit)
    {
        var result = PageRequest.Create(null, limit);

        Assert.True(result.IsSuccess);
        Assert.Equal(limit, result.Value.Limit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-5)]
    public void RejectsLimitOutsideRange(int limit)
    {
        var result = PageRequest.Create(null, limit);

        Assert.True(result.IsFailure);
        Assert.Equal(ProblemTypes.Validation, result.Error.Type);
        Assert.Equal(["limit"], result.Error.Fields!.Keys);
    }

    [Fact]
    public void CursorRoundTrips()
    {
        var result = PageRequest.Create(Sample.Encode(), 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(Sample, result.Value.After);
    }

    [Fact]
    public void EncodedCursorIsUrlSafe()
    {
        var encoded = Sample.Encode();

        Assert.All(encoded, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
    }

    [Fact]
    public void ReportsBothFieldsAtOnce()
    {
        var result = PageRequest.Create("garbage!", 0);

        Assert.Equal(["cursor", "limit"], result.Error.Fields!.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("garbage!")]
    [InlineData("not a cursor")]
    [InlineData("%%%%")]
    [InlineData("AAAA")]
    [InlineData("MTox")]
    [InlineData("____")]
    [InlineData("a")]
    public void GarbageCursorGivesValidationErrorOnCursorField(string cursor)
    {
        var result = PageRequest.Create(cursor, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ProblemTypes.Validation, result.Error.Type);
        Assert.Equal(["cursor"], result.Error.Fields!.Keys);
    }

    [Fact]
    public void TruncatedAndOversizedCursorsNeverThrow()
    {
        var encoded = Sample.Encode();

        for (var length = 1; length < encoded.Length; length++)
        {
            var result = PageRequest.Create(encoded[..length], null);

            // Some prefixes can decode by chance; the point is that nothing throws and failures name the cursor.
            Assert.True(result.IsSuccess || result.Error.Fields!.ContainsKey("cursor"));
        }

        var oversized = PageRequest.Create(new string('A', 5000), null);
        Assert.True(oversized.IsFailure);
    }

    [Fact]
    public void EmptyCursorMeansFirstPage()
    {
        var result = PageRequest.Create(string.Empty, null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.After);
    }
}
