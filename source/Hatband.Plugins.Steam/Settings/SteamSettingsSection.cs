using System.Text.Json.Serialization.Metadata;
using Hatband.Core.Abstractions.Settings;

namespace Hatband.Plugins.Steam.Settings;

public sealed class SteamSettingsSection : SettingsSection<SteamSettings>
{
    public const string SectionId = "steam.settings";

    public SteamSettingsSection()
        : base(SectionId, "Steam", version: 1)
    {
    }

    public override JsonTypeInfo<SteamSettings> JsonTypeInfo =>
        SteamSettingsJsonSerializerContext.Default.SteamSettings;

    public override bool ShowInSettingsNavigation => false;

    public override SteamSettings CreateDefaultSettings() => new();
}
