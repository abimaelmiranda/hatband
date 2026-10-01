using System.Text.Json.Serialization;
using Hatband.Core.Models.Settings;

namespace Hatband.Infrastructure.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HatbandSettings))]
internal partial class HatbandJsonSerializerContext : JsonSerializerContext
{
}
