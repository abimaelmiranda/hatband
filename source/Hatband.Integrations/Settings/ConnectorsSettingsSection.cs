using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Hatband.Core.Abstractions.Settings;
using Hatband.Integrations.Steam.Settings;

namespace Hatband.Integrations.Settings;

public sealed class ConnectorsSettingsSection : SettingsSection<ConnectorsSettings>
{
    public const string SectionId = "hatband.connectors";

    public ConnectorsSettingsSection()
        : base(SectionId, "Connectors", version: 2)
    {
    }

    public override JsonTypeInfo<ConnectorsSettings> JsonTypeInfo =>
        ConnectorsSettingsJsonSerializerContext.Default.ConnectorsSettings;

    public override ConnectorsSettings CreateDefaultSettings() => new();

    public override JsonElement Migrate(JsonElement settings, int storedVersion)
    {
        if (storedVersion is 0 or 1)
        {
            var root = new JsonObject
            {
                ["steam"] = JsonNode.Parse(settings.GetRawText())
            };
            using var document = JsonDocument.Parse(root.ToJsonString());
            return document.RootElement.Clone();
        }

        return base.Migrate(settings, storedVersion);
    }
}
