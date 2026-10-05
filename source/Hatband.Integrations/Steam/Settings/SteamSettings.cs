using System.ComponentModel.DataAnnotations;

namespace Hatband.Integrations.Steam.Settings;

public sealed class SteamSettings
{
    [Display(Name = "Silent mode")]
    public bool SilentModeEnabled { get; set; }
}
