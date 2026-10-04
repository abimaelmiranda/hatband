using System.Text.Json.Serialization.Metadata;
using Hatband.Core.Abstractions.Settings;

namespace Hatband.Core.Models.Settings;

public sealed class GeneralSettingsSection : SettingsSection<GeneralSettings>
{
    public const string SectionId = "hatband.general";

    public GeneralSettingsSection()
        : base(SectionId, "General", version: 1)
    {
    }

    public override JsonTypeInfo<GeneralSettings> JsonTypeInfo =>
        CoreSettingsJsonSerializerContext.Default.GeneralSettings;

    public override GeneralSettings CreateDefaultSettings() => new();
}
