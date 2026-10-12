using System.Text.Json.Serialization;

namespace Hatband.Plugins.Steam.Models;

internal sealed class SteamOwnedGamesEnvelope
{
    [JsonPropertyName("response")]
    public SteamOwnedGamesResponse? Response { get; set; }
}
