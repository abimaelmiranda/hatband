using System.Text.Json.Serialization;

namespace Hatband.Integrations.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConnectorsSettings))]
internal partial class ConnectorsSettingsJsonSerializerContext : JsonSerializerContext
{
}
