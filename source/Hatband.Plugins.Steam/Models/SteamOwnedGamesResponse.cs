using System.Text.Json.Serialization;

namespace Hatband.Plugins.Steam.Models;

internal sealed class SteamOwnedGamesResponse
{
    [JsonPropertyName("game_count")]
    public int? GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<SteamOwnedGame> Games { get; set; } = [];
}
