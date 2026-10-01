using System.Text.Json.Serialization;

namespace Hatband.Integrations.Steam.Models;

internal sealed class SteamOwnedGame
{
    [JsonPropertyName("appid")]
    public uint AppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playtime_forever")]
    public uint PlaytimeForever { get; set; }

    [JsonPropertyName("rtime_last_played")]
    public long LastPlayedUnix { get; set; }
}
