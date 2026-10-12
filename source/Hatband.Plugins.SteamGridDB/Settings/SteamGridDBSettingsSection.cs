using System.Text.Json.Serialization.Metadata;
using Hatband.Core.Abstractions.Settings;

namespace Hatband.Plugins.SteamGridDB.Settings;

public sealed class SteamGridDBSettingsSection : SettingsSection<SteamGridDBSettings>
{
    public const string SectionId = "steamgriddb.settings";

    public SteamGridDBSettingsSection()
        : base(SectionId, "SteamGridDB", version: 1)
    {
    }

    public override JsonTypeInfo<SteamGridDBSettings> JsonTypeInfo =>
        SteamGridDBSettingsJsonSerializerContext.Default.SteamGridDBSettings;

    public override bool ShowInSettingsNavigation => false;

    public override SteamGridDBSettings CreateDefaultSettings() => new();
}
