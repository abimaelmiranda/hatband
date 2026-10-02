using System.Text.Json.Serialization;

namespace Hatband.Integrations.Steam.Models;

[JsonSerializable(typeof(Dictionary<string, SteamAppDetailsResult>))]
internal partial class SteamJsonSerializerContext : JsonSerializerContext
{
}
