using System.Text.Json.Serialization;

namespace Hatband.Integrations.HowLongToBeat.Models;

internal sealed class HowLongToBeatSessionResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("hpKey")]
    public string? HoneypotKey { get; init; }

    [JsonPropertyName("hpVal")]
    public string? HoneypotValue { get; init; }
}
