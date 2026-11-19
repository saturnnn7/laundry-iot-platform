using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Laundry.BuildingBlocks.Application.Paging;

// The position of the last item of a page: its sort timestamp (UTC) and its id.
// The cursor is opaque to clients. It never carries tenant or filter data.
public sealed record PageCursor(DateTimeOffset Timestamp, Guid Id)
{
    private const int MaxEncodedLength = 96;

    public string Encode()
    {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{Timestamp.UtcTicks}:{Id:N}");

        return Base64Url.EncodeToString(Encoding.ASCII.GetBytes(text));
    }

    // Never throws: any input that is not a cursor produced by Encode returns false.
    public static bool TryDecode(string? value, out PageCursor cursor)
    {
        cursor = null!;

        if (string.IsNullOrEmpty(value) || value.Length > MaxEncodedLength)
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[MaxEncodedLength];
        if (Base64Url.DecodeFromChars(value, buffer, out _, out var written) != OperationStatus.Done)
        {
            return false;
        }

        var text = Encoding.ASCII.GetString(buffer[..written]);
        var parts = text.Split(':');

        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks > DateTime.MaxValue.Ticks
            || !Guid.TryParseExact(parts[1], "N", out var id))
        {
            return false;
        }

        cursor = new PageCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }
}
