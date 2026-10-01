using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hatband.Integrations.HowLongToBeat.Models;

internal sealed class HowLongToBeatSearchRequest
{
    [JsonPropertyName("searchType")]
    public string SearchType { get; init; } = "games";

    [JsonPropertyName("searchTerms")]
    public required string[] SearchTerms { get; init; }

    [JsonPropertyName("searchPage")]
    public int SearchPage { get; init; } = 1;

    [JsonPropertyName("size")]
    public int Size { get; init; } = 10;

    [JsonPropertyName("searchOptions")]
    public required HowLongToBeatSearchOptions Options { get; init; }

    [JsonPropertyName("useCache")]
    public bool UseCache { get; init; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Honeypot { get; init; }
}

internal sealed class HowLongToBeatSearchOptions
{
    [JsonPropertyName("games")]
    public required HowLongToBeatGameOptions Games { get; init; }

    [JsonPropertyName("users")]
    public HowLongToBeatSortOptions Users { get; init; } = new("postcount");

    [JsonPropertyName("lists")]
    public HowLongToBeatSortOptions Lists { get; init; } = new("follows");

    [JsonPropertyName("filter")]
    public string Filter { get; init; } = string.Empty;

    [JsonPropertyName("sort")]
    public int Sort { get; init; }

    [JsonPropertyName("randomizer")]
    public int Randomizer { get; init; }
}

internal sealed class HowLongToBeatGameOptions
{
    [JsonPropertyName("userId")]
    public int UserId { get; init; }

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = string.Empty;

    [JsonPropertyName("sortCategory")]
    public string SortCategory { get; init; } = "popular";

    [JsonPropertyName("rangeCategory")]
    public string RangeCategory { get; init; } = "main";

    [JsonPropertyName("rangeTime")]
    public HowLongToBeatRangeTimeOptions RangeTime { get; init; } = new();

    [JsonPropertyName("gameplay")]
    public HowLongToBeatGameplayOptions Gameplay { get; init; } = new();

    [JsonPropertyName("year")]
    public string Year { get; init; } = string.Empty;

    [JsonPropertyName("modifier")]
    public string Modifier { get; init; } = string.Empty;
}

internal sealed class HowLongToBeatRangeTimeOptions
{
    [JsonPropertyName("min")]
    public int? Minimum { get; init; }

    [JsonPropertyName("max")]
    public int? Maximum { get; init; }
}

internal sealed class HowLongToBeatGameplayOptions
{
    [JsonPropertyName("perspective")]
    public string Perspective { get; init; } = string.Empty;

    [JsonPropertyName("flow")]
    public string Flow { get; init; } = string.Empty;

    [JsonPropertyName("genre")]
    public string Genre { get; init; } = string.Empty;
}

internal sealed record HowLongToBeatSortOptions(
    [property: JsonPropertyName("sortCategory")] string SortCategory);
