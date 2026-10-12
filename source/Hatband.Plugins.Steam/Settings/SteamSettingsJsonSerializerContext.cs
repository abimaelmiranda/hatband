using System.Text.Json.Serialization;

namespace Hatband.Plugins.Steam.Settings;

[JsonSerializable(typeof(SteamSettings))]
internal partial class SteamSettingsJsonSerializerContext : JsonSerializerContext
{
}
