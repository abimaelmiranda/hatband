using System.Text.Json.Serialization;

namespace Hatband.Integrations.HowLongToBeat.Models;

[JsonSerializable(typeof(HowLongToBeatSearchRequest))]
[JsonSerializable(typeof(HowLongToBeatSearchResponse))]
[JsonSerializable(typeof(HowLongToBeatSessionResponse))]
internal partial class HowLongToBeatJsonSerializerContext : JsonSerializerContext
{
}
