using System.Globalization;

namespace Hatband.Core.Extensions;

public static class DateOnlyExtensions
{
    public static string ToIsoDateString(this DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static bool TryParseIsoDate(this string? value, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            value?.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }
}
