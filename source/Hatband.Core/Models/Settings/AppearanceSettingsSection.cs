using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hatband.Core.Abstractions.Settings;

namespace Hatband.Core.Models.Settings;

public sealed class AppearanceSettingsSection : SettingsSection<AppearanceSettings>
{
    public const string SectionId = "hatband.appearance";

    public AppearanceSettingsSection()
        : base(SectionId, "Appearance", version: 2)
    {
    }

    public override JsonTypeInfo<AppearanceSettings> JsonTypeInfo =>
        CoreSettingsJsonSerializerContext.Default.AppearanceSettings;

    public override AppearanceSettings CreateDefaultSettings() => new();

    public override JsonElement Migrate(JsonElement settings, int storedVersion)
    {
        if (storedVersion != 1)
        {
            return base.Migrate(settings, storedVersion);
        }

        var migrated = JsonNode.Parse(settings.GetRawText())?.AsObject()
            ?? throw new JsonException("Appearance settings must be a JSON object.");
        var legacyTitle = migrated.FirstOrDefault(property =>
            string.Equals(property.Key, "ShowSelectedGameTitle", StringComparison.OrdinalIgnoreCase));
        if (legacyTitle.Key is not null)
        {
            var enabled = legacyTitle.Value?.GetValue<bool>()
                ?? throw new JsonException("The saved selected game title setting must be a boolean.");
            migrated.Remove(legacyTitle.Key);
            migrated["LibraryTitleMode"] = enabled ? (int)LibraryTitleMode.Text : (int)LibraryTitleMode.None;
        }

        return JsonSerializer.SerializeToElement(migrated);
    }
}
