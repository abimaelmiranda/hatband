using System.ComponentModel.DataAnnotations;
using Hatband.Plugins.Steam.Localization;

namespace Hatband.Plugins.Steam.Settings;

public sealed class SteamSettings
{
    [Display(Name = nameof(SteamResources.SilentMode), ResourceType = typeof(SteamResources))]
    public bool SilentModeEnabled { get; set; }
}
