using System.Text.Json.Serialization;

namespace Hatband.Plugins.SteamGridDB.Settings;

[JsonSerializable(typeof(SteamGridDBSettings))]
internal partial class SteamGridDBSettingsJsonSerializerContext : JsonSerializerContext
{
}
