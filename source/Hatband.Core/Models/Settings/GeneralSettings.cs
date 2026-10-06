using System.ComponentModel.DataAnnotations;

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

    /// <summary>Multiplier applied to the application's shared typography scale.</summary>
    [Range(80, 140)]
    public int TextScalePercent { get; set; } = 100;
}
