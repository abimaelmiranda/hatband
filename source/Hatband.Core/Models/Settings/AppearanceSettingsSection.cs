using System.Text.Json.Serialization.Metadata;
using Hatband.Core.Abstractions.Settings;

namespace Hatband.Core.Models.Settings;

public sealed class AppearanceSettingsSection : SettingsSection<AppearanceSettings>
{
    public const string SectionId = "hatband.appearance";

    public AppearanceSettingsSection()
        : base(SectionId, "Appearance", version: 1)
    {
    }

    public override JsonTypeInfo<AppearanceSettings> JsonTypeInfo =>
        CoreSettingsJsonSerializerContext.Default.AppearanceSettings;

    public override AppearanceSettings CreateDefaultSettings() => new();
}
