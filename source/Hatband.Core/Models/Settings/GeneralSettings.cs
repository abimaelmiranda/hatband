namespace Hatband.Core.Models.Settings;

public sealed class GeneralSettings
{
    /// <summary>
    /// BCP 47 culture tag used by the presentation layer, such as "pt-BR".
    /// </summary>
    public string LanguageTag { get; set; } = "en-US";

    /// <summary>
    /// Preferred time zone ID used by the presentation layer when displaying UTC instants.
    /// </summary>
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
}
