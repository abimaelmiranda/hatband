using System.Globalization;

namespace Hatband.App.Services;

public sealed class DateTimeDisplayFormatter
{
    private TimeZoneInfo displayTimeZone = TimeZoneInfo.Local;

    public void SetTimeZone(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public string FormatUtcDate(DateTime utcDateTime)
    {
        var utcValue = EnsureUtc(utcDateTime);
        var displayDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcValue, displayTimeZone);
        return displayDateTime.ToString("d", CultureInfo.CurrentCulture);
    }

    private static DateTime EnsureUtc(DateTime dateTime)
    {
        // SQLite materializes stored UTC values as Unspecified DateTime instances.
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            DateTimeKind.Local => throw new ArgumentException(
                "The stored date and time must represent a UTC instant.",
                nameof(dateTime)),
            _ => throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime.Kind, "Unknown DateTime kind.")
        };
    }
}
