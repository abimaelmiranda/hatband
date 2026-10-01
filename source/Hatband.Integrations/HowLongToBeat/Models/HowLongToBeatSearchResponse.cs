using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hatband.Core.Models;

namespace Hatband.Integrations.HowLongToBeat.Models;

internal sealed class HowLongToBeatSearchResponse
{
    [JsonPropertyName("data")]
    public List<HowLongToBeatSearchResult>? Data { get; init; }

    public IReadOnlyList<HowLongToBeatGame> ToGames()
    {
        if (Data is null)
        {
            throw new InvalidOperationException("The HowLongToBeat response does not contain a game result list.");
        }

        var games = new List<HowLongToBeatGame>();
        foreach (var result in Data)
        {
            var gameId = ReadOptionalInt32(result.Id);
            var gameName = result.Name.ValueKind == JsonValueKind.String ? result.Name.GetString() : null;
            if (gameId is not int id || string.IsNullOrWhiteSpace(gameName))
            {
                continue;
            }

            games.Add(new HowLongToBeatGame
            {
                Id = id,
                Name = gameName,
                ReleaseYear = ReadOptionalInt32(result.ReleaseYear),
                MainStorySeconds = ReadPositiveSeconds(result.MainStorySeconds),
                MainStoryPlusExtrasSeconds = ReadPositiveSeconds(result.MainStoryPlusExtrasSeconds),
                CompletionistSeconds = ReadPositiveSeconds(result.CompletionistSeconds),
                MainStorySubmissionCount = ReadOptionalInt32(result.MainStorySubmissionCount)
            });
        }

        return games;
    }

    private static int? ReadOptionalInt32(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static long? ReadPositiveSeconds(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds) && seconds > 0
            ? seconds
            : null;
    }
}

internal sealed class HowLongToBeatSearchResult
{
    [JsonPropertyName("game_id")]
    public JsonElement Id { get; init; }

    [JsonPropertyName("game_name")]
    public JsonElement Name { get; init; }

    [JsonPropertyName("release_world")]
    public JsonElement ReleaseYear { get; init; }

    [JsonPropertyName("comp_main")]
    public JsonElement MainStorySeconds { get; init; }

    [JsonPropertyName("comp_plus")]
    public JsonElement MainStoryPlusExtrasSeconds { get; init; }

    [JsonPropertyName("comp_100")]
    public JsonElement CompletionistSeconds { get; init; }

    [JsonPropertyName("comp_main_count")]
    public JsonElement MainStorySubmissionCount { get; init; }
}
