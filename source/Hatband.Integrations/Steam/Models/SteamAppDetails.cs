using System.Text.Json.Serialization;

namespace Hatband.Integrations.Steam.Models;

internal sealed class SteamAppDetailsResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public SteamAppDetails? Data { get; init; }
}

internal sealed class SteamAppDetails
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("about_the_game")]
    public string? AboutTheGame { get; init; }

    [JsonPropertyName("short_description")]
    public string? ShortDescription { get; init; }

    [JsonPropertyName("developers")]
    public List<string>? Developers { get; init; }

    [JsonPropertyName("publishers")]
    public List<string>? Publishers { get; init; }

    [JsonPropertyName("genres")]
    public List<SteamGenre>? Genres { get; init; }

    [JsonPropertyName("release_date")]
    public SteamReleaseDate? ReleaseDate { get; init; }

    [JsonPropertyName("platforms")]
    public SteamPlatformSupport? Platforms { get; init; }
}

internal sealed class SteamGenre
{
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed class SteamReleaseDate
{
    [JsonPropertyName("date")]
    public string? Date { get; init; }
}

internal sealed class SteamPlatformSupport
{
    [JsonPropertyName("windows")]
    public bool? Windows { get; init; }

    [JsonPropertyName("mac")]
    public bool? MacOS { get; init; }

    [JsonPropertyName("linux")]
    public bool? Linux { get; init; }
}
