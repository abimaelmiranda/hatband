using System.Text.Json.Serialization;

namespace Hatband.Core.Models.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GeneralSettings))]
internal partial class CoreSettingsJsonSerializerContext : JsonSerializerContext
{
}
