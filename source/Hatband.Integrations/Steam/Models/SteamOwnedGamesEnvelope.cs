using System.Text.Json.Serialization;

namespace Hatband.Integrations.Steam.Models;

internal sealed class SteamOwnedGamesEnvelope
{
    [JsonPropertyName("response")]
    public SteamOwnedGamesResponse? Response { get; set; }
}
